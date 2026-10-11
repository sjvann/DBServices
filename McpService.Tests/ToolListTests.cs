using System.Globalization;
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
[DoNotParallelize]
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
        AssertEnteredAudit(factory, 5, "warehouse");
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
        AssertNoAuditFile(factory);
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
            AssertNoAuditFile(factory);
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
        AssertNoAuditFile(factory);
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
        AssertNoAuditFile(factory);
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
        AssertNoAuditFile(factory);
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
            AssertEnteredAudit(factory, 1, "Inventory");
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
            AssertEnteredAudit(factory, 1, "Inventory");
            Assert.AreEqual(
                JsonValueKind.False,
                ReadAudit(factory)[0].GetProperty("arguments").GetProperty("includeView").ValueKind);
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
            AssertEnteredAudit(factory, 2, "warehouse");
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
            AssertEnteredAudit(factory, 2, "warehouse");
            Assert.AreEqual(
                "nope",
                ReadAudit(factory)[0].GetProperty("arguments").GetProperty("includeView").GetString());
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
        AssertNoAuditFile(factory);
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
            AssertEnteredAudit(factory, 2, "Inventory");
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
            AssertEnteredAudit(factory, 1, "Inventory");
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
            AssertEnteredAudit(factory, 2, "Inventory");
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
            AssertEnteredAudit(factory, 4, "warehouse");
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
            AssertEnteredAudit(factory, 4, "warehouse");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Get_schema_has_connection_id_table_and_id_only()
    {
        await using var factory = CreateFactory([]);
        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);

        var get = tools.Single(tool => tool.GetProperty("name").GetString() == "get");
        var schema = get.GetProperty("inputSchema");
        var names = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "connectionId", "table", "id" }, names);

        var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.Contains(required, "connectionId");
        Assert.IsFalse(required.Contains("table"));
        Assert.IsFalse(required.Contains("id"));

        var raw = schema.GetRawText();
        Assert.IsFalse(raw.Contains("connectionString", StringComparison.OrdinalIgnoreCase), raw);
        Assert.IsFalse(raw.Contains("connectString", StringComparison.OrdinalIgnoreCase), raw);
        AssertNoAuditFile(factory);
    }

    [TestMethod]
    public async Task Get_row_matches_direct_session_and_integer_cells_are_numbers()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, -2, "neg", null);
            InsertProduct(connectionString, 0, "zero", "z");
            InsertProduct(connectionString, 1, "one", "n");
            InsertProduct(connectionString, long.MinValue, "min", null);
            InsertProduct(connectionString, long.MaxValue, "max", "m");
            InsertProduct(connectionString, 100, "hundred", null);

            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var id = 40;
            foreach (var key in new long[] { -2, 0, 1, long.MinValue, long.MaxValue })
            {
                var call = await CallToolAsync(client, "get", new Dictionary<string, object?>
                {
                    ["connectionId"] = "inventory",
                    ["table"] = "products",
                    ["id"] = key,
                }, id++);

                Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
                Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
                var result = ResultOf(call.Rpc, call.Body);
                Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
                var session = ExecuteGet(connectionString, "products", key);
                Assert.AreEqual(QueryKind.Rows, session.Kind);
                Assert.AreEqual(1, session.Rows.Count);
                AssertFieldsMatchSession(TextOf(result, call.Body), session);
            }

            var fractional = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = JsonLiteral("1.0"),
            }, id++);
            Assert.AreEqual(HttpStatusCode.OK, fractional.Status, fractional.Body);
            var fractionalResult = ResultOf(fractional.Rpc, fractional.Body);
            Assert.IsFalse(fractionalResult.GetProperty("isError").GetBoolean(), fractional.Body);
            AssertFieldsMatchSession(TextOf(fractionalResult, fractional.Body), ExecuteGet(connectionString, "products", 1));

            var negativeFractional = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = JsonLiteral("-2.0"),
            }, id++);
            Assert.AreEqual(HttpStatusCode.OK, negativeFractional.Status, negativeFractional.Body);
            var negativeResult = ResultOf(negativeFractional.Rpc, negativeFractional.Body);
            Assert.IsFalse(negativeResult.GetProperty("isError").GetBoolean(), negativeFractional.Body);
            AssertFieldsMatchSession(
                TextOf(negativeResult, negativeFractional.Body),
                ExecuteGet(connectionString, "products", -2));

            var zeroScale = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = JsonLiteral("0.0"),
            }, id++);
            Assert.AreEqual(HttpStatusCode.OK, zeroScale.Status, zeroScale.Body);
            var zeroResult = ResultOf(zeroScale.Rpc, zeroScale.Body);
            Assert.IsFalse(zeroResult.GetProperty("isError").GetBoolean(), zeroScale.Body);
            AssertFieldsMatchSession(TextOf(zeroResult, zeroScale.Body), ExecuteGet(connectionString, "products", 0));

            var scientific = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = JsonLiteral("1e2"),
            }, id);
            Assert.AreEqual(HttpStatusCode.OK, scientific.Status, scientific.Body);
            var scientificResult = ResultOf(scientific.Rpc, scientific.Body);
            Assert.IsFalse(scientificResult.GetProperty("isError").GetBoolean(), scientific.Body);
            AssertFieldsMatchSession(TextOf(scientificResult, scientific.Body), ExecuteGet(connectionString, "products", 100));
            AssertEnteredAudit(factory, 9, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Get_missing_row_returns_empty_rows_and_host_continues()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = 999,
            }, 50);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = ResultOf(call.Rpc, call.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var session = ExecuteGet(connectionString, "products", 999);
            Assert.AreEqual(QueryKind.Rows, session.Kind);
            Assert.AreEqual(0, session.Rows.Count);
            AssertFieldsMatchSession(TextOf(result, call.Body), session);

            var again = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = 1,
            }, 51);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            var againResult = ResultOf(again.Rpc, again.Body);
            Assert.IsFalse(againResult.GetProperty("isError").GetBoolean(), again.Body);
            AssertFieldsMatchSession(TextOf(againResult, again.Body), ExecuteGet(connectionString, "products", 1));
            AssertEnteredAudit(factory, 2, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public void Supplied_get_error_is_execution_failed_without_connection_string()
    {
        var outcome = TableDispatch.FromGet(QueryResult.FromError("Data Source=mcpsecretmarker-get"));

        Assert.IsTrue(outcome.IsError);
        Assert.IsFalse(outcome.Json.Contains("mcpsecretmarker", StringComparison.Ordinal), outcome.Json);
        Assert.IsFalse(outcome.Json.Contains("unknown_table", StringComparison.Ordinal), outcome.Json);
        Assert.IsFalse(outcome.Json.Contains("rows", StringComparison.Ordinal), outcome.Json);
        using var payload = JsonDocument.Parse(outcome.Json);
        var keys = payload.RootElement.EnumerateObject().Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(new[] { "code", "message" }, keys);
        Assert.AreEqual("execution_failed", payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual("依 Id 取一列失敗。", payload.RootElement.GetProperty("message").GetString());
    }

    [TestMethod]
    public async Task Get_invalid_arguments_do_not_create_database()
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

            var cases = new (int Id, Dictionary<string, object?> Arguments)[]
            {
                (70, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["id"] = 1 }),
                (71, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "   ", ["id"] = 1 }),
                (72, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "", ["id"] = 1 }),
                (73, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products" }),
                (74, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = "1" }),
                (75, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = true }),
                (76, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = new Dictionary<string, object?> { ["n"] = 1 } }),
                (77, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = new object[] { 1 } }),
                (78, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("1.5") }),
                (79, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = null }),
                (80, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("9223372036854775808") }),
                (81, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("-9223372036854775809") }),
                (83, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("1e-29") }),
                (84, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("-1e-29") }),
                (85, new Dictionary<string, object?> { ["connectionId"] = "warehouse", ["table"] = "products", ["id"] = JsonLiteral("1.00000000000000000000000000001") }),
            };

            foreach (var (caseId, arguments) in cases)
            {
                var call = await CallToolAsync(client, "get", arguments, caseId);
                AssertInvalidGet(call.Status, call.Body, call.Rpc);
                Assert.IsFalse(File.Exists(dbPath));
            }

            var again = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["table"] = "products",
                ["id"] = JsonLiteral("1.5"),
            }, 82);
            AssertInvalidGet(again.Status, again.Body, again.Rpc);
            Assert.IsFalse(File.Exists(dbPath));
            AssertEnteredAudit(factory, 16, "warehouse");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Get_missing_table_matches_session_and_is_not_unknown_table()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var session = ExecuteGet(connectionString, "no_such_table", 1);
            var call = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "no_such_table",
                ["id"] = 1,
            }, 90);

            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
            Assert.IsFalse(call.Body.Contains("unknown_table", StringComparison.Ordinal), call.Body);
            if (session.Kind == QueryKind.Error)
            {
                AssertErrorPayload(ResultOf(call.Rpc, call.Body), call.Body, "execution_failed", "依 Id 取一列失敗。");
                Assert.IsFalse(string.IsNullOrEmpty(session.Error));
                Assert.IsFalse(call.Body.Contains(session.Error!, StringComparison.Ordinal), call.Body);
            }
            else
            {
                Assert.AreEqual(QueryKind.Rows, session.Kind);
                var result = ResultOf(call.Rpc, call.Body);
                Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
                AssertFieldsMatchSession(TextOf(result, call.Body), session);
            }

            var again = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 91);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertEnteredAudit(factory, 2, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Equality_schema_has_connection_id_table_and_filters_only()
    {
        await using var factory = CreateFactory([]);
        using var client = CreateClient(factory);
        var tools = await ListToolsAsync(client);

        foreach (var name in new[] { "query", "count", "exists" })
        {
            var tool = tools.Single(candidate => candidate.GetProperty("name").GetString() == name);
            var schema = tool.GetProperty("inputSchema");
            var names = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "connectionId", "table", "filters" }, names);

            var required = schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
            CollectionAssert.Contains(required, "connectionId");
            Assert.IsFalse(required.Contains("table"), name);
            Assert.IsFalse(required.Contains("filters"), name);

            var raw = schema.GetRawText();
            Assert.IsFalse(raw.Contains("connectionString", StringComparison.OrdinalIgnoreCase), raw);
            Assert.IsFalse(raw.Contains("connectString", StringComparison.OrdinalIgnoreCase), raw);
        }

        AssertNoAuditFile(factory);
    }

    [TestMethod]
    public async Task Query_scalar_filters_match_session_and_integer_cells_are_numbers()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, -2, "neg", null);
            InsertProduct(connectionString, 0, "zero", "z");
            InsertProduct(connectionString, 1, "one", "n");

            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var id = 100;
            var combined = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = JsonLiteral("1.0"),
                    ["Name"] = "one",
                },
            }, id++);
            AssertQueryMatches(
                combined.Status,
                combined.Body,
                combined.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Id"] = 1L,
                    ["Name"] = "one",
                }));

            var negative = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = JsonLiteral("-2.0"),
                },
            }, id++);
            AssertQueryMatches(
                negative.Status,
                negative.Body,
                negative.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Id"] = -2L,
                }));

            var flag = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = true,
                },
            }, id++);
            AssertQueryMatches(
                flag.Status,
                flag.Body,
                flag.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Id"] = true,
                }));

            var zeroFlag = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = false,
                },
            }, id++);
            var zeroSession = ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
            {
                ["table"] = "products",
                ["Id"] = false,
            });
            AssertQueryMatches(zeroFlag.Status, zeroFlag.Body, zeroFlag.Rpc, zeroSession);
            AssertSingleProductId(TextOf(ResultOf(zeroFlag.Rpc, zeroFlag.Body), zeroFlag.Body), zeroSession, 0L);

            var fraction = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = JsonLiteral("1.5"),
                },
            }, id);
            AssertToolMatchesSession(
                "query",
                fraction.Status,
                fraction.Body,
                fraction.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Id"] = 1.5m,
                }));
            AssertEnteredAudit(factory, 5, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Query_with_no_matching_rows_returns_empty_rows_and_host_continues()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var call = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "missing",
                },
            }, 110);
            var session = ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
            {
                ["table"] = "products",
                ["Name"] = "missing",
            });
            AssertQueryMatches(call.Status, call.Body, call.Rpc, session);
            Assert.AreEqual(0, session.Rows.Count);

            var again = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = 1,
            }, 111);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertEnteredAudit(factory, 2, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Count_matches_session_cell_and_is_zero_when_no_rows()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            InsertProduct(connectionString, 0, "zero", null);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var matched = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 120);
            AssertScalarCallMatches(
                matched.Status,
                matched.Body,
                matched.Rpc,
                ExecuteNamed(connectionString, "count", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Name"] = "one",
                }),
                0L);

            var none = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "missing",
                },
            }, 121);
            var noneSession = ExecuteNamed(connectionString, "count", new Dictionary<string, object?>
            {
                ["table"] = "products",
                ["Name"] = "missing",
            });
            AssertScalarCallMatches(none.Status, none.Body, none.Rpc, noneSession, 0L);
            Assert.AreEqual(0L, Convert.ToInt64(noneSession.Rows[0][0], CultureInfo.InvariantCulture));

            var otherNumber = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Id"] = JsonLiteral("1.5"),
                },
            }, 122);
            AssertScalarCallMatches(
                otherNumber.Status,
                otherNumber.Body,
                otherNumber.Rpc,
                ExecuteNamed(connectionString, "count", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Id"] = 1.5m,
                }),
                0L);

            var nullFilter = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Note"] = null,
                },
            }, 123);
            AssertScalarCallMatches(
                nullFilter.Status,
                nullFilter.Body,
                nullFilter.Rpc,
                ExecuteNamed(connectionString, "count", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Note"] = null,
                }),
                0L);

            var countForZero = await CountIdAsync(0L, 124);
            var countForOne = await CountIdAsync(1L, 125);
            Assert.AreEqual(1L, countForZero);
            Assert.AreEqual(1L, countForOne);
            var totalCall = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 126);
            Assert.AreEqual(HttpStatusCode.OK, totalCall.Status, totalCall.Body);
            var total = JsonCount(totalCall.Body, totalCall.Rpc);
            Assert.IsTrue(total > 0);

            var callId = 127;
            foreach (var literal in new[] { "1e-29", "-1e-29", "1.00000000000000000000000000001" })
            {
                var actual = await CountIdAsync(JsonLiteral(literal), callId++);
                Assert.AreNotEqual(countForZero, actual, literal);
                Assert.AreNotEqual(countForOne, actual, literal);
            }

            foreach (var literal in new[] { "1e29", "1e309" })
            {
                var actual = await CountIdAsync(JsonLiteral(literal), callId++);
                Assert.AreEqual(0L, actual, literal);
                Assert.AreNotEqual(total, actual, literal);
            }

            AssertEnteredAudit(factory, 12, "Inventory");

            async Task<long> CountIdAsync(object? id, int callId)
            {
                var call = await CallToolAsync(client, "count", new Dictionary<string, object?>
                {
                    ["connectionId"] = "inventory",
                    ["table"] = "products",
                    ["filters"] = new Dictionary<string, object?>
                    {
                        ["Id"] = id,
                    },
                }, callId);
                Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
                Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
                return JsonCount(call.Body, call.Rpc);
            }
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Exists_matches_session_cell()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var present = await CallToolAsync(client, "exists", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 130);
            var presentSession = ExecuteNamed(connectionString, "exists", new Dictionary<string, object?>
            {
                ["table"] = "products",
                ["Name"] = "one",
            });
            AssertScalarCallMatches(present.Status, present.Body, present.Rpc, presentSession, false);
            Assert.AreEqual(true, presentSession.Rows[0][0]);

            var absent = await CallToolAsync(client, "exists", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "missing",
                },
            }, 131);
            var absentSession = ExecuteNamed(connectionString, "exists", new Dictionary<string, object?>
            {
                ["table"] = "products",
                ["Name"] = "missing",
            });
            AssertScalarCallMatches(absent.Status, absent.Body, absent.Rpc, absentSession, false);
            Assert.AreEqual(false, absentSession.Rows[0][0]);
            AssertEnteredAudit(factory, 2, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Omitting_filters_or_empty_object_matches_table_only_session()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            InsertProduct(connectionString, 0, "zero", null);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var tableOnly = new Dictionary<string, object?>
            {
                ["table"] = "products",
            };
            var id = 140;
            foreach (var name in new[] { "query", "count", "exists" })
            {
                var omitted = await CallToolAsync(client, name, new Dictionary<string, object?>
                {
                    ["connectionId"] = "inventory",
                    ["table"] = "products",
                }, id++);
                var empty = await CallToolAsync(client, name, new Dictionary<string, object?>
                {
                    ["connectionId"] = "inventory",
                    ["table"] = "products",
                    ["filters"] = new Dictionary<string, object?>(),
                }, id++);
                var session = ExecuteNamed(connectionString, name, tableOnly);
                AssertToolMatchesSession(name, omitted.Status, omitted.Body, omitted.Rpc, session);
                AssertToolMatchesSession(name, empty.Status, empty.Body, empty.Rpc, session);
                if (name == "count")
                    Assert.IsTrue(Convert.ToInt64(session.Rows[0][0], CultureInfo.InvariantCulture) > 0);
                if (name == "exists")
                    Assert.AreEqual(true, session.Rows[0][0]);
            }

            AssertEnteredAudit(factory, 6, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public void Supplied_equality_error_is_execution_failed_without_connection_string()
    {
        var cases = new (TableDispatchResult Outcome, string Message)[]
        {
            (TableDispatch.FromQuery(QueryResult.FromError("Data Source=mcpsecretmarker-query")), "鍵值查詢失敗。"),
            (TableDispatch.FromCount(QueryResult.FromError("Data Source=mcpsecretmarker-count")), "計數失敗。"),
            (TableDispatch.FromExists(QueryResult.FromError("Data Source=mcpsecretmarker-exists")), "是否存在失敗。"),
        };

        foreach (var (outcome, message) in cases)
        {
            Assert.IsTrue(outcome.IsError);
            Assert.AreNotEqual("0", outcome.Json);
            Assert.AreNotEqual("false", outcome.Json);
            Assert.IsFalse(outcome.Json.Contains("mcpsecretmarker", StringComparison.Ordinal), outcome.Json);
            Assert.IsFalse(outcome.Json.Contains("unknown_table", StringComparison.Ordinal), outcome.Json);
            Assert.IsFalse(outcome.Json.Contains("rows", StringComparison.Ordinal), outcome.Json);
            Assert.IsFalse(outcome.Json.Contains("columns", StringComparison.Ordinal), outcome.Json);
            using var payload = JsonDocument.Parse(outcome.Json);
            var keys = payload.RootElement.EnumerateObject().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "code", "message" }, keys);
            Assert.AreEqual("execution_failed", payload.RootElement.GetProperty("code").GetString());
            Assert.AreEqual(message, payload.RootElement.GetProperty("message").GetString());
        }
    }

    [TestMethod]
    public async Task Equality_missing_or_blank_table_is_invalid_arguments_and_does_not_create_database()
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

            var id = 150;
            foreach (var name in new[] { "query", "count", "exists" })
            {
                var cases = new Dictionary<string, object?>[]
                {
                    new() { ["connectionId"] = "warehouse" },
                    new() { ["connectionId"] = "warehouse", ["table"] = "   " },
                    new() { ["connectionId"] = "warehouse", ["table"] = "" },
                };
                foreach (var arguments in cases)
                {
                    var call = await CallToolAsync(client, name, arguments, id++);
                    AssertInvalidTable(call.Status, call.Body, call.Rpc);
                    Assert.IsFalse(File.Exists(dbPath));
                }
            }

            var again = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, id);
            AssertInvalidTable(again.Status, again.Body, again.Rpc);
            Assert.IsFalse(File.Exists(dbPath));
            AssertEnteredAudit(factory, 10, "warehouse");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Equality_missing_table_matches_session_and_is_not_unknown_table()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var id = 160;
            foreach (var name in new[] { "query", "count", "exists" })
            {
                var session = ExecuteNamed(connectionString, name, new Dictionary<string, object?>
                {
                    ["table"] = "no_such_table",
                });
                var call = await CallToolAsync(client, name, new Dictionary<string, object?>
                {
                    ["connectionId"] = "inventory",
                    ["table"] = "no_such_table",
                }, id++);

                Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
                Assert.IsFalse(call.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), call.Body);
                Assert.IsFalse(call.Body.Contains("unknown_table", StringComparison.Ordinal), call.Body);
                if (session.Kind == QueryKind.Error)
                {
                    var message = name switch
                    {
                        "count" => "計數失敗。",
                        "exists" => "是否存在失敗。",
                        _ => "鍵值查詢失敗。",
                    };
                    AssertErrorPayload(ResultOf(call.Rpc, call.Body), call.Body, "execution_failed", message);
                    Assert.IsFalse(string.IsNullOrEmpty(session.Error));
                    Assert.IsFalse(call.Body.Contains(session.Error!, StringComparison.Ordinal), call.Body);
                }
                else if (name == "query")
                {
                    AssertQueryMatches(call.Status, call.Body, call.Rpc, session);
                }
                else
                {
                    AssertScalarCallMatches(
                        call.Status,
                        call.Body,
                        call.Rpc,
                        session,
                        name == "count" ? 0L : false);
                }
            }

            var again = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, id);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);
            AssertEnteredAudit(factory, 4, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_row_tools_record_row_count_without_row_data()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "rowpayload-audit-marker");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var tables = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 200);
            Assert.AreEqual(HttpStatusCode.OK, tables.Status, tables.Body);
            var tablesResult = ResultOf(tables.Rpc, tables.Body);
            Assert.IsFalse(tablesResult.GetProperty("isError").GetBoolean(), tables.Body);
            var tablesText = TextOf(tablesResult, tables.Body);
            Assert.IsTrue(tablesText.Contains("product_view", StringComparison.Ordinal), tablesText);

            var get = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = 1,
            }, 201);
            Assert.AreEqual(HttpStatusCode.OK, get.Status, get.Body);
            var getResult = ResultOf(get.Rpc, get.Body);
            Assert.IsFalse(getResult.GetProperty("isError").GetBoolean(), get.Body);
            var getText = TextOf(getResult, get.Body);
            Assert.IsTrue(getText.Contains("rowpayload-audit-marker", StringComparison.Ordinal), getText);

            var query = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 202);
            Assert.AreEqual(HttpStatusCode.OK, query.Status, query.Body);
            var queryResult = ResultOf(query.Rpc, query.Body);
            Assert.IsFalse(queryResult.GetProperty("isError").GetBoolean(), query.Body);
            Assert.IsTrue(TextOf(queryResult, query.Body).Contains("rowpayload-audit-marker", StringComparison.Ordinal), query.Body);

            var records = ReadAudit(factory);
            Assert.AreEqual(3, records.Length);
            AssertRowAudit(records[0], "tables", RowCount(tablesText), "Inventory");
            Assert.IsFalse(records[0].GetRawText().Contains("product_view", StringComparison.Ordinal), records[0].GetRawText());
            Assert.IsFalse(records[0].GetProperty("arguments").TryGetProperty("includeView", out _));
            AssertRowAudit(records[1], "get", 1, "Inventory");
            Assert.IsFalse(records[1].GetRawText().Contains("rowpayload-audit-marker", StringComparison.Ordinal), records[1].GetRawText());
            Assert.AreEqual("products", records[1].GetProperty("arguments").GetProperty("table").GetString());
            Assert.AreEqual(1, records[1].GetProperty("arguments").GetProperty("id").GetInt64());
            AssertRowAudit(records[2], "query", 1, "Inventory");
            Assert.IsFalse(records[2].GetRawText().Contains("rowpayload-audit-marker", StringComparison.Ordinal), records[2].GetRawText());
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_count_and_exists_record_value()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var matched = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 210);
            var missing = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "missing",
                },
            }, 211);
            var present = await CallToolAsync(client, "exists", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 212);
            var absent = await CallToolAsync(client, "exists", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "missing",
                },
            }, 213);

            Assert.AreEqual(1L, JsonCount(matched.Body, matched.Rpc));
            Assert.AreEqual(0L, JsonCount(missing.Body, missing.Rpc));
            Assert.AreEqual(HttpStatusCode.OK, present.Status, present.Body);
            Assert.AreEqual("true", TextOf(ResultOf(present.Rpc, present.Body), present.Body));
            Assert.AreEqual("false", TextOf(ResultOf(absent.Rpc, absent.Body), absent.Body));

            var records = ReadAudit(factory);
            Assert.AreEqual(4, records.Length);
            AssertScalarAudit(records[0], "count", 1L, "Inventory");
            AssertScalarAudit(records[1], "count", 0L, "Inventory");
            AssertScalarAudit(records[2], "exists", true, "Inventory");
            AssertScalarAudit(records[3], "exists", false, "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_existing_rejections_record_the_error_code()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var omitted = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 220);
            AssertInvalidTable(omitted.Status, omitted.Body, omitted.Rpc);

            var unknown = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "no_such_table",
            }, 221);
            Assert.AreEqual(HttpStatusCode.OK, unknown.Status, unknown.Body);
            AssertErrorPayload(ResultOf(unknown.Rpc, unknown.Body), unknown.Body, "unknown_table", "找不到資料表。");

            var secret = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "   ",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["connectionString"] = connectionString,
                    ["ConnectString"] = connectionString,
                    ["Name"] = "one",
                },
            }, 222);
            AssertInvalidTable(secret.Status, secret.Body, secret.Rpc);

            var again = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 223);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(ResultOf(again.Rpc, again.Body).GetProperty("isError").GetBoolean(), again.Body);

            var records = ReadAudit(factory);
            Assert.AreEqual(4, records.Length);
            AssertErrorAudit(records[0], "fields", "invalid_arguments", "Inventory");
            AssertErrorAudit(records[1], "fields", "unknown_table", "Inventory");
            Assert.AreEqual("no_such_table", records[1].GetProperty("arguments").GetProperty("table").GetString());
            AssertErrorAudit(records[2], "query", "invalid_arguments", "Inventory");
            var filters = records[2].GetProperty("arguments").GetProperty("filters");
            Assert.AreEqual("one", filters.GetProperty("Name").GetString());
            Assert.IsFalse(filters.TryGetProperty("connectionString", out _));
            Assert.IsFalse(filters.TryGetProperty("ConnectString", out _));
            Assert.IsFalse(records[2].GetRawText().Contains("mcpsecretmarker", StringComparison.Ordinal), records[2].GetRawText());
            AssertRowAudit(records[3], "tables", RowCount(TextOf(ResultOf(again.Rpc, again.Body), again.Body)), "Inventory");
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_caller_header_or_anonymous_still_runs()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            await using var factory = CreateFactory(SqliteSettings("warehouse", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var named = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 230, "nightly-agent");
            var blank = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 231, "   ");
            var missing = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 232);

            Assert.IsFalse(ResultOf(named.Rpc, named.Body).GetProperty("isError").GetBoolean(), named.Body);
            Assert.IsFalse(ResultOf(blank.Rpc, blank.Body).GetProperty("isError").GetBoolean(), blank.Body);
            Assert.IsFalse(ResultOf(missing.Rpc, missing.Body).GetProperty("isError").GetBoolean(), missing.Body);

            var records = ReadAudit(factory);
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual("nightly-agent", records[0].GetProperty("caller").GetString());
            Assert.AreEqual("anonymous", records[1].GetProperty("caller").GetString());
            Assert.AreEqual("anonymous", records[2].GetProperty("caller").GetString());
            Assert.AreEqual("tables", records[0].GetProperty("tool").GetString());
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_default_and_relative_path()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        string? root = null;
        try
        {
            await using (var factory = CreateFactory(SqliteSettings("warehouse", connectionString)))
            {
                root = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
                using var client = CreateClient(factory);
                await ListToolsAsync(client);
                var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
                {
                    ["connectionId"] = "warehouse",
                }, 240);
                Assert.IsFalse(ResultOf(call.Rpc, call.Body).GetProperty("isError").GetBoolean(), call.Body);
                var records = ReadAudit(factory);
                Assert.AreEqual(1, records.Length);
                Assert.AreEqual("tables", records[0].GetProperty("tool").GetString());
                Assert.IsTrue(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")));
            }

            var settings = SqliteSettings("warehouse", connectionString).ToList();
            settings.Add(new KeyValuePair<string, string?>("Audit:Path", "nested/calls.jsonl"));
            await using var custom = CreateFactory(settings);
            using var customClient = CreateClient(custom);
            await ListToolsAsync(customClient);
            var customCall = await CallToolAsync(customClient, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 241);
            Assert.IsFalse(ResultOf(customCall.Rpc, customCall.Body).GetProperty("isError").GetBoolean(), customCall.Body);
            var customRecords = ReadAudit(custom, "nested/calls.jsonl");
            Assert.AreEqual(1, customRecords.Length);
            Assert.AreEqual("tables", customRecords[0].GetProperty("tool").GetString());
            var customRoot = custom.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            Assert.IsFalse(File.Exists(Path.Combine(customRoot, "audit", "mcp.jsonl")));
        }
        finally
        {
            if (root is not null)
            {
                var nested = Path.Combine(root, "nested");
                if (Directory.Exists(nested))
                    Directory.Delete(nested, recursive: true);
            }

            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_fields_probe_is_one_line_and_excludes_table_rows()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var tables = ExecuteTables(connectionString, true);
            var fields = ExecuteFields(connectionString, "products");
            Assert.AreNotEqual(tables.Rows.Count, fields.Rows.Count);

            var call = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 250);
            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var text = TextOf(ResultOf(call.Rpc, call.Body), call.Body);
            AssertFieldsMatchSession(text, fields);

            var records = ReadAudit(factory);
            Assert.AreEqual(1, records.Length);
            AssertRowAudit(records[0], "fields", fields.Rows.Count, "Inventory");
            Assert.AreNotEqual(tables.Rows.Count, records[0].GetProperty("rowCount").GetInt32());
            Assert.IsFalse(records[0].GetRawText().Contains("product_view", StringComparison.Ordinal), records[0].GetRawText());
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_write_failure_returns_execution_failed_without_payload()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        string? blocker = null;
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "rowpayload-audit-marker");
            var settings = SqliteSettings("Inventory", connectionString).ToList();
            settings.Add(new KeyValuePair<string, string?>("Audit:Path", "audit-not-dir/mcp.jsonl"));
            await using var factory = CreateFactory(settings);
            var root = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            blocker = Path.Combine(root, "audit-not-dir");
            File.WriteAllText(blocker, "x");
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var tables = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 260);
            AssertErrorPayload(ResultOf(tables.Rpc, tables.Body), tables.Body, "execution_failed", "列出資料表失敗。");
            Assert.IsFalse(tables.Body.Contains("rowpayload-audit-marker", StringComparison.Ordinal), tables.Body);
            Assert.IsFalse(tables.Body.Contains("\"rows\"", StringComparison.Ordinal), tables.Body);

            var count = await CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 261);
            AssertErrorPayload(ResultOf(count.Rpc, count.Body), count.Body, "execution_failed", "計數失敗。");
            Assert.AreNotEqual("1", TextOf(ResultOf(count.Rpc, count.Body), count.Body));

            var exists = await CallToolAsync(client, "exists", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 262);
            AssertErrorPayload(ResultOf(exists.Rpc, exists.Body), exists.Body, "execution_failed", "是否存在失敗。");
            var existsText = TextOf(ResultOf(exists.Rpc, exists.Body), exists.Body);
            Assert.AreNotEqual("true", existsText);
            Assert.AreNotEqual("false", existsText);
            Assert.IsFalse(exists.Body.Contains("rowpayload-audit-marker", StringComparison.Ordinal), exists.Body);

            var fields = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 263);
            AssertErrorPayload(ResultOf(fields.Rpc, fields.Body), fields.Body, "execution_failed", "列出欄位失敗。");
            Assert.IsFalse(fields.Body.Contains("\"rows\"", StringComparison.Ordinal), fields.Body);

            var get = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["id"] = 1,
            }, 264);
            AssertErrorPayload(ResultOf(get.Rpc, get.Body), get.Body, "execution_failed", "依 Id 取一列失敗。");
            Assert.IsFalse(get.Body.Contains("\"rows\"", StringComparison.Ordinal), get.Body);
            Assert.IsFalse(get.Body.Contains("rowpayload-audit-marker", StringComparison.Ordinal), get.Body);

            var query = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 265);
            AssertErrorPayload(ResultOf(query.Rpc, query.Body), query.Body, "execution_failed", "鍵值查詢失敗。");
            Assert.IsFalse(query.Body.Contains("\"rows\"", StringComparison.Ordinal), query.Body);
            Assert.IsFalse(query.Body.Contains("rowpayload-audit-marker", StringComparison.Ordinal), query.Body);

            var tools = await ListToolsAsync(client);
            AssertAdvertisedTools(tools);
            Assert.IsFalse(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")));
        }
        finally
        {
            if (blocker is not null && File.Exists(blocker))
                File.Delete(blocker);
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Audit_concurrent_calls_append_whole_lines()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var first = CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 270, "left-caller");
            var second = CallToolAsync(client, "count", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            }, 271, "right-caller");
            var completed = await Task.WhenAll(first, second);
            Assert.IsFalse(ResultOf(completed[0].Rpc, completed[0].Body).GetProperty("isError").GetBoolean(), completed[0].Body);
            Assert.AreEqual(1L, JsonCount(completed[1].Body, completed[1].Rpc));

            var records = ReadAudit(factory);
            Assert.AreEqual(2, records.Length);
            var tools = records.Select(record => record.GetProperty("tool").GetString()).ToArray();
            CollectionAssert.AreEquivalent(new[] { "tables", "count" }, tools);
            var callers = records.Select(record => record.GetProperty("caller").GetString()).ToArray();
            CollectionAssert.AreEquivalent(new[] { "left-caller", "right-caller" }, callers);
            foreach (var record in records)
            {
                using var again = JsonDocument.Parse(record.GetRawText());
                Assert.AreEqual(JsonValueKind.Object, again.RootElement.ValueKind);
            }
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Sqlite_tables_fields_and_query_match_session_without_engine_keys()
    {
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedProducts(connectionString);
            InsertProduct(connectionString, 1, "one", "n");
            await using var factory = CreateFactory(SqliteSettings("Inventory", connectionString));
            using var client = CreateClient(factory);
            var tools = await ListToolsAsync(client);
            AssertParameterKeysOmitEngine(tools);

            var tablesArguments = new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            };
            AssertArgumentsOmitEngine(tablesArguments);
            var tables = await CallToolAsync(client, "tables", tablesArguments, 280);
            Assert.AreEqual(HttpStatusCode.OK, tables.Status, tables.Body);
            Assert.IsFalse(tables.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), tables.Body);
            var tablesResult = ResultOf(tables.Rpc, tables.Body);
            Assert.IsFalse(tablesResult.GetProperty("isError").GetBoolean(), tables.Body);
            AssertPayloadMatchesSession(TextOf(tablesResult, tables.Body), ExecuteTables(connectionString, null));

            var fieldsArguments = new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
            };
            AssertArgumentsOmitEngine(fieldsArguments);
            var fields = await CallToolAsync(client, "fields", fieldsArguments, 281);
            Assert.AreEqual(HttpStatusCode.OK, fields.Status, fields.Body);
            Assert.IsFalse(fields.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), fields.Body);
            var fieldsResult = ResultOf(fields.Rpc, fields.Body);
            Assert.IsFalse(fieldsResult.GetProperty("isError").GetBoolean(), fields.Body);
            AssertFieldsMatchSession(TextOf(fieldsResult, fields.Body), ExecuteFields(connectionString, "products"));

            var queryArguments = new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
                ["table"] = "products",
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            };
            AssertArgumentsOmitEngine(queryArguments);
            var query = await CallToolAsync(client, "query", queryArguments, 282);
            AssertQueryMatches(
                query.Status,
                query.Body,
                query.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = "products",
                    ["Name"] = "one",
                }));
        }
        finally
        {
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Other_engines_match_session_or_pass_without_a_test_database()
    {
        var engines = new (string ServerType, string Variable)[]
        {
            ("SqlServer", "DBSERVICES_TEST_SQLSERVER"),
            ("MySQL", "DBSERVICES_TEST_MYSQL"),
            ("Oracle", "DBSERVICES_TEST_ORACLE"),
            ("PostgreSQL", "DBSERVICES_TEST_POSTGRESQL"),
        };

        foreach (var (serverType, variable) in engines)
            await AssertOtherEngineOrPassAsync(serverType, Environment.GetEnvironmentVariable(variable));
    }

    [TestMethod]
    public async Task Exact_server_types_are_not_connection_failed_when_connect_fails()
    {
        var engines = new (string Name, string ServerType, string ConnectionString)[]
        {
            ("sqlserver", "SqlServer", "Server=tcp:127.0.0.1,1;User ID=x;Password=x;Connect Timeout=1;Encrypt=False"),
            ("mysql", "MySQL", "Server=127.0.0.1;Port=1;User ID=x;Password=x;Connection Timeout=1"),
            ("oracle", "Oracle", "User Id=x;Password=x;Data Source=(DESCRIPTION=(CONNECT_TIMEOUT=1)(ADDRESS=(PROTOCOL=TCP)(HOST=127.0.0.1)(PORT=1)(CONNECT_TIMEOUT=1))(CONNECT_DATA=(SERVICE_NAME=x)))"),
            ("postgres", "PostgreSQL", "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1"),
        };

        var settings = new List<KeyValuePair<string, string?>>(engines.Length * 2);
        foreach (var (name, serverType, connectionString) in engines)
        {
            settings.Add(new KeyValuePair<string, string?>($"Connections:{name}:ServerType", serverType));
            settings.Add(new KeyValuePair<string, string?>($"Connections:{name}:ConnectionString", connectionString));
        }

        await using var factory = CreateFactory(settings);
        using var client = CreateClient(factory);
        await ListToolsAsync(client);

        var id = 330;
        foreach (var (name, _, _) in engines)
        {
            var call = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = name,
            }, id++);
            Assert.IsFalse(call.Body.Contains("connection_failed", StringComparison.Ordinal), name);
            Assert.IsFalse(call.Body.Contains("無法建立連線。", StringComparison.Ordinal), name);
        }
    }

    [TestMethod]
    public async Task Misspelled_server_type_is_connection_failed_then_another_tables_succeeds()
    {
        var typoPath = Path.Combine(Path.GetTempPath(), $"mcpsecretmarker-typo-{Guid.NewGuid():N}.db");
        var dbPath = NewDatabasePath();
        var connectionString = ConnectionStringFor(dbPath);
        try
        {
            SeedItemAndView(connectionString);
            Assert.IsFalse(File.Exists(typoPath));
            var settings = new List<KeyValuePair<string, string?>>
            {
                new("Connections:legacy:ServerType", "sqlite"),
                new("Connections:legacy:ConnectionString", $"Data Source={typoPath}"),
            };
            settings.AddRange(SqliteSettings("Inventory", connectionString));
            await using var factory = CreateFactory(settings);
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var failed = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
            }, 290);
            AssertConnectionFailed(failed.Status, failed.Body, failed.Rpc);
            Assert.IsFalse(File.Exists(typoPath));

            var again = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "inventory",
            }, 291);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            Assert.IsFalse(again.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), again.Body);
            var result = ResultOf(again.Rpc, again.Body);
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), again.Body);
            AssertPayloadMatchesSession(TextOf(result, again.Body), ExecuteTables(connectionString, null));
            Assert.IsFalse(File.Exists(typoPath));
        }
        finally
        {
            if (File.Exists(typoPath))
                File.Delete(typoPath);
            DeleteDatabase(dbPath);
        }
    }

    [TestMethod]
    public async Task Other_tools_keep_invalid_arguments_or_fail_connection_before_create()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcpsecretmarker-not-created-{Guid.NewGuid():N}.db");
        try
        {
            Assert.IsFalse(File.Exists(dbPath));
            await using var factory = CreateFactory(
            [
                new KeyValuePair<string, string?>("Connections:legacy:ServerType", "MySql"),
                new KeyValuePair<string, string?>("Connections:legacy:ConnectionString", $"Data Source={dbPath}"),
            ]);
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var omittedFields = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
            }, 300);
            AssertInvalidTable(omittedFields.Status, omittedFields.Body, omittedFields.Rpc);

            var fields = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
                ["table"] = "products",
            }, 301);
            AssertConnectionFailed(fields.Status, fields.Body, fields.Rpc);

            var badGet = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
                ["table"] = "products",
                ["id"] = "1",
            }, 302);
            AssertInvalidGet(badGet.Status, badGet.Body, badGet.Rpc);

            var get = await CallToolAsync(client, "get", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
                ["table"] = "products",
                ["id"] = 1,
            }, 303);
            AssertConnectionFailed(get.Status, get.Body, get.Rpc);

            foreach (var (name, invalidId, acceptedId) in new (string Name, int InvalidId, int AcceptedId)[]
            {
                ("query", 304, 305),
                ("count", 306, 307),
                ("exists", 308, 309),
            })
            {
                var invalid = await CallToolAsync(client, name, new Dictionary<string, object?>
                {
                    ["connectionId"] = "legacy",
                    ["table"] = "   ",
                }, invalidId);
                AssertInvalidTable(invalid.Status, invalid.Body, invalid.Rpc);
                Assert.IsFalse(File.Exists(dbPath));

                var accepted = await CallToolAsync(client, name, new Dictionary<string, object?>
                {
                    ["connectionId"] = "legacy",
                    ["table"] = "products",
                }, acceptedId);
                AssertConnectionFailed(accepted.Status, accepted.Body, accepted.Rpc);
                Assert.IsFalse(File.Exists(dbPath));
            }

            Assert.IsFalse(File.Exists(dbPath));
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [TestMethod]
    public async Task Misspelled_server_type_audit_is_connection_failed()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mcpsecretmarker-audit-{Guid.NewGuid():N}.db");
        try
        {
            await using var factory = CreateFactory(
            [
                new KeyValuePair<string, string?>("Connections:legacy:ServerType", "Postgress"),
                new KeyValuePair<string, string?>("Connections:legacy:ConnectionString", $"Data Source={dbPath}"),
            ]);
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var failed = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "legacy",
            }, 310);
            AssertConnectionFailed(failed.Status, failed.Body, failed.Rpc);

            var records = ReadAudit(factory);
            Assert.AreEqual(1, records.Length);
            AssertErrorAudit(records[0], "tables", "connection_failed", "legacy");
            Assert.IsFalse(File.Exists(dbPath));
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static readonly string[] EngineNames = ["Sqlite", "SqlServer", "MySQL", "Oracle", "PostgreSQL"];

    private static readonly List<string> ContentRoots = new();

    private static WebApplicationFactory<Program> CreateFactory(
        IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ContentRoots.Add(root);
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

                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AllowedHosts"] = "*",
                });
                config.AddInMemoryCollection(settings);
            });
        });
    }

    [ClassCleanup]
    public static void RemoveContentRoots()
    {
        foreach (var root in ContentRoots)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
        int id,
        string? caller = null)
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
        if (caller is not null)
            Assert.IsTrue(request.Headers.TryAddWithoutValidation("X-DBServices-Caller", caller), caller);

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
            case byte or sbyte or short or ushort or int or uint or long:
                Assert.AreEqual(JsonValueKind.Number, actual.ValueKind);
                Assert.IsTrue(actual.TryGetInt64(out var actualNumber), actual.GetRawText());
                Assert.AreEqual(Convert.ToInt64(expected, CultureInfo.InvariantCulture), actualNumber);
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

    private static void AssertInvalidGet(HttpStatusCode status, string body, JsonElement rpc)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        AssertErrorPayload(ResultOf(rpc, body), body, "invalid_arguments", "未提供資料表名稱或整數 Id。");
    }

    private static void AssertConnectionFailed(HttpStatusCode status, string body, JsonElement rpc)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        AssertErrorPayload(ResultOf(rpc, body), body, "connection_failed", "無法建立連線。");
    }

    private static void AssertParameterKeysOmitEngine(JsonElement[] tools)
    {
        foreach (var tool in tools)
        {
            var name = tool.GetProperty("name").GetString();
            Assert.IsFalse(string.IsNullOrEmpty(name));
            foreach (var engine in EngineNames)
                Assert.IsFalse(name!.Contains(engine, StringComparison.OrdinalIgnoreCase), name);

            foreach (var property in tool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject())
            {
                foreach (var engine in EngineNames)
                    Assert.IsFalse(property.Name.Contains(engine, StringComparison.OrdinalIgnoreCase), property.Name);
            }
        }
    }

    private static void AssertArgumentsOmitEngine(IReadOnlyDictionary<string, object?> arguments)
    {
        foreach (var key in arguments.Keys)
        {
            foreach (var engine in EngineNames)
                Assert.IsFalse(key.Contains(engine, StringComparison.OrdinalIgnoreCase), key);
        }
    }

    private static async Task AssertOtherEngineOrPassAsync(string serverType, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var table = "p" + Guid.NewGuid().ToString("N")[..16];
        var prepared = TryPrepareProbe(connectionString, serverType, table);
        try
        {
            if (!prepared)
                return;

            QueryResult tables;
            try
            {
                tables = ExecuteTables(connectionString, null, serverType);
            }
            catch (Exception)
            {
                return;
            }

            if (tables.Kind == QueryKind.Error)
                return;

            await using var factory = CreateFactory(
            [
                new KeyValuePair<string, string?>("Connections:warehouse:ServerType", serverType),
                new KeyValuePair<string, string?>("Connections:warehouse:ConnectionString", connectionString),
            ]);
            using var client = CreateClient(factory);
            await ListToolsAsync(client);

            var tablesCall = await CallToolAsync(client, "tables", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
            }, 320);
            Assert.IsFalse(tablesCall.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), tablesCall.Body);
            Assert.IsFalse(tablesCall.Body.Contains(connectionString, StringComparison.Ordinal), tablesCall.Body);
            var tablesResult = ResultOf(tablesCall.Rpc, tablesCall.Body);
            Assert.IsFalse(tablesResult.GetProperty("isError").GetBoolean(), tablesCall.Body);
            AssertPayloadMatchesSession(TextOf(tablesResult, tablesCall.Body), tables);
            var reported = ReportedTableName(tables, table);
            Assert.IsFalse(string.IsNullOrEmpty(reported), table);

            var fieldsSession = ExecuteFields(connectionString, reported!, serverType);
            var fieldsCall = await CallToolAsync(client, "fields", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["table"] = reported,
            }, 321);
            Assert.AreEqual(HttpStatusCode.OK, fieldsCall.Status, fieldsCall.Body);
            Assert.IsFalse(fieldsCall.Body.Contains("mcpsecretmarker", StringComparison.Ordinal), fieldsCall.Body);
            Assert.IsFalse(fieldsCall.Body.Contains(connectionString, StringComparison.Ordinal), fieldsCall.Body);
            var fieldsResult = ResultOf(fieldsCall.Rpc, fieldsCall.Body);
            Assert.IsFalse(fieldsResult.GetProperty("isError").GetBoolean(), fieldsCall.Body);
            AssertFieldsMatchSession(TextOf(fieldsResult, fieldsCall.Body), fieldsSession);

            var queryCall = await CallToolAsync(client, "query", new Dictionary<string, object?>
            {
                ["connectionId"] = "warehouse",
                ["table"] = reported,
                ["filters"] = new Dictionary<string, object?>
                {
                    ["Name"] = "one",
                },
            }, 322);
            AssertQueryMatches(
                queryCall.Status,
                queryCall.Body,
                queryCall.Rpc,
                ExecuteNamed(connectionString, "query", new Dictionary<string, object?>
                {
                    ["table"] = reported,
                    ["Name"] = "one",
                }, serverType));
            Assert.IsFalse(queryCall.Body.Contains(connectionString, StringComparison.Ordinal), queryCall.Body);
        }
        finally
        {
            TryDropProbe(connectionString, serverType, table);
        }
    }

    private static string? ReportedTableName(QueryResult tables, string table)
    {
        var nameIndex = -1;
        for (var index = 0; index < tables.Columns.Count; index++)
        {
            if (string.Equals(tables.Columns[index], "name", StringComparison.OrdinalIgnoreCase))
            {
                nameIndex = index;
                break;
            }
        }

        if (nameIndex < 0)
            return null;

        foreach (var row in tables.Rows)
        {
            if (nameIndex >= row.Count || row[nameIndex] is null)
                continue;

            var value = Convert.ToString(row[nameIndex], CultureInfo.InvariantCulture);
            if (string.Equals(value, table, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private static bool TryPrepareProbe(string connectionString, string serverType, string table)
    {
        try
        {
            var service = DbServiceFactory.Create(connectionString, serverType);
            if (service is null)
                return false;

            try
            {
                foreach (var sql in ProbeSql(serverType, table))
                    service.ExecuteSQL(sql);
                return true;
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDropProbe(string connectionString, string serverType, string table)
    {
        try
        {
            var service = DbServiceFactory.Create(connectionString, serverType);
            if (service is null)
                return;

            try
            {
                service.ExecuteSQL(DropSql(serverType, table));
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }
        }
        catch (Exception)
        {
            // 測試庫可能拒絕刪除；這一則仍要結束。
        }
    }

    private static IEnumerable<string> ProbeSql(string serverType, string table)
    {
        switch (serverType)
        {
            case "SqlServer":
                yield return $"CREATE TABLE {table} (Id int NOT NULL PRIMARY KEY, Name nvarchar(50) NOT NULL)";
                yield return $"INSERT INTO {table} (Id, Name) VALUES (1, N'one')";
                yield break;
            case "MySQL":
                yield return $"CREATE TABLE {table} (Id int NOT NULL PRIMARY KEY, Name varchar(50) NOT NULL)";
                yield return $"INSERT INTO {table} (Id, Name) VALUES (1, 'one')";
                yield break;
            case "Oracle":
                yield return $"CREATE TABLE {table} (Id number(10) NOT NULL PRIMARY KEY, Name varchar2(50) NOT NULL)";
                yield return $"INSERT INTO {table} (Id, Name) VALUES (1, 'one')";
                yield break;
            case "PostgreSQL":
                yield return $"CREATE TABLE {table} (Id integer NOT NULL PRIMARY KEY, Name text NOT NULL)";
                yield return $"INSERT INTO {table} (Id, Name) VALUES (1, 'one')";
                yield break;
        }
    }

    private static string DropSql(string serverType, string table) => serverType switch
    {
        "MySQL" => $"DROP TABLE IF EXISTS {table}",
        "PostgreSQL" => $"DROP TABLE IF EXISTS {table}",
        _ => $"DROP TABLE {table}",
    };

    private static void AssertRefused(HttpStatusCode status, string body, JsonElement rpc)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        var refused = rpc.TryGetProperty("error", out _)
            || (rpc.TryGetProperty("result", out var result)
                && result.TryGetProperty("isError", out var isError)
                && isError.ValueKind == JsonValueKind.True);
        Assert.IsTrue(refused, body);
    }

    private static void AssertToolMatchesSession(
        string name,
        HttpStatusCode status,
        string body,
        JsonElement rpc,
        QueryResult session)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        Assert.IsFalse(body.Contains("unknown_table", StringComparison.Ordinal), body);
        if (session.Kind == QueryKind.Error)
        {
            var message = name switch
            {
                "count" => "計數失敗。",
                "exists" => "是否存在失敗。",
                _ => "鍵值查詢失敗。",
            };
            AssertErrorPayload(ResultOf(rpc, body), body, "execution_failed", message);
            Assert.IsFalse(string.IsNullOrEmpty(session.Error));
            Assert.IsFalse(body.Contains(session.Error!, StringComparison.Ordinal), body);
            return;
        }

        if (name == "query")
            AssertQueryMatches(status, body, rpc, session);
        else
            AssertScalarCallMatches(status, body, rpc, session, name == "count" ? 0L : false);
    }

    private static void AssertSingleProductId(string json, QueryResult session, long id)
    {
        var idIndex = -1;
        for (var index = 0; index < session.Columns.Count; index++)
        {
            if (string.Equals(session.Columns[index], "Id", StringComparison.OrdinalIgnoreCase))
            {
                idIndex = index;
                break;
            }
        }

        Assert.IsTrue(idIndex >= 0);
        Assert.AreEqual(1, session.Rows.Count);
        Assert.AreEqual(id, Convert.ToInt64(session.Rows[0][idIndex], CultureInfo.InvariantCulture));

        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.AreEqual(1, rows.Length);
        var cell = rows[0].EnumerateArray().ElementAt(idIndex);
        Assert.AreEqual(JsonValueKind.Number, cell.ValueKind, cell.GetRawText());
        Assert.AreEqual(id, cell.GetInt64());
    }

    private static long JsonCount(string body, JsonElement rpc)
    {
        var result = ResultOf(rpc, body);
        Assert.IsFalse(result.GetProperty("isError").GetBoolean(), body);
        using var document = JsonDocument.Parse(TextOf(result, body));
        Assert.AreEqual(JsonValueKind.Number, document.RootElement.ValueKind, document.RootElement.GetRawText());
        return document.RootElement.GetInt64();
    }

    private static void AssertQueryMatches(HttpStatusCode status, string body, JsonElement rpc, QueryResult session)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        Assert.IsFalse(body.Contains("unknown_table", StringComparison.Ordinal), body);
        Assert.AreEqual(QueryKind.Rows, session.Kind, session.Error);
        var result = ResultOf(rpc, body);
        Assert.IsFalse(result.GetProperty("isError").GetBoolean(), body);
        var text = TextOf(result, body);
        AssertFieldsMatchSession(text, session);
        AssertIntegerCellsAreNumbers(text, session);
    }

    private static void AssertScalarCallMatches(
        HttpStatusCode status,
        string body,
        JsonElement rpc,
        QueryResult session,
        object emptyFallback)
    {
        Assert.AreEqual(HttpStatusCode.OK, status, body);
        Assert.IsFalse(body.Contains("mcpsecretmarker", StringComparison.Ordinal), body);
        Assert.IsFalse(body.Contains("unknown_table", StringComparison.Ordinal), body);
        Assert.AreEqual(QueryKind.Rows, session.Kind, session.Error);
        var result = ResultOf(rpc, body);
        Assert.IsFalse(result.GetProperty("isError").GetBoolean(), body);
        var cell = session.Rows.Count == 0 || session.Rows[0].Count == 0
            ? emptyFallback
            : session.Rows[0][0];
        using var document = JsonDocument.Parse(TextOf(result, body));
        Assert.AreNotEqual(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.AreNotEqual(JsonValueKind.Array, document.RootElement.ValueKind);
        AssertCellEquals(cell, document.RootElement);
    }

    private static void AssertIntegerCellsAreNumbers(string json, QueryResult session)
    {
        using var document = JsonDocument.Parse(json);
        var rows = document.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var cells = rows[rowIndex].EnumerateArray().ToArray();
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                if (session.Rows[rowIndex][cellIndex] is byte or sbyte or short or ushort or int or uint or long)
                    Assert.AreEqual(JsonValueKind.Number, cells[cellIndex].ValueKind, cells[cellIndex].GetRawText());
            }
        }
    }

    private static QueryResult ExecuteNamed(
        string connectionString,
        string name,
        IReadOnlyDictionary<string, object?> arguments,
        string serverType = "Sqlite")
    {
        var service = DbServiceFactory.Create(connectionString, serverType);
        try
        {
            return service!.GetSession().Execute(new QueryRequest
            {
                Text = name,
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase),
            });
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static QueryResult ExecuteGet(string connectionString, string table, long id)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            return service!.GetSession().Execute(new QueryRequest
            {
                Text = "get",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table,
                    ["id"] = id,
                },
            });
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static QueryResult ExecuteFields(string connectionString, string table, string serverType = "Sqlite")
    {
        var service = DbServiceFactory.Create(connectionString, serverType);
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

    private static QueryResult ExecuteTables(string connectionString, object? includeView, string serverType = "Sqlite")
    {
        var service = DbServiceFactory.Create(connectionString, serverType);
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

    private static JsonElement JsonLiteral(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void InsertProduct(string connectionString, long id, string name, string? note)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            var noteSql = note is null ? "NULL" : "'" + note.Replace("'", "''", StringComparison.Ordinal) + "'";
            var nameSql = name.Replace("'", "''", StringComparison.Ordinal);
            service!.ExecuteSQL(
                "INSERT INTO products (Id, Name, Note) VALUES ("
                + id.ToString(CultureInfo.InvariantCulture)
                + ", '" + nameSql + "', " + noteSql + ")");
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

    private static void AssertEnteredAudit(WebApplicationFactory<Program> factory, int count, string connectionId)
    {
        var records = ReadAudit(factory);
        Assert.AreEqual(count, records.Length);
        foreach (var record in records)
        {
            Assert.AreEqual("anonymous", record.GetProperty("caller").GetString());
            Assert.AreEqual(connectionId, record.GetProperty("connectionId").GetString());
            Assert.AreEqual(connectionId, record.GetProperty("arguments").GetProperty("connectionId").GetString());
        }
    }

    private static JsonElement[] ReadAudit(WebApplicationFactory<Program> factory, string? relative = null)
    {
        var root = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var path = Path.GetFullPath(relative is null
            ? Path.Combine(root, "audit", "mcp.jsonl")
            : Path.Combine(root, relative));
        Assert.IsTrue(File.Exists(path), path);
        var lines = File.ReadAllText(path)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var records = new JsonElement[lines.Length];
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            Assert.IsFalse(line.Contains("mcpsecretmarker", StringComparison.Ordinal), line);
            Assert.IsFalse(line.Contains("\"rows\"", StringComparison.Ordinal), line);
            Assert.IsFalse(line.Contains("\"columns\"", StringComparison.Ordinal), line);
            using var document = JsonDocument.Parse(line);
            var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEqual(
                new[] { "timestamp", "caller", "connectionId", "tool", "arguments", "rowCount", "value", "error" },
                names,
                line);
            var stamp = document.RootElement.GetProperty("timestamp").GetString();
            Assert.IsTrue(stamp!.EndsWith("Z", StringComparison.Ordinal), stamp);
            Assert.IsTrue(
                DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed),
                stamp);
            Assert.AreEqual(DateTimeKind.Utc, parsed.Kind);
            var modes = 0;
            if (document.RootElement.GetProperty("rowCount").ValueKind != JsonValueKind.Null)
                modes++;
            if (document.RootElement.GetProperty("value").ValueKind != JsonValueKind.Null)
                modes++;
            if (document.RootElement.GetProperty("error").ValueKind != JsonValueKind.Null)
                modes++;
            Assert.AreEqual(1, modes, line);
            foreach (var property in document.RootElement.GetProperty("arguments").EnumerateObject())
            {
                Assert.IsFalse(property.Name.Equals("connectionString", StringComparison.OrdinalIgnoreCase), line);
                Assert.IsFalse(property.Name.Equals("connectString", StringComparison.OrdinalIgnoreCase), line);
            }

            records[index] = document.RootElement.Clone();
        }

        return records;
    }

    private static void AssertRowAudit(JsonElement record, string tool, int rowCount, string connectionId)
    {
        Assert.AreEqual(tool, record.GetProperty("tool").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("connectionId").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("arguments").GetProperty("connectionId").GetString());
        Assert.AreEqual(rowCount, record.GetProperty("rowCount").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("value").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("error").ValueKind);
    }

    private static void AssertScalarAudit(JsonElement record, string tool, long value, string connectionId)
    {
        AssertScalarShape(record, tool, connectionId);
        Assert.AreEqual(JsonValueKind.Number, record.GetProperty("value").ValueKind);
        Assert.AreEqual(value, record.GetProperty("value").GetInt64());
    }

    private static void AssertScalarAudit(JsonElement record, string tool, bool value, string connectionId)
    {
        AssertScalarShape(record, tool, connectionId);
        Assert.AreEqual(value, record.GetProperty("value").GetBoolean());
    }

    private static void AssertScalarShape(JsonElement record, string tool, string connectionId)
    {
        Assert.AreEqual(tool, record.GetProperty("tool").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("connectionId").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("arguments").GetProperty("connectionId").GetString());
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("rowCount").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("error").ValueKind);
    }

    private static void AssertErrorAudit(JsonElement record, string tool, string code, string connectionId)
    {
        Assert.AreEqual(tool, record.GetProperty("tool").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("connectionId").GetString());
        Assert.AreEqual(connectionId, record.GetProperty("arguments").GetProperty("connectionId").GetString());
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("rowCount").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, record.GetProperty("value").ValueKind);
        Assert.AreEqual(code, record.GetProperty("error").GetString());
    }

    private static int RowCount(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("rows").GetArrayLength();
    }
}
