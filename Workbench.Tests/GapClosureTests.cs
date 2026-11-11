using Bunit;
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
        public OperatorRecord? FindById(string id) => _rows.FirstOrDefault(row => row.Id == id);
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) => _entries[operatorId] = entries.ToList();
        public IReadOnlyList<string> EntriesFor(string operatorId) => _entries.TryGetValue(operatorId, out var found) ? found : [];
    }
}
