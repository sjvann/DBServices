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
public class RereadAbandonTests
{
    private const string Marker = "ROWMARKER-NOT-IN-DRAFT";

    [TestMethod]
    public void Reread_abandon_rename_keeps_the_old_note()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { ActivePackage(), DraftPackage() }, Json()));
            var catalog = new AnnotationCatalog(path);
            var merged = catalog.Import(SourcePackage());
            Assert.IsTrue(merged.Ok, merged.Message);
            var package = merged.Package!;
            Assert.AreEqual("draft-1", package.Id);
            Assert.AreEqual("草稿包", package.Title);
            Assert.AreEqual("草稿說明", package.Purpose);
            Assert.AreEqual("draft", package.Status);
            Assert.IsFalse(package.Active);

            var table = package.Tables.Single(item => item.Name == "T");
            Assert.AreEqual("草稿表註", table.Note);
            Assert.IsFalse(table.Columns.Any(column => column.Name == "OFFICE_CODE"));
            var renamed = table.Columns.Single(column => column.Name == "office_code");
            Assert.AreEqual("來源註", renamed.Note);
            Assert.AreEqual("來源個資", renamed.Pii);
            Assert.AreEqual("局代碼", renamed.HName);
            Assert.AreNotEqual("局號", renamed.Note);
            Assert.AreEqual("", table.Columns.Single(column => column.Name == "KEPT").Note);
            Assert.AreEqual("留下", table.Columns.Single(column => column.Name == "SAME").Note);
            Assert.AreEqual("從啟用抄", table.Columns.Single(column => column.Name == "SHARED").Note);
            Assert.AreEqual("空白鍵", table.Columns.Single(column => column.Name == "CODE").Note);
            Assert.AreEqual("新表", package.Tables.Single(item => item.Name == "FRESH").Note);
            Assert.IsFalse(package.Tables.Any(item => item.Name == "EMPTY"));

            Assert.IsTrue(package.Changes.Any(change => change.Kind == "removed" && change.Table == "T" && change.Column == "OFFICE_CODE"));
            Assert.IsTrue(package.Changes.Any(change => change.Kind == "added" && change.Table == "T" && change.Column == "office_code"));
            Assert.IsTrue(package.Changes.Any(change => change.Kind == "removed" && change.Column == "GONE"));
            Assert.IsTrue(package.Changes.Any(change => change.Kind == "added" && change.Table == "FRESH" && change.Column == ""));
            Assert.IsTrue(package.Changes.Any(change => change.Kind == "removed" && change.Table == "EMPTY" && change.Column == ""));
            Assert.IsTrue(package.Changes.Any(change => change.Kind == "changed" && change.Column == "KEPT"));
            Assert.IsFalse(package.Changes.Any(change => change.Column == "SAME" || change.Column.Trim() == "CODE" || (change.Table == "T" && change.Column == "")));
            Assert.IsFalse(package.Links.Any(link => link.Note == "斷掉"));
            Assert.AreEqual("人補", package.Links.Single(link => link.Origin == "declared").Note);
            var keptFk = package.Links.Single(link => link.Origin == "foreign-key" && link.FromColumn == "SAME");
            Assert.AreEqual("留下理由", keptFk.Note);
            Assert.AreEqual("KEPT", keptFk.ToColumn);
            var moved = package.Links.Single(link => link.Origin == "foreign-key" && link.FromColumn == "KEPT");
            Assert.AreEqual("office_code", moved.ToColumn);
            Assert.AreEqual("來源外鍵", moved.Note);

            var live = catalog.Active().Package!;
            Assert.AreEqual("live", live.Id);
            Assert.IsTrue(live.Active);
            Assert.AreEqual("confirmed", live.Status);
            Assert.AreEqual("啟用說明", live.Purpose);
            Assert.AreEqual("KEEP-DBML", live.Dbml);
            Assert.AreEqual("啟用局號", live.Tables[0].Columns.Single(column => column.Name == "OFFICE_CODE").Note);
            Assert.IsFalse(File.ReadAllText(path).Contains("connectionString", StringComparison.OrdinalIgnoreCase));

            var echo = JsonSerializer.Deserialize<AnnotationPackage>(JsonSerializer.Serialize(package, Json()), Json())!;
            echo.Id = "other";
            echo.Title = "別的";
            echo.Purpose = "別的說明";
            echo.Tables.Single(item => item.Name == "T").Columns.Single(column => column.Name == "SAME").Note = "蓋不掉";
            var quiet = catalog.Import(echo);
            Assert.IsTrue(quiet.Ok, quiet.Message);
            Assert.AreEqual(0, quiet.Package!.Changes.Count);
            Assert.AreEqual("draft-1", quiet.Package.Id);
            Assert.AreEqual("草稿說明", quiet.Package.Purpose);
            Assert.AreEqual("留下", quiet.Package.Tables.Single(item => item.Name == "T").Columns.Single(column => column.Name == "SAME").Note);

            var seededRoot = Path.Combine(root, "seed.json");
            File.WriteAllText(seededRoot, JsonSerializer.Serialize(new[] { ActivePackage() }, Json()));
            var seeded = new AnnotationCatalog(seededRoot).Import(SourcePackage());
            Assert.IsTrue(seeded.Ok, seeded.Message);
            Assert.AreNotEqual("live", seeded.Package!.Id);
            Assert.AreEqual("從啟用抄", seeded.Package.Tables.Single(item => item.Name == "T").Columns.Single(column => column.Name == "SHARED").Note);
            Assert.AreNotEqual("局號", seeded.Package.Tables.Single(item => item.Name == "T").Columns.Single(column => column.Name == "office_code").Note);
            Assert.IsTrue(seeded.Package.Changes.Any(change => change.Kind == "removed" && change.Table == "T" && change.Column == "OFFICE_CODE"));
            Assert.IsTrue(seeded.Package.Changes.Any(change => change.Kind == "added" && change.Table == "T" && change.Column == "office_code"));
            var still = new AnnotationCatalog(seededRoot).Active().Package!;
            Assert.AreEqual("KEEP-DBML", still.Dbml);
            Assert.IsTrue(still.Active);
            Assert.AreEqual("啟用局號", still.Tables[0].Columns.Single(column => column.Name == "OFFICE_CODE").Note);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Reread_abandon_read_seeds_notes_from_the_active_package()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "reread-seed-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var connectionString = "Data Source=" + dbPath;
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE T (office_code TEXT, KEPT TEXT)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            File.WriteAllText(path, JsonSerializer.Serialize(new[] { ActivePackage() }, Json()));
            var read = new AnnotationCatalog(path).ReadStructure(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            }), "PlatformDB");
            Assert.IsTrue(read.Ok, read.Message);
            Assert.AreNotEqual("live", read.Package!.Id);
            Assert.AreEqual("draft", read.Package.Status);
            var table = read.Package.Tables.Single(item => item.Name == "T");
            Assert.AreEqual("啟用表註", table.Note);
            Assert.AreEqual("啟用保留", table.Columns.Single(column => column.Name == "KEPT").Note);
            Assert.AreNotEqual("啟用局號", table.Columns.Single(column => column.Name == "office_code").Note);
            Assert.IsTrue(read.Package.Changes.Any(change => change.Kind == "removed" && change.Table == "T" && change.Column == "OFFICE_CODE"));
            Assert.IsTrue(read.Package.Changes.Any(change => change.Kind == "added" && change.Table == "T" && change.Column == "office_code"));

            var live = new AnnotationCatalog(path).Active().Package!;
            Assert.AreEqual("live", live.Id);
            Assert.IsTrue(live.Active);
            Assert.AreEqual("confirmed", live.Status);
            Assert.AreEqual("啟用局號", live.Tables[0].Columns.Single(column => column.Name == "OFFICE_CODE").Note);
            Assert.AreEqual("啟用保留", live.Tables[0].Columns.Single(column => column.Name == "KEPT").Note);
            Assert.AreEqual("KEEP-DBML", live.Dbml);
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
    public async Task Reread_abandon_failure_does_not_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "direct.json");
            await File.WriteAllTextAsync(path, DraftText());
            var bytes = await File.ReadAllBytesAsync(path);
            var refused = new AnnotationCatalog(path).ReadStructure(new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = "   " },
            }), "PlatformDB");
            Assert.AreEqual("connection_failed", refused.Code);
            Assert.AreEqual("無法建立連線。", refused.Message);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));

            var blocked = Path.Combine(root, "not-a-file");
            Directory.CreateDirectory(blocked);
            await AssertGate(root + "-read", DraftText(),
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:PlatformDB:ServerType", "Sqlite"),
                new("Connections:PlatformDB:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key", "execution_failed", "讀取資料表結構失敗。");

            await AssertGate(root + "-none", DraftText(),
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:blank:ServerType", "Sqlite"),
                new("Connections:blank:ConnectionString", "   "),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key", "no_connection", "沒有讀取結構。");

            await AssertGate(root + "-many", DraftText(),
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:alpha:ServerType", "Sqlite"),
                new("Connections:alpha:ConnectionString", "Data Source=alpha.db"),
                new("Connections:beta:ServerType", "PostgreSQL"),
                new("Connections:beta:ConnectionString", "Host=beta"),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key", "connection_unselectable", "註記站不能選擇連線，沒有讀取結構。");

            await AssertGate(root + "-name", DraftText(),
            [
                new("Annotation:PlatformConnection", "PlatformDB"),
                new("Connections:legacy:ServerType", "Sqlite"),
                new("Connections:legacy:ConnectionString", "Data Source=" + blocked),
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Post, "/annotations/read-structure", "方晴", "unit-catalog-key", "not_platform_source", "不能用legacy讀結構或確認。");

            await AssertGate(root + "-key", DraftText(),
            [
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Post, "/annotations/read-structure", "方晴", "wrong-key", "forbidden", "目錄金鑰不符。");
            await AssertGate(root + "-delete", DraftText(),
            [
                new("Workbench:CatalogKey", "unit-catalog-key"),
            ], HttpMethod.Delete, "/annotations/draft", "方晴", "wrong-key", "forbidden", "目錄金鑰不符。");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            foreach (var extra in new[] { "-read", "-none", "-many", "-name", "-key", "-delete" })
            {
                if (Directory.Exists(root + extra))
                    Directory.Delete(root + extra, true);
            }
        }
    }

    [TestMethod]
    public async Task Reread_abandon_drops_only_the_draft()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var views = Path.Combine(root, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(views)!);
            await File.WriteAllTextAsync(views, """[{"id":"view-1","name":"已保存檢視"}]""");
            var viewBytes = await File.ReadAllBytesAsync(views);
            var annotations = Path.Combine(root, "catalog", "annotations.json");
            await File.WriteAllTextAsync(annotations, JsonSerializer.Serialize(new[]
            {
                ActivePackage(),
                DraftPackage(),
            }, Json()));

            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var deleted = await SendAsync(client, HttpMethod.Delete, "/annotations/draft", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.NoContent, deleted.Status, deleted.Body);
            var missing = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.NoContent, missing.Status, missing.Body);
            var active = await SendAsync(client, HttpMethod.Get, "/annotations/active", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, active.Status, active.Body);
            using (var document = JsonDocument.Parse(active.Body))
            {
                Assert.AreEqual("live", document.RootElement.GetProperty("id").GetString());
                Assert.AreEqual("啟用說明", document.RootElement.GetProperty("purpose").GetString());
                Assert.AreEqual("KEEP-DBML", document.RootElement.GetProperty("dbml").GetString());
                Assert.IsTrue(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreEqual("confirmed", document.RootElement.GetProperty("status").GetString());
                Assert.AreEqual("啟用局號", document.RootElement.GetProperty("tables")[0].GetProperty("columns")[0].GetProperty("note").GetString());
            }

            var text = await File.ReadAllTextAsync(annotations);
            Assert.IsFalse(text.Contains("\"draft\"", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains(Marker, StringComparison.Ordinal));
            CollectionAssert.AreEqual(viewBytes, await File.ReadAllBytesAsync(views));
            var again = await SendAsync(client, HttpMethod.Delete, "/annotations/draft", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, again.Status, again.Body);
            Assert.Contains("execution_failed", again.Body, StringComparison.Ordinal);
            Assert.Contains("沒有保存。", again.Body, StringComparison.Ordinal);
            Assert.AreEqual("KEEP-DBML", new AnnotationCatalog(annotations).Active().Package!.Dbml);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Reread_abandon_reads_no_rows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "reread-rows-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-rows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var connectionString = "Data Source=" + dbPath;
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
                service.ExecuteSQL("CREATE TABLE child (Id INTEGER PRIMARY KEY, ParentId INTEGER REFERENCES parent(Id))");
                service.ExecuteSQL("INSERT INTO parent (Id, Name) VALUES (1, '" + Marker + "')");
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
            var idType = direct.Tables.Single(table => table.Name == "parent").Columns.Single(column => column.Name == "Id").Type;
            var annotations = Path.Combine(root, "catalog", "annotations.json");
            Directory.CreateDirectory(Path.GetDirectoryName(annotations)!);
            var active = new AnnotationPackage
            {
                Id = "live",
                Title = "啟用包",
                Purpose = "啟用說明",
                Status = "confirmed",
                Active = true,
                Dbml = "KEEP-DBML",
                Tables =
                [
                    new AnnotationTable
                    {
                        Name = "parent",
                        Note = "啟用表註",
                        Columns =
                        [
                            new AnnotationColumn { Name = "Id", Type = idType, Note = "啟用主鍵", PrimaryKey = true },
                            new AnnotationColumn { Name = "Name", Type = "TEXT", Note = "名稱" },
                        ],
                    },
                ],
            };
            var draft = new AnnotationPackage
            {
                Id = "draft-1",
                Title = "草稿包",
                Purpose = "草稿說明",
                Status = "draft",
                Tables =
                [
                    new AnnotationTable
                    {
                        Name = "parent",
                        Note = "草稿表註",
                        Columns = [new AnnotationColumn { Name = "Id", Type = idType, Note = "草稿主鍵", PrimaryKey = true }],
                    },
                ],
            };
            await File.WriteAllTextAsync(annotations, JsonSerializer.Serialize(new[] { active, draft }, Json()));

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
            Assert.IsFalse(saved.Body.Contains(Marker, StringComparison.Ordinal));
            Assert.IsFalse(saved.Body.Contains(connectionString, StringComparison.Ordinal));
            using (var document = JsonDocument.Parse(saved.Body))
            {
                Assert.AreEqual("draft-1", document.RootElement.GetProperty("id").GetString());
                Assert.AreEqual("草稿包", document.RootElement.GetProperty("title").GetString());
                Assert.AreEqual("草稿說明", document.RootElement.GetProperty("purpose").GetString());
                var parent = document.RootElement.GetProperty("tables").EnumerateArray().Single(table => table.GetProperty("name").GetString() == "parent");
                Assert.AreEqual("草稿表註", parent.GetProperty("note").GetString());
                var columns = parent.GetProperty("columns");
                Assert.AreEqual("草稿主鍵", Column(columns, "Id").GetProperty("note").GetString());
                Assert.AreEqual("名稱", Column(columns, "Name").GetProperty("note").GetString());
            }

            var text = await File.ReadAllTextAsync(annotations);
            Assert.IsFalse(text.Contains(Marker, StringComparison.Ordinal));
            using var stored = JsonDocument.Parse(text);
            var live = stored.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetString() == "live");
            Assert.IsTrue(live.GetProperty("active").GetBoolean());
            Assert.AreEqual("KEEP-DBML", live.GetProperty("dbml").GetString());
            Assert.AreEqual("啟用主鍵", live.GetProperty("tables")[0].GetProperty("columns")[0].GetProperty("note").GetString());
            Assert.AreEqual(1, live.GetProperty("tables").GetArrayLength());
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
    public async Task Reread_abandon_confirm_clears_the_list()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "reread-confirm-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "mcp-reread-confirm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var connectionString = "Data Source=" + dbPath;
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY)");
                service.ExecuteSQL("INSERT INTO parent (Id) VALUES (1)");
                service.ExecuteSQL("UPDATE parent SET Id = 1");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var draft = new AnnotationPackage
            {
                Id = "draft-1",
                Title = "平台",
                Purpose = "有說明",
                Status = "draft",
                Tables = [new AnnotationTable { Name = "parent", Columns = [new AnnotationColumn { Name = "Id", Type = "int", Note = "欄註", PrimaryKey = true }] }],
                Changes = [new AnnotationChange { Kind = "added", Table = "parent", Column = "Id" }],
            };
            var old = new AnnotationPackage
            {
                Id = "old",
                Title = "舊包",
                Purpose = "舊說明",
                Status = "confirmed",
                Active = true,
                Dbml = "KEEP",
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { old, draft }, Json()));
            var catalog = new AnnotationCatalog(path);
            var tools = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var bytes = await File.ReadAllBytesAsync(path);
            var blocked = catalog.Confirm(tools, "PlatformDB",
            [
                new ViewDocument { Name = "落外", Sources = ["missing"] },
            ]);
            Assert.AreEqual("confirm_blocked", blocked.Code);
            Assert.Contains("落外", blocked.Message, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));

            var confirmed = catalog.Confirm(tools, "PlatformDB", []);
            Assert.IsTrue(confirmed.Ok, confirmed.Message);
            Assert.AreEqual(0, confirmed.Package!.Changes.Count);
            Assert.IsTrue(confirmed.Package.Active);
            Assert.AreEqual("confirmed", confirmed.Package.Status);
            var stored = new AnnotationCatalog(path);
            var live = stored.Active().Package!;
            Assert.AreEqual("draft-1", live.Id);
            Assert.AreEqual(0, live.Changes.Count);
            var replaced = stored.Active().Others.Single();
            Assert.AreEqual("old", replaced.Id);
            Assert.AreEqual("confirmed", replaced.Status);
            Assert.IsFalse(replaced.Active);
            Assert.AreEqual("舊說明", replaced.Purpose);
            Assert.AreEqual("KEEP", replaced.Dbml);
            Assert.IsFalse((await File.ReadAllTextAsync(path)).Contains("ROWMARKER", StringComparison.Ordinal));
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

    private static AnnotationPackage ActivePackage() => new()
    {
        Id = "live",
        Title = "啟用包",
        Purpose = "啟用說明",
        Status = "confirmed",
        Active = true,
        Dbml = "KEEP-DBML",
        Tables =
        [
            new AnnotationTable
            {
                Name = "T",
                Note = "啟用表註",
                Columns =
                [
                    new AnnotationColumn { Name = "OFFICE_CODE", Type = "char(3)", Note = "啟用局號", Pii = "啟用個資", NotNull = true, PrimaryKey = true },
                    new AnnotationColumn { Name = "KEPT", Type = "text", Note = "啟用保留", UniqueIndex = true },
                    new AnnotationColumn { Name = "SHARED", Type = "text", Note = "從啟用抄" },
                ],
            },
        ],
    };

    private static AnnotationPackage DraftPackage() => new()
    {
        Id = "draft-1",
        Title = "草稿包",
        Purpose = "草稿說明",
        Status = "draft",
        Tables =
        [
            new AnnotationTable
            {
                Name = "T",
                Note = "草稿表註",
                Columns =
                [
                    new AnnotationColumn { Name = "OFFICE_CODE", Type = "char(3)", Note = "局號", Pii = "草稿個資", NotNull = true, PrimaryKey = true },
                    new AnnotationColumn { Name = "KEPT", Type = "text", Note = "", UniqueIndex = true },
                    new AnnotationColumn { Name = "GONE", Type = "int", Note = "將刪" },
                    new AnnotationColumn { Name = "SAME", Type = "text", Note = "留下" },
                    new AnnotationColumn { Name = " CODE ", Type = "text", Note = "空白鍵" },
                ],
            },
            new AnnotationTable { Name = "EMPTY", Note = "空表" },
        ],
        Links =
        [
            new AnnotationLink { From = "T", FromColumn = "KEPT", To = "T", ToColumn = "OFFICE_CODE", Origin = "declared", Note = "斷掉" },
            new AnnotationLink { From = "T", FromColumn = "SAME", To = "T", ToColumn = "KEPT", Origin = "declared", Note = "人補" },
            new AnnotationLink { From = "T", FromColumn = "KEPT", To = "T", ToColumn = "KEPT", Origin = "foreign-key", Note = "舊外鍵理由" },
            new AnnotationLink { From = "T", FromColumn = "SAME", To = "T", ToColumn = "KEPT", Origin = "foreign-key", Note = "留下理由" },
        ],
    };

    private static AnnotationPackage SourcePackage() => new()
    {
        Id = "other",
        Title = "別的",
        Purpose = "來源說明",
        Tables =
        [
            new AnnotationTable
            {
                Name = "T",
                Note = "來源表註",
                Columns =
                [
                    new AnnotationColumn { Name = "office_code", Type = "varchar(10)", Note = "來源註", Pii = "來源個資", HName = "局代碼" },
                    new AnnotationColumn { Name = "KEPT", Type = "text", NotNull = true, Note = "來源保留", Pii = "來源pii" },
                    new AnnotationColumn { Name = "SAME", Type = "text", Note = "蓋不掉" },
                    new AnnotationColumn { Name = "SHARED", Type = "text", Note = "來源共享" },
                    new AnnotationColumn { Name = "NEXT", Type = "int", Note = "新欄" },
                    new AnnotationColumn { Name = "CODE", Type = "text", Note = "不該用" },
                ],
            },
            new AnnotationTable { Name = "FRESH", Note = "新表" },
        ],
        Links =
        [
            new AnnotationLink { From = "T", FromColumn = "KEPT", To = "T", ToColumn = "office_code", Origin = "foreign-key", Note = "來源外鍵" },
            new AnnotationLink { From = "T", FromColumn = "SAME", To = "T", ToColumn = "KEPT", Origin = "foreign-key", Note = "來源理由" },
        ],
    };

    private static string DraftText()
        => """[{"id":"draft-1","title":"草稿包","purpose":"草稿說明","status":"draft","active":false,"tables":[],"links":[],"dbml":""}]""";

    private static JsonElement Column(JsonElement columns, string name)
        => columns.EnumerateArray().Single(column => column.GetProperty("name").GetString() == name);

    private static async Task AssertGate(
        string root,
        string body,
        KeyValuePair<string, string?>[] settings,
        HttpMethod method,
        string url,
        string caller,
        string key,
        string code,
        string message)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "catalog", "annotations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, body);
        var bytes = await File.ReadAllBytesAsync(path);
        await using var factory = Factory(root, settings);
        using var client = factory.CreateClient();
        var response = await SendAsync(client, method, url, caller, key);
        var expected = code == "forbidden" ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest;
        Assert.AreEqual(expected, response.Status, response.Body);
        Assert.Contains(code, response.Body, StringComparison.Ordinal);
        Assert.Contains(message, response.Body, StringComparison.Ordinal);
        if (code == "connection_unselectable")
        {
            Assert.IsFalse(response.Body.Contains("alpha", StringComparison.Ordinal));
            Assert.IsFalse(response.Body.Contains("beta", StringComparison.Ordinal));
        }

        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
    }

    private static JsonSerializerOptions Json() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
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
