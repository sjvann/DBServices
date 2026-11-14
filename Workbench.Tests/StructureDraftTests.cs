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
        Assert.AreEqual("已寫", nameCells[3].TextContent.Trim());
        Assert.IsFalse(nameCells[3].ClassList.Contains("unnoted"));
        Assert.AreEqual("", nameCells[4].TextContent.Trim());
        var link = cut.Find(".link-editor");
        Assert.AreEqual(0, link.QuerySelectorAll("button").Length);
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
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
        var alert = cut.Find(".status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        Assert.IsTrue(alert.InnerHtml.IndexOf(WorkbenchMessages.DraftExists, StringComparison.Ordinal) < alert.InnerHtml.IndexOf("draft_exists", StringComparison.Ordinal));
        Assert.AreEqual(1, annotations.Commits);
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
        var before = JsonSerializer.Serialize(host.Annotations.Draft);
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var again = await person.RawAsync(HttpMethod.Post, "api/annotations/read-structure");
        Assert.AreEqual(400, again.Status, again.Text);
        Assert.AreEqual("draft_exists", again.Body.GetProperty("code").GetString());
        Assert.AreEqual(WorkbenchMessages.DraftExists, again.Body.GetProperty("message").GetString());
        Assert.AreEqual(before, JsonSerializer.Serialize(host.Annotations.Draft));
        Assert.AreEqual(0, host.Annotations.Commits);
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
            Assert.AreNotEqual("確認", text);
            Assert.AreNotEqual("匯出", text);
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
        public string Path { get; private set; } = "";
        public string RecordedBody { get; private set; } = "";
        public HttpContentHeaders? ContentHeaders { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
            ContentHeaders = request.Content?.Headers;
            RecordedBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }
}
