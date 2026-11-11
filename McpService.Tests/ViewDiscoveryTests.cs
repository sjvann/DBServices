using System.Net;
using System.Text;
using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Mcp;
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
            var description = tools[0].GetProperty("description").GetString() ?? "";
            Assert.Contains("說明出貨", description);
            Assert.Contains("products", description);
            Assert.Contains("Name", description);
            Assert.Contains("名稱", description);
            Assert.IsFalse(description.Contains("SQL", StringComparison.OrdinalIgnoreCase), description);
            Assert.IsFalse(description.Contains("表名", StringComparison.Ordinal), description);
            var required = tools[0].GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.IsFalse(required.Contains("connectionId", StringComparer.OrdinalIgnoreCase));
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
            using (var payload = JsonDocument.Parse(text))
            {
                var rows = payload.RootElement.GetProperty("rows");
                Assert.AreEqual(1, rows.GetArrayLength());
                Assert.AreEqual("one", rows[0][0].GetString());
            }

            Assert.IsFalse(text.Contains("two", StringComparison.Ordinal), text);
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
    public async Task Two_source_foreign_key_call_returns_the_joined_row()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-join-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE customers (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
                service.ExecuteSQL("CREATE TABLE orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Title TEXT NOT NULL)");
                service.ExecuteSQL("INSERT INTO customers (Id, Name) VALUES (1, 'ada')");
                service.ExecuteSQL("INSERT INTO customers (Id, Name) VALUES (2, 'bob')");
                service.ExecuteSQL("INSERT INTO orders (Id, CustomerId, Title) VALUES (1, 1, 'box')");
                service.ExecuteSQL("INSERT INTO orders (Id, CustomerId, Title) VALUES (2, 2, 'tape')");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
            var saved = await SendAsync(client, HttpMethod.Put, "/views/join-1", "林可", "unit-catalog-key",
                """
                {"id":"join-1","name":"關聯","purpose":"訂單客戶","confirmed":true,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"CustomerId","to":"customers","toColumn":"Id"}],"parameters":[{"name":"標題","column":"Title","source":"orders"}],"columns":[{"name":"Title","source":"orders"},{"name":"Name","source":"customers"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var published = await SendAsync(client, HttpMethod.Post, "/views/join-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, published.Status, published.Body);

            var call = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"關聯","arguments":{"connectionId":"warehouse","標題":"box"}}}
                """);
            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var resultElement = call.Rpc.GetProperty("result");
            var text = resultElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.IsFalse(resultElement.GetProperty("isError").GetBoolean(), text);
            using var payload = JsonDocument.Parse(text);
            var rows = payload.RootElement.GetProperty("rows");
            Assert.AreEqual(1, rows.GetArrayLength(), text);
            Assert.AreEqual("box", rows[0][0].GetString());
            Assert.AreEqual("ada", rows[0][1].GetString());
            Assert.IsFalse(text.Contains("bob", StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains("tape", StringComparison.Ordinal), text);
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
    public async Task Invalid_view_writes_do_not_change_the_stored_document()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"Ship","purpose":"說明出貨","published":false,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var before = await SendAsync(client, HttpMethod.Get, "/views/ship-1", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, before.Status, before.Body);

            var duplicate = await SendAsync(client, HttpMethod.Put, "/views/ship-2", "林可", "unit-catalog-key",
                """
                {"id":"ship-2","name":"Ship","purpose":"另一個","published":false,"sources":["products"],"links":[],"parameters":[],"columns":[]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, duplicate.Status);
            Assert.Contains("名稱已存在", duplicate.Body);

            var caseOnly = await SendAsync(client, HttpMethod.Put, "/views/ship-3", "林可", "unit-catalog-key",
                """
                {"id":"ship-3","name":"ship","purpose":"大小寫","published":false,"sources":["products"],"links":[],"parameters":[],"columns":[]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, caseOnly.Status);
            Assert.Contains("名稱已存在", caseOnly.Body);

            var blank = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":" ","purpose":"空白","published":false,"sources":["products"],"links":[],"parameters":[],"columns":[]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, blank.Status);
            Assert.Contains("名稱不可空白", blank.Body);

            var disconnected = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"Ship","purpose":"斷開","published":false,"sources":["orders","customers"],"links":[],"parameters":[],"columns":[]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, disconnected.Status);
            Assert.Contains("來源連不起來", disconnected.Body);

            var after = await SendAsync(client, HttpMethod.Get, "/views/ship-1", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, after.Status, after.Body);
            Assert.AreEqual(before.Body, after.Body);
            Assert.AreEqual(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/views/ship-2", "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/views/ship-3", "林可", "unit-catalog-key")).Status);
        }
        finally
        {
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

    [TestMethod]
    public async Task Closed_and_unknown_names_are_audited_without_a_query()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-closed-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        var secret = "SecretMarkerDoNotLeak";
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
            using var client = Client(factory);
            var insert = await CallPayloadAsync(client,
                "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"insert\",\"arguments\":{\"connectionString\":\"" + secret + "\"}}}");
            AssertError(insert, "tool_disabled", "這個工具已關閉。");
            Assert.IsFalse(insert.Body.Contains(secret, StringComparison.Ordinal), insert.Body);
            Assert.IsFalse(insert.Body.Contains(connectionString, StringComparison.Ordinal), insert.Body);

            var tables = await CallPayloadAsync(client,
                "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"tables\",\"arguments\":{\"connectionId\":\"warehouse\"}}}");
            AssertError(tables, "unknown_tool", "沒有這個工具。");

            var audit = File.ReadAllText(Path.Combine(root, "audit", "mcp.jsonl"));
            Assert.Contains("\"error\":\"tool_disabled\"", audit);
            Assert.Contains("\"error\":\"unknown_tool\"", audit);
            Assert.IsFalse(audit.Contains(secret, StringComparison.Ordinal), audit);
            Assert.IsFalse(audit.Contains(connectionString, StringComparison.Ordinal), audit);
            using var line = JsonDocument.Parse(audit.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]);
            foreach (var field in new[] { "timestamp", "caller", "connectionId", "tool", "arguments", "rowCount", "value", "error" })
                Assert.IsTrue(line.RootElement.TryGetProperty(field, out _), field);

            var catalog = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["warehouse"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var rows = TableDispatch.ExecuteQuery(catalog, "warehouse", "products", null);
            Assert.Contains("one", rows.Json);
            Assert.Contains("two", rows.Json);
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
    public async Task Connection_decision_follows_available_names()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-conn-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Seed(connectionString);
            await using var single = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(single);
            var saved = await PutAsync(client, "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var published = await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, published.Status, published.Body);

            var omitted = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"出貨","arguments":{"名稱":"one"}}}
                """);
            Assert.AreEqual(HttpStatusCode.OK, omitted.Status, omitted.Body);
            Assert.IsFalse(omitted.Rpc.GetProperty("result").GetProperty("isError").GetBoolean(), omitted.Body);
            var omittedText = omitted.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.Contains("one", omittedText);

            var missing = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"missing","名稱":"one"}}}
                """);
            AssertError(missing, "unknown_connection", "找不到連線。");
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

        var manyRoot = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(manyRoot);
        try
        {
            await using var many = Factory(manyRoot,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Connections:other:ServerType", "Sqlite"),
                new("Connections:other:ConnectionString", "Data Source=" + dbPath + "-other"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(many);
            Assert.AreEqual(HttpStatusCode.OK, (await PutAsync(client, "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);
            var required = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"出貨","arguments":{"名稱":"one"}}}
                """);
            AssertError(required, "connection_required", "請指定連線。");
        }
        finally
        {
            if (Directory.Exists(manyRoot))
                Directory.Delete(manyRoot, true);
        }

        var blankRoot = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(blankRoot);
        try
        {
            await using var blank = Factory(blankRoot,
            [
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(blank);
            Assert.AreEqual(HttpStatusCode.OK, (await PutAsync(client, "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);
            var failed = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"blank","名稱":"one"}}}
                """);
            AssertError(failed, "connection_failed", "無法建立連線。");
            var text = failed.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.IsFalse(text.Contains("\"rows\"", StringComparison.Ordinal), text);
            var omitted = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"出貨","arguments":{"名稱":"one"}}}
                """);
            AssertError(omitted, "connection_required", "請指定連線。");
        }
        finally
        {
            if (Directory.Exists(blankRoot))
                Directory.Delete(blankRoot, true);
        }
    }

    [TestMethod]
    public async Task Secret_arguments_are_rejected_without_echoing_the_value()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        var secret = "SecretMarkerDoNotLeak";
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", "Data Source=ignored.db"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await PutAsync(client, "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);
            var rejected = await CallPayloadAsync(client,
                "{\"jsonrpc\":\"2.0\",\"id\":14,\"method\":\"tools/call\",\"params\":{\"name\":\"出貨\",\"arguments\":{\"connectionId\":\"warehouse\",\"connectionString\":\"" + secret + "\",\"名稱\":\"one\"}}}");
            AssertError(rejected, "invalid_arguments", "參數無法接受。");
            Assert.IsFalse(rejected.Body.Contains(secret, StringComparison.Ordinal), rejected.Body);
            var audit = File.ReadAllText(Path.Combine(root, "audit", "mcp.jsonl"));
            Assert.IsFalse(audit.Contains(secret, StringComparison.Ordinal), audit);
            Assert.Contains("\"error\":\"invalid_arguments\"", audit);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Publish_requires_a_confirmed_purpose_and_drops_it_when_the_purpose_changes()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = Client(factory);
            var unconfirmed = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":false,"published":false,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, unconfirmed.Status, unconfirmed.Body);
            var refused = await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, refused.Status, refused.Body);
            Assert.Contains("unconfirmed", refused.Body);
            Assert.Contains("解說尚未確認。", refused.Body);
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);

            var blankPurpose = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"出貨","purpose":"   ","confirmed":true,"published":true,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, blankPurpose.Status, blankPurpose.Body);
            using (var stored = JsonDocument.Parse(blankPurpose.Body))
            {
                Assert.IsFalse(stored.RootElement.GetProperty("published").GetBoolean());
                Assert.IsFalse(stored.RootElement.GetProperty("confirmed").GetBoolean());
            }
            var stillRefused = await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, stillRefused.Status, stillRefused.Body);
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);

            var confirmed = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, confirmed.Status, confirmed.Body);
            using (var cleared = JsonDocument.Parse(confirmed.Body))
                Assert.IsFalse(cleared.RootElement.GetProperty("confirmed").GetBoolean());
            var again = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            using (var kept = JsonDocument.Parse(again.Body))
                Assert.IsTrue(kept.RootElement.GetProperty("confirmed").GetBoolean());
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);
            var tools = await ListToolsAsync(client);
            Assert.AreEqual(1, tools.Length);
            Assert.AreEqual("出貨", tools[0].GetProperty("name").GetString());
            var description = tools[0].GetProperty("description").GetString() ?? "";
            Assert.Contains("說明出貨", description);
            Assert.Contains("products", description);
            Assert.Contains("Name", description);
            Assert.IsFalse(description.Contains("SQL", StringComparison.OrdinalIgnoreCase), description);

            var changed = await SendAsync(client, HttpMethod.Put, "/views/ship-1", "林可", "unit-catalog-key",
                """
                {"id":"ship-1","name":"出貨","purpose":"改過的說明","confirmed":true,"published":true,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, changed.Status, changed.Body);
            using (var stored = JsonDocument.Parse(changed.Body))
            {
                Assert.IsFalse(stored.RootElement.GetProperty("confirmed").GetBoolean());
                Assert.IsFalse(stored.RootElement.GetProperty("published").GetBoolean());
            }
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task A_second_key_on_the_same_pair_drops_partial_matches()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-keys-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE customers (Id INTEGER PRIMARY KEY, RegionId INTEGER NOT NULL, Name TEXT NOT NULL)");
                service.ExecuteSQL("CREATE TABLE orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, RegionId INTEGER NOT NULL, Title TEXT NOT NULL)");
                service.ExecuteSQL("INSERT INTO customers (Id, RegionId, Name) VALUES (1, 10, 'ada')");
                service.ExecuteSQL("INSERT INTO customers (Id, RegionId, Name) VALUES (2, 99, 'zed')");
                service.ExecuteSQL("INSERT INTO orders (Id, CustomerId, RegionId, Title) VALUES (1, 1, 10, 'both')");
                service.ExecuteSQL("INSERT INTO orders (Id, CustomerId, RegionId, Title) VALUES (2, 1, 99, 'only-customer')");
                service.ExecuteSQL("INSERT INTO orders (Id, CustomerId, RegionId, Title) VALUES (3, 2, 10, 'only-region')");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(factory);
            var saved = await SendAsync(client, HttpMethod.Put, "/views/keys-1", "林可", "unit-catalog-key",
                """
                {"id":"keys-1","name":"雙鍵","purpose":"兩條都要中","confirmed":true,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"CustomerId","to":"customers","toColumn":"Id"},{"kind":"accepted","from":"orders","fromColumn":"RegionId","to":"customers","toColumn":"RegionId"}],"parameters":[],"columns":[{"name":"Title","source":"orders"},{"name":"Name","source":"customers"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/keys-1/publish", "林可", "unit-catalog-key")).Status);
            var tools = await ListToolsAsync(client);
            var description = tools[0].GetProperty("description").GetString() ?? "";
            Assert.Contains("orders.CustomerId = customers.Id", description);
            Assert.Contains("orders.RegionId = customers.RegionId", description);
            Assert.Contains("Title", description);
            Assert.IsFalse(description.Contains("SQL", StringComparison.OrdinalIgnoreCase), description);

            var call = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":15,"method":"tools/call","params":{"name":"雙鍵","arguments":{"connectionId":"warehouse"}}}
                """);
            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var text = call.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.IsFalse(call.Rpc.GetProperty("result").GetProperty("isError").GetBoolean(), text);
            using var payload = JsonDocument.Parse(text);
            var rows = payload.RootElement.GetProperty("rows");
            Assert.AreEqual(1, rows.GetArrayLength(), text);
            Assert.AreEqual("both", rows[0][0].GetString());
            Assert.AreEqual("ada", rows[0][1].GetString());
            Assert.IsFalse(text.Contains("only-customer", StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains("only-region", StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains("zed", StringComparison.Ordinal), text);
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
    public async Task Schema_preview_does_not_read_business_rows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"view-preview-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Seed(connectionString);
            var catalog = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["warehouse"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var direct = SchemaGraph.Preview(catalog, "warehouse", new ViewDocument
            {
                Id = "ship-1",
                Name = "出貨",
                Purpose = "說明出貨",
                Confirmed = true,
                Sources = ["products"],
                Columns = [new ViewColumn { Name = "Name", Source = "products" }],
            }, null);
            Assert.IsFalse(direct.Ok);
            Assert.AreEqual("preview_unavailable", direct.Code);
            Assert.AreEqual("不讀取業務列。", direct.Message);
            Assert.AreEqual(0, direct.Rows.Count);

            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var preview = await SendAsync(client, HttpMethod.Post, "/schema/preview", "林可", "unit-catalog-key",
                """
                {"connectionId":"warehouse","view":{"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":true,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, preview.Status, preview.Body);
            Assert.Contains("preview_unavailable", preview.Body);
            Assert.Contains("不讀取業務列。", preview.Body);
            Assert.IsFalse(preview.Body.Contains("\"rows\"", StringComparison.Ordinal), preview.Body);
            Assert.IsFalse(preview.Body.Contains("one", StringComparison.Ordinal), preview.Body);

            var graph = await SendAsync(client, HttpMethod.Get, "/schema/graph?connectionId=warehouse", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, graph.Status, graph.Body);
            Assert.Contains("\"tables\"", graph.Body);
            Assert.Contains("products", graph.Body);
            Assert.IsFalse(graph.Body.Contains("one", StringComparison.Ordinal), graph.Body);
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
    public async Task Configured_engines_keep_the_same_view_parameter_keys()
    {
        var expected = new[] { "connectionId", "名稱" };
        await AssertViewParameterKeysAsync("Sqlite", "Data Source=ignored.db", expected);
        var engines = new (string ServerType, string Variable)[]
        {
            ("SqlServer", "DBSERVICES_TEST_SQLSERVER"),
            ("MySQL", "DBSERVICES_TEST_MYSQL"),
            ("Oracle", "DBSERVICES_TEST_ORACLE"),
            ("PostgreSQL", "DBSERVICES_TEST_POSTGRESQL"),
        };
        foreach (var (serverType, variable) in engines)
        {
            var connectionString = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(connectionString))
                continue;
            await AssertViewParameterKeysAsync(serverType, connectionString, expected);
        }
    }

    private static async Task AssertViewParameterKeysAsync(string serverType, string connectionString, string[] expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", serverType),
                new("Connections:warehouse:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await PutAsync(client, "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);
            var tools = await ListToolsAsync(client);
            var names = tools[0].GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(expected, names);
            Assert.IsFalse(names.Contains("table", StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Stored_unconfirmed_view_is_absent_from_tools_list()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "catalog", "views.json"),
                """
                [{"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":false,"published":true,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}]
                """);
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = Client(factory);
            Assert.AreEqual(0, (await ListToolsAsync(client)).Length);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Unconfirmed_saves_and_rejected_calls_stay_out_of_discovery()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-views-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", "Data Source=ignored.db"),
                new("Connections:other:ServerType", "Sqlite"),
                new("Connections:other:ConnectionString", "Data Source=other.db"),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = Client(factory);

            var blankPurpose = await SendAsync(client, HttpMethod.Put, "/views/blank-1", "林可", "unit-catalog-key",
                """
                {"id":"blank-1","name":"空白說明","purpose":"   ","confirmed":true,"published":true,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, blankPurpose.Status, blankPurpose.Body);
            using (var stored = JsonDocument.Parse(blankPurpose.Body))
            {
                Assert.IsFalse(stored.RootElement.GetProperty("published").GetBoolean());
                Assert.IsTrue(stored.RootElement.GetProperty("confirmed").GetBoolean());
            }

            var refused = await SendAsync(client, HttpMethod.Post, "/views/blank-1/publish", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, refused.Status, refused.Body);
            Assert.Contains("unconfirmed", refused.Body);
            Assert.Contains("解說尚未確認。", refused.Body);

            var unconfirmed = await SendAsync(client, HttpMethod.Put, "/views/plain-1", "林可", "unit-catalog-key",
                """
                {"id":"plain-1","name":"未確認","purpose":"有說明","confirmed":false,"published":true,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, unconfirmed.Status, unconfirmed.Body);
            using (var stored = JsonDocument.Parse(unconfirmed.Body))
                Assert.IsFalse(stored.RootElement.GetProperty("published").GetBoolean());
            Assert.IsFalse((await ListToolsAsync(client)).Any(tool => tool.GetProperty("name").GetString() == "未確認"));

            Assert.AreEqual(HttpStatusCode.OK, (await PutAsync(client, "林可", "unit-catalog-key")).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish", "林可", "unit-catalog-key")).Status);

            var filtered = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":21,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"warehouse","名稱":"one","filters":{}}}}
                """);
            AssertError(filtered, "invalid_arguments", "參數無法接受。");
            var filteredText = filtered.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            Assert.IsFalse(filteredText.Contains("\"rows\"", StringComparison.Ordinal), filteredText);

            foreach (var name in new[] { "update", "delete", "sql" })
            {
                var closed = await CallPayloadAsync(client,
                    "{\"jsonrpc\":\"2.0\",\"id\":22,\"method\":\"tools/call\",\"params\":{\"name\":\"" + name + "\",\"arguments\":{}}}");
                AssertError(closed, "tool_disabled", "這個工具已關閉。");
            }

            var unknown = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":23,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"missing","名稱":"one"}}}
                """);
            AssertError(unknown, "unknown_connection", "找不到連線。");
            var required = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":24,"method":"tools/call","params":{"name":"出貨","arguments":{"名稱":"one"}}}
                """);
            AssertError(required, "connection_required", "請指定連線。");
            var failed = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":25,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"blank","名稱":"one"}}}
                """);
            AssertError(failed, "connection_failed", "無法建立連線。");

            var missingParameter = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":26,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"warehouse"}}}
                """);
            AssertError(missingParameter, "invalid_arguments", "未提供資料表名稱。");

            var emptyName = await CallPayloadAsync(client,
                """
                {"jsonrpc":"2.0","id":27,"method":"tools/call","params":{"name":"","arguments":{}}}
                """);
            AssertError(emptyName, "unknown_tool", "沒有這個工具。");

            var audit = AuditRows(root);
            foreach (var name in new[] { "update", "delete", "sql" })
                Assert.IsTrue(audit.Any(row => row.Tool == name && row.Error == "tool_disabled"));
            Assert.IsTrue(audit.Any(row => row.Error == "unknown_connection"));
            Assert.IsTrue(audit.Any(row => row.Error == "connection_required"));
            Assert.IsTrue(audit.Any(row => row.Error == "connection_failed"));
            Assert.IsTrue(audit.Any(row => row.Tool == "出貨" && row.Error == "invalid_arguments"));
            Assert.IsTrue(audit.Any(row => row.Tool == "" && row.Error == "unknown_tool"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static List<(string Tool, string? Error)> AuditRows(string root)
    {
        var rows = new List<(string Tool, string? Error)>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "audit", "mcp.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var error = document.RootElement.GetProperty("error");
            rows.Add((
                document.RootElement.GetProperty("tool").GetString() ?? "",
                error.ValueKind == JsonValueKind.String ? error.GetString() : null));
        }

        return rows;
    }

    private static void AssertError((HttpStatusCode Status, string Body, JsonElement Rpc) call, string code, string message)
    {
        Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
        var result = call.Rpc.GetProperty("result");
        Assert.IsTrue(result.GetProperty("isError").GetBoolean(), call.Body);
        using var payload = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString() ?? "{}");
        Assert.AreEqual(code, payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual(message, payload.RootElement.GetProperty("message").GetString());
    }

    private static void Seed(string connectionString)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            service!.ExecuteSQL("CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            service.ExecuteSQL("INSERT INTO products (Id, Name) VALUES (1, 'one')");
            service.ExecuteSQL("INSERT INTO products (Id, Name) VALUES (2, 'two')");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        return client;
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
            {"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
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

    private static Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallAsync(HttpClient client, string caller)
        => CallPayloadAsync(client,
            """
            {"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"出貨","arguments":{"connectionId":"warehouse","名稱":"one"}}}
            """,
            caller);

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallPayloadAsync(HttpClient client, string payload, string caller = "   ")
    {
        await InitializeAsync(client);
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
