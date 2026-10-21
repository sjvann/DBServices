using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DbServices.Api.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
[DoNotParallelize]
public class ConnectionProbeTests
{
    [TestMethod]
    public void Missing_driver_is_reported_without_opening()
    {
        var result = ConnectionProbe.Try("PostgreSQL", "Host=localhost;Port=5432;Database=app;Username=postgres;Password=secret", _ => null);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual("driver_missing", result.Code);
        Assert.AreEqual("缺少 PostgreSQL 驅動程式（Npgsql）。", result.Message);
    }

    [TestMethod]
    public void Installed_drivers_can_be_loaded()
    {
        foreach (var assembly in new[] { "Microsoft.Data.Sqlite", "Microsoft.Data.SqlClient", "MySql.Data", "Oracle.ManagedDataAccess", "Npgsql" })
            Assert.IsNotNull(System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(assembly)), assembly);
    }

    [TestMethod]
    public void Sqlite_memory_connects_and_a_missing_file_is_not_created()
    {
        var memory = ConnectionProbe.Try("Sqlite", "Data Source=:memory:");
        Assert.IsTrue(memory.Ok, memory.Message);
        Assert.AreEqual("已連上資料庫。", memory.Message);

        var path = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"), "warehouse.db");
        var created = ConnectionProbe.Try("Sqlite", "Data Source=" + path + ";Mode=ReadWriteCreate");
        Assert.IsTrue(created.Ok, created.Message);
        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(created.Message.Contains(path, StringComparison.Ordinal));

        var missing = ConnectionProbe.Try("Sqlite", "Data Source=" + path + ";Mode=ReadOnly");
        Assert.AreEqual("connection_failed", missing.Code);
        Assert.AreEqual("檔案不存在。", missing.Message);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task Probe_does_not_save_and_hides_the_connection_string()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Workbench:CatalogKey", "unit-catalog-key"),
        ]);
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Probe("Sqlite", "Data Source=:memory:"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
        Assert.IsFalse(body.Contains(":memory:", StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("Data Source", StringComparison.OrdinalIgnoreCase));
        using var document = JsonDocument.Parse(body);
        Assert.AreEqual("已連上資料庫。", document.RootElement.GetProperty("message").GetString());

        using var listed = await client.GetAsync("/connections");
        var listBody = await listed.Content.ReadAsStringAsync();
        Assert.AreEqual("[]", listBody.Trim());
    }

    [TestMethod]
    public async Task Unreachable_database_is_not_a_missing_driver()
    {
        const string hidden = "SecretMarkerDoNotLeak";
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Workbench:CatalogKey", "unit-catalog-key"),
        ]);
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(Probe(
            "PostgreSQL",
            "Host=127.0.0.1;Port=1;Database=app;Username=postgres;Password=" + hidden));
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, body);
        using var document = JsonDocument.Parse(body);
        Assert.AreEqual("connection_failed", document.RootElement.GetProperty("code").GetString());
        Assert.AreNotEqual("driver_missing", document.RootElement.GetProperty("code").GetString());
        var message = document.RootElement.GetProperty("message").GetString() ?? "";
        Assert.StartsWith("連不上資料庫。", message);
        Assert.IsFalse(body.Contains(hidden, StringComparison.Ordinal));
        Assert.IsFalse(message.Contains("Password=", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpRequestMessage Probe(string serverType, string connectionString)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/connections/probe")
        {
            Content = JsonContent.Create(new { serverType, connectionString }),
        };
        request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", "unit-catalog-key");
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", "admin");
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

                config.AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "*" });
                config.AddInMemoryCollection(settings);
            });
        });
    }
}
