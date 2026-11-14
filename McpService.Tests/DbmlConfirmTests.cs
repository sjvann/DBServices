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
public class DbmlConfirmTests
{
    private const string Marker = "ROWMARKER-NOT-IN-DRAFT";

    [TestMethod]
    public async Task Dbml_confirm_promotes_the_only_active_package_and_reads_no_rows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-confirm-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-confirm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
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
            var parent = direct.Tables.Single(table => table.Columns.Any(column => column.Name == "Name"));
            var viewPath = Path.Combine(root, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(viewPath)!);
            var views = new ViewCatalog(viewPath);
            var kept = views.Save(new ViewDocument
            {
                Id = "kept-view",
                Name = "好檢視",
                Purpose = "已保存",
                Confirmed = true,
                Published = true,
                Sources = [parent.Name],
                Columns = [new ViewColumn { Name = "Name", Source = parent.Name }],
                Links =
                [
                    new ViewLink
                    {
                        Kind = "accepted",
                        From = parent.Name,
                        FromColumn = "Id",
                        To = parent.Name,
                        ToColumn = "Name",
                    },
                ],
            });
            Assert.IsTrue(kept.Ok, kept.Message);
            Assert.IsTrue(kept.Document!.Published);
            var viewBytes = await File.ReadAllBytesAsync(viewPath);

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "platformdb"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var draft = Package(parent.Name, "整包說明", "Id", "Name");
            var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", draft);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var path = Path.Combine(root, "catalog", "annotations.json");
            using (var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path)))
                Assert.AreEqual("", stored.RootElement[0].GetProperty("dbml").GetString());

            var exported = await SendAsync(client, HttpMethod.Get, "/annotations/draft/dbml", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, exported.Status, exported.Body);
            Assert.Contains("Project", exported.Body, StringComparison.Ordinal);
            Assert.Contains("Note: '整包說明'", exported.Body, StringComparison.Ordinal);
            Assert.Contains("Ref: " + parent.Name + ".Id > " + parent.Name + ".Name\n", exported.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("[note:", exported.Body.Split("Ref:", StringSplitOptions.None)[^1], StringComparison.Ordinal);
            Assert.DoesNotContain(Marker, exported.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(connectionString, exported.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("connectionString", exported.Body, StringComparison.OrdinalIgnoreCase);
            using (var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path)))
                Assert.AreEqual("", stored.RootElement[0].GetProperty("dbml").GetString());

            var confirmed = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, confirmed.Status, confirmed.Body);
            using (var document = JsonDocument.Parse(confirmed.Body))
            {
                Assert.AreEqual("confirmed", document.RootElement.GetProperty("status").GetString());
                Assert.IsTrue(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreEqual(exported.Body, document.RootElement.GetProperty("dbml").GetString());
            }

            Assert.DoesNotContain(Marker, confirmed.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(connectionString, confirmed.Body, StringComparison.Ordinal);
            using (var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path)))
            {
                Assert.AreEqual(1, stored.RootElement.GetArrayLength());
                var package = stored.RootElement[0];
                Assert.AreEqual("confirmed", package.GetProperty("status").GetString());
                Assert.IsTrue(package.GetProperty("active").GetBoolean());
                Assert.AreEqual(exported.Body, package.GetProperty("dbml").GetString());
                Assert.AreEqual(0, stored.RootElement.EnumerateArray().Count(item => item.GetProperty("status").GetString() == "draft"));
                Assert.AreEqual(1, stored.RootElement.EnumerateArray().Count(item => item.GetProperty("active").GetBoolean()));
            }

            var gone = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.NoContent, gone.Status);
            var active = await SendAsync(client, HttpMethod.Get, "/annotations/active", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, active.Status, active.Body);
            using (var document = JsonDocument.Parse(active.Body))
            {
                Assert.IsTrue(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreEqual(0, document.RootElement.GetProperty("others").GetArrayLength());
            }

            CollectionAssert.AreEqual(viewBytes, await File.ReadAllBytesAsync(viewPath));
            var again = SchemaGraph.Read(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            }), "PlatformDB");
            Assert.IsTrue(again.Ok, again.Message);
            CollectionAssert.AreEqual(
                direct.Tables.Select(table => table.Name).ToList(),
                again.Tables.Select(table => table.Name).ToList());
            var check = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                var values = check!.GetValueSetByFieldName("Name", parent.Name);
                Assert.IsTrue(values!.Any(value => (value?.ToString() ?? "").Contains(Marker, StringComparison.Ordinal)));
            }
            finally
            {
                (check as IDisposable)?.Dispose();
            }
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
    public async Task Dbml_confirm_replaces_the_previous_active_package()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-replace-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-replace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var path = Path.Combine(root, "catalog", "annotations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "old",
                "title": "舊包",
                "purpose": "舊說明",
                "status": "confirmed",
                "active": true,
                "tables": [{ "name": "legacy", "note": "留著", "columns": [{ "name": "Id", "type": "int", "note": "" }], "indexes": [] }],
                "links": [],
                "dbml": "Table legacy {\n}\n"
              }
            ]
            """);

            var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", Package("parent", "新說明", "Id", "Name"));
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var confirmed = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, confirmed.Status, confirmed.Body);

            using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var packages = stored.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, packages.Count);
            Assert.AreEqual(0, packages.Count(item => item.GetProperty("status").GetString() == "draft"));
            Assert.AreEqual(1, packages.Count(item => item.GetProperty("active").GetBoolean()));
            var old = packages.Single(item => item.GetProperty("id").GetString() == "old");
            Assert.AreEqual("舊包", old.GetProperty("title").GetString());
            Assert.AreEqual("舊說明", old.GetProperty("purpose").GetString());
            Assert.AreEqual("confirmed", old.GetProperty("status").GetString());
            Assert.IsFalse(old.GetProperty("active").GetBoolean());
            Assert.AreEqual("Table legacy {\n}\n", old.GetProperty("dbml").GetString());
            Assert.AreEqual("legacy", old.GetProperty("tables")[0].GetProperty("name").GetString());
            var current = packages.Single(item => item.GetProperty("active").GetBoolean());
            Assert.AreEqual("confirmed", current.GetProperty("status").GetString());
            Assert.AreEqual("parent", current.GetProperty("tables")[0].GetProperty("name").GetString());

            var active = await SendAsync(client, HttpMethod.Get, "/annotations/active", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, active.Status, active.Body);
            using var document = JsonDocument.Parse(active.Body);
            Assert.AreEqual(current.GetProperty("id").GetString(), document.RootElement.GetProperty("id").GetString());
            Assert.AreEqual("parent", document.RootElement.GetProperty("tables")[0].GetProperty("name").GetString());
            var others = document.RootElement.GetProperty("others");
            Assert.AreEqual(1, others.GetArrayLength());
            Assert.AreEqual("old", others[0].GetProperty("id").GetString());
            Assert.AreEqual("舊包", others[0].GetProperty("title").GetString());
            Assert.AreEqual("legacy", others[0].GetProperty("tables")[0].GetProperty("name").GetString());
            Assert.DoesNotContain("legacy", document.RootElement.GetProperty("tables").ToString(), StringComparison.Ordinal);
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
    public async Task Dbml_confirm_rejects_incomplete_without_reading()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-incomplete-" + Guid.NewGuid().ToString("N"));
        var blocked = Path.Combine(root, "not-a-file");
        Directory.CreateDirectory(blocked);
        try
        {
            var bodies = new[]
            {
                Package("parent", "   ", "Id", "Name"),
                "{\"id\":\"draft-1\",\"title\":\"平台\",\"purpose\":\"有說明\",\"status\":\"draft\",\"active\":false,\"tables\":[],\"links\":[],\"dbml\":\"\"}",
                "{\"id\":\"draft-1\",\"title\":\"平台\",\"purpose\":\"有說明\",\"status\":\"draft\",\"active\":false,\"tables\":[{\"name\":\"parent\",\"note\":\"\",\"columns\":[],\"indexes\":[]}],\"links\":[],\"dbml\":\"\"}",
            };
            for (var index = 0; index < bodies.Length; index++)
            {
                var folder = Path.Combine(root, index.ToString());
                Directory.CreateDirectory(folder);
                await using var factory = Factory(folder,
                [
                    new("Annotation:PlatformConnection", "PlatformDB"),
                    new("Connections:PlatformDB:ServerType", "Sqlite"),
                    new("Connections:PlatformDB:ConnectionString", "Data Source=" + blocked),
                    new("Workbench:CatalogKey", "unit-catalog-key"),
                ]);
                using var client = factory.CreateClient();
                var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", bodies[index]);
                Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
                var path = Path.Combine(folder, "catalog", "annotations.json");
                var bytes = await File.ReadAllBytesAsync(path);
                var rejected = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
                Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
                using var document = JsonDocument.Parse(rejected.Body);
                Assert.AreEqual("confirm_incomplete", document.RootElement.GetProperty("code").GetString());
                Assert.AreEqual("無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。", document.RootElement.GetProperty("message").GetString());
                CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
            }

            var catalog = new AnnotationCatalog(Path.Combine(root, "direct.json"));
            var imported = catalog.Import(JsonSerializer.Deserialize<AnnotationPackage>(Package("parent", "", "Id", "Name"), Json())!);
            Assert.IsTrue(imported.Ok, imported.Message);
            var direct = catalog.Confirm(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = "Data Source=" + blocked },
            }), "PlatformDB", []);
            Assert.AreEqual("confirm_incomplete", direct.Code);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_rejects_connection_without_reading()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-conn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var none = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var noneClient = none.CreateClient();
            await PutReady(noneClient);
            var path = Path.Combine(root, "catalog", "annotations.json");
            var bytes = await File.ReadAllBytesAsync(path);
            var noConnection = await SendAsync(noneClient, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, noConnection.Status, noConnection.Body);
            Assert.Contains("\"code\":\"no_connection\"", noConnection.Body, StringComparison.Ordinal);
            Assert.Contains("沒有讀取結構。", noConnection.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));

            await using var many = Factory(root + "-many",
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:alpha:ServerType", "Sqlite"),
                new("Connections:alpha:ConnectionString", "Data Source=alpha.db"),
                new("Connections:beta:ServerType", "PostgreSQL"),
                new("Connections:beta:ConnectionString", "Host=beta"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            Directory.CreateDirectory(root + "-many");
            using var manyClient = many.CreateClient();
            await PutReady(manyClient);
            var manyPath = Path.Combine(root + "-many", "catalog", "annotations.json");
            var manyBytes = await File.ReadAllBytesAsync(manyPath);
            var unselectable = await SendAsync(manyClient, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, unselectable.Status, unselectable.Body);
            Assert.Contains("\"code\":\"connection_unselectable\"", unselectable.Body, StringComparison.Ordinal);
            Assert.Contains("註記站不能選擇連線，沒有讀取結構。", unselectable.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("alpha", unselectable.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("beta", unselectable.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(manyBytes, await File.ReadAllBytesAsync(manyPath));

            var blocked = Path.Combine(root, "not-a-file");
            Directory.CreateDirectory(blocked);
            await using var other = Factory(root + "-other",
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:legacy:ServerType", "Sqlite"),
                new("Connections:legacy:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            Directory.CreateDirectory(root + "-other");
            using var otherClient = other.CreateClient();
            await PutReady(otherClient);
            var otherPath = Path.Combine(root + "-other", "catalog", "annotations.json");
            var otherBytes = await File.ReadAllBytesAsync(otherPath);
            var wrong = await SendAsync(otherClient, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, wrong.Status, wrong.Body);
            Assert.Contains("\"code\":\"not_platform_source\"", wrong.Body, StringComparison.Ordinal);
            Assert.Contains("不能用legacy讀結構或確認。", wrong.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(blocked, wrong.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(otherBytes, await File.ReadAllBytesAsync(otherPath));
        }
        finally
        {
            foreach (var directory in new[] { root, root + "-many", root + "-other" })
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_rejects_unmatched_names_and_blocks_views()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-block-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
                service.ExecuteSQL("INSERT INTO parent (Id, Name) VALUES (7, '" + Marker + "')");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var viewPath = Path.Combine(root, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(viewPath)!);
            var views = new ViewCatalog(viewPath);
            Assert.IsTrue(views.Save(Document("好檢視", ["parent"], [new ViewColumn { Name = "Name", Source = "parent" }], [])).Ok);
            Assert.IsTrue(views.Save(Document("壞表", ["outside"], [], [])).Ok);
            Assert.IsTrue(views.Save(Document("壞欄", ["parent"], [new ViewColumn { Name = "Missing", Source = "parent" }], [])).Ok);
            Assert.IsTrue(views.Save(Document("壞關聯", ["parent"], [],
            [
                new ViewLink { Kind = "accepted", From = "parent", FromColumn = "Id", To = "parent", ToColumn = "Gone" },
            ])).Ok);
            var viewBytes = await File.ReadAllBytesAsync(viewPath);

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var unmatchedBody = """
            {"id":"draft-1","title":"平台","purpose":"有說明","status":"draft","active":false,"dbml":"","links":[],"tables":[
              {"name":"missing_table","note":"","indexes":[],"columns":[{"name":"Id","type":"int","note":""}]},
              {"name":"parent","note":"","indexes":[],"columns":[{"name":"Id","type":"int","note":""},{"name":"Gone","type":"int","note":""}]}
            ]}
            """;
            var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", unmatchedBody);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var path = Path.Combine(root, "catalog", "annotations.json");
            var bytes = await File.ReadAllBytesAsync(path);
            var unmatched = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, unmatched.Status, unmatched.Body);
            using (var document = JsonDocument.Parse(unmatched.Body))
            {
                Assert.AreEqual("confirm_unmatched", document.RootElement.GetProperty("code").GetString());
                Assert.AreEqual("無法確認。這些表或欄對不上來源：missing_table、parent.Gone。", document.RootElement.GetProperty("message").GetString());
            }

            Assert.DoesNotContain(Marker, unmatched.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
            CollectionAssert.AreEqual(viewBytes, await File.ReadAllBytesAsync(viewPath));
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        var blockedRoot = Path.Combine(Path.GetTempPath(), "mcp-blocked-" + Guid.NewGuid().ToString("N"));
        var blockedDb = Path.Combine(Path.GetTempPath(), "ann-blocked-" + Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(blockedRoot);
        try
        {
            var blockedService = DbServiceFactory.Create("Data Source=" + blockedDb, "Sqlite");
            try
            {
                blockedService!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
            }
            finally
            {
                (blockedService as IDisposable)?.Dispose();
            }

            var blockedViews = Path.Combine(blockedRoot, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(blockedViews)!);
            var blockedCatalog = new ViewCatalog(blockedViews);
            Assert.IsTrue(blockedCatalog.Save(Document("好檢視", ["parent"], [new ViewColumn { Name = "Name", Source = "parent" }], [])).Ok);
            Assert.IsTrue(blockedCatalog.Save(Document("壞表", ["outside"], [], [])).Ok);
            Assert.IsTrue(blockedCatalog.Save(Document("壞欄", ["parent"], [new ViewColumn { Name = "Missing", Source = "parent" }], [])).Ok);
            Assert.IsTrue(blockedCatalog.Save(Document("壞關聯", ["parent"], [],
            [
                new ViewLink { Kind = "accepted", From = "parent", FromColumn = "Id", To = "parent", ToColumn = "Gone" },
            ])).Ok);
            var blockedViewBytes = await File.ReadAllBytesAsync(blockedViews);
            await using var blockedFactory = Factory(blockedRoot,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", "Data Source=" + blockedDb),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var blockedClient = blockedFactory.CreateClient();
            var ready = await SendAsync(blockedClient, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", Package("parent", "有說明", "Id", "Name"));
            Assert.AreEqual(HttpStatusCode.OK, ready.Status, ready.Body);
            var blockedPath = Path.Combine(blockedRoot, "catalog", "annotations.json");
            var blockedBytes = await File.ReadAllBytesAsync(blockedPath);
            var blocked = await SendAsync(blockedClient, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, blocked.Status, blocked.Body);
            using (var document = JsonDocument.Parse(blocked.Body))
            {
                Assert.AreEqual("confirm_blocked", document.RootElement.GetProperty("code").GetString());
                Assert.AreEqual("無法確認。這些檢視會落到新包之外：壞表、壞欄、壞關聯。", document.RootElement.GetProperty("message").GetString());
            }

            CollectionAssert.AreEqual(blockedBytes, await File.ReadAllBytesAsync(blockedPath));
            CollectionAssert.AreEqual(blockedViewBytes, await File.ReadAllBytesAsync(blockedViews));
            using var keptViews = JsonDocument.Parse(blockedViewBytes);
            Assert.IsTrue(keptViews.RootElement.EnumerateArray().All(item => item.GetProperty("published").GetBoolean()));
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(blockedDb))
                File.Delete(blockedDb);
            if (Directory.Exists(blockedRoot))
                Directory.Delete(blockedRoot, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_rejects_blank_source_when_the_column_name_is_on_a_view_source()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-blank-source-" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-blank-source-" + Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var viewPath = Path.Combine(root, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(viewPath)!);
            var views = new ViewCatalog(viewPath);
            Assert.IsTrue(views.Save(Document("好檢視", ["parent"],
            [
                new ViewColumn { Name = "Name", Source = "" },
                new ViewColumn { Name = "Id", Source = " " },
            ], [])).Ok);
            Assert.IsTrue(views.Save(Document("壞欄", ["parent"], [new ViewColumn { Name = "Missing", Source = "" }], [])).Ok);

            await using var factory = Factory(root,
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", connectionString),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ]);
            using var client = factory.CreateClient();
            var ready = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", Package("parent", "有說明", "Id", "Name"));
            Assert.AreEqual(HttpStatusCode.OK, ready.Status, ready.Body);
            var blocked = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, blocked.Status, blocked.Body);
            using var document = JsonDocument.Parse(blocked.Body);
            Assert.AreEqual("confirm_blocked", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("無法確認。這些檢視會落到新包之外：好檢視、壞欄。", document.RootElement.GetProperty("message").GetString());
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
    public async Task Dbml_confirm_forks_once_and_forbidden_callers_do_not_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-fork-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var path = Path.Combine(root, "catalog", "annotations.json");
            var missing = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", null, "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, missing.Status, missing.Body);
            var wrong = await SendAsync(client, HttpMethod.Get, "/annotations/draft/dbml", "方晴", "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, wrong.Status, wrong.Body);
            Assert.Contains("目錄金鑰不符", wrong.Body, StringComparison.Ordinal);
            var denied = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, denied.Status, denied.Body);
            Assert.IsFalse(File.Exists(path));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "live",
                "title": "啟用包",
                "purpose": "原說明",
                "status": "confirmed",
                "active": true,
                "tables": [{ "name": "parent", "note": "原表註", "columns": [{ "name": "Id", "type": "int", "note": "原欄註", "pii": "" }], "indexes": [] }],
                "links": [],
                "dbml": "Table parent {\n}\n"
              }
            ]
            """);
            var before = await File.ReadAllBytesAsync(path);
            var empty = await SendAsync(client, HttpMethod.Get, "/annotations/active", "方晴", "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, empty.Status, empty.Body);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));

            var forked = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", """
            {"id":"live","title":"啟用包","purpose":"新說明","status":"confirmed","active":true,"tables":[{"name":"parent","note":"新表註","columns":[{"name":"Id","type":"text","note":"新欄註","pii":"個資"}],"indexes":[]}],"links":[],"dbml":"SHOULD-NOT-COPY"}
            """);
            Assert.AreEqual(HttpStatusCode.OK, forked.Status, forked.Body);
            using var document = JsonDocument.Parse(forked.Body);
            Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
            Assert.IsFalse(document.RootElement.GetProperty("active").GetBoolean());
            Assert.AreNotEqual("live", document.RootElement.GetProperty("id").GetString());
            Assert.AreEqual("新說明", document.RootElement.GetProperty("purpose").GetString());
            Assert.AreEqual("新表註", document.RootElement.GetProperty("tables")[0].GetProperty("note").GetString());
            Assert.AreEqual("int", document.RootElement.GetProperty("tables")[0].GetProperty("columns")[0].GetProperty("type").GetString());
            Assert.AreEqual("", document.RootElement.GetProperty("dbml").GetString());

            using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var packages = stored.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, packages.Count);
            var live = packages.Single(item => item.GetProperty("id").GetString() == "live");
            Assert.AreEqual("原說明", live.GetProperty("purpose").GetString());
            Assert.AreEqual("原表註", live.GetProperty("tables")[0].GetProperty("note").GetString());
            Assert.AreEqual("confirmed", live.GetProperty("status").GetString());
            Assert.IsTrue(live.GetProperty("active").GetBoolean());
            Assert.AreEqual("Table parent {\n}\n", live.GetProperty("dbml").GetString());

            var snapshot = await File.ReadAllBytesAsync(path);
            var again = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", """
            {"id":"live","title":"啟用包","purpose":"第三次","status":"confirmed","active":true,"tables":[],"links":[],"dbml":""}
            """);
            Assert.AreEqual(HttpStatusCode.BadRequest, again.Status, again.Body);
            Assert.Contains("draft_exists", again.Body, StringComparison.Ordinal);
            Assert.Contains("已有工作中的草稿，沒有改。", again.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(snapshot, await File.ReadAllBytesAsync(path));

            var noActive = Path.Combine(root, "empty.json");
            var catalog = new AnnotationCatalog(noActive);
            Assert.IsNull(catalog.Active().Package);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_active_without_package_is_no_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-active-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var path = Path.Combine(root, "catalog", "annotations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "draft-1",
                "title": "草稿",
                "purpose": "有說明",
                "status": "draft",
                "active": false,
                "tables": [],
                "links": [],
                "dbml": ""
              }
            ]
            """);
            var before = await File.ReadAllBytesAsync(path);
            var active = await SendAsync(client, HttpMethod.Get, "/annotations/active", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.NoContent, active.Status, active.Body);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_read_failure_does_not_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-readfail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
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
            await PutReady(client);
            var path = Path.Combine(root, "catalog", "annotations.json");
            var before = await File.ReadAllBytesAsync(path);
            var failed = await SendAsync(client, HttpMethod.Post, "/annotations/confirm", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, failed.Status, failed.Body);
            using var document = JsonDocument.Parse(failed.Body);
            Assert.AreEqual("execution_failed", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("讀取資料表結構失敗。", document.RootElement.GetProperty("message").GetString());
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_fork_rejects_link_outside()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var path = Path.Combine(root, "catalog", "annotations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "live",
                "title": "啟用包",
                "purpose": "原說明",
                "status": "confirmed",
                "active": true,
                "tables": [{ "name": "parent", "note": "原表註", "columns": [{ "name": "Id", "type": "int", "note": "原欄註", "pii": "" }], "indexes": [] }],
                "links": [],
                "dbml": "Table parent {\n}\n"
              }
            ]
            """);
            var before = await File.ReadAllBytesAsync(path);
            var rejected = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", """
            {"id":"live","title":"啟用包","purpose":"原說明","status":"confirmed","active":true,"tables":[{"name":"parent","note":"原表註","columns":[{"name":"Id","type":"int","note":"原欄註","pii":""}],"indexes":[]}],"links":[{"from":"parent","fromColumn":"Id","to":"outside","toColumn":"Id","origin":"declared","note":""}],"dbml":""}
            """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var document = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("link_outside", document.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("關聯的一端不在這份註記包裡。", document.RootElement.GetProperty("message").GetString());
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Dbml_confirm_render_escapes_notes_and_quotes_names()
    {
        var text = AnnotationCatalog.Render(new AnnotationPackage
        {
            Title = "平台",
            Purpose = "說明",
            Tables =
            [
                new AnnotationTable
                {
                    Name = "訂單",
                    Columns = [new AnnotationColumn { Name = "Id", Type = "int", Note = "a'b\\c\r\nd" }],
                },
            ],
        });
        Assert.Contains("Table \"訂單\" {", text, StringComparison.Ordinal);
        var noteLine = text.Split('\n').Single(line => line.Contains("[note:", StringComparison.Ordinal));
        Assert.Contains("\\'", noteLine, StringComparison.Ordinal);
        Assert.Contains("\\\\", noteLine, StringComparison.Ordinal);
        Assert.Contains("\\r", noteLine, StringComparison.Ordinal);
        Assert.Contains("\\n", noteLine, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", noteLine, StringComparison.Ordinal);
    }

    private static async Task PutReady(HttpClient client)
    {
        var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key", Package("parent", "有說明", "Id", "Name"));
        Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
    }

    private static string Package(string table, string purpose, string left, string right)
        => "{\"id\":\"draft-1\",\"title\":\"平台\",\"purpose\":" + JsonSerializer.Serialize(purpose)
           + ",\"status\":\"draft\",\"active\":false,\"dbml\":\"\",\"links\":[{\"from\":\"" + table + "\",\"fromColumn\":\"" + left + "\",\"to\":\"" + table + "\",\"toColumn\":\"" + right + "\",\"origin\":\"declared\",\"note\":\"\"}],\"tables\":["
           + Table(table, left, right) + "]}";

    private static string Table(string table, string left, string right)
        => "{\"name\":\"" + table + "\",\"note\":\"表註\",\"indexes\":[],\"columns\":[{\"name\":\"" + left + "\",\"type\":\"int\",\"notNull\":true,\"note\":\"\",\"primaryKey\":true},{\"name\":\"" + right + "\",\"type\":\"text\",\"notNull\":false,\"note\":\"欄註\",\"primaryKey\":false}]}";

    private static ViewDocument Document(string name, List<string> sources, List<ViewColumn> columns, List<ViewLink> links)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Purpose = "已保存",
            Confirmed = true,
            Published = true,
            Sources = sources,
            Columns = columns,
            Links = links,
        };

    private static JsonSerializerOptions Json() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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
