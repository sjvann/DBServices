using System.Net;
using System.Text;
using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Core.Session;
using DbServices.Mcp;
using Microsoft.AspNetCore.Hosting;
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
        foreach (var name in new[] { "fields", "get", "query", "count", "exists" })
            await AssertCallDoesNotExecuteAsync(client, name, id++, "warehouse");

        Assert.IsFalse(File.Exists(dbPath));
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
    }

    [TestMethod]
    public void Connection_lookup_ignores_case_and_server_type_stays_ordinal()
    {
        var catalog = new ToolCatalog(new Dictionary<string, ConnectionSetting>
        {
            ["Inventory"] = new ConnectionSetting
            {
                ServerType = "Sqlite",
                ConnectionString = "Data Source=inventory.db",
            },
            ["lower"] = new ConnectionSetting
            {
                ServerType = "sqlite",
                ConnectionString = "Data Source=lower.db",
            },
        });

        Assert.IsTrue(catalog.Connections.TryGetValue("inventory", out var inventory));
        Assert.AreEqual("Sqlite", inventory!.ServerType);
        Assert.IsTrue(inventory.Available);
        Assert.IsTrue(catalog.Connections.TryGetValue("INVENTORY", out _));

        Assert.IsTrue(catalog.Connections.TryGetValue("lower", out var lower));
        Assert.AreEqual("sqlite", lower!.ServerType);
        Assert.IsFalse(lower.Available);
    }

    [TestMethod]
    public async Task Tables_schema_has_connection_id_and_optional_include_view()
    {
        await using var factory = CreateFactory([]);
        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);

        var tables = tools.Single(tool => tool.GetProperty("name").GetString() == "tables");
        var schema = tables.GetProperty("inputSchema");
        var names = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "connectionId", "includeView" }, names);

        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.Contains(required, "connectionId");
        Assert.IsFalse(required.Contains("includeView"));

        var raw = schema.GetRawText();
        Assert.IsFalse(raw.Contains("connectionString", StringComparison.OrdinalIgnoreCase), raw);
        Assert.IsFalse(raw.Contains("connectString", StringComparison.OrdinalIgnoreCase), raw);
    }

    [TestMethod]
    public async Task Tables_omitting_include_view_matches_session_and_includes_view()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedItemAndView(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 10);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var session = ExecuteTables(connectionString, null);
            AssertPayloadMatchesSession(TextOf(result, call.Body), session);
            Assert.IsTrue(session.Rows.SelectMany(row => row).Any(cell => Convert.ToString(cell) == "item_view"));
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Tables_include_view_false_matches_session()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedItemAndView(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["includeView"] = false,
            }, 11);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            AssertPayloadMatchesSession(TextOf(result, call.Body), ExecuteTables(connectionString, false));
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Empty_database_returns_empty_rows_and_host_continues()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            Assert.IsFalse(File.Exists(dbPath));
            await using var factory = CreateFactory(SqliteSettings("warehouse", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);
            Assert.IsFalse(File.Exists(dbPath));

            var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 12);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            Assert.IsTrue(File.Exists(dbPath));
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var session = ExecuteTables(connectionString, null);
            Assert.AreEqual(0, session.Rows.Count);
            AssertPayloadMatchesSession(TextOf(result, call.Body), session);

            var again = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 13);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Session_error_returns_execution_failed_without_connection_string()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            var sessionError = ExecuteTables(connectionString, "nope");
            Assert.AreEqual(QueryKind.Error, sessionError.Kind);

            await using var factory = CreateFactory(SqliteSettings("warehouse", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["includeView"] = "nope",
            }, 14);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsTrue(result.GetProperty("isError").GetBoolean(), call.Body);
            Assert.IsFalse(
                result.TryGetProperty("structuredContent", out var structured)
                && structured.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined,
                call.Body);

            using var payload = JsonDocument.Parse(TextOf(result, call.Body));
            var keys = payload.RootElement.EnumerateObject().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "code", "message" }, keys);
            Assert.AreEqual("execution_failed", payload.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("列出資料表失敗。", payload.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(payload.RootElement.TryGetProperty("rows", out _), call.Body);

            var again = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 15);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Fields_schema_has_connection_id_and_table_only()
    {
        await using var factory = CreateFactory([]);
        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);

        var fields = tools.Single(tool => tool.GetProperty("name").GetString() == "fields");
        var schema = fields.GetProperty("inputSchema");
        var names = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "connectionId", "table" }, names);

        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.Contains(required, "connectionId");
        Assert.IsFalse(required.Contains("table"));

        var raw = schema.GetRawText();
        Assert.IsFalse(raw.Contains("connectionString", StringComparison.OrdinalIgnoreCase), raw);
        Assert.IsFalse(raw.Contains("connectString", StringComparison.OrdinalIgnoreCase), raw);
        Assert.IsFalse(raw.Contains("includeView", StringComparison.OrdinalIgnoreCase), raw);
    }

    [TestMethod]
    public async Task Fields_for_products_matches_direct_session_and_includes_view()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 20);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var session = ExecuteFields(connectionString, "products");
            Assert.AreEqual(QueryKind.Rows, session.Kind);
            Assert.IsTrue(session.Rows.Any(row => row.Any(cell => cell is bool)));
            AssertFieldsMatchSession(TextOf(result, call.Body), session);

            var view = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "product_view",
            }, 21);
            Assert.AreEqual(HttpStatusCode.OK, view.Status, view.Body);
            var viewResult = ResultOf(view.Rpc, view.Body);
            Assert.IsFalse(viewResult.GetProperty("isError").GetBoolean(), view.Body);
            AssertFieldsMatchSession(TextOf(viewResult, view.Body), ExecuteFields(connectionString, "product_view"));
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Fields_table_name_differs_only_by_case_and_matches_session()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "Products",
            }, 22);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            AssertFieldsMatchSession(TextOf(result, call.Body), ExecuteFields(connectionString, "Products"));
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Missing_table_is_unknown_table_and_host_continues()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "no_such_table",
            }, 23);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            AssertErrorPayload(result, call.Body, "unknown_table", "找不到資料表。");

            var again = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 24);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public void Unknown_table_does_not_call_fields()
    {
        var called = false;
        var outcome = TableDispatch.FieldsAfterTables(
            QueryResult.FromRows("tables", ["name"], [["products"]]),
            "no_such_table",
            () =>
            {
                called = true;
                return QueryResult.FromRows("fields", ["name", "type", "pk", "notNull"], []);
            });

        Assert.IsFalse(called);
        Assert.IsTrue(outcome.IsError);
        using var payload = JsonDocument.Parse(outcome.Json);
        Assert.AreEqual("unknown_table", payload.RootElement.GetProperty("code").GetString());
        Assert.IsFalse(payload.RootElement.TryGetProperty("columns", out _));
        Assert.IsFalse(payload.RootElement.TryGetProperty("rows", out _));
    }

    [TestMethod]
    public void Probe_error_is_execution_failed_and_does_not_call_fields()
    {
        var called = false;
        var outcome = TableDispatch.FieldsAfterTables(
            QueryResult.FromError("Data Source=mcpsecretmarker-probe"),
            "products",
            () =>
            {
                called = true;
                return QueryResult.FromRows("fields", ["name", "type", "pk", "notNull"], [["Id", "INTEGER", true, true]]);
            });

        Assert.IsFalse(called);
        Assert.IsTrue(outcome.IsError);
        Assert.IsFalse(outcome.Json.Contains("mcpsecretmarker", StringComparison.Ordinal), outcome.Json);
        Assert.IsFalse(outcome.Json.Contains("unknown_table", StringComparison.Ordinal), outcome.Json);
        using var payload = JsonDocument.Parse(outcome.Json);
        var keys = payload.RootElement.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "code", "message" }, keys);
        Assert.AreEqual("execution_failed", payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual("列出欄位失敗。", payload.RootElement.GetProperty("message").GetString());
    }

    [TestMethod]
    public void Fields_error_after_hit_is_execution_failed()
    {
        var called = false;
        var outcome = TableDispatch.FieldsAfterTables(
            QueryResult.FromRows("tables", ["name"], [["products"]]),
            "products",
            () =>
            {
                called = true;
                return QueryResult.FromError("Data Source=mcpsecretmarker-fields");
            });

        Assert.IsTrue(called);
        Assert.IsTrue(outcome.IsError);
        Assert.IsFalse(outcome.Json.Contains("mcpsecretmarker", StringComparison.Ordinal), outcome.Json);
        using var payload = JsonDocument.Parse(outcome.Json);
        Assert.AreEqual("execution_failed", payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual("列出欄位失敗。", payload.RootElement.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task Missing_or_blank_table_is_invalid_arguments_and_does_not_create_database()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            Assert.IsFalse(File.Exists(dbPath));
            await using var factory = CreateFactory(SqliteSettings("warehouse", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);
            Assert.IsFalse(File.Exists(dbPath));

            var omitted = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 25);
            AssertInvalidTable(omitted.Status, omitted.Body, omitted.Rpc);
            Assert.IsFalse(File.Exists(dbPath));

            var blank = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["table"] = "   ",
            }, 26);
            AssertInvalidTable(blank.Status, blank.Body, blank.Rpc);
            Assert.IsFalse(File.Exists(dbPath));

            var empty = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["table"] = "",
            }, 27);
            AssertInvalidTable(empty.Status, empty.Body, empty.Rpc);
            Assert.IsFalse(File.Exists(dbPath));

            var again = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 28);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertNoAuditFile(factory);
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Other_read_tools_do_not_execute_or_create_database()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            await using var factory = CreateFactory(SqliteSettings("warehouse", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);
            Assert.IsFalse(File.Exists(dbPath));

            var id = 29;
            foreach (var name in new[] { "get", "query", "count", "exists" })
                await AssertCallDoesNotExecuteAsync(client, name, id++, "warehouse");

            Assert.IsFalse(File.Exists(dbPath));
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
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

    private static async Task AssertCallDoesNotExecuteAsync(HttpClient client, string name, int id, string connectionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{\"name\":\"" + name + "\",\"arguments\":{\"connectionId\":\"" + connectionId + "\"}}}",
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

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallToolAsync(
        HttpClient client,
        string name,
        IReadOnlyDictionary<string, object?> arguments,
        int id)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "tools/call",
            ["params"] = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["arguments"] = arguments,
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body, ParseRpc(body));
    }

    private static JsonElement ResultOf(JsonElement rpc, string body)
    {
        Assert.IsFalse(rpc.TryGetProperty("error", out _), body);
        Assert.IsTrue(rpc.TryGetProperty("result", out var result), body);
        return result;
    }

    private static string TextOf(JsonElement result, string body)
    {
        var items = result.GetProperty("content").EnumerateArray().ToArray();
        Assert.AreEqual(1, items.Length, body);
        Assert.AreEqual("text", items[0].GetProperty("type").GetString(), body);
        return items[0].GetProperty("text").GetString() ?? "";
    }

    private static void AssertPayloadMatchesSession(string json, QueryResult session)
    {
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement;
        var keys = payload.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "columns", "rows" }, keys);

        var columns = payload.GetProperty("columns").EnumerateArray().Select(column => column.GetString()).ToArray();
        CollectionAssert.AreEqual(session.Columns.ToArray(), columns);

        var actual = payload.GetProperty("rows").EnumerateArray()
            .Select(row => row.EnumerateArray().Select(cell => cell.GetString()).ToArray())
            .ToArray();
        var expected = session.Rows.Select(row => row.Select(cell => Convert.ToString(cell)).ToArray()).ToArray();
        Assert.AreEqual(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
            CollectionAssert.AreEqual(expected[index], actual[index]);
    }

    private static void AssertFieldsMatchSession(string json, QueryResult session)
    {
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement;
        var keys = payload.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "columns", "rows" }, keys);

        var columns = payload.GetProperty("columns").EnumerateArray().Select(column => column.GetString()).ToArray();
        CollectionAssert.AreEqual(session.Columns.ToArray(), columns);

        var rows = payload.GetProperty("rows").EnumerateArray().ToArray();
        Assert.AreEqual(session.Rows.Count, rows.Length);
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var cells = rows[rowIndex].EnumerateArray().ToArray();
            Assert.AreEqual(session.Rows[rowIndex].Count, cells.Length);
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                AssertCellEquals(session.Rows[rowIndex][cellIndex], cells[cellIndex]);
        }
    }

    private static void AssertCellEquals(object? expected, JsonElement actual)
    {
        switch (expected)
        {
            case null:
                Assert.AreEqual(JsonValueKind.Null, actual.ValueKind);
                return;
            case bool flag:
                Assert.AreEqual(flag ? JsonValueKind.True : JsonValueKind.False, actual.ValueKind);
                Assert.AreEqual(flag, actual.GetBoolean());
                return;
            case string text:
                Assert.AreEqual(JsonValueKind.String, actual.ValueKind);
                Assert.AreEqual(text, actual.GetString());
                return;
            default:
                Assert.AreEqual(Convert.ToString(expected), actual.ToString());
                return;
        }
    }

    private static void AssertErrorPayload(JsonElement result, string body, string code, string message)
    {
        Assert.IsTrue(result.GetProperty("isError").GetBoolean(), body);
        Assert.IsFalse(
            result.TryGetProperty("structuredContent", out var structured)
            && structured.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined,
            body);

        using var payload = JsonDocument.Parse(TextOf(result, body));
        var keys = payload.RootElement.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "code", "message" }, keys);
        Assert.AreEqual(code, payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual(message, payload.RootElement.GetProperty("message").GetString());
        Assert.IsFalse(payload.RootElement.TryGetProperty("columns", out _), body);
        Assert.IsFalse(payload.RootElement.TryGetProperty("rows", out _), body);
        Assert.IsFalse(message.Contains("mcpsecretmarker", StringComparison.Ordinal), message);
    }

    private static void AssertInvalidTable(HttpStatusCode status, string body, JsonElement rpc)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        AssertErrorPayload(ResultOf(rpc, body), body, "invalid_arguments", "未提供資料表名稱。");
    }

    private static QueryResult ExecuteFields(string connectionString, string table)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            return service!.GetSession().Execute(new QueryRequest
            {
                Text = "fields",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table,
                },
            });
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static QueryResult ExecuteTables(string connectionString, object? includeView)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (includeView is not null)
                arguments["includeView"] = includeView;
            return service!.GetSession().Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call,
                Arguments = arguments,
            });
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static void SeedProducts(string connectionString)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            service!.ExecuteSQL("CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Note TEXT)");
            service.ExecuteSQL("CREATE VIEW product_view AS SELECT Id, Name, Note FROM products");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static void SeedItemAndView(string connectionString)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            service!.ExecuteSQL("CREATE TABLE item (Id INTEGER PRIMARY KEY, Name TEXT)");
            service.ExecuteSQL("CREATE VIEW item_view AS SELECT Id, Name FROM item");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static KeyValuePair<string, string?>[] SqliteSettings(string name, string connectionString)
        =>
        [
            new($"Connections:{name}:ServerType", "Sqlite"),
            new($"Connections:{name}:ConnectionString", connectionString),
        ];

    private static string NewDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"mcpsecretmarker-{Guid.NewGuid():N}.db");

    private static string ConnectionStringFor(string dbPath) => $"Data Source={dbPath}";

    private static void DeleteDatabase(string dbPath)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var connectionType = assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection");
            if (connectionType is null)
                continue;

            connectionType.GetMethod("ClearAllPools", Type.EmptyTypes)?.Invoke(null, null);
            break;
        }

        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    private static void AssertNoAuditFile(WebApplicationFactory<Program> factory)
    {
        var root = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        Assert.IsFalse(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")), root);
    }
}
