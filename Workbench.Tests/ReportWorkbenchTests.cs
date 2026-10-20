using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UglyToad.PdfPig;
using Workbench.Avalonia;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class ReportWorkbenchTests
{
    private const string Password = "seed-secret";
    private const string LinSecret = "lin-secret";
    private const string ZhouSecret = "zhou-secret";
    private const string ChenSecret = "chen-secret";
    private const string HeSecret = "he-secret";
    private static readonly string[] TableTools = ["tables", "fields", "get", "query", "count", "exists"];

    [TestMethod]
    public async Task Seed_admin_is_one_hashed_operator_and_restart_does_not_reread()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string verifier;
            await using (var first = new WorkbenchHost(options =>
            {
                options.ContentRoot = root;
                options.PreserveRoot = true;
            }))
            {
                var admin = await first.LoginAsync();
                Assert.IsTrue(admin.Me.IsAdmin);
                verifier = ReadVerifier(first.SqlitePath);
                Assert.AreEqual(1, CountOperators(first.SqlitePath));
                Assert.IsTrue(verifier.StartsWith("pbkdf2$", StringComparison.Ordinal));
                Assert.AreNotEqual(Password, verifier);
                AssertNoSecrets(ReadShared(first.SqlitePath), Password, "Data Source=", "Password=");
            }

            await using var second = new WorkbenchHost(options =>
            {
                options.ContentRoot = root;
                options.PreserveRoot = true;
                options.AdminPassword = "other-secret";
            });
            Assert.AreEqual(verifier, ReadVerifier(second.SqlitePath));
            Assert.AreEqual(1, CountOperators(second.SqlitePath));
            var again = await second.LoginAsync("管理者", Password);
            Assert.IsTrue(again.Me.IsAdmin);
            var rejected = await second.Open().PostAsJsonAsync("api/sessions", new LoginRequest { DisplayName = "管理者", Secret = "other-secret" });
            Assert.AreEqual(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Missing_password_does_not_offer_an_entry()
    {
        await using var host = new WorkbenchHost(options => options.SeedAdmin = false);
        using var client = host.Open();
        var response = await client.PostAsJsonAsync("api/sessions", new LoginRequest { DisplayName = "管理者", Secret = Password });
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("workbench_unavailable", body);
        AssertNoSecrets(body, Password);
    }

    [TestMethod]
    public async Task Anonymous_requests_return_unauthenticated_and_do_not_call()
    {
        await using var host = new WorkbenchHost();
        using var client = host.Open();
        foreach (var url in new[] { "api/views", "api/styles", "api/records", "api/reports/current" })
        {
            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode, url);
            Assert.Contains("unauthenticated", body);
            AssertNoSecrets(body, Password);
        }

        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Entry_assignment_routes_one_station_or_the_switch_page_and_rejects_the_rest()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var lin = await CreateAsync(admin, "林可", LinSecret, EntryNames.Analyst);
        var both = await CreateAsync(admin, "陳安", ChenSecret, EntryNames.Analyst, EntryNames.Terminal);
        var none = await CreateAsync(admin, "周寧", ZhouSecret);

        var analyst = await host.LoginAsync("林可", LinSecret);
        Assert.AreEqual("/views", analyst.Me.Destination);
        Assert.AreEqual(1, analyst.Me.Entries.Count);

        var multi = await host.LoginAsync("陳安", ChenSecret);
        Assert.AreEqual("/stations", multi.Me.Destination);
        Assert.AreEqual(2, multi.Me.Entries.Count);

        var (status, text, _) = await none.RawAsync(HttpMethod.Get, "api/views");
        Assert.AreEqual(403, status);
        Assert.Contains("forbidden", text);
        Assert.AreEqual(0, host.Caller.Count);
        Assert.IsFalse(none.Me.IsAdmin);
        _ = lin;
    }

    [TestMethod]
    public async Task Second_admin_is_rejected_and_data_stays()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var before = CountOperators(host.SqlitePath);
        var (status, text, _) = await admin.RawAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest
        {
            DisplayName = "另一個管理者",
            Secret = "another-secret",
            IsAdmin = true,
        });
        Assert.AreEqual(403, status);
        Assert.Contains("不能建立另一個管理者", text);
        Assert.AreEqual(before, CountOperators(host.SqlitePath));
        Assert.AreEqual(0, CountNamed(host.SqlitePath, "另一個管理者"));

        var created = await CreateAsync(admin, "林可", LinSecret);
        var (promote, _, _) = await admin.RawAsync(HttpMethod.Put, "api/operators/" + created.Me.Id, new OperatorWriteRequest
        {
            DisplayName = "林可",
            IsAdmin = true,
        });
        Assert.AreEqual(403, promote);
        Assert.IsFalse(ReadAdmin(host.SqlitePath, "林可"));
    }

    [TestMethod]
    public async Task Analyst_saves_a_connected_view_and_rejects_a_broken_or_duplicate_one()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        await CreateAsync(admin, "林可", LinSecret, EntryNames.Analyst);
        var lin = await host.LoginAsync("林可", LinSecret);
        var saved = await SaveViewAsync(lin, View("ship-1", "出貨", "說明出貨"));
        Assert.AreEqual(200, saved.Status, saved.Text);
        Assert.IsFalse(saved.Text.Contains("ROWMARKER", StringComparison.Ordinal));
        AssertNoSecrets(saved.Text, Password, "Data Source=", "Password=");
        Assert.AreEqual("ship-1", host.Catalog.Stored("ship-1")!.Id);

        var linked = View("ship-2", "兩表", "關聯");
        linked.Sources = ["orders", "customers"];
        linked.Links = [new ViewLinkDto { Kind = "foreign-key", From = "orders", FromColumn = "customerId", To = "customers", ToColumn = "id" }];
        var linkSaved = await SaveViewAsync(lin, linked);
        Assert.AreEqual(200, linkSaved.Status, linkSaved.Text);

        var broken = View("ship-3", "斷開", "斷");
        broken.Sources = ["orders", "customers"];
        var before = host.Catalog.Stored("ship-1")!.Purpose;
        var rejected = await SaveViewAsync(lin, broken);
        Assert.AreEqual(400, rejected.Status);
        Assert.Contains("來源連不起來", rejected.Text);
        Assert.IsNull(host.Catalog.Stored("ship-3"));
        Assert.AreEqual(before, host.Catalog.Stored("ship-1")!.Purpose);

        var duplicate = View("ship-4", "出貨", "另一個");
        var clash = await SaveViewAsync(lin, duplicate);
        Assert.AreEqual(400, clash.Status);
        Assert.Contains("名稱已存在", clash.Text);
        Assert.IsNull(host.Catalog.Stored("ship-4"));
        Assert.AreEqual("說明出貨", host.Catalog.Stored("ship-1")!.Purpose);
    }

    [TestMethod]
    public async Task Publish_sets_discovery_and_a_referenced_view_stays_frozen_except_purpose()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "舊目的"));
        var published = await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        Assert.AreEqual(200, published.Status, published.Text);
        CollectionAssert.AreEquivalent(new[] { "出貨" }, host.Catalog.PublishedNames().ToArray());
        foreach (var tool in TableTools)
            Assert.IsFalse(host.Catalog.PublishedNames().Contains(tool, StringComparer.Ordinal));

        var style = await AcceptAsync(world.Zhou, "ship-1", "出貨樣式", shared: true);
        Assert.AreEqual(200, style.Status, style.Text);

        var deleted = await world.Lin.RawAsync(HttpMethod.Delete, "api/views/ship-1");
        Assert.AreEqual(403, deleted.Status);
        Assert.IsNotNull(host.Catalog.Stored("ship-1"));

        var unpublished = await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/unpublish");
        Assert.AreEqual(403, unpublished.Status);
        Assert.IsTrue(host.Catalog.Stored("ship-1")!.Published);

        var changed = View("ship-1", "出貨", "舊目的");
        changed.Sources = ["other"];
        changed.Published = true;
        var structure = await SaveViewAsync(world.Lin, changed);
        Assert.AreEqual(403, structure.Status);
        CollectionAssert.AreEqual(new[] { "orders" }, host.Catalog.Stored("ship-1")!.Sources.ToArray());

        var purpose = View("ship-1", "出貨", "新目的");
        purpose.Published = true;
        var retitled = await SaveViewAsync(world.Lin, purpose);
        Assert.AreEqual(200, retitled.Status, retitled.Text);
        Assert.AreEqual("新目的", host.Catalog.Stored("ship-1")!.Purpose);
        CollectionAssert.AreEqual(new[] { "orders" }, host.Catalog.Stored("ship-1")!.Sources.ToArray());
    }

    [TestMethod]
    public async Task Designer_accepts_four_blocks_or_leaves_them_when_ignored_or_unknown()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        var suggested = await world.Zhou.RawAsync(HttpMethod.Post, "api/styles/suggest", new SuggestRequest { ViewId = "ship-1" });
        Assert.AreEqual(200, suggested.Status, suggested.Text);
        var blocks = suggested.Body.EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[] { "title", "text", "parameter", "table" }, blocks.Select(block => block.GetProperty("type").GetString()).ToArray());
        Assert.AreEqual(0, blocks.Min(block => block.GetProperty("order").GetInt32()));
        Assert.IsFalse(suggested.Text.Contains("ROWMARKER", StringComparison.Ordinal));

        var accepted = await AcceptAsync(world.Zhou, "ship-1", "出貨樣式", shared: false, blocks: Blocks(suggested.Body));
        Assert.AreEqual(200, accepted.Status, accepted.Text);
        Assert.AreEqual("ship-1", accepted.Body.GetProperty("viewId").GetString());
        var styleJson = ReadStyles(host.SqlitePath);
        Assert.IsFalse(styleJson.Contains("ROWMARKER", StringComparison.Ordinal));
        AssertNoSecrets(styleJson, Password, "Data Source=", "Password=", "完成");

        var count = CountStyles(host.SqlitePath);
        var unknown = Blocks(suggested.Body);
        unknown.Last(block => block.Type == "table").Columns.Add("不存在");
        var rejected = await AcceptAsync(world.Zhou, "ship-1", "壞樣式", shared: true, blocks: unknown);
        Assert.AreEqual(403, rejected.Status);
        Assert.Contains("未知欄", rejected.Text);
        Assert.AreEqual(count, CountStyles(host.SqlitePath));

        var before = ReadStyles(host.SqlitePath);
        var ignored = await world.Zhou.RawAsync(HttpMethod.Post, "api/styles/ignore", new IgnoreStyleRequest
        {
            StyleId = accepted.Body.GetProperty("id").GetString(),
        });
        Assert.AreEqual(200, ignored.Status, ignored.Text);
        Assert.AreEqual(before, ReadStyles(host.SqlitePath));
    }

    [TestMethod]
    public async Task Utterance_not_ready_reads_nothing_and_an_unreadable_style_is_rejected()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        var style = await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);
        var other = await AcceptAsync(world.Zhou, "ship-1", "庫存", shared: true);
        var styleId = style.Body.GetProperty("id").GetString();
        Assert.AreEqual(200, other.Status, other.Text);

        var missing = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "沒有這張" });
        Assert.AreEqual(200, missing.Status);
        Assert.AreEqual("語句對不到樣式。", missing.Body.GetProperty("reason").GetString());
        Assert.AreEqual(0, missing.Body.GetProperty("readCount").GetInt32());

        var many = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 庫存" });
        Assert.AreEqual("語句對到多筆樣式。", many.Body.GetProperty("reason").GetString());
        Assert.AreEqual(0, many.Body.GetProperty("readCount").GetInt32());

        var incomplete = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨" });
        Assert.AreEqual(0, incomplete.Body.GetProperty("readCount").GetInt32());
        Assert.Contains("缺少參數", incomplete.Body.GetProperty("reason").GetString() ?? "");
        Assert.AreEqual(0, host.Caller.Count);

        var hidden = await AcceptAsync(world.Zhou, "ship-1", "私有出貨", shared: false);
        var denied = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest
        {
            Text = "私有出貨 狀態=完成",
            StyleId = hidden.Body.GetProperty("id").GetString(),
        });
        Assert.AreEqual(403, denied.Status);
        Assert.AreEqual(0, host.Caller.Count);
        _ = styleId;
    }

    [TestMethod]
    public async Task Ready_utterance_calls_the_view_once_and_zero_rows_still_succeed()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);

        var ready = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        Assert.AreEqual(200, ready.Status, ready.Text);
        Assert.AreEqual("ready", ready.Body.GetProperty("status").GetString());
        Assert.AreEqual(1, ready.Body.GetProperty("readCount").GetInt32());
        Assert.AreEqual(1, host.Caller.Count);
        Assert.AreEqual("出貨", host.Caller.ToolNames[0]);
        Assert.IsFalse(host.Caller.Parameters[0].ContainsKey("filters"));
        Assert.AreEqual("完成", host.Caller.Parameters[0]["狀態"]);
        Assert.AreEqual("ROWMARKER", ready.Body.GetProperty("rows")[0][0].GetString());
        Assert.IsFalse(ready.Text.Contains("warehouse", StringComparison.Ordinal));

        host.Caller.Rows = [];
        var empty = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        Assert.AreEqual(200, empty.Status, empty.Text);
        Assert.AreEqual("ready", empty.Body.GetProperty("status").GetString());
        Assert.AreEqual(0, empty.Body.GetProperty("rows").GetArrayLength());
        Assert.AreEqual(2, host.Caller.Count);
    }

    [TestMethod]
    public async Task Failed_call_does_not_write_a_report_or_change_style_or_records()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);
        var styles = ReadStyles(host.SqlitePath);
        host.Caller.Fail = true;
        var failed = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        Assert.AreEqual(400, failed.Status);
        Assert.AreEqual("execution_failed", failed.Body.GetProperty("code").GetString());
        Assert.AreEqual("鍵值查詢失敗。", failed.Body.GetProperty("message").GetString());
        Assert.AreEqual(1, host.Caller.Count);
        Assert.AreEqual(styles, ReadStyles(host.SqlitePath));
        Assert.AreEqual(0, CountRecords(host.SqlitePath));
        var current = await world.Chen.RawAsync(HttpMethod.Get, "api/reports/current");
        Assert.AreEqual("empty", current.Body.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Zero_or_many_connections_do_not_call()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);

        foreach (var count in new[] { 0, 2 })
        {
            host.Connections.Available = count;
            var result = await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
            Assert.AreEqual(400, result.Status, count.ToString());
            Assert.AreEqual("", result.Body.GetProperty("code").GetString());
            Assert.AreEqual("無法決定連線，沒有讀取資料。", result.Body.GetProperty("message").GetString());
            AssertNoSecrets(result.Text, "Data Source=", "Password=", "warehouse");
        }

        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Records_reopen_without_mcp_and_stay_private_to_the_owner()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);
        await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        var calls = host.Caller.Count;
        var saved = await world.Chen.RawAsync(HttpMethod.Post, "api/records", new RecordWriteRequest { Shared = false });
        Assert.AreEqual(200, saved.Status, saved.Text);
        var id = saved.Body.GetProperty("id").GetString()!;
        var recordText = ReadRecords(host.SqlitePath);
        Assert.Contains("ROWMARKER", recordText);
        Assert.Contains("完成", System.Text.RegularExpressions.Regex.Unescape(recordText));
        AssertNoSecrets(recordText, "Data Source=", "Password=", "warehouse");

        var opened = await world.Chen.RawAsync(HttpMethod.Get, "api/records/" + id);
        Assert.AreEqual(200, opened.Status, opened.Text);
        Assert.AreEqual("ROWMARKER", opened.Body.GetProperty("rows")[0][0].GetString());
        Assert.AreEqual("完成", opened.Body.GetProperty("parameters").GetProperty("狀態").GetString());
        Assert.IsTrue(opened.Body.GetProperty("blocks").GetArrayLength() > 0);
        Assert.AreEqual(calls, host.Caller.Count);

        var hidden = await world.He.RawAsync(HttpMethod.Get, "api/records/" + id);
        Assert.AreEqual(403, hidden.Status);
        Assert.IsFalse(hidden.Text.Contains("ROWMARKER", StringComparison.Ordinal));
        var deleted = await world.He.RawAsync(HttpMethod.Delete, "api/records/" + id);
        Assert.AreEqual(403, deleted.Status);
        var sharedDenied = await world.He.RawAsync(HttpMethod.Put, "api/records/" + id, new RecordWriteRequest { Shared = true });
        Assert.AreEqual(403, sharedDenied.Status);
        Assert.AreEqual("0", ReadColumn(host.SqlitePath, "SELECT shared FROM generation_records"));

        var share = await world.Chen.RawAsync(HttpMethod.Put, "api/records/" + id, new RecordWriteRequest { Shared = true });
        Assert.AreEqual(200, share.Status, share.Text);
        var visible = await world.He.RawAsync(HttpMethod.Get, "api/records/" + id);
        Assert.AreEqual(200, visible.Status, visible.Text);
        Assert.AreEqual(calls, host.Caller.Count);
        var still = await world.He.RawAsync(HttpMethod.Delete, "api/records/" + id);
        Assert.AreEqual(403, still.Status);
        Assert.AreEqual(1, CountRecords(host.SqlitePath));
    }

    [TestMethod]
    public async Task Saving_a_layout_creates_a_style_owned_by_the_terminal_user_without_rows()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);
        await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        var calls = host.Caller.Count;
        var saved = await world.Chen.RawAsync(HttpMethod.Post, "api/reports/style", new LayoutWriteRequest { Name = "陳安的版面", Shared = false });
        Assert.AreEqual(200, saved.Status, saved.Text);
        Assert.AreEqual(world.Chen.Me.Id, saved.Body.GetProperty("ownerId").GetString());
        Assert.AreEqual(calls, host.Caller.Count);
        var json = System.Text.RegularExpressions.Regex.Unescape(ReadStyles(host.SqlitePath));
        Assert.Contains(world.Chen.Me.Id, ReadColumn(host.SqlitePath, "SELECT owner_id FROM styles"));
        Assert.IsFalse(json.Contains("ROWMARKER", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("完成", StringComparison.Ordinal));
        AssertNoSecrets(json, "Data Source=", "Password=");
    }

    [TestMethod]
    public async Task Pdf_matches_the_report_and_leaving_unsaved_blocks_export()
    {
        await using var host = new WorkbenchHost();
        var world = await WorldAsync(host);
        await SaveViewAsync(world.Lin, View("ship-1", "出貨", "說明"));
        await world.Lin.RawAsync(HttpMethod.Post, "api/views/ship-1/publish");
        var accepted = await AcceptAsync(world.Zhou, "ship-1", "出貨", shared: true);
        var blockCount = accepted.Body.GetProperty("blocks").GetArrayLength();
        await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        var calls = host.Caller.Count;
        var pdf = await world.Chen.Client.GetAsync("api/export/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.AreEqual(HttpStatusCode.OK, pdf.StatusCode);
        AssertPdf(bytes, blockCount);
        Assert.AreEqual(calls, host.Caller.Count);

        var left = await world.Chen.RawAsync(HttpMethod.Post, "api/reports/leave");
        Assert.AreEqual(200, left.Status);
        var denied = await world.Chen.Client.GetAsync("api/export/pdf");
        var deniedText = await denied.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("未保存就離開後不能匯出", deniedText);

        var record = await world.Chen.RawAsync(HttpMethod.Post, "api/records", new RecordWriteRequest());
        Assert.AreEqual(403, record.Status);
        Assert.Contains("沒有可以保存的報表", record.Text);
        await world.Chen.RawAsync(HttpMethod.Post, "api/utterance", new UtteranceRequest { Text = "出貨 狀態=完成" });
        record = await world.Chen.RawAsync(HttpMethod.Post, "api/records", new RecordWriteRequest { Shared = true });
        var recordId = record.Body.GetProperty("id").GetString();
        await world.Chen.RawAsync(HttpMethod.Post, "api/reports/leave");
        calls = host.Caller.Count;
        var again = await world.Chen.Client.GetAsync("api/export/pdf?recordId=" + recordId);
        var print = await world.Chen.Client.GetAsync("api/export/print?recordId=" + recordId);
        AssertPdf(await again.Content.ReadAsByteArrayAsync(), blockCount);
        AssertPdf(await print.Content.ReadAsByteArrayAsync(), blockCount);
        Assert.AreEqual(calls, host.Caller.Count);
    }

    [TestMethod]
    public async Task Either_shell_logout_rejects_the_other_shell()
    {
        await using var host = new WorkbenchHost();
        var browser = await host.LoginAsync();
        var avaloniaClient = host.Open();
        avaloniaClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browser.SessionId);
        var shell = new ShellApi(avaloniaClient);
        var me = await shell.MeAsync();
        Assert.AreEqual(browser.SessionId, shell.SessionId is null ? browser.SessionId : browser.SessionId);
        Assert.IsNotNull(me);

        await shell.LogoutAsync();
        var afterShell = await browser.Client.GetAsync("api/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterShell.StatusCode);

        var browserAgain = await host.LoginAsync();
        var other = host.Open();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserAgain.SessionId);
        await browserAgain.Client.DeleteAsync("api/sessions");
        var afterBrowser = await other.GetAsync("api/me");
        Assert.AreEqual(HttpStatusCode.Unauthorized, afterBrowser.StatusCode);

        var avalonia = typeof(ShellApi).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.IsFalse(avalonia.Any(name => name!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(avalonia.Contains("Workbench.Adapters"));
        var web = typeof(CircuitSession).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.IsFalse(web.Contains("Workbench.Adapters"));
        Assert.IsFalse(web.Contains("DbServices.Mcp"));
        var core = typeof(SessionService).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
        Assert.IsFalse(core.Any(name => name!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(core.Contains("DbServices.Mcp"));
    }

    private static async Task<World> WorldAsync(WorkbenchHost host)
    {
        var admin = await host.LoginAsync();
        await CreateAsync(admin, "林可", LinSecret, EntryNames.Analyst);
        await CreateAsync(admin, "周寧", ZhouSecret, EntryNames.Designer);
        await CreateAsync(admin, "陳安", ChenSecret, EntryNames.Terminal);
        await CreateAsync(admin, "何遠", HeSecret, EntryNames.Terminal);
        return new World(
            await host.LoginAsync("林可", LinSecret),
            await host.LoginAsync("周寧", ZhouSecret),
            await host.LoginAsync("陳安", ChenSecret),
            await host.LoginAsync("何遠", HeSecret));
    }

    private static async Task<Session> CreateAsync(Session admin, string name, string secret, params string[] entries)
    {
        var created = await admin.RawAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest
        {
            DisplayName = name,
            Secret = secret,
        });
        Assert.AreEqual(200, created.Status, created.Text);
        AssertNoSecrets(created.Text, secret, Password);
        var id = created.Body.GetProperty("id").GetString()!;
        if (entries.Length > 0)
        {
            var assigned = await admin.RawAsync(HttpMethod.Put, "api/operators/" + id + "/entries", new EntryAssignmentRequest
            {
                Entries = entries.ToList(),
            });
            Assert.AreEqual(200, assigned.Status, assigned.Text);
        }

        return await Task.FromResult(new Session(admin.Client, id, new MeResponse { Id = id, DisplayName = name }));
    }

    private static ViewDocumentDto View(string id, string name, string purpose) => new()
    {
        Id = id,
        Name = name,
        Purpose = purpose,
        Sources = ["orders"],
        Parameters = [new ViewParameterDto { Name = "狀態", Column = "status", Source = "orders" }],
        Columns = [new ViewColumnDto { Name = "Name", Source = "orders" }],
    };

    private static Task<(int Status, string Text, JsonElement Body)> SaveViewAsync(Session session, ViewDocumentDto document)
        => session.RawAsync(HttpMethod.Put, "api/views/" + document.Id, document);

    private static Task<(int Status, string Text, JsonElement Body)> AcceptAsync(
        Session session,
        string viewId,
        string name,
        bool shared,
        List<StyleBlockDto>? blocks = null)
        => session.RawAsync(HttpMethod.Post, "api/styles", new StyleWriteRequest
        {
            Name = name,
            ViewId = viewId,
            Shared = shared,
            Blocks = blocks ??
            [
                new StyleBlockDto { Type = "title", Order = 0, Text = name },
                new StyleBlockDto { Type = "text", Order = 1, Text = "說明" },
                new StyleBlockDto { Type = "parameter", Order = 2, Parameter = "狀態", Label = "狀態" },
                new StyleBlockDto { Type = "table", Order = 3, Columns = ["Name"] },
            ],
        });

    private static List<StyleBlockDto> Blocks(JsonElement array) => array.EnumerateArray().Select(item => new StyleBlockDto
    {
        Type = item.GetProperty("type").GetString() ?? "",
        Order = item.GetProperty("order").GetInt32(),
        Text = item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() : null,
        Parameter = item.TryGetProperty("parameter", out var parameter) && parameter.ValueKind == JsonValueKind.String ? parameter.GetString() : null,
        Label = item.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String ? label.GetString() : null,
        Columns = item.TryGetProperty("columns", out var columns) && columns.ValueKind == JsonValueKind.Array
            ? columns.EnumerateArray().Select(column => column.GetString() ?? "").ToList()
            : [],
    }).ToList();

    private static void AssertPdf(byte[] bytes, int pages)
    {
        using var document = PdfDocument.Open(bytes);
        Assert.AreEqual(pages, document.NumberOfPages);
        var text = string.Join("\n", document.GetPages().Select(page => page.Text));
        Assert.Contains("ROWMARKER", text);
        Assert.Contains("完成", text);
        AssertNoSecrets(text, Password, "Data Source=", "Password=");
    }

    private static void AssertNoSecrets(string text, params string[] secrets)
    {
        foreach (var secret in secrets)
            Assert.IsFalse(text.Contains(secret, StringComparison.Ordinal), secret);
    }

    private static int CountNamed(string path, string name)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM operators WHERE display_name = $name";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string ReadVerifier(string path)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT password_verifier FROM operators";
        return (string)command.ExecuteScalar()!;
    }

    private static int CountOperators(string path)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM operators";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool ReadAdmin(string path, string name)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_admin FROM operators WHERE display_name = $name";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static int CountStyles(string path)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM styles";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CountRecords(string path)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM generation_records";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string ReadStyles(string path) => ReadColumn(path, "SELECT blocks_json FROM styles ORDER BY name");

    private static string ReadRecords(string path) => ReadColumn(path, "SELECT blocks_json || columns_json || rows_json || parameters_json FROM generation_records");

    private static string ReadColumn(string path, string sql)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var builder = new StringBuilder();
        while (reader.Read())
            builder.Append(reader.GetString(0));
        return builder.ToString();
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection("Data Source=" + path);
        connection.Open();
        return connection;
    }

    private sealed record World(Session Lin, Session Zhou, Session Chen, Session He);
}
