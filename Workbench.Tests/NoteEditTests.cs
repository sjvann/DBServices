using System.Net;
using System.Net.Http;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class NoteEditTests
{
    [TestMethod]
    public async Task Note_edit_switch_table_keeps_unsaved_text()
    {
        var caller = new RecordingCaller();
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(caller, annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal)));
        var order = Focusables(cut.Find(".page"));
        int At(string marker) => order.FindIndex(element =>
            element.Id == marker
            || element.GetAttribute("aria-label") == marker
            || element.TextContent.Trim() == marker);
        Assert.IsTrue(At("annotation-import") < At("annotation-read"));
        Assert.IsTrue(At("annotation-read") < At("annotation-save"));
        Assert.IsTrue(At("annotation-save") < At("整包功能說明"));
        Assert.IsTrue(At("整包功能說明") < At("第一"));
        Assert.IsTrue(At("第一") < At("表註"));
        Assert.IsTrue(At("表註") < At("甲 欄註"));
        Assert.IsTrue(At("甲 欄註") < At("關聯起表"));

        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "說明還在" });
        await cut.Find("[aria-label='表註']").InputAsync(new ChangeEventArgs { Value = "第一表註" });
        await cut.Find("[aria-label='甲 欄註']").InputAsync(new ChangeEventArgs { Value = "甲的欄註" });
        await cut.FindAll(".table-list button").Single(button => button.TextContent.Contains("第二", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.AreEqual("第二表原註", Field(cut.Find("[aria-label='表註']"))));
        Assert.AreEqual("丙註", Field(cut.Find("[aria-label='丙 欄註']")));
        Assert.AreEqual(0, cut.FindAll("[aria-label='甲 欄註']").Count);
        await cut.FindAll(".table-list button").Single(button => button.TextContent.Contains("第一", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        Assert.AreEqual("說明還在", Field(cut.Find("[aria-label='整包功能說明']")));
        Assert.AreEqual("第一表註", Field(cut.Find("[aria-label='表註']")));
        Assert.AreEqual("甲的欄註", Field(cut.Find("[aria-label='甲 欄註']")));
        Assert.AreEqual(0, annotations.SaveAttempts);
        Assert.AreEqual(0, annotations.Commits);
        Assert.AreEqual(0, caller.Count);
    }

    [TestMethod]
    public async Task Note_edit_empty_station_names_the_next_step()
    {
        await using var context = Station(new RecordingCaller(), new RecordingAnnotations());
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".lead").TextContent.Contains("還沒有註記包", StringComparison.Ordinal)));
        Assert.AreEqual("註記", cut.Find("h1").TextContent.Trim());
        Assert.AreEqual(1, cut.FindAll("input").Count);
        Assert.AreEqual("file", cut.Find("input").GetAttribute("type"));
        Assert.AreEqual(0, cut.FindAll("select, textarea, table").Count);
    }

    [TestMethod]
    public async Task Note_edit_blank_purpose_is_named_beside_confirm()
    {
        await using var context = Station(new RecordingCaller(), new RecordingAnnotations { Draft = Sample() });
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("整包功能說明還是空白", cut.Find("#confirm-hint").TextContent.Trim()));
        Assert.IsFalse(cut.Find("#annotation-confirm").HasAttribute("disabled"));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "有說明" });
        Assert.AreEqual(0, cut.FindAll("#confirm-hint").Count);
    }

    [TestMethod]
    public async Task Note_edit_next_blank_selects_the_table()
    {
        await using var context = Station(new RecordingCaller(), new RecordingAnnotations { Draft = Sample() });
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsNotNull(cut.Find("#annotation-next-blank")));
        await cut.FindAll(".table-list button").Single(button => button.TextContent.Contains("第二", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.Find("#annotation-next-blank").ClickAsync(new MouseEventArgs());
        var first = cut.FindAll(".table-list button").Single(button => button.TextContent.Contains("第一", StringComparison.Ordinal));
        Assert.AreEqual("true", first.GetAttribute("aria-current"));
        Assert.AreEqual(1, cut.FindAll("[aria-label='甲 欄註']").Count);
        Assert.AreEqual("true", cut.FindAll("button").Single(button => button.TextContent.Trim() == "尚未註記").GetAttribute("aria-pressed"));
    }

    [TestMethod]
    public async Task Note_edit_index_note_round_trips()
    {
        var package = Sample();
        package.Tables[0].Indexes.Add(new AnnotationIndexDto { Name = "ix_append", Note = "不是真正的唯一鍵", Columns = ["甲"] });
        var annotations = new RecordingAnnotations { Draft = package };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("不是真正的唯一鍵", Field(cut.Find("[aria-label='ix_append 索引說明']"))));
        await cut.Find("[aria-label='ix_append 索引說明']").InputAsync(new ChangeEventArgs { Value = "異動為 append" });
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.Commits));
        Assert.AreEqual("異動為 append", annotations.Draft!.Tables[0].Indexes[0].Note);
    }

    [TestMethod]
    public async Task Note_edit_link_shows_origin_in_words()
    {
        await using var context = Station(new RecordingCaller(), new RecordingAnnotations { Draft = Sample() });
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".link-editor").TextContent.Contains("外鍵", StringComparison.Ordinal)));
        Assert.IsFalse(cut.Markup.Contains("foreign-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Note_edit_reveals_dictionary_sides_when_names_differ()
    {
        var package = Sample();
        package.Tables[0].Columns[0].HName = "POST_BRH_NO";
        package.Tables[0].Columns[0].HType = "char(6)";
        var annotations = new RecordingAnnotations { Draft = package };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find("[data-column='甲'] details").HasAttribute("open")));
        Assert.IsTrue(cut.Find("[data-column='甲']").TextContent.Contains("POST_BRH_NO", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("sourceFile", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Note_edit_diff_jump_selects_the_column()
    {
        var package = Sample();
        package.Changes.Add(new AnnotationChangeDto { Kind = "changed", Table = "第二", Column = "丙" });
        await using var context = Station(new RecordingCaller(), new RecordingAnnotations { Draft = package });
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsNotNull(cut.Find(".diff-list .changed button")));
        await cut.Find(".diff-list .changed button").ClickAsync(new MouseEventArgs());
        var second = cut.FindAll(".table-list button").Single(button => button.TextContent.Contains("第二", StringComparison.Ordinal));
        Assert.AreEqual("true", second.GetAttribute("aria-current"));
        Assert.IsTrue(cut.Find("[data-column='丙']").ClassList.Contains("marked"));
    }

    [TestMethod]
    public async Task Note_edit_blank_fields_save_without_danger()
    {
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("原欄註", Field(cut.Find("[aria-label='乙 欄註']"))));

        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "" });
        await cut.Find("[aria-label='表註']").InputAsync(new ChangeEventArgs { Value = "" });
        await cut.Find("[aria-label='乙 欄註']").InputAsync(new ChangeEventArgs { Value = "暫時" });
        Assert.IsFalse(cut.Find("[data-column='乙']").TextContent.Contains("尚未註記", StringComparison.Ordinal));
        await cut.Find("[aria-label='乙 欄註']").InputAsync(new ChangeEventArgs { Value = "   " });

        var purpose = cut.Find("[aria-label='整包功能說明']");
        var tableNote = cut.Find("[aria-label='表註']");
        Assert.IsTrue(purpose.ClassList.Contains("purpose-field"));
        Assert.IsTrue(tableNote.ClassList.Contains("purpose-field"));
        Assert.IsFalse(purpose.ClassList.Contains("unnoted"));
        Assert.IsFalse(tableNote.ClassList.Contains("unnoted"));
        var badge = cut.Find("[data-column='乙'] .unnoted");
        Assert.AreEqual("尚未註記", badge.TextContent.Trim());
        var css = File.ReadAllText(FindUp("Workbench.Web", "wwwroot", "tokens.css"));
        var rule = css.Split(".purpose-field", 2)[1].Split('}', 2)[0];
        Assert.IsTrue(rule.Contains("var(--border-hairline)", StringComparison.Ordinal));
        Assert.IsFalse(rule.Contains("var(--danger)", StringComparison.Ordinal));

        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.Commits));
        Assert.AreEqual(0, cut.FindAll(".status-message").Count);
        Assert.AreEqual("", annotations.Draft!.Purpose);
        Assert.AreEqual("", annotations.Draft.Tables[0].Note);
        Assert.AreEqual("", annotations.Draft.Tables[0].Columns.Single(column => column.Name == "乙").Note);
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal));
        Assert.AreEqual("尚未註記", cut.Find("[data-column='乙'] .unnoted").TextContent.Trim());
        Assert.AreEqual("draft", annotations.Draft.Status);
        Assert.IsFalse(annotations.Draft.Active);
    }

    [TestMethod]
    public async Task Note_edit_pii_text_keeps_unique_mark()
    {
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find("[aria-label='甲 唯一索引']").HasAttribute("disabled")));
        Assert.IsTrue(cut.Find("[aria-label='甲 唯一索引']").HasAttribute("checked"));

        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "這不是真正的唯一鍵" });
        await cut.Find("[aria-label='甲 個資']").InputAsync(new ChangeEventArgs { Value = "身分證字號原樣" });
        Assert.IsTrue(cut.Find("[aria-label='甲 唯一索引']").HasAttribute("checked"));
        Assert.IsFalse(cut.Find("[aria-label='甲 個資']").ClassList.Contains("unnoted"));
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.SaveAttempts));

        var column = annotations.Draft!.Tables[0].Columns.Single(item => item.Name == "甲");
        Assert.AreEqual("身分證字號原樣", column.Pii);
        Assert.IsTrue(column.UniqueIndex);
        Assert.AreEqual("這不是真正的唯一鍵", annotations.Draft.Purpose);
        Assert.IsTrue(cut.Find("[aria-label='甲 唯一索引']").HasAttribute("checked"));
        Assert.IsFalse(cut.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Note_edit_links_can_be_added_and_removed()
    {
        var caller = new RecordingCaller();
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(caller, annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".link-editor").TextContent.Contains("舊理由", StringComparison.Ordinal) || cut.Find(".link-editor").InnerHtml.Contains("舊理由", StringComparison.Ordinal)));
        await cut.Find("[aria-label='新關聯理由']").InputAsync(new ChangeEventArgs { Value = "同向理由" });
        await cut.Find("[aria-label='關聯起表']").ChangeAsync(new ChangeEventArgs { Value = "第一" });
        await cut.Find("[aria-label='關聯起欄']").ChangeAsync(new ChangeEventArgs { Value = "甲" });
        await cut.Find("[aria-label='關聯迄表']").ChangeAsync(new ChangeEventArgs { Value = "第二" });
        await cut.Find("[aria-label='關聯迄欄']").ChangeAsync(new ChangeEventArgs { Value = "丙" });
        await cut.FindAll(".link-editor button").Single(button => button.TextContent.Trim() == "新增").ClickAsync(new MouseEventArgs());
        Assert.AreEqual(2, cut.FindAll(".link-editor > li").Count);
        Assert.AreEqual("同向理由", Field(cut.Find("[aria-label='關聯理由']")));
        Assert.AreEqual("foreign-key", annotations.Draft!.Links[0].Origin);
        await cut.Find("[aria-label='新關聯理由']").InputAsync(new ChangeEventArgs { Value = "對調理由" });
        await cut.Find("[aria-label='關聯起表']").ChangeAsync(new ChangeEventArgs { Value = "第二" });
        await cut.Find("[aria-label='關聯起欄']").ChangeAsync(new ChangeEventArgs { Value = "丙" });
        await cut.Find("[aria-label='關聯迄表']").ChangeAsync(new ChangeEventArgs { Value = "第一" });
        await cut.Find("[aria-label='關聯迄欄']").ChangeAsync(new ChangeEventArgs { Value = "甲" });
        await cut.FindAll(".link-editor button").Single(button => button.TextContent.Trim() == "新增").ClickAsync(new MouseEventArgs());
        Assert.AreEqual(2, cut.FindAll(".link-editor > li").Count);
        Assert.AreEqual("對調理由", Field(cut.Find("[aria-label='關聯理由']")));
        Assert.AreEqual("foreign-key", annotations.Draft.Links[0].Origin);
        Assert.AreEqual(1, annotations.Draft.Links.Count);
        Assert.AreEqual(0, annotations.SaveAttempts);
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual("對調理由", annotations.Draft.Links[0].Note));
        Assert.AreEqual("foreign-key", annotations.Draft.Links[0].Origin);
        Assert.AreEqual(1, annotations.Draft.Links.Count);

        await cut.FindAll(".link-editor button").First(button => button.TextContent.Trim() == "刪除").ClickAsync(new MouseEventArgs());
        Assert.AreEqual(1, cut.FindAll(".link-editor > li").Count);
        Assert.IsFalse(cut.Find(".link-editor").InnerHtml.Contains("舊理由", StringComparison.Ordinal));

        await cut.Find("[aria-label='新關聯理由']").InputAsync(new ChangeEventArgs { Value = "補上的理由" });
        await cut.Find("[aria-label='關聯起表']").ChangeAsync(new ChangeEventArgs { Value = "第一" });
        await cut.Find("[aria-label='關聯起欄']").ChangeAsync(new ChangeEventArgs { Value = "乙" });
        await cut.Find("[aria-label='關聯迄表']").ChangeAsync(new ChangeEventArgs { Value = "第二" });
        await cut.Find("[aria-label='關聯迄欄']").ChangeAsync(new ChangeEventArgs { Value = "丙" });
        await cut.FindAll(".link-editor button").Single(button => button.TextContent.Trim() == "新增").ClickAsync(new MouseEventArgs());
        Assert.AreEqual(2, cut.FindAll(".link-editor > li").Count);

        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(2, annotations.Commits));
        Assert.AreEqual(1, annotations.Draft!.Links.Count);
        Assert.AreEqual("declared", annotations.Draft.Links[0].Origin);
        Assert.AreEqual("補上的理由", annotations.Draft.Links[0].Note);
        Assert.AreEqual("第一", annotations.Draft.Links[0].From);
        Assert.AreEqual("乙", annotations.Draft.Links[0].FromColumn);
        Assert.AreEqual("第二", annotations.Draft.Links[0].To);
        Assert.AreEqual("丙", annotations.Draft.Links[0].ToColumn);
        Assert.AreEqual("pkg", annotations.Draft.Id);
        Assert.AreEqual("draft", annotations.Draft.Status);
        Assert.AreEqual(0, annotations.Attempts);
        Assert.AreEqual(0, caller.Count);
        Assert.IsFalse(cut.Markup.Contains("foreign-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Note_edit_save_failure_restores_screen()
    {
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsNotNull(cut.Find("#annotation-save")));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "一次" });
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual("一次", annotations.Draft!.Purpose));

        annotations.SaveFailure = AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "二次" });
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
        var alert = cut.Find(".status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        Assert.IsTrue(alert.InnerHtml.IndexOf("沒有保存。", StringComparison.Ordinal) < alert.InnerHtml.IndexOf("execution_failed", StringComparison.Ordinal));
        Assert.AreEqual("一次", Field(cut.Find("[aria-label='整包功能說明']")));
        Assert.AreEqual("一次", annotations.Draft.Purpose);
        Assert.AreEqual(0, context.Services.GetRequiredService<IViewCaller>() is RecordingCaller caller ? caller.Count : -1);
    }

    [TestMethod]
    public async Task Note_edit_transport_failure_restores_screen()
    {
        var package = Sample();
        package.Purpose = "已存";
        var annotations = new ThrowingSave { Draft = package };
        await using var context = Station(new RecordingCaller(), annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("已存", Field(cut.Find("[aria-label='整包功能說明']"))));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "改了" });
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
        var alert = cut.Find(".status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        Assert.IsTrue(alert.InnerHtml.IndexOf("沒有保存。", StringComparison.Ordinal) < alert.InnerHtml.IndexOf("execution_failed", StringComparison.Ordinal));
        Assert.AreEqual("已存", Field(cut.Find("[aria-label='整包功能說明']")));
    }

    [TestMethod]
    public async Task Note_edit_reopen_shows_draft_layers()
    {
        var caller = new RecordingCaller();
        var annotations = new RecordingAnnotations { Draft = Sample() };
        await using var context = Station(caller, annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsNotNull(cut.Find("#annotation-save")));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "重開說明" });
        await cut.Find("[aria-label='表註']").InputAsync(new ChangeEventArgs { Value = "表還在" });
        await cut.Find("[aria-label='甲 個資']").InputAsync(new ChangeEventArgs { Value = "個資原文" });
        await cut.Find("[aria-label='乙 欄註']").InputAsync(new ChangeEventArgs { Value = "" });
        await cut.Find("#annotation-save").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.Commits));

        var again = context.Render<Workbench.Web.Components.Governance>();
        again.WaitForAssertion(() => Assert.IsTrue(again.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal)));
        Assert.AreEqual("重開說明", Field(again.Find("[aria-label='整包功能說明']")));
        Assert.AreEqual("表還在", Field(again.Find("[aria-label='表註']")));
        Assert.AreEqual("個資原文", Field(again.Find("[aria-label='甲 個資']")));
        Assert.AreEqual("尚未註記", again.Find("[data-column='乙'] .unnoted").TextContent.Trim());
        Assert.IsTrue(again.Find("[aria-label='甲 唯一索引']").HasAttribute("checked"));
        Assert.IsFalse(again.Markup.Contains("啟用", StringComparison.Ordinal));
        Assert.IsFalse(again.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(again.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.AreEqual("draft", annotations.Draft!.Status);
        Assert.IsFalse(annotations.Draft.Active);
        Assert.AreEqual(0, caller.Count);
        var stored = JsonSerializer.Serialize(annotations.Draft);
        Assert.IsFalse(stored.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(stored.Contains("\"rows\"", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Note_edit_unheld_save_does_not_write()
    {
        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        var before = JsonSerializer.Serialize(host.Annotations.Draft);
        var person = await PersonAsync(host, "林可", EntryNames.Analyst);
        var rejected = await person.RawAsync(HttpMethod.Put, "api/annotations/draft", new AnnotationPackageDto
        {
            Id = "kept",
            Purpose = "改掉",
            Status = "draft",
        });
        Assert.AreEqual(403, rejected.Status, rejected.Text);
        Assert.AreEqual("forbidden", rejected.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", rejected.Body.GetProperty("message").GetString());
        Assert.AreEqual(0, host.Annotations.SaveAttempts);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.AreEqual(before, JsonSerializer.Serialize(host.Annotations.Draft));
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Note_edit_held_save_updates_same_draft()
    {
        await using var host = new WorkbenchHost();
        host.Annotations.Draft = Sample();
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var saved = await person.RawAsync(HttpMethod.Put, "api/annotations/draft", new AnnotationPackageDto
        {
            Id = "pkg",
            Title = "註記包",
            Purpose = "從入口保存",
            Status = "confirmed",
            Active = true,
            Tables = Sample().Tables,
            Links = [],
        });
        Assert.AreEqual(200, saved.Status, saved.Text);
        Assert.AreEqual("draft", saved.Body.GetProperty("status").GetString());
        Assert.IsFalse(saved.Body.GetProperty("active").GetBoolean());
        Assert.AreEqual("從入口保存", host.Annotations.Draft!.Purpose);
        Assert.AreEqual(1, host.Annotations.SaveAttempts);
        Assert.AreEqual(0, host.Annotations.Attempts);
        Assert.AreEqual(1, host.Annotations.Commits);
        Assert.AreEqual("pkg", host.Annotations.Draft.Id);
        Assert.IsFalse(saved.Text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Note_edit_client_saves_with_the_draft_put()
    {
        var handler = new RecordingHandler
        {
            Status = HttpStatusCode.OK,
            Body = """{"id":"pkg","title":"註記包","purpose":"說明","status":"draft","active":false,"tables":[],"links":[],"dbml":""}""",
        };
        using var http = new HttpClient(handler);
        var client = new AnnotationCatalogClient(http, "http://127.0.0.1:9/", "unit-catalog-key");
        var saved = await client.SaveAsync(new AnnotationPackageDto { Id = "pkg", Purpose = "說明" }, "方晴", CancellationToken.None);
        Assert.IsTrue(saved.Ok, saved.Message);
        Assert.AreEqual(HttpMethod.Put, handler.Method);
        Assert.AreEqual("annotations/draft", handler.Path);
        using var document = JsonDocument.Parse(handler.RecordedBody);
        Assert.AreEqual("說明", document.RootElement.GetProperty("purpose").GetString());
        Assert.AreEqual("unit-catalog-key", handler.WorkbenchKey);
    }

    private static AnnotationPackageDto Sample() => new()
    {
        Id = "pkg",
        Title = "註記包",
        Purpose = "",
        Status = "draft",
        Active = false,
        Dbml = "",
        Tables =
        [
            new AnnotationTableDto
            {
                Name = "第一",
                Note = "",
                Columns =
                [
                    new AnnotationColumnDto { Name = "甲", Type = "text", Note = "", UniqueIndex = true, PrimaryKey = true },
                    new AnnotationColumnDto { Name = "乙", Type = "text", Note = "原欄註" },
                ],
            },
            new AnnotationTableDto
            {
                Name = "第二",
                Note = "第二表原註",
                Columns = [new AnnotationColumnDto { Name = "丙", Type = "int", Note = "丙註" }],
            },
        ],
        Links =
        [
            new AnnotationLinkDto
            {
                From = "第一",
                FromColumn = "甲",
                To = "第二",
                ToColumn = "丙",
                Origin = "foreign-key",
                Note = "舊理由",
            },
        ],
    };

    private static string Field(IElement element)
    {
        var text = element.TextContent.Trim();
        if (text.Length > 0)
            return text;
        return element.GetAttribute("value") ?? "";
    }

    private static List<IElement> Focusables(IElement root)
    {
        var list = new List<IElement>();
        void Walk(IElement node)
        {
            if (node.LocalName is "button" or "input" or "select" or "textarea" or "a")
                list.Add(node);
            foreach (var child in node.Children)
                Walk(child);
        }

        Walk(root);
        return list;
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

    private static BunitContext Station(RecordingCaller caller, IAnnotationCatalog annotations)
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

    private sealed class ThrowingSave : IAnnotationCatalog
    {
        public AnnotationPackageDto? Draft { get; set; }

        public Task<AnnotationPackageDto?> DraftAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult(Draft);

        public Task<AnnotationActiveDto> ActiveAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult(new AnnotationActiveDto());

        public Task<string?> DraftDbmlAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<AnnotationWriteResult> ConfirmAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult(AnnotationWriteResult.Fail("execution_failed", "沒有保存。"));

        public Task<AnnotationWriteResult> ImportAsync(AnnotationPackageDto package, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(AnnotationWriteResult.Fail("execution_failed", "沒有保存。"));

        public Task<AnnotationWriteResult> SaveAsync(AnnotationPackageDto package, string callerId, CancellationToken cancellationToken)
            => throw new HttpRequestException("down");

        public Task<AnnotationWriteResult> ReadStructureAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult(AnnotationWriteResult.Fail("execution_failed", "沒有保存。"));

        public Task<AnnotationWriteResult> AbandonAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult(AnnotationWriteResult.Fail("execution_failed", "沒有保存。"));
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
        public HttpMethod? Method { get; private set; }
        public string Path { get; private set; } = "";
        public string RecordedBody { get; private set; } = "";
        public string? WorkbenchKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
            WorkbenchKey = request.Headers.TryGetValues("X-DBServices-Workbench", out var values) ? values.Single() : null;
            RecordedBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }
}
