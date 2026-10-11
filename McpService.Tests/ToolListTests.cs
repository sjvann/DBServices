using System.Net;
using System.Text;
using System.Text.Json;
using DbServices.Mcp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
public class ToolListTests
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

    private static readonly string[] ClosedNames = ["insert", "update", "delete", "sql"];

    [TestMethod]
    public async Task Known_connection_lists_six_readonly_tools_and_does_not_create()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcp-no-create-{Guid.NewGuid():N}.db");
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:0:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:0:ConnectionString", $"Data Source={dbPath}"),
        ]);

        var tools = await ListToolsAsync(factory);
        AssertAdvertisedTools(tools);

        var snapshot = factory.Services.GetRequiredService<ToolCatalog>().Connections.Single();
        Assert.AreEqual("Sqlite", snapshot.ServerType);
        Assert.IsTrue(snapshot.Available);
        Assert.IsFalse(File.Exists(dbPath));
        AssertFactoryCreateAbsent();
    }

    [TestMethod]
    public async Task Unknown_server_type_is_unavailable_list_stays_six_and_host_stays_up()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:0:ServerType", "Informix"),
            new KeyValuePair<string, string?>("Connections:0:ConnectionString", "Server=ignored"),
        ]);

        var tools = await ListToolsAsync(factory);
        AssertAdvertisedTools(tools);

        var snapshot = factory.Services.GetRequiredService<ToolCatalog>().Connections.Single();
        Assert.AreEqual("Informix", snapshot.ServerType);
        Assert.IsFalse(snapshot.Available);
        AssertFactoryCreateAbsent();
    }

    [TestMethod]
    public async Task No_complete_connections_still_starts_and_lists_six()
    {
        await using var factory = CreateFactory([]);

        var tools = await ListToolsAsync(factory);
        AssertAdvertisedTools(tools);

        var connections = factory.Services.GetRequiredService<ToolCatalog>().Connections;
        Assert.AreEqual(0, connections.Count);
        AssertFactoryCreateAbsent();
    }

    [TestMethod]
    public async Task Blank_connection_string_is_unavailable_and_list_stays_six()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:0:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:0:ConnectionString", "   "),
        ]);

        var tools = await ListToolsAsync(factory);
        AssertAdvertisedTools(tools);

        var snapshot = factory.Services.GetRequiredService<ToolCatalog>().Connections.Single();
        Assert.IsFalse(snapshot.Available);
        AssertFactoryCreateAbsent();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IEnumerable<KeyValuePair<string, string?>> settings)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(settings);
            });
        });
    }

    private static async Task<JsonElement[]> ListToolsAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");

        using (var initRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"mcp-tests","version":"1.0"}}}
                """,
                Encoding.UTF8,
                "application/json"),
        })
        {
            using var initResponse = await client.SendAsync(initRequest);
            var initBody = await initResponse.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, initResponse.StatusCode, initBody);
        }

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
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode, body);

        using var sseRequest = new HttpRequestMessage(HttpMethod.Get, "/mcp/sse");
        using var sseResponse = await client.SendAsync(sseRequest);
        Assert.AreEqual(HttpStatusCode.NotFound, sseResponse.StatusCode);

        var json = ParseRpc(body);
        return json.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.Clone()).ToArray();
    }

    private static JsonElement ParseRpc(string body)
    {
        if (!body.Contains("data:", StringComparison.Ordinal))
            return JsonDocument.Parse(body).RootElement.Clone();

        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = trimmed["data:".Length..].Trim();
            if (data.Length == 0)
                continue;

            using var document = JsonDocument.Parse(data);
            if (document.RootElement.TryGetProperty("result", out var result)
                && result.TryGetProperty("tools", out _))
            {
                return document.RootElement.Clone();
            }
        }

        Assert.Fail(body);
        return default;
    }

    private static void AssertAdvertisedTools(JsonElement[] tools)
    {
        var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();
        Assert.AreEqual(Expected.Length, names.Length);
        CollectionAssert.AreEquivalent(Expected.Select(tool => tool.Name).ToArray(), names);

        foreach (var expected in Expected)
        {
            var tool = tools.Single(candidate => candidate.GetProperty("name").GetString() == expected.Name);
            var description = tool.GetProperty("description").GetString();
            Assert.AreEqual(expected.Description, description);
            Assert.IsFalse(description!.Contains("SQL", StringComparison.OrdinalIgnoreCase));

            var annotations = tool.GetProperty("annotations");
            Assert.IsTrue(annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.IsFalse(annotations.GetProperty("destructiveHint").GetBoolean());
        }

        foreach (var closed in ClosedNames)
            Assert.IsFalse(names.Contains(closed, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertFactoryCreateAbsent()
    {
        foreach (var assembly in new[] { typeof(ToolCatalog).Assembly, typeof(Program).Assembly })
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(assembly.Location));
            Assert.IsFalse(
                text.Contains("DbServiceFactory", StringComparison.Ordinal),
                assembly.GetName().Name);
        }
    }
}
