using System.Net;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using DbServices.Mcp;
using Microsoft.AspNetCore.Components;
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
public class DbmlConfirmTests
{
    [TestMethod]
    public void Dbml_confirm_sample_tables_match_the_imported_workbook()
    {
        var bytes = File.ReadAllBytes(Sample("Samples.xlsx"));
        var sample = File.ReadAllText(Sample("LH_TXN_MLST.sample.dbml")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var parsed = DictionaryWorkbook.Read("C:\\inbox\\Samples.xlsx", bytes);
        Assert.IsTrue(parsed.Ok, parsed.Error?.Message);
        var json = JsonSerializer.Serialize(parsed.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var package = JsonSerializer.Deserialize<AnnotationPackage>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        })!;
        var root = Path.Combine(Path.GetTempPath(), "wb-dbml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "annotations.json");
            var catalog = new AnnotationCatalog(path);
            var imported = catalog.Import(package);
            Assert.IsTrue(imported.Ok, imported.Message);
            var before = File.ReadAllBytes(path);
            var text = catalog.DraftDbml();
            Assert.IsNotNull(text);
            Assert.AreEqual(TableBlock(sample), TableBlock(text));
            Assert.DoesNotContain("由 Requirements/Samples.xlsx 產生", text, StringComparison.Ordinal);
            Assert.DoesNotContain("connectionString", text, StringComparison.OrdinalIgnoreCase);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            using var stored = JsonDocument.Parse(before);
            Assert.AreEqual("", stored.RootElement[0].GetProperty("dbml").GetString());

            imported.Package!.Purpose = "清單異動的整包說明";
            var saved = catalog.Save(imported.Package);
            Assert.IsTrue(saved.Ok, saved.Message);
            var noted = catalog.DraftDbml()!;
            Assert.IsTrue(noted.StartsWith("Project Samples {\n  Note: '清單異動的整包說明'\n}\n\n", StringComparison.Ordinal), noted);
            Assert.AreEqual(TableBlock(sample), TableBlock(noted));
            using var after = JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual("", after.RootElement[0].GetProperty("dbml").GetString());

            var linked = AnnotationCatalog.Render(new AnnotationPackage
            {
                Title = "關聯",
                Purpose = "有說明",
                Tables =
                [
                    new AnnotationTable
                    {
                        Name = "parent",
                        Columns = [new AnnotationColumn { Name = "Id", Type = "int", NotNull = true }],
                    },
                ],
                Links =
                [
                    new AnnotationLink { From = "parent", FromColumn = "Id", To = "parent", ToColumn = "Id", Origin = "declared", Note = "   " },
                    new AnnotationLink { From = "parent", FromColumn = "Id", To = "parent", ToColumn = "Id", Origin = "declared", Note = "理由" },
                ],
            });
            Assert.Contains("Ref: parent.Id > parent.Id\n", linked, StringComparison.Ordinal);
            Assert.Contains("Ref: parent.Id > parent.Id [note: '理由']", linked, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Dbml_confirm_export_shows_the_block_and_download_name()
    {
        var annotations = new RecordingAnnotations
        {
            Draft = Draft("報表/草稿*"),
            DraftDbml = "Table LH_TXN_MLST {\n}\n",
        };
        await using var context = Station(annotations);
        context.JSInterop.SetupVoid("eval", _ => true);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("報表/草稿*", StringComparison.Ordinal)));
        await cut.Find("#annotation-export").ClickAsync(new MouseEventArgs());
        var script = context.JSInterop.Invocations.Single(call => call.Identifier == "eval").Arguments[0] as string ?? "";
        var payload = "Table LH_TXN_MLST {\n}\n";
        Assert.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)), script, StringComparison.Ordinal);
        Assert.Contains("\"報表草稿.dbml\"", script, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.DbmlAttempts));
        var block = cut.Find("[aria-label='DBML']");
        Assert.IsTrue(block.HasAttribute("readonly"));
        Assert.AreEqual("Table LH_TXN_MLST {\n}\n", block.TextContent);
        Assert.AreEqual("報表草稿.dbml", block.GetAttribute("data-download"));
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("報表/草稿*", StringComparison.Ordinal));
        Assert.AreEqual("", annotations.Draft!.Dbml);
        Assert.AreEqual(0, annotations.ConfirmAttempts);

        annotations.Draft = Draft("*");
        annotations.Draft.Title = "*";
        var again = context.Render<Workbench.Web.Components.Governance>();
        again.WaitForAssertion(() => Assert.IsTrue(again.Find(".package-bar .code").TextContent.Contains("*", StringComparison.Ordinal)));
        await again.Find("#annotation-export").ClickAsync(new MouseEventArgs());
        again.WaitForAssertion(() => Assert.AreEqual("annotation.dbml", again.Find("[aria-label='DBML']").GetAttribute("data-download")));

        var css = File.ReadAllText(FindUp("Workbench.Web", "wwwroot", "tokens.css"));
        var dbml = css.Split(".dbml-block", 2)[1].Split('}', 2)[0];
        var result = css.Split(".result-line", 2)[1].Split('}', 2)[0];
        Assert.IsTrue(dbml.Contains("var(--code)", StringComparison.Ordinal));
        Assert.IsTrue(dbml.Contains("var(--surface-raised)", StringComparison.Ordinal));
        Assert.IsTrue(dbml.Contains("var(--border-hairline)", StringComparison.Ordinal));
        Assert.IsTrue(dbml.Contains("var(--ink-primary)", StringComparison.Ordinal));
        Assert.IsFalse(dbml.Contains("var(--danger)", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("var(--surface-raised)", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("var(--border-hairline)", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("var(--ink-primary)", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("var(--danger)", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Dbml_confirm_escape_and_cancel_leave_the_draft()
    {
        var annotations = new RecordingAnnotations
        {
            Draft = Draft("草稿"),
            Active = new AnnotationPackageDto { Id = "old", Title = "舊包", Status = "confirmed", Active = true, Dbml = "Table old {\n}\n" },
        };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsFalse(cut.Find("#annotation-confirm").HasAttribute("disabled")));
        await cut.Find("#annotation-confirm").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("確認這份草稿？將取代舊包。", cut.Find(".dialog p").TextContent.Trim());
        await cut.InvokeAsync(() => DialogHost.Escape());
        cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".dialog").Count));
        Assert.AreEqual(0, annotations.ConfirmAttempts);
        Assert.AreEqual("draft", annotations.Draft!.Status);

        await cut.Find("#annotation-confirm").ClickAsync(new MouseEventArgs());
        await cut.Find(".dialog .secondary").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".dialog").Count));
        Assert.AreEqual(0, annotations.ConfirmAttempts);
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Dbml_confirm_success_uses_status_and_names_the_replaced_package()
    {
        var annotations = new RecordingAnnotations
        {
            Draft = Draft("新包"),
            DraftDbml = "Table kept {\n}\n",
            Active = new AnnotationPackageDto { Id = "old", Title = "舊包", Status = "confirmed", Active = true, Dbml = "Table old {\n}\n" },
        };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsFalse(cut.Find("#annotation-confirm").HasAttribute("disabled")));
        await cut.Find("#annotation-confirm").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("確認這份草稿？將取代舊包。", cut.Find(".dialog p").TextContent.Trim());
        await cut.Find(".dialog .confirm").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.ConfirmAttempts));
        var line = cut.Find(".result-line");
        Assert.AreEqual("status", line.GetAttribute("role"));
        Assert.AreEqual("已確認。設計目錄改看這一包。取代了舊包。", line.TextContent.Trim());
        Assert.IsFalse(line.ClassList.Contains("status-message"));
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("已確認", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll(".dialog").Count);
        Assert.IsNull(annotations.Draft);
        Assert.IsTrue(annotations.Active!.Active);
        Assert.AreEqual("新包", annotations.Active.Title);

        annotations.Draft = null;
        var plain = new RecordingAnnotations { Draft = Draft("只有草稿") };
        await using var plainContext = Station(plain);
        var plainCut = plainContext.Render<Workbench.Web.Components.Governance>();
        plainCut.WaitForAssertion(() => Assert.IsFalse(plainCut.Find("#annotation-confirm").HasAttribute("disabled")));
        await plainCut.Find("#annotation-confirm").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("確認這份草稿？", plainCut.Find(".dialog p").TextContent.Trim());
        await plainCut.Find(".dialog .confirm").ClickAsync(new MouseEventArgs());
        plainCut.WaitForAssertion(() => Assert.AreEqual("已確認。設計目錄改看這一包。", plainCut.Find(".result-line").TextContent.Trim()));
    }

    [TestMethod]
    public async Task Dbml_confirm_readonly_package_exports_stored_dbml_and_edit_forks_once()
    {
        var retained = new AnnotationPackageDto
        {
            Id = "old",
            Title = "被取代",
            Status = "confirmed",
            Active = false,
            Dbml = "STORED-DBML",
            Tables = [new AnnotationTableDto { Name = "legacy", Columns = [new AnnotationColumnDto { Name = "Id", Type = "int" }] }],
        };
        var active = new AnnotationPackageDto
        {
            Id = "live",
            Title = "啟用包",
            Status = "confirmed",
            Active = true,
            Purpose = "原說明",
            Dbml = "ACTIVE-DBML",
            Tables = [new AnnotationTableDto { Name = "parent", Note = "原表註", Columns = [new AnnotationColumnDto { Name = "Id", Type = "int", Note = "原欄註" }] }],
        };
        var annotations = new RecordingAnnotations { Active = active };
        annotations.Others.Add(retained);
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("已確認", StringComparison.Ordinal)));
        await cut.FindAll(".package-bar button").Single(button => button.TextContent.Trim() == "被取代").ClickAsync(new MouseEventArgs());
        Assert.IsTrue(cut.Find("#annotation-import").HasAttribute("disabled"));
        Assert.IsTrue(cut.Find("#annotation-read").HasAttribute("disabled"));
        Assert.IsTrue(cut.Find("#annotation-confirm").HasAttribute("disabled"));
        Assert.IsTrue(cut.Find("[aria-label='整包功能說明']").HasAttribute("disabled"));
        Assert.IsFalse(cut.Find("#annotation-export").HasAttribute("disabled"));
        await cut.Find("#annotation-export").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual("STORED-DBML", cut.Find("[aria-label='DBML']").TextContent));
        Assert.AreEqual(0, annotations.DbmlAttempts);
        Assert.AreEqual(0, annotations.SaveAttempts);
        Assert.AreEqual("原說明", annotations.Active!.Purpose);

        await cut.FindAll(".package-bar button").Single(button => button.TextContent.Trim() == "啟用包").ClickAsync(new MouseEventArgs());
        Assert.IsFalse(cut.Find("[aria-label='整包功能說明']").HasAttribute("disabled"));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "另寫草稿" });
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.SaveAttempts));
        Assert.AreEqual("原說明", annotations.Active.Purpose);
        Assert.AreEqual("confirmed", annotations.Active.Status);
        Assert.IsTrue(annotations.Active.Active);
        Assert.AreEqual("另寫草稿", annotations.Draft!.Purpose);
        Assert.AreEqual("draft", annotations.Draft.Status);
        Assert.IsFalse(annotations.Draft.Active);
        Assert.AreNotEqual("live", annotations.Draft.Id);
        Assert.AreEqual("int", annotations.Draft.Tables[0].Columns[0].Type);
        Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal));

        var occupied = new RecordingAnnotations { Draft = Draft("工作中"), Active = active };
        await using var occupiedContext = Station(occupied);
        var occupiedCut = occupiedContext.Render<Workbench.Web.Components.Governance>();
        occupiedCut.WaitForAssertion(() => Assert.IsTrue(occupiedCut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal)));
        await occupiedCut.FindAll(".package-bar button").Single(button => button.TextContent.Trim() == "啟用包").ClickAsync(new MouseEventArgs());
        Assert.IsTrue(occupiedCut.Find("[aria-label='整包功能說明']").HasAttribute("disabled"));
        Assert.AreEqual(0, occupied.SaveAttempts);
        Assert.AreEqual("原說明", occupied.Active!.Purpose);
        Assert.IsTrue(occupiedCut.FindAll(".package-bar button").Any(button => button.TextContent.Trim() == "工作中"));
    }

    [TestMethod]
    public async Task Dbml_confirm_refused_confirm_shows_alert_and_keeps_the_draft()
    {
        var annotations = new RecordingAnnotations
        {
            Draft = Draft("草稿"),
            ConfirmFailure = Workbench.Core.Ports.AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。"),
        };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsFalse(cut.Find("#annotation-confirm").HasAttribute("disabled")));
        await cut.Find("#annotation-confirm").ClickAsync(new MouseEventArgs());
        await cut.Find(".dialog .confirm").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
        Assert.AreEqual("alert", cut.Find(".status-message").GetAttribute("role"));
        Assert.AreEqual("draft", annotations.Draft!.Status);
        Assert.AreEqual(0, cut.FindAll(".result-line").Count);
    }

    [TestMethod]
    public async Task Dbml_confirm_failed_fork_restores_the_purpose()
    {
        var annotations = new RecordingAnnotations
        {
            Active = new AnnotationPackageDto
            {
                Id = "live",
                Title = "啟用包",
                Status = "confirmed",
                Active = true,
                Purpose = "原說明",
                Tables = [new AnnotationTableDto { Name = "parent", Columns = [new AnnotationColumnDto { Name = "Id", Type = "int", Note = "原欄註" }] }],
            },
            SaveFailure = Workbench.Core.Ports.AnnotationWriteResult.Fail("execution_failed", "沒有保存。"),
        };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("原說明", Field(cut.Find("[aria-label='整包功能說明']"))));
        await cut.Find("[aria-label='整包功能說明']").InputAsync(new ChangeEventArgs { Value = "改掉" });
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
        Assert.AreEqual("alert", cut.Find(".status-message").GetAttribute("role"));
        Assert.AreEqual("原說明", Field(cut.Find("[aria-label='整包功能說明']")));
        Assert.IsNull(annotations.Draft);
        Assert.AreEqual("原說明", annotations.Active!.Purpose);
    }

    [TestMethod]
    public async Task Dbml_confirm_column_note_forks_without_changing_the_active_note()
    {
        var annotations = new RecordingAnnotations
        {
            Active = new AnnotationPackageDto
            {
                Id = "live",
                Title = "啟用包",
                Status = "confirmed",
                Active = true,
                Purpose = "原說明",
                Tables = [new AnnotationTableDto { Name = "parent", Columns = [new AnnotationColumnDto { Name = "Id", Type = "int", Note = "原欄註" }] }],
            },
        };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.AreEqual("原欄註", Field(cut.Find("[aria-label='Id 欄註']"))));
        await cut.Find("[aria-label='Id 欄註']").InputAsync(new ChangeEventArgs { Value = "新欄註" });
        cut.WaitForAssertion(() => Assert.AreEqual(1, annotations.SaveAttempts));
        Assert.AreEqual("新欄註", annotations.Draft!.Tables[0].Columns[0].Note);
        Assert.AreEqual("原欄註", annotations.Active!.Tables[0].Columns[0].Note);
        Assert.AreEqual("confirmed", annotations.Active.Status);
        Assert.IsTrue(annotations.Active.Active);
    }

    [TestMethod]
    public async Task Dbml_confirm_busy_disables_confirm_and_unheld_does_not_write()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var annotations = new RecordingAnnotations { Draft = Draft("草稿"), ReadGate = release.Task };
        await using var context = Station(annotations);
        var cut = context.Render<Workbench.Web.Components.Governance>();
        cut.WaitForAssertion(() => Assert.IsFalse(cut.Find("#annotation-confirm").HasAttribute("disabled")));
        var click = cut.Find("#annotation-read").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.IsTrue(cut.Find("#annotation-confirm").HasAttribute("disabled")));
        release.TrySetResult();
        await click;

        await using var host = new WorkbenchHost();
        host.Annotations.Draft = Draft("Kept");
        host.Annotations.DraftDbml = "Table kept {\n}\n";
        var before = JsonSerializer.Serialize(host.Annotations.Draft);
        var person = await PersonAsync(host, "林可", EntryNames.Analyst);
        var confirm = await person.RawAsync(HttpMethod.Post, "api/annotations/confirm");
        Assert.AreEqual(403, confirm.Status, confirm.Text);
        Assert.AreEqual("forbidden", confirm.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", confirm.Body.GetProperty("message").GetString());
        var export = await person.RawAsync(HttpMethod.Get, "api/annotations/draft/dbml");
        Assert.AreEqual(403, export.Status, export.Text);
        Assert.AreEqual("沒有權限。", export.Body.GetProperty("message").GetString());
        Assert.AreEqual(0, host.Annotations.ConfirmAttempts);
        Assert.AreEqual(0, host.Annotations.DbmlAttempts);
        Assert.AreEqual(before, JsonSerializer.Serialize(host.Annotations.Draft));
    }

    [TestMethod]
    public async Task Dbml_confirm_client_calls_export_confirm_and_active()
    {
        var handler = new RecordingHandler
        {
            Status = HttpStatusCode.OK,
            Body = "Table samples {\n}\n",
        };
        using var http = new HttpClient(handler);
        var client = new AnnotationCatalogClient(http, "http://127.0.0.1:9/", "unit-catalog-key");
        var text = await client.DraftDbmlAsync("方晴", CancellationToken.None);
        Assert.AreEqual("Table samples {\n}\n", text);
        Assert.AreEqual(HttpMethod.Get, handler.Method);
        Assert.AreEqual("annotations/draft/dbml", handler.Path);

        handler.Status = HttpStatusCode.OK;
        handler.Body = """
        {"id":"live","title":"啟用包","purpose":"說明","status":"confirmed","active":true,"tables":[{"name":"parent","note":"","columns":[],"indexes":[]}],"links":[],"dbml":"ACTIVE","others":[{"id":"old","title":"舊包","dbml":"OLD","tables":[{"name":"legacy","note":"","columns":[],"indexes":[]}]}]}
        """;
        var active = await client.ActiveAsync("方晴", CancellationToken.None);
        Assert.AreEqual("annotations/active", handler.Path);
        Assert.AreEqual("live", active.Package!.Id);
        Assert.AreEqual("parent", active.Package.Tables[0].Name);
        Assert.AreEqual("old", active.Others[0].Id);
        Assert.AreEqual("舊包", active.Others[0].Title);

        handler.Status = HttpStatusCode.NoContent;
        handler.Body = "";
        var empty = await client.ActiveAsync("方晴", CancellationToken.None);
        Assert.IsNull(empty.Package);
        Assert.AreEqual(0, empty.Others.Count);

        handler.Status = HttpStatusCode.OK;
        handler.Body = """{"id":"live","title":"啟用包","status":"confirmed","active":true,"tables":[],"links":[],"dbml":"ACTIVE"}""";
        var confirmed = await client.ConfirmAsync("方晴", CancellationToken.None);
        Assert.IsTrue(confirmed.Ok, confirmed.Message);
        Assert.AreEqual(HttpMethod.Post, handler.Method);
        Assert.AreEqual("annotations/confirm", handler.Path);
        Assert.AreEqual("confirmed", confirmed.Package!.Status);
    }

    private static AnnotationPackageDto Draft(string title) => new()
    {
        Id = "pkg",
        Title = title,
        Purpose = "說明",
        Status = "draft",
        Active = false,
        Dbml = "",
        Tables = [new AnnotationTableDto { Name = "parent", Columns = [new AnnotationColumnDto { Name = "Id", Type = "int", Note = "欄註" }] }],
    };

    private static string TableBlock(string text)
    {
        var start = text.IndexOf("Table ", StringComparison.Ordinal);
        return start < 0 ? text : text[start..];
    }

    private static string Sample(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Requirements", name);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(name);
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

    private static string Field(IElement element)
    {
        var text = element.TextContent.Trim();
        return text.Length > 0 ? text : element.GetAttribute("value") ?? "";
    }

    private static BunitContext Station(RecordingAnnotations annotations)
    {
        var store = new MemoryOperators();
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
        context.Services.AddSingleton<IViewCaller>(new RecordingCaller());
        return context;
    }

    private sealed class MemoryOperators : IOperatorStore
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? "";
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }
}
