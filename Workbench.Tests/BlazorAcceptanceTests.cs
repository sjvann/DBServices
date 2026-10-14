using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Avalonia;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Web;
using Workbench.Web.Components;

namespace Workbench.Tests;

[TestClass]
[DoNotParallelize]
public class BlazorAcceptanceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task Planner_click_runs_query_and_avalonia_uses_the_same_keys()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        using var ctx = new BunitContext();
        Share(ctx, host, admin.SessionId);
        var cut = ctx.Render<LivePreview>();
        Assert.IsTrue(cut.Markup.Contains("確認之前，這裡沒有業務列。", StringComparison.Ordinal));
        Assert.IsTrue(cut.Markup.Contains("請先選擇資料表。", StringComparison.Ordinal));
        cut.FindAll("button").First(button => button.TextContent.Contains("載入資料表", StringComparison.Ordinal)).Click();
        cut.FindAll("select")[1].Change("products");
        Assert.IsFalse(cut.FindAll("button").First(button => button.TextContent.Contains("查詢", StringComparison.Ordinal)).HasAttribute("disabled"));
        cut.FindAll("button").First(button => button.TextContent.Contains("查詢", StringComparison.Ordinal)).Click();
        Assert.IsTrue(host.Surface.Calls.Contains("query"));
        host.Surface.AssertAllowed();

        var api = new ShellApi(host.CreateClient());
        Assert.IsTrue(await api.LoginAsync("管理者", "seed-secret"));
        var queried = await api.QueryAsync(new PlanRequest { Surface = "mcp", Connection = "inventory_pg", Table = "products", Filters = [new FilterDto { Key = "Name", Value = "one" }] });
        var counted = await api.CountAsync(new PlanRequest { Surface = "mcp", Connection = "inventory", Table = "products", Filters = [new FilterDto { Key = "Name", Value = "one" }] });
        Assert.AreEqual("query", queried.Tools[0]);
        Assert.AreEqual("count", counted.Tools[0]);
        CollectionAssert.AreEqual(queried.ParameterKeys, counted.ParameterKeys);
        Assert.IsTrue(queried.Rows.Any(row => row.Contains("one")));
    }

    [TestMethod]
    public async Task Rail_assignment_intent_and_preview_clicks_call_use_cases()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var id = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "陳安", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + id, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Terminal, WorkRoles.Editor],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.Design }],
        });
        var terminal = await host.LoginAsync("陳安", "user-secret");
        var template = await terminal.SendAsync(HttpMethod.Post, "api/templates", new TemplateDto
        {
            GroupId = group,
            Name = "月報",
            Components =
            [
                new ComponentDto { Type = ComponentKinds.Parameter, Field = "Name", Label = "名稱" },
                new ComponentDto { Type = ComponentKinds.Table, Table = "products", Columns = ["Name"] },
            ],
        });

        using var rail = new BunitContext();
        Share(rail, host, terminal.SessionId);
        var layout = rail.Render<WorkbenchLayout>(parameters => parameters.Add(component => component.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>主欄</p>"))));
        Assert.IsFalse(layout.Markup.Contains("活資料預覽", StringComparison.Ordinal));
        Assert.IsTrue(layout.Markup.Contains("交談套版", StringComparison.Ordinal));
        Assert.IsTrue(layout.Markup.Contains("預覽", StringComparison.Ordinal));

        using var idleCtx = new BunitContext();
        var idleId = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "空白", Secret = "user-secret" })).GetProperty("id").GetString()!;
        var idle = await host.LoginAsync("空白", "user-secret");
        Share(idleCtx, host, idle.SessionId);
        var empty = idleCtx.Render<WorkbenchLayout>(parameters => parameters.Add(component => component.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>主欄</p>"))));
        Assert.IsTrue(empty.Markup.Contains("尚未指派工作角色。", StringComparison.Ordinal));
        Assert.AreEqual(idleId, idle.Me.Id);

        using var assign = new BunitContext();
        Share(assign, host, admin.SessionId);
        var page = assign.Render<Assignments>();
        page.Find("select").Change(id);
        page.FindAll("input[type=checkbox]")[0].Change(true);
        page.FindAll("button").First(button => button.TextContent.Contains("儲存", StringComparison.Ordinal)).Click();
        page.Find(".dialog").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        var untouched = await admin.GetAsync("api/assignments/" + id);
        Assert.IsFalse(untouched.GetProperty("workRoles").EnumerateArray().Any(role => role.GetString() == WorkRoles.Planner));
        page.FindAll("input[type=checkbox]")[0].Change(true);
        page.FindAll("button").First(button => button.TextContent.Contains("儲存", StringComparison.Ordinal)).Click();
        page.FindAll("button").First(button => button.TextContent == "確認").Click();
        var saved = await admin.GetAsync("api/assignments/" + id);
        Assert.IsTrue(saved.GetProperty("workRoles").EnumerateArray().Any(role => role.GetString() == WorkRoles.Planner));

        using var intent = new BunitContext();
        Share(intent, host, terminal.SessionId);
        var talk = intent.Render<Intent>();
        talk.Find("input").Change("inventory inventory_pg 月報 Name one");
        await talk.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.IsTrue(talk.Markup.Contains("ambiguous", StringComparison.Ordinal));
        Assert.IsTrue(talk.Find("button.confirm").HasAttribute("disabled"));
        Assert.IsTrue(talk.Find(".reason").TextContent.Length > 0);
        var fills = host.Surface.Calls.Count(call => call is "query" or "count");
        talk.Find("input").Change("inventory 月報 Name one");
        await talk.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        talk.Find("button.confirm").Click();
        Assert.IsTrue(host.Surface.Calls.Count(call => call is "query" or "count") > fills);
        var snapshot = new Uri(intent.Services.GetRequiredService<NavigationManager>().Uri).Segments[^1];
        using var preview = new BunitContext();
        Share(preview, host, terminal.SessionId);
        var sheet = preview.Render<Preview>(parameters => parameters.Add(component => component.SnapshotId, snapshot));
        Assert.IsTrue(sheet.Markup.Contains("one", StringComparison.Ordinal));
        sheet.FindAll("button").First(button => button.TextContent.Contains("匯出 CSV", StringComparison.Ordinal)).Click();
        Assert.IsTrue(sheet.Markup.Contains("one", StringComparison.Ordinal));
        sheet.Find("input").Change("加入");
        sheet.FindAll("button").First(button => button.TextContent.Contains("加入關鍵字", StringComparison.Ordinal)).Click();
        Assert.IsTrue(sheet.Markup.Contains("已加入關鍵字", StringComparison.Ordinal));
        _ = template;
    }

    [TestMethod]
    public async Task Avalonia_save_keeps_groups_and_tokens_stay_light()
    {
        await using var host = new WorkbenchHost();
        var admin = await host.LoginAsync();
        var group = (await admin.SendAsync(HttpMethod.Post, "api/groups", new GroupWriteRequest { Name = "月報" })).GetProperty("id").GetString()!;
        var id = (await admin.SendAsync(HttpMethod.Post, "api/operators", new OperatorWriteRequest { DisplayName = "林可", Secret = "user-secret" })).GetProperty("id").GetString()!;
        await admin.SendAsync(HttpMethod.Put, "api/assignments/" + id, new AssignmentDto
        {
            WorkRoles = [WorkRoles.Planner],
            Groups = [new GroupRoleDto { GroupId = group, Role = GroupRoles.Design }],
        });
        var api = new ShellApi(host.CreateClient());
        Assert.IsFalse(await api.LoginAsync("沒有這個人", "nope"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(api.LastError));
        Assert.IsTrue(await api.LoginAsync("管理者", "seed-secret"));
        var current = await api.GetAssignmentsAsync(id);
        var savedRoles = await api.SaveAssignmentsAsync(id, current);
        Assert.AreEqual(group, savedRoles.Groups[0].GroupId);
        Assert.AreEqual(GroupRoles.Design, savedRoles.Groups[0].Role);
        var kept = await admin.GetAsync("api/assignments/" + id);
        Assert.AreEqual(group, kept.GetProperty("groups")[0].GetProperty("groupId").GetString());
        Assert.AreEqual(GroupRoles.Design, kept.GetProperty("groups")[0].GetProperty("role").GetString());

        var windowSource = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Workbench.Avalonia", "MainWindow.cs")));
        Assert.IsTrue(windowSource.Contains("MinWidth = 1100", StringComparison.Ordinal));
        Assert.IsTrue(windowSource.Contains("MinHeight = 720", StringComparison.Ordinal));
        Assert.IsTrue(windowSource.Contains("SaveRolesAsync", StringComparison.Ordinal));
        Assert.IsTrue(windowSource.Contains("GetAssignmentsAsync", StringComparison.Ordinal));
        var css = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Workbench.Web", "wwwroot", "tokens.css")));
        Assert.IsTrue(css.Contains(DesignTokens.SurfaceBase, StringComparison.Ordinal));
        Assert.IsTrue(css.Contains(DesignTokens.Action, StringComparison.Ordinal));
        Assert.IsTrue(css.Contains(".reason { font-size: var(--body);", StringComparison.Ordinal));
        Assert.IsFalse(css.Contains("#2E7D32", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Command_line_does_not_clear_the_model_key()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "model.json"), """
            {"Model":{"enabled":false,"placement":"local","endpoint":"http://127.0.0.1:9","modelId":"unit","apiKey":"file-key-value"}}
            """);
        var database = Path.Combine(root, "workbench.db");
        var dll = typeof(Program).Assembly.Location;
        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(dll);
        psi.ArgumentList.Add("--contentRoot");
        psi.ArgumentList.Add(root);
        psi.ArgumentList.Add("--urls");
        psi.ArgumentList.Add("http://127.0.0.1:5217");
        psi.ArgumentList.Add("--Model:apiKey=");
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["Workbench__SqlitePath"] = database;
        psi.Environment["Workbench__InitialAdmin__DisplayName"] = "管理者";
        psi.Environment["Workbench__InitialAdmin__Password"] = "seed-secret";
        using var process = System.Diagnostics.Process.Start(psi)!;
        try
        {
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (!process.HasExited && !ready.IsCancellationRequested)
            {
                try
                {
                    using var probe = new HttpClient();
                    var response = await probe.GetAsync("http://127.0.0.1:5217/api/me", ready.Token);
                    if ((int)response.StatusCode is 401 or 503)
                        break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(200, ready.Token);
                }
            }

            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5217/") };
            var login = await client.PostAsJsonAsync("api/sessions", new LoginRequest { DisplayName = "管理者", Secret = "seed-secret" }, Json);
            Assert.AreEqual(System.Net.HttpStatusCode.OK, login.StatusCode);
            var session = await login.Content.ReadFromJsonAsync<SessionResponse>(Json);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session!.SessionId);
            var model = await client.GetFromJsonAsync<ModelDto>("api/model", Json);
            Assert.IsTrue(model!.ApiKeySet);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    private static void Share(BunitContext context, WorkbenchHost host, string sessionId)
    {
        context.AddBunitPersistentComponentState();
        var sessions = host.Services.GetRequiredService<SessionService>();
        var profiles = host.Services.GetRequiredService<Profiles>();
        var circuit = new CircuitSession();
        circuit.Adopt(sessionId);
        context.Services.AddSingleton(circuit);
        context.Services.AddSingleton(new OperatorGate(circuit, sessions, profiles));
        context.Services.AddSingleton(sessions);
        context.Services.AddSingleton(profiles);
        context.Services.AddSingleton(host.Services.GetRequiredService<LivePreviewService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<RoleRailService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<AccountService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<TemplateLibraryService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<SuggestionService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<IntentService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<FavoriteService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<KeywordService>());
        context.Services.AddSingleton(host.Services.GetRequiredService<SnapshotExportService>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
