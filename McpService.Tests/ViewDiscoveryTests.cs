using System.Net;
using System.Text;
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
public class ViewDiscoveryTests
{
    private static readonly string[] TableTools = ["tables", "fields", "get", "query", "count", "exists"];

    [TestMethod]
    public async Task Published_view_name_is_discovery_and_blank_caller_still_calls_it()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-disc-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Seed(connectionString);
            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");

            var empty = await ListToolsAsync(client);
            Assert.AreEqual(0, empty.Length);
            AssertTableToolsAbsent(empty);

            var missingCaller = await PutAsync(client, caller: null, key: "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, missingCaller.Status);
            Assert.Contains("未提供呼叫者", missingCaller.Body);

            var wrongKey = await PutAsync(client, caller: "林可", key: "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, wrongKey.Status);
            Assert.Contains("目錄金鑰不符", wrongKey.Body);
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);

            var saved = await PutAsync(client, caller: "林可", key: "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);

            var published = await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, published.Status, published.Body);

            var tools = await ListToolsAsync(client);
            Assert.AreEqual(1, tools.Length);
            Assert.AreEqual("出貨", tools[0].GetProperty("name").GetString());
            Assert.AreEqual("說明出貨", tools[0].GetProperty("description").GetString());
            var annotations = tools[0].GetProperty("annotations");
            Assert.IsTrue(annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.IsFalse(annotations.GetProperty("destructiveHint").GetBoolean());
            var properties = tools[0].GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "connectionId", "名稱" }, properties);
            Assert.IsFalse(properties.Contains("filters"));
            AssertTableToolsAbsent(tools);
            Assert.IsFalse(tools[0].GetRawText().Contains(connectionString, StringComparison.Ordinal));

            var call = await CallAsync(client, "   ");
            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = call.Rpc.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.Contains("one", text);
            Assert.IsFalse(call.Body.Contains(connectionString, StringComparison.Ordinal), call.Body);
            Assert.IsFalse(text.Contains("Data Source", StringComparison.OrdinalIgnoreCase));

            var auditPath = Path.Combine(root, "audit", "mcp.jsonl");
            JsonElement? matched = null;
            foreach (var line in File.ReadAllLines(auditPath))
            {
                using var auditLine = JsonDocument.Parse(line);
                if (auditLine.RootElement.GetProperty("tool").GetString() != "出貨")
                    continue;
                matched = auditLine.RootElement.Clone();
                Assert.IsFalse(line.Contains(connectionString, StringComparison.Ordinal));
            }

            Assert.IsNotNull(matched);
            Assert.AreEqual("anonymous", matched.Value.GetProperty("caller").GetString());
            Assert.AreEqual("出貨", matched.Value.GetProperty("tool").GetString());

            using var connections = await client.GetAsync("/connections");
            var connectionBody = await connections.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, connections.StatusCode);
            using var document = JsonDocument.Parse(connectionBody);
            var row = document.RootElement.EnumerateArray().Single();
            CollectionAssert.AreEquivalent(
                new[] { "name", "serverType" },
                row.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.AreEqual("warehouse", row.GetProperty("name").GetString());
            Assert.AreEqual("Sqlite", row.GetProperty("serverType").GetString());
            Assert.IsFalse(connectionBody.Contains(connectionString, StringComparison.Ordinal));
            Assert.IsFalse(connectionBody.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection")?.GetMethod("ClearAllPools")?.Invoke(null, null);
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Blank_catalog_key_rejects_the_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var rejected = await PutAsync(client, "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, rejected.Status);
            Assert.Contains("目錄金鑰不符", rejected.Body);
            Assert.IsFalse(File.Exists(Path.Combine(root, "catalog", "views.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void Seed(string connectionString)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            service!.ExecuteSQL("CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            service.ExecuteSQL("INSERT INTO products (Id, Name) VALUES (1, 'one')");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static WebApplicationFactory<Program> Factory(string root, IEnumerable<KeyValuePair<string, string?>> settings)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

    private static async Task<(HttpStatusCode Status, string Body)> PutAsync(HttpClient client, string? caller, string? key)
    {
        const string body =
            """
            {"id":"ship-1","name":"出貨","purpose":"說明出貨","published":false,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
            """;
        return await SendAsync(client, HttpMethod.Put, "/views/ship-1", caller, key, body);
    }

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? caller,
        string? key,
        string? body = null)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (caller is not null)
            request.Headers.TryAddWithoutValidation("X-DBServices-Caller", caller);
        if (key is not null)
            request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", key);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallAsync(HttpClient client, string caller)
    {
        await InitializeAsync(client);
        var payload =
            """
            {"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"warehouse","名稱":"one"}}}
            """;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", caller);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body, ParseRpc(body));
    }

    private static async Task<JsonElement[]> ListToolsAsync(HttpClient client)
    {
        await InitializeAsync(client);
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
        return ParseRpc(body).GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.Clone()).ToArray();
    }

    private static async Task InitializeAsync(HttpClient client)
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
        Assert.AreEqual(HttpStatusCode.OK, initResponse.StatusCode, await initResponse.Content.ReadAsStringAsync());
    }

    private static void AssertTableToolsAbsent(JsonElement[] tools)
    {
        var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();
        foreach (var name in TableTools)
            Assert.IsFalse(names.Contains(name, StringComparer.OrdinalIgnoreCase));
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
            var clone = document.RootElement.Clone();
            if (clone.TryGetProperty("result", out _))
                return clone;
            last = clone;
        }

        return last ?? throw new AssertFailedException(body);
    }
}
