using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Contracts;

namespace Workbench.Tests;

[TestClass]
[DoNotParallelize]
public class MatrixTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task Seed_admin_is_one_verifier_and_restart_ignores_password_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-seed-" + Guid.NewGuid().ToString("N"));
        await using (var first = new WorkbenchHost(options =>
        {
            options.ContentRoot = root;
            options.PreserveRoot = true;
        }))
        {
            var admin = await first.LoginAsync();
            Assert.IsTrue(admin.Me.IsAdmin);
            var again = await first.LoginAsync();
            Assert.AreEqual(admin.Me.Id, again.Me.Id);
            var verifier = ReadText(first.SqlitePath, "SELECT password_verifier FROM operators");
            Assert.IsFalse(verifier.Contains("seed-secret", StringComparison.Ordinal));
        }

        await using var second = new WorkbenchHost(options =>
        {
            options.ContentRoot = root;
            options.SeedAdmin = false;
        });
        var restarted = await second.LoginAsync();
        Assert.IsTrue(restarted.Me.IsAdmin);
    }

    [TestMethod]
    public async Task Missing_password_returns_workbench_unavailable_and_does_not_call_mcp()
    {
        await using var host = new WorkbenchHost(options => options.SeedAdmin = false);
        using var client = host.Open();
        var login = await client.PostAsJsonAsync("api/sessions", new LoginRequest { DisplayName = "管理者", Secret = "x" }, Json);
        var feature = await client.GetAsync("api/connections?surface=mcp");
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, feature.StatusCode);
        Assert.IsTrue((await login.Content.ReadAsStringAsync()).Contains("workbench_unavailable", StringComparison.Ordinal));
        Assert.AreEqual(0, host.Surface.Calls.Count);
    }

    [TestMethod]
    public async Task Anonymous_request_returns_nothing_and_does_not_call_mcp()
    {
        await using var host = new WorkbenchHost();
        using var client = host.Open();
        foreach (var url in new[] { "api/connections?surface=mcp", "api/templates", "api/favorites", "api/structure/tables?surface=mcp&connection=inventory" })
        {
            var response = await client.GetAsync(url);
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.IsTrue((await response.Content.ReadAsStringAsync()).Contains("unauthenticated", StringComparison.Ordinal));
        }

        Assert.AreEqual(0, host.Surface.Calls.Count);
        Assert.IsFalse(host.Surface.Calls.Contains("anonymous"));
    }

    [TestMethod]
    public async Task Logout_and_restart_invalidate_the_same_session_for_both_shells()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-session-" + Guid.NewGuid().ToString("N"));
        string sessionId;
        await using (var host = new WorkbenchHost(options =>
        {
            options.ContentRoot = root;
            options.PreserveRoot = true;
        }))
        {
            var first = await host.LoginAsync();
            sessionId = first.SessionId;
            using var second = host.Open();
            second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            Assert.AreEqual(HttpStatusCode.OK, (await second.GetAsync("api/me")).StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, (await first.Client.DeleteAsync("api/sessions")).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await first.Client.GetAsync("api/me")).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await second.GetAsync("api/me")).StatusCode);
        }

        await using var restarted = new WorkbenchHost(options =>
        {
            options.ContentRoot = root;
            options.SeedAdmin = false;
        });
        using var old = restarted.Open();
        old.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await old.GetAsync("api/connections?surface=mcp")).StatusCode);
    }

    [TestMethod]
    public async Task Admin_creates_named_account_and_assignment_does_not_fill()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var created = await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "林可", Secret = "user-secret" });
        Assert.IsFalse(created.TryGetProperty("secret", out _));
        Assert.IsFalse(created.TryGetProperty("password", out _));
        var listed = await admin.GetAsync("api/operators");
        Assert.IsFalse(listed.GetRawText().Contains("user-secret", StringComparison.Ordinal));
        var id = created.GetProperty("id").GetString()!;
        var group = await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" });
        var before = host.Surface.Calls.Count;
        var saved = await admin.SendAsync(HttpMethod.Put, "api/assignments/" + id, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Planner, WorkRoles.Editor, WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = group.GetProperty("id").GetString()!, Role = GroupRoles.Design }],
        });
        Assert.AreEqual(3, saved.GetProperty("workRoles").GetArrayLength());
        Assert.AreEqual(before, host.Surface.Calls.Count);
        var user = await host.LoginAsync("林可", "user-secret");
        Assert.IsFalse(user.Me.IsAdmin);
        var denied = await user.RawAsync(HttpMethod.Put, "api/assignments/" + admin.Me.Id, new AssignmentDto());
        Assert.AreEqual(403, denied.Status);
        var unchanged = await admin.GetAsync("api/assignments/" + id);
        Assert.AreEqual(3, unchanged.GetProperty("workRoles").GetArrayLength());
    }

    [TestMethod]
    public async Task Rail_follows_work_roles_and_blocks_missing_surfaces()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        Assert.IsTrue(admin.Me.Rail.LivePreview && admin.Me.Rail.Templates && admin.Me.Rail.Intent && admin.Me.Rail.Favorites && admin.Me.Rail.Preview);
        Assert.IsTrue(admin.Me.Rail.Accounts && admin.Me.Rail.Assignments && admin.Me.Rail.Model);
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "群" })).GetProperty("id").GetString()!;
        await AssertRail(host, admin, "規劃", [WorkRoles.Planner], group, live: true, templates: false, intent: false, favorites: false, preview: true, empty: false);
        await AssertRail(host, admin, "編輯", [WorkRoles.Editor], group, live: false, templates: true, intent: false, favorites: false, preview: true, empty: false);
        await AssertRail(host, admin, "終端", [WorkRoles.Terminal], group, live: false, templates: true, intent: true, favorites: true, preview: true, empty: false);
        var idleId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "空白", Secret = "user-secret" })).GetProperty("id").GetString()!;
        var idle = await host.LoginAsync("空白", "user-secret");
        Assert.AreEqual("尚未指派工作角色。", idle.Me.Rail.EmptyMessage);
        Assert.IsFalse(idle.Me.Rail.LivePreview || idle.Me.Rail.Templates || idle.Me.Rail.Intent || idle.Me.Rail.Preview);
        Assert.AreEqual(403, (await idle.RawAsync(HttpMethod.Get, "api/connections?surface=mcp")).Status);
        Assert.AreEqual(idleId, idle.Me.Id);
    }

    [TestMethod]
    public async Task Connection_list_projects_name_and_server_type_only()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var mcp = await admin.GetAsync("api/connections?surface=mcp");
        Assert.AreEqual(2, mcp.GetArrayLength());
        foreach (var row in mcp.EnumerateArray())
        {
            var names = row.EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "name", "serverType" }, names);
        }

        var web = await admin.GetAsync("api/connections?surface=web");
        Assert.AreEqual(1, web.GetArrayLength());
        Assert.AreEqual("default", web[0].GetProperty("name").GetString());
        Assert.AreEqual("Sqlite", web[0].GetProperty("serverType").GetString());
    }

    [TestMethod]
    public async Task Structure_uses_tables_and_fields_without_a_fill()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var tables = await admin.GetAsync("api/structure/tables?surface=mcp&connection=inventory");
        var fields = await admin.GetAsync("api/structure/fields?surface=mcp&connection=inventory&table=products");
        Assert.IsTrue(host.Surface.IncludeViewSeen);
        CollectionAssert.AreEquivalent(new[] { "tables", "fields" }, host.Surface.Calls);
        Assert.AreEqual("Name", fields.GetProperty("fields")[1].GetProperty("name").GetString());
        Assert.IsTrue(fields.GetProperty("fields")[0].GetProperty("primaryKey").GetBoolean());
        Assert.IsTrue(fields.GetProperty("fields")[1].GetProperty("notNull").GetBoolean());
        Assert.AreEqual("products", tables.GetProperty("tables")[0].GetString());
        host.Surface.Tables = [];
        var empty = await admin.GetAsync("api/structure/tables?surface=mcp&connection=inventory");
        Assert.AreEqual(0, empty.GetProperty("tables").GetArrayLength());
        Assert.IsFalse(host.Surface.Calls.Contains("query"));
    }

    [TestMethod]
    public async Task Structure_error_keeps_the_mcp_code_and_a_trace_outside_sqlite()
    {
        await using var host = new WorkbenchHost();
        host.Surface.UnknownTable = "missing";
        var admin = await host.LoginAsync();
        var fields = await admin.GetAsync("api/structure/fields?surface=mcp&connection=inventory&table=missing");
        Assert.AreEqual("unknown_table", fields.GetProperty("errorCode").GetString());
        Assert.IsFalse(fields.GetProperty("errorMessage").GetString()!.Contains("Server=", StringComparison.OrdinalIgnoreCase));
        var trace = fields.GetProperty("traces")[0];
        Assert.AreEqual(admin.Me.Id, trace.GetProperty("operatorId").GetString());
        Assert.AreEqual("inventory", trace.GetProperty("connection").GetString());
        Assert.AreEqual("fields", trace.GetProperty("tool").GetString());
        Assert.AreEqual("unknown_table", trace.GetProperty("errorCode").GetString());
        var raw = ReadText(host.SqlitePath, "SELECT COALESCE(group_concat(components_json, ''), '') FROM templates");
        Assert.IsFalse(raw.Contains("unknown_table", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Query_is_one_call_failure_keeps_the_previous_snapshot_and_web_rejects_extra_keys()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var before = host.Surface.Calls.Count;
        Assert.AreEqual(before, host.Surface.Calls.Count);
        var queried = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        CollectionAssert.AreEqual(new[] { "query" }, queried.GetProperty("tools").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("Name", queried.GetProperty("parameterKeys")[0].GetString());
        Assert.AreEqual("one", queried.GetProperty("rows")[0][1].GetString());
        var snapshot = queried.GetProperty("snapshotId").GetString()!;
        host.Surface.FailQueryConnection = "inventory";
        var failed = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory"));
        Assert.AreEqual("connection_failed", failed.GetProperty("errorCode").GetString());
        Assert.IsFalse(failed.TryGetProperty("snapshotId", out var replaced) && replaced.ValueKind == JsonValueKind.String);
        var kept = await admin.GetAsync("api/preview/" + snapshot);
        Assert.AreEqual("one", kept.GetProperty("blocks")[0].GetProperty("rows")[0][1].GetString());
        host.Surface.FailQueryConnection = null;
        host.Surface.Rows = [];
        var empty = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory"));
        Assert.AreEqual("沒有符合的列。", empty.GetProperty("notice").GetString());
        host.Surface.AssertAllowed();
    }

    [TestMethod]
    public async Task Web_query_sends_zero_or_one_key_and_count_is_rejected()
    {
        var capture = new WebCapture();
        await using var host = WebHost(capture);
        var admin = await host.LoginAsync();
        var fields = await admin.GetAsync("api/structure/fields?surface=web&connection=default&table=products");
        var body = fields.GetRawText();
        Assert.AreEqual("Id", fields.GetProperty("fields")[0].GetProperty("name").GetString());
        Assert.IsTrue(fields.GetProperty("fields")[0].GetProperty("primaryKey").GetBoolean());
        Assert.IsTrue(fields.GetProperty("fields")[0].GetProperty("notNull").GetBoolean());
        Assert.IsFalse(body.Contains("ConnectString", StringComparison.Ordinal));
        Assert.IsFalse(body.Contains("Server=", StringComparison.Ordinal));
        capture.Calls.Clear();
        await admin.SendAsync(HttpMethod.Post, "api/plans/query", new PlanRequest { Surface = "web", Connection = "default", Table = "products" });
        Assert.IsTrue(capture.Calls.Any(call => call.Method == "GET" && call.Path.EndsWith("/products", StringComparison.Ordinal)));
        capture.Calls.Clear();
        await admin.SendAsync(HttpMethod.Post, "api/plans/query", new PlanRequest
        {
            Surface = "web",
            Connection = "default",
            Table = "products",
            Filters = [new FilterDto { Key = "Id", Value = "1" }],
        });
        Assert.IsTrue(capture.Calls.Any(call => call.Path.Contains("Key=Id", StringComparison.Ordinal) && call.Path.Contains("Value=1", StringComparison.Ordinal)));
        var countBefore = capture.Calls.Count;
        var rejected = await admin.RawAsync(HttpMethod.Post, "api/plans/query", new PlanRequest
        {
            Surface = "web",
            Connection = "default",
            Table = "products",
            Filters = [new FilterDto { Key = "Id", Value = "1" }, new FilterDto { Key = "Name", Value = "one" }],
        });
        Assert.AreEqual(400, rejected.Status);
        Assert.AreEqual("single_key_only", rejected.Body.GetProperty("code").GetString());
        Assert.AreEqual(countBefore, capture.Calls.Count);
        var counted = await admin.RawAsync(HttpMethod.Post, "api/plans/count", new PlanRequest { Surface = "web", Connection = "default", Table = "products" });
        Assert.AreEqual(403, counted.Status);
        Assert.AreEqual(countBefore, capture.Calls.Count);
        Assert.IsTrue(capture.Calls.All(call => call.Method == "GET"));
    }

    [TestMethod]
    public async Task Count_is_one_mcp_call_and_engines_share_parameter_keys()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var first = await admin.SendAsync(HttpMethod.Post, "api/plans/count", Plan("inventory", "Name", "one"));
        var second = await admin.SendAsync(HttpMethod.Post, "api/plans/count", Plan("inventory_pg", "Name", "one"));
        Assert.AreEqual(1, first.GetProperty("tools").GetArrayLength());
        Assert.AreEqual("count", first.GetProperty("tools")[0].GetString());
        Assert.AreEqual(1, first.GetProperty("count").GetDecimal());
        CollectionAssert.AreEqual(
            first.GetProperty("parameterKeys").EnumerateArray().Select(item => item.GetString()).ToArray(),
            second.GetProperty("parameterKeys").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.IsFalse(first.GetRawText().Contains("Data Source", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(second.GetRawText().Contains("Server=", StringComparison.OrdinalIgnoreCase));
        var queryA = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        var queryB = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory_pg", "Name", "one"));
        Assert.AreEqual(queryA.GetProperty("tools")[0].GetString(), queryB.GetProperty("tools")[0].GetString());
        host.Surface.AssertAllowed();
    }

    [TestMethod]
    public async Task Audit_match_keeps_rows_when_the_line_is_missing_or_not_exact()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var missed = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        Assert.AreEqual(false, missed.GetProperty("auditMatched").GetBoolean());
        Assert.AreEqual("one", missed.GetProperty("rows")[0][1].GetString());
        host.Audit.Entries.Add(new Workbench.Core.Ports.AuditEntry
        {
            Caller = admin.Me.Id,
            ConnectionId = "inventory",
            Tool = "query",
            ArgumentsJson = """{"connectionId":"inventory","table":"products","filters":{"Name":"one","Extra":"x"}}""",
            RowCount = 1,
        });
        var extra = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        Assert.AreEqual(false, extra.GetProperty("auditMatched").GetBoolean());
        host.Audit.Entries.Add(new Workbench.Core.Ports.AuditEntry
        {
            Caller = admin.Me.Id,
            ConnectionId = "inventory",
            Tool = "query",
            ArgumentsJson = """{"connectionId":"inventory","table":"products","filters":{"Name":"one"}}""",
            RowCount = 1,
        });
        var matched = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        Assert.AreEqual(true, matched.GetProperty("auditMatched").GetBoolean());
    }

    [TestMethod]
    public async Task One_connection_failure_leaves_the_other_usable()
    {
        await using var host = new WorkbenchHost();
        host.Surface.FailQueryConnection = "inventory_pg";
        var admin = await host.LoginAsync();
        var failed = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory_pg"));
        Assert.AreEqual("connection_failed", failed.GetProperty("errorCode").GetString());
        var other = await admin.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory"));
        Assert.AreEqual("one", other.GetProperty("rows")[0][1].GetString());
        Assert.AreEqual(HttpStatusCode.OK, (await admin.Client.GetAsync("api/me")).StatusCode);
    }

    [TestMethod]
    public async Task Template_save_canvas_suggestion_and_visibility()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var reports = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var salary = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "薪資" })).GetProperty("id").GetString()!;
        var editorId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "周寧", Secret = "user-secret" })).GetProperty("id").GetString()!;
        var viewerId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "陳安", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + editorId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Planner],
            Groups = [new GroupRoleDto { GroupId = reports, Role = GroupRoles.Design }],
        });
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + viewerId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = reports, Role = GroupRoles.View }],
        });
        var editor = await host.LoginAsync("周寧", "user-secret");
        var denied = await editor.RawAsync(HttpMethod.Post, "api/templates", Template(salary, "隱藏", Table("salary", "Amount")));
        Assert.AreEqual(403, denied.Status);
        var saved = await editor.SendAsync(HttpMethod.Post, "api/templates", Template(reports, "月報", Five()));
        var id = saved.GetProperty("id").GetString()!;
        Assert.AreEqual(5, saved.GetProperty("components").GetArrayLength());
        var orders = saved.GetProperty("components").EnumerateArray().Select(item => item.GetProperty("order").GetInt32()).ToArray();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, orders);
        var stored = ReadText(host.SqlitePath, "SELECT COALESCE(group_concat(components_json, ''), '') FROM templates");
        Assert.IsFalse(stored.Contains("\"x\"", StringComparison.Ordinal));
        Assert.IsFalse(stored.Contains("inventory", StringComparison.Ordinal));
        var proposal = await editor.SendAsync(HttpMethod.Post, "api/suggestions", new SuggestionRequest { Surface = "mcp", Connection = "inventory", Table = "products", TemplateId = id });
        var columns = proposal.EnumerateArray().SelectMany(item => item.GetProperty("columns").EnumerateArray().Select(column => column.GetString()!)).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Id", "Name" }, columns);
        Assert.IsTrue(host.Surface.Calls.All(call => call == "fields"));
        var unknown = await editor.RawAsync(HttpMethod.Post, "api/suggestions/accept", new SuggestionRequest
        {
            Surface = "mcp",
            Connection = "inventory",
            Table = "products",
            TemplateId = id,
            Proposal = [Table("products", "Missing")],
        });
        Assert.AreEqual(400, unknown.Status);
        Assert.AreEqual("suggestion_rejected", unknown.Body.GetProperty("code").GetString());
        Assert.AreEqual(5, (await editor.GetAsync("api/templates/" + id)).GetProperty("components").GetArrayLength());
        Assert.IsFalse(host.Surface.Calls.Contains("query"));
        var accepted = proposal.EnumerateArray().Select(item => JsonSerializer.Deserialize<ComponentDto>(item.GetRawText(), Json)!).ToList();
        accepted.Insert(0, new ComponentDto { Type = ComponentKinds.Title, Text = "標題" });
        var acceptedBody = await editor.SendAsync(HttpMethod.Post, "api/suggestions/accept", new SuggestionRequest
        {
            Surface = "mcp",
            Connection = "inventory",
            TemplateId = id,
            Proposal = accepted,
        });
        Assert.AreEqual("title", acceptedBody.GetProperty("components")[0].GetProperty("type").GetString());
        var trimmed = acceptedBody.Deserialize<TemplateDto>(Json)!;
        trimmed.Components.RemoveAt(trimmed.Components.Count - 1);
        var reopened = await editor.SendAsync(HttpMethod.Put, "api/templates/" + id, trimmed);
        Assert.IsFalse(reopened.GetRawText().Contains("\"rows\"", StringComparison.Ordinal));
        Assert.IsFalse(reopened.GetRawText().Contains("Server=", StringComparison.Ordinal));
        var viewer = await host.LoginAsync("陳安", "user-secret");
        Assert.AreEqual(403, (await viewer.RawAsync(HttpMethod.Put, "api/templates/" + id, reopened)).Status);
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + editorId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Planner],
            Groups = [new GroupRoleDto { GroupId = reports, Role = GroupRoles.Design }, new GroupRoleDto { GroupId = salary, Role = GroupRoles.Design }],
        });
        var hidden = await editor.SendAsync(HttpMethod.Post, "api/templates", Template(salary, "薪資表", Table("salary", "Amount")));
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + editorId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Planner],
            Groups = [new GroupRoleDto { GroupId = reports, Role = GroupRoles.Design }],
        });
        var moved = await editor.RawAsync(HttpMethod.Put, "api/templates/" + id, new TemplateDto
        {
            Id = id,
            GroupId = salary,
            Name = "月報",
            Components = trimmed.Components,
        });
        Assert.AreEqual(403, moved.Status);
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + editorId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Planner],
            Groups = [new GroupRoleDto { GroupId = reports, Role = GroupRoles.Design }],
        });
        Assert.AreEqual(reports, (await editor.GetAsync("api/templates/" + id)).GetProperty("groupId").GetString());
        await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "空白角色", Secret = "user-secret" });
        var outsiderId = (await admin.GetAsync("api/operators")).EnumerateArray().First(item => item.GetProperty("displayName").GetString() == "空白角色").GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + outsiderId, new AssignmentDto { WorkRoles = [WorkRoles.Terminal] });
        var outsider = await host.LoginAsync("空白角色", "user-secret");
        Assert.AreEqual(0, (await outsider.GetAsync("api/templates")).GetArrayLength());
        Assert.AreEqual(403, (await outsider.RawAsync(HttpMethod.Get, "api/templates/" + id)).Status);
        var renamed = await admin.SendAsync(HttpMethod.Put, "api/groups/" + reports, new GroupWriteRequest { Name = "月報新名" });
        Assert.AreEqual(reports, renamed.GetProperty("id").GetString());
        Assert.AreEqual(reports, (await editor.GetAsync("api/templates/" + id)).GetProperty("groupId").GetString());
        Assert.AreEqual(400, (await admin.RawAsync(HttpMethod.Delete, "api/groups/" + reports)).Status);
        var adminSeen = await admin.GetAsync("api/templates");
        Assert.IsTrue(adminSeen.GetRawText().Contains(id, StringComparison.Ordinal));
        Assert.AreEqual(403, (await admin.RawAsync(HttpMethod.Put, "api/templates/" + id, reopened)).Status);
    }

    [TestMethod]
    public async Task Template_preview_rejects_a_bad_plan_and_a_later_call_does_not_replace_the_snapshot()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var editorId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "周寧", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + editorId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Planner, WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.Design }],
        });
        var editor = await host.LoginAsync("周寧", "user-secret");
        var template = await editor.SendAsync(HttpMethod.Post, "api/templates", Template(group, "月報",
            new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
            Table("products", "Name"),
            new ComponentDto { Type = ComponentKinds.Count, Table = "products" }));
        var id = template.GetProperty("id").GetString()!;
        var missing = await editor.RawAsync(HttpMethod.Post, "api/templates/" + id + "/preview", Plan("inventory"));
        Assert.AreEqual(400, missing.Status);
        Assert.IsFalse(host.Surface.Calls.Contains("query"));
        var first = await editor.SendAsync(HttpMethod.Post, "api/plans/query", Plan("inventory", "Name", "one"));
        var snapshot = first.GetProperty("snapshotId").GetString()!;
        host.Surface.FailCount = true;
        var failed = await editor.SendAsync(HttpMethod.Post, "api/templates/" + id + "/preview", Plan("inventory", "Name", "one"));
        Assert.AreEqual("execution_failed", failed.GetProperty("errorCode").GetString());
        var kept = await editor.GetAsync("api/preview/" + snapshot);
        Assert.AreEqual("one", kept.GetProperty("blocks")[0].GetProperty("rows")[0][1].GetString());
        host.Surface.FailCount = false;
        var preview = await editor.SendAsync(HttpMethod.Post, "api/templates/" + id + "/preview", Plan("inventory", "Name", "one"));
        var tools = preview.GetProperty("tools").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.AreEqual(new[] { "query", "count" }, tools);
        Assert.IsTrue(preview.GetProperty("parameterKeys").EnumerateArray().Any(item => item.GetString() == "Name"));
    }

    [TestMethod]
    public async Task Intent_confirm_rechecks_and_favorites_export_and_keywords()
    {
        await using var host = new WorkbenchHost(options => options.ModelEnabled = true);
        var admin = await host.LoginAsync();
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var salary = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "薪資" })).GetProperty("id").GetString()!;
        var terminalId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "陳安", Secret = "user-secret" })).GetProperty("id").GetString()!;
        var otherId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "周寧", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + terminalId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.View }],
        });
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + otherId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor, WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.Design }, new GroupRoleDto { GroupId = salary, Role = GroupRoles.Design }],
        });
        var designer = await host.LoginAsync("周寧", "user-secret");
        var template = await designer.SendAsync(HttpMethod.Post, "api/templates", Template(group, "月報",
            new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
            Table("products", "Id", "Name")));
        var hidden = await designer.SendAsync(HttpMethod.Post, "api/templates", Template(salary, "薪資", Table("salary", "Amount")));
        var terminal = await host.LoginAsync("陳安", "user-secret");
        Assert.IsFalse(terminal.Me.Rail.LivePreview);
        var listed = await terminal.GetAsync("api/templates");
        Assert.IsFalse(listed.GetRawText().Contains(hidden.GetProperty("id").GetString()!, StringComparison.Ordinal));
        Assert.AreEqual(403, (await terminal.RawAsync(HttpMethod.Post, "api/templates/" + hidden.GetProperty("id").GetString() + "/preview", Plan("inventory"))).Status);
        var fills = host.Surface.Calls.Count(call => call is "query" or "count");
        var ambiguous = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory inventory_pg 月報 Name one", Surface = "mcp" });
        Assert.AreEqual("ambiguous", ambiguous.GetProperty("status").GetString());
        Assert.IsFalse(ambiguous.GetProperty("canConfirm").GetBoolean());
        Assert.AreEqual(fills, host.Surface.Calls.Count(call => call is "query" or "count"));
        var incomplete = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "月報", Surface = "mcp" });
        Assert.AreEqual("incomplete", incomplete.GetProperty("status").GetString());
        var ready = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月報 Name one", Surface = "mcp" });
        Assert.AreEqual("ready", ready.GetProperty("status").GetString());
        Assert.IsTrue(ready.GetProperty("canConfirm").GetBoolean());
        var changed = JsonSerializer.Deserialize<TemplateDto>(template.GetRawText(), Json)!;
        changed.Components.Add(Table("products", "Missing"));
        await designer.SendAsync(HttpMethod.Put, "api/templates/" + changed.Id, changed);
        var mismatch = await terminal.RawAsync(HttpMethod.Post, "api/intent/confirm", new ConfirmRequest { PlanId = ready.GetProperty("planId").GetString() });
        Assert.AreEqual(400, mismatch.Status);
        Assert.AreEqual("plan_mismatch", mismatch.Body.GetProperty("code").GetString());
        changed.Components.RemoveAt(changed.Components.Count - 1);
        await designer.SendAsync(HttpMethod.Put, "api/templates/" + changed.Id, changed);
        var parsed = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月報 Name one", Surface = "mcp" });
        var confirmed = await terminal.SendAsync(HttpMethod.Post, "api/intent/confirm", new ConfirmRequest { PlanId = parsed.GetProperty("planId").GetString() });
        Assert.AreEqual("one", confirmed.GetProperty("rows")[0][1].GetString());
        var snapshot = confirmed.GetProperty("snapshotId").GetString()!;
        var again = await terminal.RawAsync(HttpMethod.Post, "api/intent/confirm", new ConfirmRequest { PlanId = parsed.GetProperty("planId").GetString() });
        Assert.AreEqual("plan_mismatch", again.Body.GetProperty("code").GetString());
        var calls = host.Surface.Calls.Count;
        var pdf = await terminal.Client.GetByteArrayAsync("api/preview/" + snapshot + "/pdf");
        var csv = await terminal.Client.GetStringAsync("api/preview/" + snapshot + "/csv");
        var print = await terminal.Client.GetStringAsync("api/preview/" + snapshot + "/print");
        Assert.AreEqual(calls, host.Surface.Calls.Count);
        Assert.IsTrue(pdf.Length > 4 && pdf[0] == (byte)'%' );
        using (var document = UglyToad.PdfPig.PdfDocument.Open(pdf))
        {
            var text = string.Join('\n', document.GetPages().Select(page => page.Text));
            Assert.IsTrue(text.Contains("Name", StringComparison.Ordinal));
            Assert.IsTrue(text.Contains("one", StringComparison.Ordinal));
        }

        Assert.IsTrue(csv.Contains("one", StringComparison.Ordinal));
        Assert.IsTrue(print.Contains("one", StringComparison.Ordinal));
        Assert.IsFalse(print.Contains("&quot;", StringComparison.Ordinal) && print.Contains("one"));
        var favorite = await terminal.SendAsync(HttpMethod.Post, "api/favorites", new FavoriteWriteRequest
        {
            Name = "常用月報",
            TemplateId = changed.Id,
            Surface = "mcp",
            ConnectionName = "inventory",
            Parameters = new Dictionary<string, string> { ["Name"] = "one" },
        });
        var opened = await terminal.SendAsync(HttpMethod.Post, "api/favorites/" + favorite.GetProperty("id").GetString() + "/open", new { });
        Assert.AreEqual(changed.Id, opened.GetProperty("template").GetProperty("id").GetString());
        Assert.AreEqual(calls, host.Surface.Calls.Count);
        var other = await host.LoginAsync("周寧", "user-secret");
        Assert.AreEqual(0, (await other.GetAsync("api/favorites")).GetArrayLength());
        changed.Components.RemoveAll(item => item.Type == ComponentKinds.Parameter);
        await designer.SendAsync(HttpMethod.Put, "api/templates/" + changed.Id, changed);
        var aligned = await terminal.SendAsync(HttpMethod.Post, "api/favorites/" + favorite.GetProperty("id").GetString() + "/open", new { });
        Assert.AreEqual("alignment_failed", aligned.GetProperty("errorCode").GetString());
        var blank = await terminal.RawAsync(HttpMethod.Post, "api/keyword-rules", new KeywordWriteRequest { Phrase = "  ", TemplateId = changed.Id, ColumnNames = ["Name"] });
        Assert.AreEqual(400, blank.Status);
        var restored = Template(group, "月報",
            new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
            Table("products", "Id", "Name"));
        restored.Id = changed.Id;
        await designer.SendAsync(HttpMethod.Put, "api/templates/" + changed.Id, restored);
        var human = await terminal.RawAsync(HttpMethod.Post, "api/keyword-rules", new KeywordWriteRequest { Phrase = "月結人", TemplateId = changed.Id, ColumnNames = ["Name"] });
        Assert.AreEqual(200, human.Status);
        var humanParse = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月結人 Name one", Surface = "mcp" });
        Assert.AreEqual("ready", humanParse.GetProperty("status").GetString());
        var fresh = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月報 Name one", Surface = "mcp" });
        var shot = (await terminal.SendAsync(HttpMethod.Post, "api/intent/confirm", new ConfirmRequest { PlanId = fresh.GetProperty("planId").GetString() })).GetProperty("snapshotId").GetString();
        Assert.AreEqual(200, (await terminal.RawAsync(HttpMethod.Post, "api/keyword-rules/from-confirm", new KeywordWriteRequest { Phrase = "月結", SnapshotId = shot })).Status);
        var reparsed = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月結 Name one", Surface = "mcp" });
        Assert.AreEqual("ready", reparsed.GetProperty("status").GetString());
        await using var insert = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + host.SqlitePath);
        insert.Open();
        using var command = insert.CreateCommand();
        command.CommandText = "INSERT INTO keyword_rules (id, phrase, column_names_json, source, template_id, owner_id) VALUES ('empty', '全部', '[]', 'ai', $id, $owner)";
        command.Parameters.AddWithValue("$id", changed.Id);
        command.Parameters.AddWithValue("$owner", terminal.Me.Id);
        command.ExecuteNonQuery();
        var skipped = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 全部", Surface = "mcp" });
        Assert.AreNotEqual("ready", skipped.GetProperty("status").GetString());
        Assert.AreNotEqual("ambiguous", skipped.GetProperty("status").GetString());
        var next = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "inventory 月報 Name one", Surface = "mcp" });
        var replaced = await terminal.SendAsync(HttpMethod.Post, "api/intent/confirm", new ConfirmRequest { PlanId = next.GetProperty("planId").GetString() });
        Assert.AreEqual(400, (await terminal.RawAsync(HttpMethod.Get, "api/preview/" + snapshot + "/csv")).Status);
        Assert.AreEqual("one", replaced.GetProperty("rows")[0][1].GetString());
    }

    [TestMethod]
    public async Task Model_probe_does_not_write_and_parse_recomputes_or_voids()
    {
        var rootCheck = Path.Combine(Path.GetTempPath(), "wb-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootCheck);
        await using var host = new WorkbenchHost(options =>
        {
            options.ModelEnabled = true;
            options.ContentRoot = rootCheck;
            options.PreserveRoot = true;
        });
        var admin = await host.LoginAsync();
        await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "林可", Secret = "user-secret" });
        var user = await host.LoginAsync("林可", "user-secret");
        Assert.AreEqual(403, (await user.RawAsync(HttpMethod.Post, "api/model/probe", new ProbeRequest())).Status);
        var modelFile = Path.Combine(host.ContentRoot, "model.json");
        var invalid = await admin.SendAsync(HttpMethod.Post, "api/model/probe", new ProbeRequest { Placement = "cloud", Endpoint = "http://example.com", ModelId = "m", ApiKey = "draft" });
        Assert.AreEqual("model_invalid_settings", invalid.GetProperty("code").GetString());
        Assert.AreEqual(0, host.Sidecar.Requests.Count);
        Assert.IsFalse(File.Exists(modelFile));
        var probed = await admin.SendAsync(HttpMethod.Post, "api/model/probe", new ProbeRequest { Placement = "local", Endpoint = "http://127.0.0.1:9", ModelId = "unit" });
        Assert.AreEqual("model_ok", probed.GetProperty("code").GetString());
        Assert.AreEqual("probe", host.Sidecar.Requests[0].Kind);
        Assert.AreEqual("local", host.Sidecar.Requests[0].Placement);
        Assert.AreEqual("http://127.0.0.1:9", host.Sidecar.Requests[0].Endpoint);
        Assert.AreEqual("unit", host.Sidecar.Requests[0].ModelId);
        Assert.IsNull(host.Sidecar.Requests[0].ParseJson);
        Assert.AreEqual("local", host.Sidecar.Requests[0].ApiKey);
        Assert.IsFalse(File.Exists(modelFile));
        var saved = await admin.SendAsync(HttpMethod.Put, "api/model", new ModelWriteRequest
        {
            Enabled = true,
            Placement = "local",
            Endpoint = "http://127.0.0.1:9",
            ModelId = "unit",
            ApiKey = "kept-secret",
        });
        Assert.IsTrue(saved.GetProperty("apiKeySet").GetBoolean());
        Assert.IsFalse(saved.GetRawText().Contains("kept-secret", StringComparison.Ordinal));
        var blank = await admin.SendAsync(HttpMethod.Put, "api/model", new ModelWriteRequest
        {
            Enabled = true,
            Placement = "local",
            Endpoint = "http://127.0.0.1:9",
            ModelId = "unit",
            ApiKey = "",
        });
        Assert.IsTrue(blank.GetProperty("apiKeySet").GetBoolean());
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var id = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "陳安", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + id, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Terminal],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.View }],
        });
        var designerId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "周寧", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + designerId, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Editor],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.Design }],
        });
        var designer = await host.LoginAsync("周寧", "user-secret");
        var template = await designer.SendAsync(HttpMethod.Post, "api/templates", Template(group, "月報",
            new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
            Table("products", "Name")));
        var terminal = await host.LoginAsync("陳安", "user-secret");
        host.Sidecar.Next = new Workbench.Core.Ports.SidecarResponse
        {
            Code = "model_ok",
            Started = true,
            Stdout = "{\"candidates\":[{\"connection\":\"inventory\",\"templateId\":\"" + template.GetProperty("id").GetString() + "\",\"parameters\":{\"Name\":\"one\"}}]}",
        };
        var parsed = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "請幫我看報表", Surface = "mcp" });
        Assert.AreEqual("ready", parsed.GetProperty("status").GetString());
        Assert.AreEqual(0, host.Surface.Calls.Count(call => call is "query" or "count"));
        host.Sidecar.Next = new Workbench.Core.Ports.SidecarResponse
        {
            Code = "model_ok",
            Started = true,
            Stdout = "{\"candidates\":[{\"connection\":\"inventory\",\"templateId\":\"" + template.GetProperty("id").GetString() + "\",\"parameters\":{\"Name\":\"one\"}},{\"connection\":\"not-listed\",\"templateId\":\"" + template.GetProperty("id").GetString() + "\",\"parameters\":{\"Name\":\"one\"}}]}",
        };
        var voided = await terminal.SendAsync(HttpMethod.Post, "api/intent/parse", new IntentParseRequest { Utterance = "換一種說法", Surface = "mcp" });
        Assert.AreNotEqual("ready", voided.GetProperty("status").GetString());
        Assert.IsFalse(voided.GetProperty("canConfirm").GetBoolean());
    }

    private static string ReadText(string path, string sql)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()?.ToString() ?? "";
    }

    private static async Task AssertRail(WorkbenchHost host, Session admin, string name, List<string> roles, string group, bool live, bool templates, bool intent, bool favorites, bool preview, bool empty)
    {
        var id = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = name, Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + id, new AssignmentDto
        {
            WorkRoles = roles,
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.View }],
        });
        var me = (await host.LoginAsync(name, "user-secret")).Me.Rail;
        Assert.AreEqual(live, me.LivePreview);
        Assert.AreEqual(templates, me.Templates);
        Assert.AreEqual(intent, me.Intent);
        Assert.AreEqual(favorites, me.Favorites);
        Assert.AreEqual(preview, me.Preview);
        Assert.AreEqual(empty, me.EmptyMessage is not null);
    }

    private static PlanRequest Plan(string connection, string? key = null, string? value = null) => new()
    {
        Surface = "mcp",
        Connection = connection,
        Table = "products",
        Filters = key is null ? [] : [new FilterDto { Key = key, Value = value }],
    };

    private static TemplateDto Template(string groupId, string name, params ComponentDto[] components) => new()
    {
        GroupId = groupId,
        Name = name,
        Components = components.ToList(),
    };

    private static ComponentDto Table(string table, params string[] columns) => new()
    {
        Type = ComponentKinds.Table,
        Table = table,
        Columns = columns.ToList(),
    };

    private static ComponentDto[] Five() =>
    [
        new ComponentDto { Type = ComponentKinds.Title, Text = "標題" },
        new ComponentDto { Type = ComponentKinds.Text, Text = "說明" },
        new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
        Table("products", "Name"),
        new ComponentDto { Type = ComponentKinds.Count, Table = "products" },
    ];

    private static WorkbenchHost WebHost(WebCapture capture)
    {
        var http = new HttpClient(capture) { BaseAddress = new Uri("http://localhost/db/") };
        return new WorkbenchHost(options =>
        {
            options.UseRecordingSurface = false;
            options.SurfaceOverride = new Workbench.Adapters.RoutingDataSurface(
                new Workbench.Adapters.McpSurface(new HttpClient(), "http://127.0.0.1:9"),
                new Workbench.Adapters.WebSurface(http, "http://localhost/db/"));
        });
    }
}

internal sealed class WebCapture : HttpMessageHandler
{
    public List<(string Method, string Path)> Calls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";
        Calls.Add((request.Method.Method, path));
        if (request.Method != HttpMethod.Get)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
        var absolute = request.RequestUri?.AbsolutePath ?? "";
        string? json = absolute.EndsWith("/meta", StringComparison.Ordinal)
            ? "[\"products\"]"
            : absolute.Contains("/fieldSet", StringComparison.Ordinal)
                ? """{"ConnectString":"Server=hidden","FieldDefine":[{"FieldName":"Id","FieldType":"INTEGER","IsPK":"True","IsNotNull":"True"},{"FieldName":"Name","FieldType":"TEXT","IsPK":"False","IsNotNull":"True"}]}"""
                : absolute.Contains("/products", StringComparison.Ordinal)
                    ? """{"ConnectString":"Server=hidden","RecordData":[{"Name":"one","ConnectString":"Server=hidden"}]}"""
                    : null;
        if (json is null)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }
}
