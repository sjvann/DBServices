using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UglyToad.PdfPig;
using Workbench;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class GapClosureTests
{
    [TestMethod]
    public void Missing_font_file_does_not_throw_and_pdf_bytes_are_produced()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-msjh-" + Guid.NewGuid().ToString("N") + ".ttc");
        Assert.IsFalse(File.Exists(missing));
        var family = SnapshotPdfWriter.ResolveFontFamily(missing);
        Assert.AreEqual(SnapshotPdfWriter.BuiltInFamily, family);
        var field = typeof(SnapshotPdfWriter).GetField("FontFamilyName", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var previous = field.GetValue(null);
        field.SetValue(null, family);
        try
        {
            var bytes = new SnapshotPdfWriter().Write(new PrintReport
            {
                Blocks = [new PrintBlock { Type = BlockKinds.Title, Text = "標題" }],
            });
            Assert.IsGreaterThan(4, bytes.Length);
            using var document = PdfDocument.Open(bytes);
            Assert.AreEqual(1, document.NumberOfPages);
        }
        finally
        {
            field.SetValue(null, previous);
        }
    }

    [TestMethod]
    public async Task Preview_shows_the_server_message_and_no_business_rows()
    {
        await using var context = new BunitContext();
        var operators = new Operators();
        operators.Add(new OperatorRecord { Id = "analyst", DisplayName = "林可", IsAdmin = false });
        operators.ReplaceEntries("analyst", [EntryNames.Analyst]);
        var sessions = new MemorySessionStore();
        var hasher = new Pbkdf2PasswordHasher();
        var sessionService = new SessionService(operators, hasher, sessions, new SeedAdminService(operators, hasher));
        var circuit = new CircuitSession();
        circuit.Adopt(sessions.Create("analyst"));
        var profiles = new Profiles(operators);
        var catalog = new RecordingCatalog();
        await catalog.SaveAsync(new ViewDocumentDto
        {
            Id = "ship-1",
            Name = "出貨",
            Purpose = "說明出貨",
            Confirmed = true,
            Sources = ["products"],
            Columns = [new ViewColumnDto { Name = "Name", Source = "products" }],
        }, "analyst", CancellationToken.None);
        var browser = new PreviewBrowser();
        var connections = new RecordingConnections();
        var environments = new EnvironmentService(
            connections,
            new RecordingConnectionAdmin(),
            new RecordingModel(true),
            new RecordingModelEndpoint(),
            profiles);
        context.Services.AddSingleton(new OperatorGate(circuit, sessionService, profiles));
        context.Services.AddSingleton(new ViewService(profiles, catalog, new EmptyStyles(), environments));
        context.Services.AddSingleton(new SchemaService(profiles, browser, connections, environments));
        context.Services.AddSingleton(environments);

        var cut = context.Render<Workbench.Web.Components.Views>();
        await cut.FindAll("button").Single(button => button.TextContent.Contains("出貨", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        var generation = cut.Instance.GetType().GetField("_previewGeneration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(cut.Instance);
        var run = cut.Instance.GetType().GetMethod("RunPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        await cut.InvokeAsync(() => (Task)run!.Invoke(cut.Instance, [generation])!);

        var confirmed = cut.FindAll("label").Single(label => label.TextContent.Contains("解說已確認", StringComparison.Ordinal));
        Assert.IsTrue(confirmed.QuerySelector("input")!.HasAttribute("checked"));
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());
        Assert.IsTrue(catalog.Stored("ship-1")!.Confirmed);
        Assert.Contains("不讀取業務列。", cut.Markup);
        Assert.IsFalse(cut.Markup.Contains("沒有符合的資料", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("業務列標記", StringComparison.Ordinal));
        Assert.IsGreaterThan(0, browser.PreviewCalls);
    }

    [TestMethod]
    public async Task Governance_entry_save_failure_restores_checks_and_shows_two_lines()
    {
        var store = new Operators();
        store.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴", IsAdmin = false });
        store.ReplaceEntries("fang", [EntryNames.Analyst]);
        store.Add(new OperatorRecord { Id = "lin", DisplayName = "林可", IsAdmin = false });
        store.ReplaceEntries("lin", [EntryNames.Terminal]);
        store.Add(new OperatorRecord { Id = "admin", DisplayName = "管理者", IsAdmin = true });
        var people = store.Count();
        await using var context = SignedIn(store, "admin");

        var cut = context.Render<Workbench.Web.Components.Entries>();
        Assert.IsTrue(Checked(cut, "系統分析師"));
        Assert.IsFalse(Checked(cut, "資料治理"));
        await Check(cut, "資料治理", true);
        Assert.IsTrue(Checked(cut, "資料治理"));

        store.HideOnFind = "fang";
        await cut.Find("button.confirm").ClickAsync(new MouseEventArgs());

        Assert.IsTrue(Checked(cut, "系統分析師"));
        Assert.IsFalse(Checked(cut, "資料治理"));
        Assert.IsFalse(Checked(cut, "設計師"));
        Assert.IsFalse(Checked(cut, "終端使用者"));
        var alert = cut.Find(".status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        var lines = alert.QuerySelectorAll("span");
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("找不到操作者。", lines[0].TextContent.Trim());
        Assert.AreEqual("forbidden", lines[1].TextContent.Trim());
        Assert.IsTrue(lines[1].ClassList.Contains("code"));
        Assert.IsTrue(alert.InnerHtml.IndexOf("找不到操作者。", StringComparison.Ordinal) < alert.InnerHtml.IndexOf("forbidden", StringComparison.Ordinal));

        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "lin" });
        Assert.AreEqual(0, cut.FindAll(".status-message").Count);
        Assert.IsTrue(Checked(cut, "終端使用者"));
        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "fang" });
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("StackTrace", StringComparison.Ordinal));
        CollectionAssert.AreEquivalent(new[] { EntryNames.Analyst }, store.EntriesFor("fang").ToArray());
        Assert.AreEqual(people, store.Count());

        store.HideOnFind = null;
        await Check(cut, "資料治理", true);
        await cut.Find("button.confirm").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(0, cut.FindAll(".status-message").Count);
        Assert.IsTrue(Checked(cut, "資料治理"));
        Assert.IsTrue(Checked(cut, "系統分析師"));
        CollectionAssert.AreEquivalent(
            new[] { EntryNames.Analyst, EntryNames.Governance },
            store.EntriesFor("fang").ToArray());
        Assert.AreEqual(people, store.Count());

        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "lin" });
        Assert.IsFalse(Checked(cut, "資料治理"));
        Assert.IsTrue(Checked(cut, "終端使用者"));
        await cut.Find("select").ChangeAsync(new ChangeEventArgs { Value = "fang" });
        Assert.IsTrue(Checked(cut, "資料治理"));
        Assert.IsTrue(Checked(cut, "系統分析師"));
        Assert.IsFalse(Checked(cut, "終端使用者"));
    }

    [TestMethod]
    public async Task Governance_entry_empty_station_shows_title_without_reading_rows()
    {
        var store = new Operators();
        store.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴", IsAdmin = false });
        store.ReplaceEntries("fang", [EntryNames.Governance]);
        var caller = new RecordingCaller();
        await using var context = SignedIn(store, "fang", caller);

        var cut = context.Render<Workbench.Web.Components.Governance>();
        Assert.AreEqual("註記", cut.Find("h1").TextContent.Trim());
        Assert.AreEqual(0, cut.FindAll("[role='alert']").Count);
        Assert.AreEqual(1, cut.FindAll("input").Count);
        Assert.AreEqual("file", cut.Find("input").GetAttribute("type"));
        Assert.AreEqual(0, cut.FindAll("select, textarea, table").Count);
        Assert.IsFalse(cut.Markup.Contains("沒有權限", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cut.Markup.Contains("連線字串", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("ROWMARKER", StringComparison.Ordinal));
        Assert.AreEqual(0, caller.Count);
    }

    [TestMethod]
    public async Task Governance_entry_denied_pages_omit_the_station_and_the_checkboxes()
    {
        var store = new Operators();
        store.Add(new OperatorRecord { Id = "lin", DisplayName = "林可", IsAdmin = false });
        store.ReplaceEntries("lin", [EntryNames.Analyst]);
        await using var context = SignedIn(store, "lin");
        context.AddBunitPersistentComponentState();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/governance");

        var entries = context.Render<Workbench.Web.Components.Entries>();
        Assert.AreEqual("沒有權限。", entries.Find("[role='alert']").TextContent.Trim());
        Assert.AreEqual(0, entries.FindAll("input").Count);
        Assert.IsFalse(entries.Markup.Contains("資料治理", StringComparison.Ordinal));

        var station = context.Render<Workbench.Web.Components.Governance>();
        Assert.AreEqual("沒有權限。", station.Find("[role='alert']").TextContent.Trim());
        Assert.AreEqual(0, station.FindAll("h1").Count);
        Assert.IsFalse(station.Markup.Contains("註記", StringComparison.Ordinal));
        Assert.IsFalse(station.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.AreEqual(0, station.FindAll("input, select, textarea, table").Count);

        var rail = context.Render<Workbench.Web.Components.WorkbenchLayout>();
        Assert.AreEqual(0, rail.FindAll(".rail a").Count);
        Assert.IsFalse(rail.Markup.Contains("註記", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Governance_entry_rail_shows_only_annotation()
    {
        var alone = new Operators();
        alone.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴", IsAdmin = false });
        alone.ReplaceEntries("fang", [EntryNames.Governance]);
        await using (var context = SignedIn(alone, "fang"))
        {
            context.AddBunitPersistentComponentState();
            context.Services.GetRequiredService<NavigationManager>().NavigateTo("/governance");
            var cut = context.Render<Workbench.Web.Components.WorkbenchLayout>();
            CollectionAssert.AreEqual(new[] { "註記" }, RailLinks(cut).ToArray());
            var link = cut.Find(".rail a");
            Assert.IsTrue(link.ClassList.Contains("active"));
            Assert.IsTrue(link.ClassList.Contains("rail-link-active"));
            Assert.AreEqual("/governance", link.GetAttribute("href"));
            Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        }

        var both = new Operators();
        both.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴", IsAdmin = false });
        both.ReplaceEntries("fang", [EntryNames.Analyst, EntryNames.Governance]);
        await using var shared = SignedIn(both, "fang");
        shared.Services.AddSingleton(new EnvironmentService(
            new RecordingConnections(),
            new RecordingConnectionAdmin(),
            new RecordingModel(true),
            new RecordingModelEndpoint(),
            new Profiles(both)));
        shared.AddBunitPersistentComponentState();
        var nav = shared.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/governance");

        var stations = shared.Render<Workbench.Web.Components.Stations>();
        var choice = stations.FindAll("a").Single(link => link.TextContent.Contains("資料治理", StringComparison.Ordinal));
        Assert.AreEqual("/governance", choice.GetAttribute("href"));
        Assert.IsTrue(stations.FindAll("a").Any(link => link.GetAttribute("href") == "/views"));

        var rail = shared.Render<Workbench.Web.Components.WorkbenchLayout>();
        CollectionAssert.AreEquivalent(new[] { "註記", "換工作" }, RailLinks(rail).ToArray());
        var note = rail.FindAll(".rail a").Single(link => link.TextContent.Trim() == "註記");
        Assert.IsTrue(note.ClassList.Contains("active"));
        Assert.IsTrue(note.ClassList.Contains("rail-link-active"));
        Assert.IsFalse(RailLinks(rail).Contains("檢視"));

        await rail.InvokeAsync(() => nav.NavigateTo("/views"));
        rail.WaitForAssertion(() => CollectionAssert.AreEquivalent(new[] { "檢視", "換工作" }, RailLinks(rail).ToArray()));
        Assert.IsFalse(RailLinks(rail).Contains("註記"));
        Assert.IsFalse(RailLinks(rail).Contains("樣式"));
        Assert.IsFalse(RailLinks(rail).Contains("產生報表"));

        await rail.InvokeAsync(() => nav.NavigateTo("/governance"));
        rail.WaitForAssertion(() => CollectionAssert.AreEquivalent(new[] { "註記", "換工作" }, RailLinks(rail).ToArray()));
        note = rail.FindAll(".rail a").Single(link => link.TextContent.Trim() == "註記");
        Assert.IsTrue(note.ClassList.Contains("active"));
        Assert.IsTrue(note.ClassList.Contains("rail-link-active"));
        Assert.IsFalse(RailLinks(rail).Contains("檢視"));
    }

    private static BunitContext SignedIn(Operators store, string operatorId, RecordingCaller? caller = null)
    {
        var context = new BunitContext();
        var sessions = new MemorySessionStore();
        var hasher = new Pbkdf2PasswordHasher();
        var profiles = new Profiles(store);
        var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
        var circuit = new CircuitSession();
        circuit.Adopt(sessions.Create(operatorId));
        context.Services.AddSingleton(circuit);
        context.Services.AddSingleton(sessionService);
        context.Services.AddSingleton(new OperatorGate(circuit, sessionService, profiles));
        context.Services.AddSingleton(new OperatorService(store, hasher, profiles));
        if (caller is not null)
            context.Services.AddSingleton<IViewCaller>(caller);
        context.Services.AddSingleton<IAnnotationCatalog>(new RecordingAnnotations());
        return context;
    }

    private static List<string> RailLinks(IRenderedComponent<Workbench.Web.Components.WorkbenchLayout> cut)
        => cut.FindAll(".rail a").Select(link => link.TextContent.Trim()).ToList();

    private static bool Checked(IRenderedComponent<Workbench.Web.Components.Entries> cut, string name)
        => cut.FindAll("label").Single(label => label.TextContent.Contains(name, StringComparison.Ordinal))
            .QuerySelector("input")!.HasAttribute("checked");

    private static Task Check(IRenderedComponent<Workbench.Web.Components.Entries> cut, string name, bool selected)
        => cut.FindAll("label").Single(label => label.TextContent.Contains(name, StringComparison.Ordinal))
            .QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = selected });

    private sealed class PreviewBrowser : ISchemaBrowser
    {
        public int PreviewCalls { get; private set; }

        public Task<SchemaReadResult> GraphAsync(string connectionId, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(SchemaReadResult.FromGraph(new SchemaGraphDto
            {
                Tables =
                [
                    new SchemaTableDto
                    {
                        Name = "products",
                        Columns = [new SchemaColumnDto { Name = "Name" }],
                    },
                ],
            }));

        public Task<SchemaReadResult> PreviewAsync(
            string connectionId,
            ViewDocumentDto document,
            IReadOnlyDictionary<string, string> parameters,
            string callerId,
            CancellationToken cancellationToken)
        {
            PreviewCalls++;
            return Task.FromResult(SchemaReadResult.Fail("preview_unavailable", "不讀取業務列。"));
        }
    }

    private sealed class EmptyStyles : IStyleStore
    {
        public void Add(StyleRecord record) { }
        public bool TryUpdate(StyleRecord record) => false;
        public StyleRecord? Find(string id) => null;
        public IReadOnlyList<StyleRecord> ReadableBy(string operatorId) => [];
        public bool References(string viewId) => false;
        public bool Delete(string id, string ownerId) => false;
    }

    private sealed class Operators : IOperatorStore
    {
        private readonly List<OperatorRecord> _rows = [];
        private readonly Dictionary<string, List<string>> _entries = [];

        public int Count() => _rows.Count;
        public void Add(OperatorRecord record) => _rows.Add(record);
        public OperatorRecord? FindByName(string name) => _rows.FirstOrDefault(row => row.DisplayName == name);
        public string? HideOnFind { get; set; }

        public OperatorRecord? FindById(string id)
        {
            if (HideOnFind is not null && string.Equals(id, HideOnFind, StringComparison.Ordinal))
                return null;
            return _rows.FirstOrDefault(row => row.Id == id);
        }
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) => _entries[operatorId] = entries.ToList();
        public IReadOnlyList<string> EntriesFor(string operatorId) => _entries.TryGetValue(operatorId, out var found) ? found : [];
    }
}
