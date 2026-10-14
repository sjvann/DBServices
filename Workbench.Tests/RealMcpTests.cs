extern alias mcp;

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Tests;

[TestClass]
[DoNotParallelize]
public class RealMcpTests
{
    [TestMethod]
    public async Task Workbench_reads_real_connections_query_cell_and_audit_line()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-live-" + Guid.NewGuid().ToString("N"));
        var database = Path.Combine(root, "products.db");
        Directory.CreateDirectory(root);
        Seed(database);
        await using var mcpHost = new WebApplicationFactory<mcp::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(root);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                for (var index = config.Sources.Count - 1; index >= 0; index--)
                {
                    var source = config.Sources[index];
                    var name = source.GetType().Name;
                    if (source is EnvironmentVariablesConfigurationSource
                        || name.Contains("UserSecrets", StringComparison.Ordinal)
                        || name.Contains("CommandLine", StringComparison.Ordinal))
                    {
                        config.Sources.RemoveAt(index);
                    }
                }

                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AllowedHosts"] = "*",
                    ["Connections:inventory:ServerType"] = "Sqlite",
                    ["Connections:inventory:ConnectionString"] = "Data Source=" + database,
                    ["Connections:blank:ServerType"] = "Sqlite",
                    ["Connections:blank:ConnectionString"] = " ",
                });
            });
        });

        using var probe = mcpHost.CreateClient();
        var listed = await probe.GetFromJsonAsync<List<ConnectionDto>>("/connections");
        Assert.IsNotNull(listed);
        Assert.AreEqual(1, listed.Count);
        Assert.AreEqual("inventory", listed[0].Name);
        Assert.AreEqual("Sqlite", listed[0].ServerType);

        var handler = mcpHost.Server.CreateHandler();
        var baseUrl = mcpHost.Server.BaseAddress!.ToString();
        var http = new HttpClient(handler) { BaseAddress = mcpHost.Server.BaseAddress };
        await using var host = new WorkbenchHost(options =>
        {
            options.UseRecordingSurface = false;
            options.UseRecordingCatalog = false;
            options.UseRecordingAudit = false;
            options.SurfaceOverride = new McpBridge(new McpSurface(http, baseUrl));
            options.CatalogOverride = new McpCatalogBridge(new McpConnectionCatalog(new HttpClient(mcpHost.Server.CreateHandler()) { BaseAddress = mcpHost.Server.BaseAddress }, baseUrl));
            options.AuditOverride = new McpAuditReader(Path.Combine(root, "audit", "mcp.jsonl"));
        });

        var admin = await host.LoginAsync();
        var connections = await admin.GetAsync("api/connections?surface=mcp");
        Assert.AreEqual("inventory", connections[0].GetProperty("name").GetString());
        Assert.AreEqual("Sqlite", connections[0].GetProperty("serverType").GetString());
        Assert.AreEqual(2, connections[0].EnumerateObject().Count());
        var queried = await admin.SendAsync(HttpMethod.Post, "api/plans/query", new PlanRequest
        {
            Surface = "mcp",
            Connection = "inventory",
            Table = "products",
            Filters = [new FilterDto { Key = "Name", Value = "one" }],
        });
        var error = queried.TryGetProperty("errorCode", out var errorCode) && errorCode.ValueKind == JsonValueKind.String ? errorCode.GetString() : "";
        var detail = queried.TryGetProperty("errorMessage", out var errorMessage) && errorMessage.ValueKind == JsonValueKind.String ? errorMessage.GetString() : "";
        Assert.AreEqual("", error, detail);
        Assert.IsTrue(queried.GetProperty("rows").EnumerateArray().Any(row => row.EnumerateArray().Any(cell => cell.GetString() == "one")));
        Assert.AreEqual(true, queried.GetProperty("auditMatched").GetBoolean());
        var line = File.ReadAllLines(Path.Combine(root, "audit", "mcp.jsonl"))
            .Select(text => JsonDocument.Parse(text).RootElement.Clone())
            .Last(row => row.GetProperty("tool").GetString() == "query");
        Assert.AreEqual(admin.Me.Id, line.GetProperty("caller").GetString());
        Assert.AreEqual("inventory", line.GetProperty("connectionId").GetString());
        Assert.AreEqual("products", line.GetProperty("arguments").GetProperty("table").GetString());
        Assert.AreEqual(queried.GetProperty("rows").GetArrayLength(), line.GetProperty("rowCount").GetInt32());
        SqliteConnection.ClearAllPools();
    }

    private static void Seed(string database)
    {
        using var connection = new SqliteConnection("Data Source=" + database);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Note TEXT); INSERT INTO products (Id, Name) VALUES (1, 'one');";
        command.ExecuteNonQuery();
    }

    private sealed class McpBridge : IDataSurface
    {
        private readonly McpSurface _surface;
        public McpBridge(McpSurface surface) => _surface = surface;
        public Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken) => _surface.TablesAsync(request, cancellationToken);
        public Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken) => _surface.FieldsAsync(request, cancellationToken);
        public Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken) => _surface.QueryAsync(request, cancellationToken);
        public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken) => _surface.CountAsync(request, cancellationToken);
    }

    private sealed class McpCatalogBridge : IConnectionCatalog
    {
        private readonly McpConnectionCatalog _catalog;
        public McpCatalogBridge(McpConnectionCatalog catalog) => _catalog = catalog;
        public Task<CatalogOutcome> ListAsync(string surface, CancellationToken cancellationToken) => _catalog.ListAsync(cancellationToken);
    }
}
