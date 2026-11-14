using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class StructureDraftTests
{
    [TestMethod]
    [Timeout(15000)]
    public async Task Structure_draft_screen_lists_structure_without_reading_rows()
    {
        var caller = new RecordingCaller();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var annotations = new RecordingAnnotations
        {
            ReadGate = release.Task,
            StructurePackage = SamplePackage(),
        };
        await using var context = Station(caller, annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        Assert.AreEqual("讀結構", cut.Find("#annotation-read").TextContent.Trim());
        Assert.IsTrue(cut.Find("#annotation-read").ClassList.Contains("secondary"));
        Assert.AreEqual(0, cut.FindAll(".progress-line").Count);
        Assert.AreEqual(0, cut.FindAll(".status-message").Count);
        AssertAbsentActions(cut.FindAll("button"));

        var click = cut.Find("#annotation-read").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(WorkbenchMessages.ReadingStructure, cut.Find(".progress-line").TextContent.Trim());
            Assert.AreEqual(1, cut.FindAll(".progress-line").Count);
            Assert.IsTrue(cut.Find("#annotation-import").HasAttribute("disabled"));
            Assert.IsTrue(cut.Find("#annotation-read").HasAttribute("disabled"));
            Assert.IsFalse(cut.Markup.Contains(WorkbenchMessages.Importing, StringComparison.Ordinal));
        });
        release.TrySetResult();
        await click;

        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("平台*<DB>", StringComparison.Ordinal)));
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll(".progress-line").Count);
        Assert.IsFalse(cut.Find("#annotation-read").HasAttribute("disabled"));
        var key = cut.Find("tr[data-column='Id']");
        var keyCells = key.QuerySelectorAll("td");
        Assert.AreEqual("", keyCells[2].TextContent.Trim());
        Assert.AreEqual("尚未註記", keyCells[3].TextContent.Trim());
        Assert.IsTrue(keyCells[3].ClassList.Contains("unnoted"));
        Assert.AreEqual("主鍵", keyCells[4].TextContent.Trim());
        var name = cut.Find("tr[data-column='Name']");
        var nameCells = name.QuerySelectorAll("td");
        Assert.AreEqual("已寫", nameCells[3].QuerySelector("textarea")!.GetAttribute("value"));
        Assert.IsFalse(nameCells[3].ClassList.Contains("unnoted"));
        Assert.AreEqual("", nameCells[4].TextContent.Trim());
        var link = cut.Find(".link-editor");
        Assert.IsTrue(link.QuerySelectorAll("button").Length > 0);
        foreach (var button in link.QuerySelectorAll("button"))
        {
            var text = button.TextContent.Trim();
            Assert.IsTrue(text is "刪除" or "新增", text);
        }
        Assert.IsTrue(link.TextContent.Contains("child", StringComparison.Ordinal));
        Assert.IsTrue(link.TextContent.Contains("ParentId", StringComparison.Ordinal));
        Assert.IsTrue(link.TextContent.Contains("parent", StringComparison.Ordinal));
        Assert.IsTrue(link.TextContent.Contains("Id", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("foreign-key", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        AssertAbsentActions(cut.FindAll("button"));
        Assert.AreEqual(0, caller.Count);
        Assert.AreEqual(1, annotations.Commits);

        var css = File.ReadAllText(FindUp("Workbench.Web", "wwwroot", "tokens.css"));
        var rule = css.Split(".unnoted", 2)[1];
        Assert.IsTrue(rule.Contains("var(--surface-sunken)", StringComparison.Ordinal));
        Assert.IsTrue(rule.Contains("var(--caution)", StringComparison.Ordinal));

        await cut.Find("#annotation-read").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(2, annotations.Commits));
        Assert.AreEqual(0, cut.FindAll(".status-message").Count);
        Assert.AreEqual(0, cut.FindAll(".diff-list").Count);
        Assert.AreEqual(0, caller.Count);
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("平台*<DB>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Structure_draft_unheld_caller_does_not_write()
    {
        await using var host = new WorkbenchHost();
        var person = await PersonAsync(host, "林可", EntryNames.Analyst);
        var rejected = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure", new { connectionId = "PlatformDB" });
        Assert.AreEqual(403, rejected.Status, rejected.Text);
        Assert.AreEqual("forbidden", rejected.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", rejected.Body.GetProperty("message").GetString());
        Assert.AreEqual(0, host.Annotations.StructureAttempts);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.IsNull(host.Annotations.Draft);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Structure_draft_existing_draft_is_unchanged()
    {
        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var again = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure");
        Assert.AreEqual(200, again.Status, again.Text);
        Assert.AreEqual("kept", again.Body.GetProperty("id").GetString());
        Assert.AreEqual("Kept", again.Body.GetProperty("title").GetString());
        Assert.AreEqual("原說明", again.Body.GetProperty("purpose").GetString());
        Assert.AreEqual("kept", host.Annotations.Draft.Id);
        Assert.AreEqual("原說明", host.Annotations.Draft.Purpose);
        Assert.AreEqual(1, host.Annotations.Commits);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Structure_draft_host_surfaces_connection_gates_without_reading_rows()
    {
        await using var host = new WorkbenchHost();
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        foreach (var (code, message) in new (string, string)[]
        {
            ("no_connection", WorkbenchMessages.NoConnection),
            ("connection_unselectable", WorkbenchMessages.ConnectionUnselectable),
            ("not_platform_source", WorkbenchMessages.NotPlatformSource("legacy")),
        })
        {
            host.Annotations.StructureFailure = AnnotationWriteResult.Fail(code, message);
            var rejected = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure", new { connectionId = "PlatformDB", connectionString = "Data Source=secret" });
            Assert.AreEqual(400, rejected.Status, rejected.Text);
            Assert.AreEqual(code, rejected.Body.GetProperty("code").GetString());
            Assert.AreEqual(message, rejected.Body.GetProperty("message").GetString());
            Assert.IsFalse(rejected.Text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rejected.Text.Contains("Data Source=", StringComparison.Ordinal));
            Assert.IsNull(host.Annotations.Draft);
            Assert.AreEqual(0, host.Caller.Count);
        }

        host.Annotations.StructureFailure = null;
        host.Annotations.StructurePackage = new AnnotationPackageDto { Id = "s", Title = "平台", Purpose = "", Dbml = "", Status = "draft" };
        var saved = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure", new { connectionId = "secret" });
        Assert.AreEqual(200, saved.Status, saved.Text);
        Assert.AreEqual("平台", saved.Body.GetProperty("title").GetString());
        Assert.AreEqual("draft", saved.Body.GetProperty("status").GetString());
        Assert.IsFalse(saved.Body.GetProperty("active").GetBoolean());
        Assert.IsFalse(saved.Text.Contains("secret", StringComparison.Ordinal));
        Assert.IsFalse(saved.Text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Structure_draft_client_treats_not_found_as_failure()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        var client = new AnnotationCatalogClient(http, "http://127.0.0.1:9/", "unit-catalog-key");

        handler.Status = HttpStatusCode.NotFound;
        handler.Body = """{"code":"not_found","message":"找不到。"}""";
        var missing = await client.ReadStructureAsync("方晴", CancellationToken.None);
        Assert.IsFalse(missing.Ok);
        Assert.IsNull(missing.Package);
        Assert.AreEqual("not_found", missing.Code);
        Assert.AreEqual("annotations/read-structure", handler.Path);
        Assert.AreEqual("", handler.RecordedBody);
        Assert.IsNull(handler.ContentHeaders);

        handler.Status = HttpStatusCode.NoContent;
        handler.Body = "";
        Assert.IsNull(await client.DraftAsync("方晴", CancellationToken.None));

        handler.Status = HttpStatusCode.NoContent;
        handler.Body = "";
        var empty = await client.ReadStructureAsync("方晴", CancellationToken.None);
        Assert.IsFalse(empty.Ok);
        Assert.IsNull(empty.Package);

        handler.Status = HttpStatusCode.NoContent;
        handler.Body = "";
        var abandoned = await client.AbandonAsync("方晴", CancellationToken.None);
        Assert.IsTrue(abandoned.Ok, abandoned.Message);
        Assert.IsNull(abandoned.Package);
        Assert.AreEqual(HttpMethod.Delete, handler.Method);
        Assert.AreEqual("annotations/draft", handler.Path);
    }

    [TestMethod]
    public void Reread_abandon_rename_keeps_the_old_note()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-reread-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var active = new DbServices.Mcp.AnnotationPackage
            {
                Id = "live",
                Title = "啟用包",
                Purpose = "啟用說明",
                Status = "confirmed",
                Active = true,
                Dbml = "KEEP",
                Tables =
                [
                    new DbServices.Mcp.AnnotationTable
                    {
                        Name = "T",
                        Note = "啟用表註",
                        Columns = [new DbServices.Mcp.AnnotationColumn { Name = "KEPT", Note = "從啟用抄", Type = "text" }],
                    },
                ],
            };
            var draft = new DbServices.Mcp.AnnotationPackage
            {
                Id = "draft-1",
                Title = "草稿包",
                Purpose = "草稿說明",
                Status = "draft",
                Tables =
                [
                    new DbServices.Mcp.AnnotationTable
                    {
                        Name = "T",
                        Note = "草稿表註",
                        Columns = [new DbServices.Mcp.AnnotationColumn { Name = "OFFICE_CODE", Note = "局號", Pii = "識別", Type = "char(3)" }],
                    },
                ],
            };
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { active, draft }));
            var catalog = new DbServices.Mcp.AnnotationCatalog(path);
            var merged = catalog.Import(new DbServices.Mcp.AnnotationPackage
            {
                Id = "other",
                Title = "別的",
                Purpose = "來源說明",
                Tables =
                [
                    new DbServices.Mcp.AnnotationTable
                    {
                        Name = "T",
                        Note = "來源表註",
                        Columns =
                        [
                            new DbServices.Mcp.AnnotationColumn { Name = "office_code", Note = "來源註", Pii = "來源個資", Type = "varchar(10)" },
                            new DbServices.Mcp.AnnotationColumn { Name = "KEPT", Note = "來源保留", Type = "text" },
                        ],
                    },
                ],
            });
            Assert.IsTrue(merged.Ok, merged.Message);
            Assert.AreEqual("draft-1", merged.Package!.Id);
            Assert.AreEqual("草稿包", merged.Package.Title);
            Assert.AreEqual("草稿說明", merged.Package.Purpose);
            var office = merged.Package.Tables[0].Columns.Single(column => column.Name == "office_code");
            Assert.AreEqual("來源註", office.Note);
            Assert.AreNotEqual("局號", office.Note);
            Assert.AreEqual("從啟用抄", merged.Package.Tables[0].Columns.Single(column => column.Name == "KEPT").Note);
            Assert.AreEqual("草稿表註", merged.Package.Tables[0].Note);
            Assert.IsTrue(merged.Package.Changes.Any(change => change.Kind == "removed" && change.Column == "OFFICE_CODE"));
            Assert.IsTrue(merged.Package.Changes.Any(change => change.Kind == "added" && change.Column == "office_code"));
            var live = catalog.Active().Package!;
            Assert.AreEqual("KEEP", live.Dbml);
            Assert.AreEqual("啟用說明", live.Purpose);
            Assert.IsTrue(live.Active);
            Assert.AreEqual("confirmed", live.Status);
            Assert.AreEqual("從啟用抄", live.Tables[0].Columns[0].Note);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Reread_abandon_failure_does_not_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-reread-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            File.WriteAllText(path, """[{"id":"draft-1","title":"草稿包","purpose":"草稿說明","status":"draft","active":false,"tables":[],"links":[],"dbml":""}]""");
            var bytes = await File.ReadAllBytesAsync(path);
            var catalog = new DbServices.Mcp.AnnotationCatalog(path);
            var refused = catalog.ReadStructure(new DbServices.Mcp.ToolCatalog(new Dictionary<string, DbServices.Mcp.ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = "   " },
            }), "PlatformDB");
            Assert.IsFalse(refused.Ok);
            Assert.AreEqual("connection_failed", refused.Code);
            Assert.AreEqual("無法建立連線。", refused.Message);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        host.Annotations.Active = new AnnotationPackageDto { Id = "live", Title = "啟用包", Status = "confirmed", Active = true, Purpose = "原說明", Dbml = "KEEP" };
        host.Annotations.StructureFailure = AnnotationWriteResult.Fail("no_connection", WorkbenchMessages.NoConnection);
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var rejected = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure");
        Assert.AreEqual(400, rejected.Status, rejected.Text);
        Assert.AreEqual("no_connection", rejected.Body.GetProperty("code").GetString());
        Assert.AreEqual("kept", host.Annotations.Draft.Id);
        Assert.AreEqual("原說明", host.Annotations.Draft.Purpose);
        Assert.AreEqual("KEEP", host.Annotations.Active.Dbml);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Reread_abandon_shows_diff_and_drops_only_the_draft()
    {
        var caller = new RecordingCaller();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var annotations = new RecordingAnnotations
        {
            ReadGate = release.Task,
            StructurePackage = new AnnotationPackageDto { Id = "incoming", Title = "別的", Purpose = "來源說明" },
            Draft = new AnnotationPackageDto
            {
                Id = "draft-1",
                Title = "草稿包",
                Purpose = "草稿說明",
                Status = "draft",
                Tables = [new AnnotationTableDto { Name = "T", Columns = [new AnnotationColumnDto { Name = "A", Type = "text" }] }],
                Changes =
                [
                    new AnnotationChangeDto { Kind = "added", Table = "NEW", Column = "A" },
                    new AnnotationChangeDto { Kind = "removed", Table = "OLD", Column = "B" },
                    new AnnotationChangeDto { Kind = "changed", Table = "T", Column = "A" },
                ],
            },
            Active = new AnnotationPackageDto
            {
                Id = "live",
                Title = "啟用包",
                Purpose = "原說明",
                Status = "confirmed",
                Active = true,
                Dbml = "KEEP",
                Tables = [new AnnotationTableDto { Name = "T", Note = "原表", Columns = [new AnnotationColumnDto { Name = "A", Type = "text", Note = "原欄" }] }],
            },
        };
        await using (var context = Station(caller, annotations))
        {
            var cut = context.Render<Workbench.Web.Components.Governance>();
            cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".diff-list").Count));
            var diff = cut.Find(".diff-list");
            Assert.AreEqual("0", diff.GetAttribute("tabindex"));
            var added = diff.QuerySelector(".added")!;
            var removed = diff.QuerySelector(".removed")!;
            var changed = diff.QuerySelector(".changed")!;
            Assert.IsTrue(added.TextContent.Contains("新增", StringComparison.Ordinal));
            Assert.IsTrue(added.TextContent.Contains("NEW", StringComparison.Ordinal));
            Assert.IsTrue(removed.TextContent.Contains("刪除", StringComparison.Ordinal));
            Assert.IsTrue(removed.TextContent.Contains("OLD", StringComparison.Ordinal));
            Assert.IsFalse(removed.InnerHtml.Contains("line-through", StringComparison.Ordinal));
            Assert.IsFalse(removed.InnerHtml.Contains("<s>", StringComparison.Ordinal) || removed.InnerHtml.Contains("<s ", StringComparison.Ordinal) || removed.InnerHtml.Contains("<del", StringComparison.Ordinal) || removed.InnerHtml.Contains("<strike", StringComparison.Ordinal));
            Assert.IsTrue(changed.TextContent.Contains("已改變", StringComparison.Ordinal));
            var markup = cut.Markup;
            Assert.IsTrue(markup.IndexOf("link-editor", StringComparison.Ordinal) < markup.IndexOf("diff-list", StringComparison.Ordinal));
            Assert.AreEqual(0, cut.FindAll(".dbml-block").Count);

            var css = File.ReadAllText(FindUp("Workbench.Web", "wwwroot", "tokens.css"));
            var rule = css.Split(".diff-list", 2)[1].Split(".dbml-block", 2)[0];
            Assert.IsTrue(rule.Contains("var(--surface-raised)", StringComparison.Ordinal));
            Assert.IsTrue(rule.Contains("var(--border-hairline)", StringComparison.Ordinal));
            Assert.IsTrue(rule.Contains("var(--action-soft)", StringComparison.Ordinal));
            Assert.IsTrue(rule.Contains("var(--danger)", StringComparison.Ordinal));
            Assert.IsTrue(rule.Contains("var(--caution)", StringComparison.Ordinal));
            Assert.IsFalse(rule.Contains("line-through", StringComparison.Ordinal));

            var click = cut.Find("#annotation-read").ClickAsync(new MouseEventArgs());
            cut.WaitForAssertion(() =>
            {
                Assert.AreEqual(WorkbenchMessages.ReadingStructure, cut.Find(".progress-line").TextContent.Trim());
                Assert.IsTrue(cut.Find("#annotation-abandon").HasAttribute("disabled"));
                Assert.IsTrue(cut.Find("#annotation-read").HasAttribute("disabled"));
                Assert.IsTrue(cut.Find("#annotation-import").HasAttribute("disabled"));
                Assert.IsTrue(cut.Find("#annotation-confirm").HasAttribute("disabled"));
            });
            release.TrySetResult();
            await click;
            cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.Commits));
            Assert.AreEqual("draft-1", annotations.Draft!.Id);
            Assert.AreEqual("草稿包", annotations.Draft.Title);
            Assert.AreEqual(0, caller.Count);

            await cut.Find("#annotation-abandon").ClickAsync(new MouseEventArgs());
            Assert.AreEqual("放棄這份草稿？已確認的包維持原樣。", cut.Find(".dialog p").TextContent.Trim());
            await cut.InvokeAsync(() => DialogHost.Escape());
            cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".dialog").Count));
            Assert.AreEqual(0, annotations.AbandonAttempts);
            Assert.AreEqual("draft-1", annotations.Draft.Id);

            await cut.Find("#annotation-abandon").ClickAsync(new MouseEventArgs());
            await cut.Find(".dialog .secondary").ClickAsync(new MouseEventArgs());
            cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".dialog").Count));
            Assert.AreEqual("draft-1", annotations.Draft.Id);
            Assert.AreEqual("原說明", annotations.Active!.Purpose);

            await cut.Find("#annotation-abandon").ClickAsync(new MouseEventArgs());
            await cut.Find(".dialog .confirm").ClickAsync(new MouseEventArgs());
            cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".dialog").Count));
            Assert.IsNull(annotations.Draft);
            Assert.AreEqual("KEEP", annotations.Active.Dbml);
            Assert.AreEqual("原說明", annotations.Active.Purpose);
            Assert.IsTrue(annotations.Active.Active);
            cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("啟用包", StringComparison.Ordinal)));
            Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("已確認", StringComparison.Ordinal));
            Assert.AreEqual(0, cut.FindAll(".diff-list").Count);
            Assert.AreEqual(0, cut.FindAll("#annotation-abandon").Count);
            Assert.AreEqual(0, caller.Count);
        }

        var cold = new RecordingAnnotations
        {
            Draft = new AnnotationPackageDto
            {
                Id = "only",
                Title = "僅草稿",
                Purpose = "說明",
                Status = "draft",
                Tables = [new AnnotationTableDto { Name = "T", Columns = [new AnnotationColumnDto { Name = "A", Type = "text" }] }],
            },
        };
        await using var coldContext = Station(caller, cold);
        var plain = coldContext.Render<Workbench.Web.Components.Governance>();
        plain.WaitForAssertion(() => Assert.AreEqual(0, plain.FindAll(".diff-list").Count));
        await plain.Find("#annotation-abandon").ClickAsync(new MouseEventArgs());
        await plain.Find(".dialog .confirm").ClickAsync(new MouseEventArgs());
        plain.WaitForAssertion(() => Assert.AreEqual(0, plain.FindAll(".package-bar .code").Count));
        Assert.IsNull(cold.Draft);
        Assert.AreEqual(0, plain.FindAll(".table-list").Count);
        Assert.AreEqual(0, caller.Count);
    }

    [TestMethod]
    public async Task Reread_abandon_unheld_does_not_write()
    {
        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        host.Annotations.Active = new AnnotationPackageDto { Id = "live", Title = "啟用包", Status = "confirmed", Active = true, Purpose = "原說明", Dbml = "KEEP" };
        var person = await PersonAsync(host, "林可", EntryNames.Analyst);
        var read = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure");
        Assert.AreEqual(403, read.Status, read.Text);
        Assert.AreEqual("forbidden", read.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", read.Body.GetProperty("message").GetString());
        var abandoned = await person.RawAsync(HttpMethod.Delete, "api/annotations/draft");
        Assert.AreEqual(403, abandoned.Status, abandoned.Text);
        Assert.AreEqual("forbidden", abandoned.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", abandoned.Body.GetProperty("message").GetString());
        Assert.AreEqual("kept", host.Annotations.Draft.Id);
        Assert.AreEqual("原說明", host.Annotations.Draft.Purpose);
        Assert.AreEqual("KEEP", host.Annotations.Active.Dbml);
        Assert.IsTrue(host.Annotations.Active.Active);
        Assert.AreEqual(0, host.Annotations.StructureAttempts);
        Assert.AreEqual(0, host.Annotations.AbandonAttempts);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Reread_abandon_delete_leaves_the_active_package()
    {
        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        host.Annotations.Active = new AnnotationPackageDto { Id = "live", Title = "啟用包", Status = "confirmed", Active = true, Purpose = "原說明", Dbml = "KEEP" };
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var abandoned = await person.RawAsync(HttpMethod.Delete, "api/annotations/draft");
        Assert.AreEqual(204, abandoned.Status, abandoned.Text);
        Assert.IsNull(host.Annotations.Draft);
        Assert.AreEqual("live", host.Annotations.Active!.Id);
        Assert.AreEqual("KEEP", host.Annotations.Active.Dbml);
        Assert.IsTrue(host.Annotations.Active.Active);
        Assert.AreEqual("原說明", host.Annotations.Active.Purpose);
    }

    private static AnnotationPackageDto SamplePackage() => new()
    {
        Id = "pkg",
        Title = "平台*<DB>",
        Purpose = "",
        Dbml = "",
        Status = "draft",
        Active = false,
        Tables =
        [
            new AnnotationTableDto
            {
                Name = "parent",
                Note = "",
                Columns =
                [
                    new AnnotationColumnDto { Name = "Id", Type = "INTEGER", PrimaryKey = true },
                    new AnnotationColumnDto { Name = "Name", Type = "TEXT", Note = "已寫" },
                ],
            },
        ],
        Links =
        [
            new AnnotationLinkDto
            {
                From = "child",
                FromColumn = "ParentId",
                To = "parent",
                ToColumn = "Id",
                Origin = "foreign-key",
            },
        ],
    };

    private static void AssertAbsentActions(IEnumerable<IElement> buttons)
    {
        foreach (var button in buttons)
        {
            var text = button.TextContent.Trim();
            Assert.AreNotEqual("放棄", text);
        }
    }

    private static async Task<Session> PersonAsync(WorkbenchHost host, string name, string entry)
    {
        var admin = await host.LoginAsync();
        var created = await admin.RawAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest
        {
            DisplayName = name,
            Secret = "person-secret",
        });
        Assert.AreEqual(200, created.Status, created.Text);
        var id = created.Body.GetProperty("id").GetString();
        var assigned = await admin.RawAsync(HttpMethod.Put, "api/operators/" + id + "/entries", new EntryAssignmentRequest
        {
            Entries = [entry],
        });
        Assert.AreEqual(200, assigned.Status, assigned.Text);
        return await host.LoginAsync(name, "person-secret");
    }

    private static BunitContext Station(RecordingCaller caller, RecordingAnnotations annotations)
    {
        var store = new Operators();
        store.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴" });
        store.ReplaceEntries("fang", [EntryNames.Governance]);
        var context = new BunitContext();
        var sessions = new MemorySessionStore();
        var hasher = new Pbkdf2PasswordHasher();
        var profiles = new Profiles(store);
        var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
        var circuit = new CircuitSession();
        circuit.Adopt(sessions.Create("fang"));
        context.Services.AddSingleton(circuit);
        context.Services.AddSingleton(sessionService);
        context.Services.AddSingleton(new OperatorGate(circuit, sessionService, profiles));
        context.Services.AddSingleton<IAnnotationCatalog>(annotations);
        context.Services.AddSingleton<IViewCaller>(caller);
        return context;
    }

    private static string FindUp(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed class Operators : IOperatorStore
    {
        private readonly List<OperatorRecord> _rows = [];
        private readonly Dictionary<string, List<string>> _entries = [];

        public int Count() => _rows.Count;
        public void Add(OperatorRecord record) => _rows.Add(record);
        public OperatorRecord? FindByName(string name) => _rows.FirstOrDefault(row => row.DisplayName == name);
        public OperatorRecord? FindById(string id) => _rows.FirstOrDefault(row => row.Id == id);
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) => _entries[operatorId] = entries.ToList();
        public IReadOnlyList<string> EntriesFor(string operatorId) => _entries.TryGetValue(operatorId, out var found) ? found : [];
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; }
        public string Body { get; set; } = "";
        public HttpMethod Method { get; private set; } = HttpMethod.Get;
        public string Path { get; private set; } = "";
        public string RecordedBody { get; private set; } = "";
        public HttpContentHeaders? ContentHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
            ContentHeaders = request.Content?.Headers;
            RecordedBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }
}
