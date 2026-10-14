using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
[DoNotParallelize]
public class ConnectionListTests
{
    private static readonly (string Name, string Description)[] Expected =
    [
        ("tables", "列出資料表"),
        ("fields", "列出欄位"),
        ("get", "依 Id 取一列"),
        ("query", "鍵值查詢"),
        ("count", "計數"),
        ("exists", "是否存在"),
    ];

    [TestMethod]
    public async Task Connections_lists_only_complete_entries_as_name_and_server_type()
    {
        const string hidden = "SecretMarkerDoNotLeak";
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:warehouse:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:warehouse:ConnectionString", "Data Source=:memory:"),
            new KeyValuePair<string, string?>("Connections:blank:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:blank:ConnectionString", "   "),
            new KeyValuePair<string, string?>("Connections:legacy:ServerType", "Informix"),
            new KeyValuePair<string, string?>("Connections:legacy:ConnectionString", "Password=" + hidden),
        ]);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        using var response = await client.GetAsync("/connections");
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsFalse(body.Contains(hidden, StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(body.Contains("Available", StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("Data Source", StringComparison.OrdinalIgnoreCase));

        using var document = JsonDocument.Parse(body);
        var rows = document.RootElement.EnumerateArray().ToArray();
        Assert.AreEqual(1, rows.Length);
        var names = rows[0].EnumerateObject().Select(property => property.Name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "name", "serverType" }, names);
        Assert.AreEqual("warehouse", rows[0].GetProperty("name").GetString());
        Assert.AreEqual("Sqlite", rows[0].GetProperty("serverType").GetString());

        var tools = await ListToolsAsync(client);
        var advertised = tools.Select(tool => (tool.GetProperty("name").GetString(), tool.GetProperty("description").GetString())).ToArray();
        CollectionAssert.AreEquivalent(Expected, advertised);
        Assert.IsFalse(advertised.Any(tool => tool.Item1 is "insert" or "update" or "delete" or "sql"));
    }

    private static WebApplicationFactory<Program> CreateFactory(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-connections-" + Guid.NewGuid().ToString("N"));
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

    private static async Task<JsonElement[]> ListToolsAsync(HttpClient client)
    {
        using var initRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"mcp-tests","version":"1.0"}}}
                """,
                Encoding.UTF8,
                "application/json"),
        };
        using var initResponse = await client.SendAsync(initRequest);
        Assert.AreEqual(HttpStatusCode.OK, initResponse.StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json"),
        };
        listRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        using var listResponse = await client.SendAsync(listRequest);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
        using var document = JsonDocument.Parse(ParseRpc(body));
        return document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.Clone()).ToArray();
    }

    private static string ParseRpc(string body)
    {
        if (!body.Contains("data:", StringComparison.Ordinal))
            return body;

        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var data = trimmed["data:".Length..].Trim();
            if (data.Contains("\"tools\"", StringComparison.Ordinal))
                return data;
        }

        return body;
    }
}
