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
public class StructureDraftTests
{
    private const string Marker = "ROWMARKER-NOT-IN-DRAFT";

    [TestMethod]
    public async Task Structure_draft_matches_schema_read_and_reads_no_rows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-struct-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-struct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
                service.ExecuteSQL("CREATE TABLE child (Id INTEGER PRIMARY KEY, ParentId INTEGER NOT NULL REFERENCES parent(Id), GhostId INTEGER REFERENCES missing(Gone))");
                service.ExecuteSQL("CREATE VIEW parent_view AS SELECT Id, Name FROM parent");
                service.ExecuteSQL("INSERT INTO parent (Id, Name) VALUES (7, '" + Marker + "')");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var direct = SchemaGraph.Read(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            }), "PlatformDB");
            Assert.IsTrue(direct.Ok, direct.Message);
            Assert.IsTrue(direct.Tables.Any(table => table.Name == "parent_view"));
            Assert.IsTrue(direct.Tables.Single(table => table.Name == "parent").Columns.Single(column => column.Name == "Id").PrimaryKey);
            Assert.IsFalse(direct.Tables.Single(table => table.Name == "parent").Columns.Single(column => column.Name == "Name").PrimaryKey);

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "platformdb"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Connections:legacy:ServerType", "Informix"),
                new("Connections:legacy:ConnectionString", "Server=ignored"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key",
                """{"connectionId":"legacy"}""");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            var package = document.RootElement;
            Assert.AreEqual("PlatformDB", package.GetProperty("title").GetString());
            Assert.AreEqual("", package.GetProperty("purpose").GetString());
            Assert.AreEqual("", package.GetProperty("dbml").GetString());
            Assert.AreEqual("draft", package.GetProperty("status").GetString());
            Assert.IsFalse(package.GetProperty("active").GetBoolean());
            Assert.IsFalse(saved.Body.Contains(connectionString, StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(saved.Body.Contains(Marker, StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("Data Source=", StringComparison.Ordinal));

            var tables = package.GetProperty("tables");
            Assert.AreEqual(direct.Tables.Count, tables.GetArrayLength());
            for (var tableIndex = 0; tableIndex < direct.Tables.Count; tableIndex++)
            {
                var expected = direct.Tables[tableIndex];
                var actual = tables[tableIndex];
                Assert.AreEqual(expected.Name, actual.GetProperty("name").GetString());
                Assert.AreEqual("", actual.GetProperty("note").GetString());
                Assert.AreEqual(0, actual.GetProperty("indexes").GetArrayLength());
                var columns = actual.GetProperty("columns");
                Assert.AreEqual(expected.Columns.Count, columns.GetArrayLength());
                for (var columnIndex = 0; columnIndex < expected.Columns.Count; columnIndex++)
                {
                    var expectedColumn = expected.Columns[columnIndex];
                    var actualColumn = columns[columnIndex];
                    Assert.AreEqual(expectedColumn.Name, actualColumn.GetProperty("name").GetString());
                    Assert.AreEqual(expectedColumn.Type, actualColumn.GetProperty("type").GetString());
                    Assert.AreEqual(expectedColumn.PrimaryKey, actualColumn.GetProperty("primaryKey").GetBoolean());
                    Assert.IsFalse(actualColumn.GetProperty("notNull").GetBoolean());
                    Assert.IsFalse(actualColumn.GetProperty("uniqueIndex").GetBoolean());
                    Assert.AreEqual("", actualColumn.GetProperty("note").GetString());
                    Assert.AreEqual("", actualColumn.GetProperty("pii").GetString());
                }
            }

            var links = package.GetProperty("links");
            Assert.AreEqual(1, links.GetArrayLength());
            var link = links[0];
            Assert.AreEqual("child", link.GetProperty("from").GetString());
            Assert.AreEqual("ParentId", link.GetProperty("fromColumn").GetString());
            Assert.AreEqual("parent", link.GetProperty("to").GetString());
            Assert.AreEqual("Id", link.GetProperty("toColumn").GetString());
            Assert.AreEqual("foreign-key", link.GetProperty("origin").GetString());
            Assert.AreEqual("", link.GetProperty("note").GetString());
            Assert.IsFalse(saved.Body.Contains("missing", StringComparison.Ordinal));

            var path = Path.Combine(root, "catalog", "annotations.json");
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.IsFalse(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")));
            var kept = package.GetProperty("id").GetString();

            var again = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, again.Status, again.Body);
            Assert.Contains("draft_exists", again.Body, StringComparison.Ordinal);
            Assert.Contains("已有工作中的草稿，沒有改。", again.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
            using var stored = JsonDocument.Parse(bytes);
            Assert.AreEqual(1, stored.RootElement.GetArrayLength());
            Assert.AreEqual(kept, stored.RootElement[0].GetProperty("id").GetString());

            var titled = new AnnotationCatalog(Path.Combine(root, "titled.json"));
            var special = titled.ReadStructure(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["平台*<DB>"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            }), "平台*<DB>");
            Assert.IsTrue(special.Ok, special.Message);
            Assert.AreEqual("平台*<DB>", special.Package!.Title);
            Assert.AreEqual(direct.Tables.Count, special.Package.Tables.Count);
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_empty_tables_still_writes()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-empty-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var connectionString = "Data Source=" + dbPath;
            var direct = SchemaGraph.Read(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            }), "PlatformDB");
            Assert.IsTrue(direct.Ok, direct.Message);
            Assert.AreEqual(0, direct.Tables.Count);

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual(0, document.RootElement.GetProperty("tables").GetArrayLength());
            Assert.AreEqual(0, document.RootElement.GetProperty("links").GetArrayLength());
            Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
            Assert.IsFalse(saved.Body.Contains("\"code\"", StringComparison.Ordinal));
            Assert.IsTrue(File.Exists(Path.Combine(root, "catalog", "annotations.json")));
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_no_connection_does_not_read()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Connections:legacy:ServerType", "Informix"),
                new("Connections:legacy:ConnectionString", "Server=ignored"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key",
                """{"connectionId":"PlatformDB"}""");
            Assert.AreEqual(HttpStatusCode.BadRequest, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual("no_connection", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("沒有讀取結構。", document.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(saved.Body.Contains("blank", StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("legacy", StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("PlatformDB", StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(Path.Combine(root, "catalog", "annotations.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_many_connections_are_unselectable()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-many-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:alpha:ServerType", "Sqlite"),
                new("Connections:alpha:ConnectionString", "Data Source=alpha.db"),
                new("Connections:beta:ServerType", "PostgreSQL"),
                new("Connections:beta:ConnectionString", "Host=beta"),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key",
                """{"connectionId":"alpha"}""");
            Assert.AreEqual(HttpStatusCode.BadRequest, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual("connection_unselectable", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("註記站不能選擇連線，沒有讀取結構。", document.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(saved.Body.Contains("alpha", StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("beta", StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("blank", StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(Path.Combine(root, "catalog", "annotations.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "audit", "mcp.jsonl")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "alpha.db")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_other_name_is_not_platform_source()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-other-" + Guid.NewGuid().ToString("N"));
        var blocked = Path.Combine(root, "not-a-file");
        Directory.CreateDirectory(blocked);
        try
        {
            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:legacy:ServerType", "Sqlite"),
                new("Connections:legacy:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key",
                """{"connectionId":"PlatformDB"}""");
            Assert.AreEqual(HttpStatusCode.BadRequest, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual("not_platform_source", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("不能用legacy讀結構或確認。", document.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(saved.Body.Contains(blocked, StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(File.Exists(Path.Combine(root, "catalog", "annotations.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_read_failure_writes_nothing()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-fail-" + Guid.NewGuid().ToString("N"));
        var blocked = Path.Combine(root, "not-a-file");
        Directory.CreateDirectory(blocked);
        try
        {
            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual("execution_failed", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("讀取資料表結構失敗。", document.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(saved.Body.Contains(blocked, StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(Path.Combine(root, "catalog", "annotations.json")));

            var path = Path.Combine(root, "direct.json");
            var catalog = new AnnotationCatalog(path);
            var refused = catalog.ReadStructure(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = "   " },
            }), "PlatformDB");
            Assert.IsFalse(refused.Ok);
            Assert.AreEqual("connection_failed", refused.Code);
            Assert.AreEqual("無法建立連線。", refused.Message);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_existing_draft_bytes_stay()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-kept-" + Guid.NewGuid().ToString("N"));
        var blocked = Path.Combine(root, "not-a-file");
        Directory.CreateDirectory(blocked);
        try
        {
            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var created = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key",
                """{"id":"kept","title":"Kept","purpose":"原說明","status":"confirmed","active":true,"tables":[],"links":[],"dbml":""}""");
            Assert.AreEqual(HttpStatusCode.OK, created.Status, created.Body);
            var path = Path.Combine(root, "catalog", "annotations.json");
            var bytes = await File.ReadAllBytesAsync(path);
            var again = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, again.Status, again.Body);
            Assert.Contains("draft_exists", again.Body, StringComparison.Ordinal);
            Assert.Contains("已有工作中的草稿，沒有改。", again.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Structure_draft_rejects_bad_callers_and_appends_beside_confirmed()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-kept-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = "Data Source=" + dbPath;
        var root = Path.Combine(Path.GetTempPath(), "mcp-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            (service as IDisposable)?.Dispose();

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var path = Path.Combine(root, "catalog", "annotations.json");

            var missing = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", null, "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, missing.Status, missing.Body);
            Assert.Contains("未提供呼叫者", missing.Body);
            var wrong = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "林可", "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, wrong.Status, wrong.Body);
            Assert.Contains("目錄金鑰不符", wrong.Body);
            Assert.IsFalse(File.Exists(path));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "old",
                "title": "Old",
                "purpose": "已確認",
                "status": "confirmed",
                "active": true,
                "tables": [],
                "links": [],
                "dbml": ""
              }
            ]
            """);
            var saved = await SendAsync(client, HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using (var document = JsonDocument.Parse(saved.Body))
            {
                Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
                Assert.IsFalse(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreNotEqual("old", document.RootElement.GetProperty("id").GetString());
            }

            using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var packages = stored.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, packages.Count);
            var old = packages.Single(item => item.GetProperty("id").GetString() == "old");
            Assert.AreEqual("Old", old.GetProperty("title").GetString());
            Assert.AreEqual("已確認", old.GetProperty("purpose").GetString());
            Assert.AreEqual("confirmed", old.GetProperty("status").GetString());
            Assert.IsTrue(old.GetProperty("active").GetBoolean());
            Assert.AreEqual("", old.GetProperty("dbml").GetString());
            Assert.AreEqual(0, old.GetProperty("tables").GetArrayLength());
            Assert.AreEqual(0, old.GetProperty("links").GetArrayLength());
            var fresh = packages.Single(item => item.GetProperty("id").GetString() != "old");
            Assert.AreEqual("draft", fresh.GetProperty("status").GetString());
            Assert.IsFalse(fresh.GetProperty("active").GetBoolean());
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
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

    private static void ReleaseSqlite()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection")?.GetMethod("ClearAllPools")?.Invoke(null, null);
    }
}
