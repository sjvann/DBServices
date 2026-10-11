using System.Net;
using System.Text;
using System.Text.Json;
using DbServices.Mcp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
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
    public async Task Known_connection_lists_six_readonly_tools_and_call_does_not_create_database()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcp-no-create-{Guid.NewGuid():N}.db");
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:warehouse:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:warehouse:ConnectionString", $"Data Source={dbPath}"),
        ]);

        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);
        AssertAdvertisedTools(tools);

        var catalog = factory.Services.GetRequiredService<ToolCatalog>();
        Assert.IsTrue(catalog.Connections.TryGetValue("warehouse", out var snapshot));
        Assert.AreEqual("Sqlite", snapshot!.ServerType);
        Assert.IsTrue(snapshot.Available);
        Assert.AreSame(catalog, factory.Services.GetRequiredService<ToolCatalog>());

        Assert.IsFalse(File.Exists(dbPath));
        var id = 3;
        foreach (var tool in Expected)
            await AssertCallDoesNotExecuteAsync(client, tool.Name, id++);

        Assert.IsFalse(File.Exists(dbPath));
        AssertFactoryCreateAbsent();
        await AssertLegacySseAbsentAsync(client);
    }

    [TestMethod]
    public async Task Unknown_server_type_is_unavailable_list_stays_six_and_host_stays_up()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:legacy:ServerType", "Informix"),
            new KeyValuePair<string, string?>("Connections:legacy:ConnectionString", "Server=ignored"),
        ]);

        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);
        AssertAdvertisedTools(tools);

        var connections = factory.Services.GetRequiredService<ToolCatalog>().Connections;
        Assert.IsTrue(connections.TryGetValue("legacy", out var snapshot));
        Assert.AreEqual("Informix", snapshot!.ServerType);
        Assert.IsFalse(snapshot.Available);
        AssertFactoryCreateAbsent();
    }

    [TestMethod]
    public async Task No_complete_connections_still_starts_and_lists_six()
    {
        Environment.SetEnvironmentVariable("Connections__from_env__ServerType", "Sqlite");
        Environment.SetEnvironmentVariable("Connections__from_env__ConnectionString", "Data Source=ignored.db");
        try
        {
            await using var factory = CreateFactory([]);

            using var client = CreateClient(factory);
            var tools = await ListToolsAsync(client);
            AssertAdvertisedTools(tools);

            var connections = factory.Services.GetRequiredService<ToolCatalog>().Connections;
            Assert.AreEqual(0, connections.Count);
            Assert.IsFalse(connections.ContainsKey("from_env"));
            AssertFactoryCreateAbsent();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Connections__from_env__ServerType", null);
            Environment.SetEnvironmentVariable("Connections__from_env__ConnectionString", null);
        }
    }

    [TestMethod]
    public async Task Blank_connection_string_is_unavailable_and_name_is_kept()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:blank:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:blank:ConnectionString", "   "),
        ]);

        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);
        AssertAdvertisedTools(tools);

        var connections = factory.Services.GetRequiredService<ToolCatalog>().Connections;
        Assert.IsTrue(connections.TryGetValue("blank", out var snapshot));
        Assert.AreEqual("Sqlite", snapshot!.ServerType);
        Assert.IsFalse(snapshot.Available);
        AssertFactoryCreateAbsent();
    }

    [TestMethod]
    public async Task Non_localhost_host_still_lists_tools()
    {
        await using var factory = CreateFactory(
        [
            new KeyValuePair<string, string?>("Connections:warehouse:ServerType", "Sqlite"),
            new KeyValuePair<string, string?>("Connections:warehouse:ConnectionString", "Data Source=:memory:"),
        ]);

        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client, "agent.example");
        AssertAdvertisedTools(tools);
        AssertFactoryCreateAbsent();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IEnumerable<KeyValuePair<string, string?>> settings)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
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

                config.AddInMemoryCollection(settings);
            });
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        return client;
    }

    private static async Task<JsonElement[]> ListToolsAsync(HttpClient client, string? host = null)
    {
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
            if (host is not null)
                initRequest.Headers.Host = host;

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
        if (host is not null)
            listRequest.Headers.Host = host;

        using var listResponse = await client.SendAsync(listRequest);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode, body);

        var json = ParseRpc(body);
        return json.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.Clone()).ToArray();
    }

    private static async Task AssertCallDoesNotExecuteAsync(HttpClient client, string name, int id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{\"name\":\"" + name + "\",\"arguments\":{}}}",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);

        var json = ParseRpc(body);
        var refused = json.TryGetProperty("error", out _)
            || (json.TryGetProperty("result", out var result)
                && result.TryGetProperty("isError", out var isError)
                && isError.ValueKind == JsonValueKind.True);
        Assert.IsTrue(refused, body);
    }

    private static async Task AssertLegacySseAbsentAsync(HttpClient client)
    {
        using var sseResponse = await client.GetAsync("/sse");
        Assert.AreEqual(HttpStatusCode.NotFound, sseResponse.StatusCode);

        using var messageResponse = await client.GetAsync("/message");
        Assert.AreEqual(HttpStatusCode.NotFound, messageResponse.StatusCode);
    }

    private static JsonElement ParseRpc(string body)
    {
        if (!body.Contains("data:", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        JsonElement? last = null;
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = trimmed["data:".Length..].Trim();
            if (data.Length == 0)
                continue;

            using var document = JsonDocument.Parse(data);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                continue;

            var clone = document.RootElement.Clone();
            if (clone.TryGetProperty("result", out var result) && result.TryGetProperty("tools", out _))
                return clone;

            last = clone;
        }

        if (last is { } found)
            return found;

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
