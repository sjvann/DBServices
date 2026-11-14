using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DbServices.Mcp;

namespace McpService.Tests;

[TestClass]
public class NoteEditTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [TestMethod]
    public async Task Note_edit_pii_and_unique_mark_keep_source_fields()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-note-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var confirmed = Confirmed();
            var draft = DraftPackage();
            draft.Tables[1].Columns[0].Pii = "表二個資";
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { confirmed, draft }, Json));
            var catalog = new AnnotationCatalog(path);
            var request = Clone(draft);
            request.Title = "改掉的標題";
            request.Status = "confirmed";
            request.Active = true;
            request.Dbml = "Table hack {}";
            request.Purpose = "這不是真正的唯一鍵";
            request.Tables[0].Name = "第一";
            request.Tables[0].Note = "新表註";
            request.Tables[0].Columns[0].Type = "blob";
            request.Tables[0].Columns[0].NotNull = false;
            request.Tables[0].Columns[0].PrimaryKey = false;
            request.Tables[0].Columns[0].UniqueIndex = false;
            request.Tables[0].Columns[0].HName = "改名";
            request.Tables[0].Columns[0].SourceFile = "secret.xlsx";
            request.Tables[0].Columns[0].Note = "新欄註";
            request.Tables[0].Columns[0].Pii = "身分證字號原樣";
            request.Tables.Add(new AnnotationTable { Name = "幽靈", Note = "不該出現", Columns = [new AnnotationColumn { Name = "X" }] });

            var saved = catalog.Save(request);
            Assert.IsTrue(saved.Ok, saved.Message);
            Assert.AreEqual("draft", saved.Package!.Status);
            Assert.IsFalse(saved.Package.Active);
            Assert.AreEqual("註記包", saved.Package.Title);
            Assert.AreEqual("Table 註記包 { }", saved.Package.Dbml);
            Assert.AreEqual("這不是真正的唯一鍵", saved.Package.Purpose);
            Assert.AreEqual(2, saved.Package.Tables.Count);
            var column = saved.Package.Tables[0].Columns[0];
            Assert.AreEqual("char(6)", column.Type);
            Assert.IsTrue(column.NotNull);
            Assert.IsTrue(column.PrimaryKey);
            Assert.IsTrue(column.UniqueIndex);
            Assert.AreEqual("H甲", column.HName);
            Assert.AreEqual("Samples.xlsx", column.SourceFile);
            Assert.AreEqual("新欄註", column.Note);
            Assert.AreEqual("身分證字號原樣", column.Pii);
            Assert.AreEqual("新表註", saved.Package.Tables[0].Note);
            Assert.AreEqual("ix_第一_append", saved.Package.Tables[0].Indexes[0].Name);

            var omitted = Clone(saved.Package);
            omitted.Tables =
            [
                new AnnotationTable
                {
                    Name = "第一",
                    Note = "只送這一張",
                    Columns = [],
                },
            ];
            var keptNotes = catalog.Save(omitted);
            Assert.IsTrue(keptNotes.Ok, keptNotes.Message);
            Assert.AreEqual("只送這一張", keptNotes.Package!.Tables[0].Note);
            Assert.AreEqual("新欄註", keptNotes.Package.Tables[0].Columns[0].Note);
            Assert.AreEqual("身分證字號原樣", keptNotes.Package.Tables[0].Columns[0].Pii);
            Assert.AreEqual("第二表", keptNotes.Package.Tables[1].Note);
            Assert.AreEqual("丙註", keptNotes.Package.Tables[1].Columns[0].Note);
            Assert.AreEqual("表二個資", keptNotes.Package.Tables[1].Columns[0].Pii);

            using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var packages = stored.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, packages.Count);
            var old = packages.Single(item => item.GetProperty("id").GetString() == "old");
            Assert.AreEqual("confirmed", old.GetProperty("status").GetString());
            Assert.IsTrue(old.GetProperty("active").GetBoolean());
            Assert.AreEqual("已確認", old.GetProperty("purpose").GetString());
            Assert.AreEqual("keep-me", old.GetProperty("dbml").GetString());
            var fresh = packages.Single(item => item.GetProperty("id").GetString() == "pkg");
            Assert.AreEqual("draft", fresh.GetProperty("status").GetString());
            Assert.IsFalse(fresh.GetProperty("active").GetBoolean());
            var text = await File.ReadAllTextAsync(path);
            Assert.IsFalse(text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("\"rows\"", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("幽靈", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Note_edit_blank_fields_save()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-blank-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var draft = DraftPackage();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { draft }, Json));
            var catalog = new AnnotationCatalog(path);
            var request = Clone(draft);
            request.Purpose = "";
            request.Tables[0].Note = "";
            request.Tables[0].Columns[0].Note = "先寫";
            request.Tables[0].Columns[0].Note = "   ";
            var saved = catalog.Save(request);
            Assert.IsTrue(saved.Ok, saved.Message);
            Assert.AreEqual("", saved.Package!.Purpose);
            Assert.AreEqual("", saved.Package.Tables[0].Note);
            Assert.AreEqual("", saved.Package.Tables[0].Columns[0].Note);
            Assert.IsTrue(saved.Package.Tables[0].Columns[0].UniqueIndex);
            Assert.AreEqual("draft", catalog.Draft()!.Status);
            Assert.IsFalse(catalog.Draft()!.Active);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Note_edit_links_keep_origin_or_declare()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var confirmed = Confirmed();
            var draft = DraftPackage();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { confirmed, draft }, Json));
            var catalog = new AnnotationCatalog(path);
            var request = Clone(draft);
            request.Links =
            [
                new AnnotationLink
                {
                    From = "第二",
                    FromColumn = "丙",
                    To = "第一",
                    ToColumn = "甲",
                    Origin = "declared",
                    Note = "改理由",
                },
                new AnnotationLink
                {
                    From = "第二",
                    FromColumn = "丙",
                    To = "第二",
                    ToColumn = "丁",
                    Origin = "foreign-key",
                    Note = "",
                },
            ];
            var saved = catalog.Save(request);
            Assert.IsTrue(saved.Ok, saved.Message);
            Assert.AreEqual(2, saved.Package!.Links.Count);
            var kept = saved.Package.Links.Single(link => link.FromColumn == "甲" || link.ToColumn == "甲");
            Assert.AreEqual("foreign-key", kept.Origin);
            Assert.AreEqual("改理由", kept.Note);
            var added = saved.Package.Links.Single(link => link.FromColumn == "丁" || link.ToColumn == "丁");
            Assert.AreEqual("declared", added.Origin);
            Assert.AreEqual("", added.Note);
            Assert.AreEqual("第二", added.From);
            Assert.AreEqual("丙", added.FromColumn);
            Assert.AreEqual("第二", added.To);
            Assert.AreEqual("丁", added.ToColumn);
            Assert.IsFalse(saved.Package.Links.Any(link => link.Note == "要刪"));

            var sameDirection = Clone(saved.Package);
            sameDirection.Links =
            [
                new AnnotationLink
                {
                    From = "第二",
                    FromColumn = "丙",
                    To = "第一",
                    ToColumn = "甲",
                    Origin = "declared",
                    Note = "同向理由",
                },
                new AnnotationLink
                {
                    From = saved.Package.Links.Single(link => link.Origin == "declared").From,
                    FromColumn = saved.Package.Links.Single(link => link.Origin == "declared").FromColumn,
                    To = saved.Package.Links.Single(link => link.Origin == "declared").To,
                    ToColumn = saved.Package.Links.Single(link => link.Origin == "declared").ToColumn,
                    Origin = "foreign-key",
                    Note = saved.Package.Links.Single(link => link.Origin == "declared").Note,
                },
            ];
            var resaved = catalog.Save(sameDirection);
            Assert.IsTrue(resaved.Ok, resaved.Message);
            var same = resaved.Package!.Links.Single(link => link.FromColumn == "丙" && link.ToColumn == "甲" || link.FromColumn == "甲" && link.ToColumn == "丙");
            Assert.AreEqual("foreign-key", same.Origin);
            Assert.AreEqual("同向理由", same.Note);
            Assert.AreEqual("第二", same.From);
            Assert.AreEqual("丙", same.FromColumn);
            Assert.AreEqual("第一", same.To);
            Assert.AreEqual("甲", same.ToColumn);
            Assert.AreEqual(2, resaved.Package.Links.Count);

            using var stored = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.AreEqual(2, stored.RootElement.GetArrayLength());
            Assert.AreEqual(1, stored.RootElement.EnumerateArray().Count(item => item.GetProperty("status").GetString() == "draft"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Note_edit_link_outside_keeps_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var draft = DraftPackage();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { draft }, Json));
            var before = await File.ReadAllBytesAsync(path);
            var catalog = new AnnotationCatalog(path);
            var request = Clone(draft);
            request.Purpose = "不該寫入";
            request.Links.Add(new AnnotationLink
            {
                From = "外面",
                FromColumn = "無",
                To = "第一",
                ToColumn = "甲",
                Origin = "declared",
                Note = "",
            });
            var saved = catalog.Save(request);
            Assert.IsFalse(saved.Ok);
            Assert.AreEqual("link_outside", saved.Code);
            Assert.AreEqual("關聯的一端不在這份註記包裡。", saved.Message);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
            var current = catalog.Draft()!;
            Assert.AreEqual("原說明", current.Purpose);
            Assert.AreEqual(2, current.Links.Count);
            Assert.IsTrue(current.Links.Any(link => link.Note == "要刪"));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Note_edit_write_failure_keeps_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var draft = DraftPackage();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { draft }, Json));
            var before = await File.ReadAllBytesAsync(path);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            var catalog = new AnnotationCatalog(path);
            var request = Clone(draft);
            request.Purpose = "寫不進去";
            var saved = catalog.Save(request);
            Assert.IsFalse(saved.Ok);
            Assert.AreEqual("execution_failed", saved.Code);
            Assert.AreEqual("沒有保存。", saved.Message);
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path));
            Assert.AreEqual("原說明", catalog.Draft()!.Purpose);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (File.Exists(path))
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Note_edit_missing_draft_does_not_create()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-none-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var catalog = new AnnotationCatalog(path);
            var saved = catalog.Save(DraftPackage());
            Assert.IsFalse(saved.Ok);
            Assert.AreEqual("execution_failed", saved.Code);
            Assert.AreEqual("沒有保存。", saved.Message);
            Assert.IsFalse(File.Exists(path));
            Assert.IsNull(catalog.Draft());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Note_edit_reopen_keeps_draft_and_other_id_still_imports()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-note-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var views = Path.Combine(root, "catalog", "views.json");
            Directory.CreateDirectory(Path.GetDirectoryName(views)!);
            await File.WriteAllTextAsync(views, """[{"id":"view-1","name":"已保存檢視"}]""");
            var viewBytes = await File.ReadAllBytesAsync(views);
            var annotations = Path.Combine(root, "catalog", "annotations.json");
            await File.WriteAllTextAsync(annotations, """
            [
              {
                "id": "old",
                "title": "Old",
                "purpose": "已確認",
                "status": "confirmed",
                "active": true,
                "tables": [],
                "links": [],
                "dbml": "keep-me"
              }
            ]
            """);

            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            var created = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key",
                """{"id":"pkg","title":"註記包","purpose":"","status":"confirmed","active":true,"tables":[{"name":"第一","note":"","columns":[{"name":"甲","type":"text","notNull":true,"note":"","pii":"","primaryKey":true,"uniqueIndex":true}]}],"links":[],"dbml":""}""");
            Assert.AreEqual(HttpStatusCode.OK, created.Status, created.Body);

            var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key",
                """{"id":"pkg","title":"被丟棄","purpose":"重開說明","status":"confirmed","active":true,"tables":[{"name":"第一","note":"表還在","columns":[{"name":"甲","type":"blob","notNull":false,"note":"","pii":"個資原文","primaryKey":false,"uniqueIndex":false}]}],"links":[],"dbml":"hack","connectionString":"Data Source=secret"}""");
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using (var document = JsonDocument.Parse(saved.Body))
            {
                Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
                Assert.IsFalse(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreEqual("重開說明", document.RootElement.GetProperty("purpose").GetString());
                Assert.AreEqual("註記包", document.RootElement.GetProperty("title").GetString());
                var column = document.RootElement.GetProperty("tables")[0].GetProperty("columns")[0];
                Assert.AreEqual("text", column.GetProperty("type").GetString());
                Assert.IsTrue(column.GetProperty("uniqueIndex").GetBoolean());
                Assert.AreEqual("個資原文", column.GetProperty("pii").GetString());
                Assert.AreEqual("", column.GetProperty("note").GetString());
            }

            var again = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "方晴", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, again.Status, again.Body);
            using (var document = JsonDocument.Parse(again.Body))
            {
                Assert.AreEqual("pkg", document.RootElement.GetProperty("id").GetString());
                Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
                Assert.IsFalse(document.RootElement.GetProperty("active").GetBoolean());
                Assert.AreEqual("重開說明", document.RootElement.GetProperty("purpose").GetString());
                Assert.AreEqual("表還在", document.RootElement.GetProperty("tables")[0].GetProperty("note").GetString());
                Assert.AreEqual("", document.RootElement.GetProperty("tables")[0].GetProperty("columns")[0].GetProperty("note").GetString());
                Assert.AreEqual("個資原文", document.RootElement.GetProperty("tables")[0].GetProperty("columns")[0].GetProperty("pii").GetString());
            }

            var text = await File.ReadAllTextAsync(annotations);
            Assert.IsFalse(text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("Data Source=", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("\"rows\"", StringComparison.OrdinalIgnoreCase));
            using var stored = JsonDocument.Parse(text);
            var packages = stored.RootElement.EnumerateArray().ToList();
            Assert.AreEqual(2, packages.Count);
            var old = packages.Single(item => item.GetProperty("id").GetString() == "old");
            Assert.AreEqual("confirmed", old.GetProperty("status").GetString());
            Assert.IsTrue(old.GetProperty("active").GetBoolean());
            Assert.AreEqual("已確認", old.GetProperty("purpose").GetString());
            Assert.AreEqual("keep-me", old.GetProperty("dbml").GetString());
            CollectionAssert.AreEqual(viewBytes, await File.ReadAllBytesAsync(views));

            var bytes = await File.ReadAllBytesAsync(annotations);
            var rejected = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "方晴", "unit-catalog-key",
                """{"id":"other","title":"Other","purpose":"新的一份","status":"draft","active":false,"tables":[],"links":[],"dbml":""}""");
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            Assert.Contains("draft_exists", rejected.Body, StringComparison.Ordinal);
            Assert.Contains("已有工作中的草稿，沒有改。", rejected.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(annotations));
            CollectionAssert.AreEqual(viewBytes, await File.ReadAllBytesAsync(views));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static AnnotationPackage Confirmed() => new()
    {
        Id = "old",
        Title = "Old",
        Purpose = "已確認",
        Status = "confirmed",
        Active = true,
        Dbml = "keep-me",
    };

    private static AnnotationPackage DraftPackage() => new()
    {
        Id = "pkg",
        Title = "註記包",
        Purpose = "原說明",
        Status = "draft",
        Active = false,
        Dbml = "Table 註記包 { }",
        Tables =
        [
            new AnnotationTable
            {
                Name = "第一",
                Note = "原表註",
                Columns =
                [
                    new AnnotationColumn
                    {
                        Name = "甲",
                        Type = "char(6)",
                        NotNull = true,
                        Note = "原欄註",
                        Pii = "",
                        PrimaryKey = true,
                        UniqueIndex = true,
                        HName = "H甲",
                        SName = "S甲",
                        SourceFile = "Samples.xlsx",
                    },
                ],
                Indexes = [new AnnotationIndex { Name = "ix_第一_append", Note = "S_唯一索引", Columns = ["甲"] }],
            },
            new AnnotationTable
            {
                Name = "第二",
                Note = "第二表",
                Columns =
                [
                    new AnnotationColumn { Name = "丙", Type = "int", Note = "丙註" },
                    new AnnotationColumn { Name = "丁", Type = "int", Note = "" },
                ],
            },
        ],
        Links =
        [
            new AnnotationLink { From = "第一", FromColumn = "甲", To = "第二", ToColumn = "丙", Origin = "foreign-key", Note = "舊理由" },
            new AnnotationLink { From = "第一", FromColumn = "甲", To = "第二", ToColumn = "丁", Origin = "declared", Note = "要刪" },
        ],
    };

    private static AnnotationPackage Clone(AnnotationPackage package)
        => JsonSerializer.Deserialize<AnnotationPackage>(JsonSerializer.Serialize(package, Json), Json)!;

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
}
