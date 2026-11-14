using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
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
public class ExcelDraftTests
{
    private const string ImportRejected = "無法匯入。這份活頁簿對不上字典契約。既有草稿沒有改。";
    private const string DraftExists = "已有工作中的草稿，沒有改。";

    [TestMethod]
    public async Task Excel_draft_sample_matches_without_reading_rows()
    {
        var bytes = File.ReadAllBytes(Sample("Samples.xlsx"));
        var dbml = File.ReadAllText(Sample("LH_TXN_MLST.sample.dbml"));
        var parsed = DictionaryWorkbook.Read("C:\\inbox\\Samples.xlsx", bytes);
        Assert.IsTrue(parsed.Ok, parsed.Error?.Message);
        var package = parsed.Value!;
        Assert.AreEqual("Samples", package.Title);
        Assert.AreEqual("", package.Purpose);
        Assert.AreEqual("draft", package.Status);
        Assert.IsFalse(package.Active);
        Assert.AreEqual(0, package.Links.Count);
        Assert.AreEqual("", package.Dbml);
        Assert.AreEqual(1, package.Tables.Count);

        var table = package.Tables[0];
        var expected = Expected(dbml);
        Assert.AreEqual("LH_TXN_MLST", table.Name);
        Assert.AreEqual(expected.Note, table.Note);
        Assert.AreEqual(expected.Columns.Count, table.Columns.Count);
        for (var index = 0; index < expected.Columns.Count; index++)
        {
            Assert.AreEqual(expected.Columns[index].Name, table.Columns[index].Name);
            Assert.AreEqual(expected.Columns[index].Type, table.Columns[index].Type);
            Assert.AreEqual(expected.Columns[index].NotNull, table.Columns[index].NotNull);
            Assert.AreEqual(expected.Columns[index].Note, table.Columns[index].Note);
        }

        Assert.AreEqual(1, table.Indexes.Count);
        CollectionAssert.AreEqual(expected.IndexColumns, table.Indexes[0].Columns);
        Assert.AreEqual(expected.IndexName, table.Indexes[0].Name);
        Assert.AreEqual(expected.IndexNote, table.Indexes[0].Note);
        Assert.AreEqual("POST_BRH_NO", table.Columns[0].HName);
        Assert.AreEqual("OFFICE_CODE", table.Columns[0].SName);
        Assert.IsTrue(table.Columns[0].UniqueIndex);
        var date = table.Columns.Single(column => column.Name == "TABLE_DATE");
        Assert.AreEqual("TABLE_DATE", date.HName);
        Assert.AreEqual("", date.SName);
        Assert.AreEqual("text", date.Type);
        Assert.IsTrue(date.NotNull);
        Assert.IsFalse(date.Note.Contains("H 欄名", StringComparison.Ordinal));
        var stored = JsonSerializer.Serialize(package);
        Assert.IsFalse(stored.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(stored.Contains("\"rows\"", StringComparison.OrdinalIgnoreCase));

        var caller = new RecordingCaller();
        var annotations = new RecordingAnnotations();
        await using (var context = Station(caller, annotations))
        {
            var cut = context.Render<Workbench.Web.Components.Governance>();
            cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(bytes, "Samples.xlsx"));
            cut.WaitForAssertion(() => Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("Samples", StringComparison.Ordinal)));
            Assert.IsTrue(cut.Find(".package-bar").TextContent.Contains("草稿", StringComparison.Ordinal));
            Assert.AreEqual("LH_TXN_MLST", cut.Find(".table-list .code").TextContent.Trim());
            Assert.AreEqual(expected.Note, cut.Find(".table-note").TextContent.Trim());
            foreach (var column in expected.Columns)
            {
                var row = cut.Find($"tr[data-column='{column.Name}']");
                var cells = row.QuerySelectorAll("td");
                Assert.AreEqual(column.Name, cells[0].TextContent.Trim());
                Assert.AreEqual(column.Type, cells[1].TextContent.Trim());
                Assert.AreEqual(column.NotNull ? "not null" : "", cells[2].TextContent.Trim());
                Assert.AreEqual(column.Note, cells[3].TextContent.Trim());
            }

            var index = cut.Find($"[data-index='{expected.IndexName}']");
            Assert.IsTrue(index.TextContent.Contains(expected.IndexNote, StringComparison.Ordinal));
            foreach (var name in expected.IndexColumns)
                Assert.IsTrue(index.TextContent.Contains(name, StringComparison.Ordinal));
            Assert.AreEqual(0, cut.FindAll(".progress-line").Count);
            Assert.IsFalse(cut.Find("input").HasAttribute("disabled"));
            Assert.IsFalse(cut.Markup.Contains("啟用", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("sourceFile", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(cut.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
            Assert.AreEqual(1, annotations.Commits);
            Assert.AreEqual(0, caller.Count);
            var kept = annotations.Draft!.Id;

            cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(bytes, "Samples.xlsx"));
            cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
            var alert = cut.Find(".status-message");
            Assert.AreEqual("alert", alert.GetAttribute("role"));
            Assert.IsTrue(alert.InnerHtml.IndexOf(DraftExists, StringComparison.Ordinal) < alert.InnerHtml.IndexOf("draft_exists", StringComparison.Ordinal));
            Assert.AreEqual(1, annotations.Commits);
            Assert.AreEqual(kept, annotations.Draft.Id);
            Assert.AreEqual(0, caller.Count);
        }

        var connections = new RecordingConnections { Available = 0 };
        await using var host = new WorkbenchHost(options => options.Connections = connections);
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var imported = await PostAsync(person, bytes, "Samples.xlsx");
        Assert.AreEqual(200, imported.Status, imported.Text);
        Assert.AreEqual("Samples", imported.Body.GetProperty("title").GetString());
        Assert.AreEqual("draft", imported.Body.GetProperty("status").GetString());
        Assert.IsFalse(imported.Body.GetProperty("active").GetBoolean());
        Assert.AreEqual("", imported.Body.GetProperty("purpose").GetString());
        Assert.AreEqual("", imported.Body.GetProperty("dbml").GetString());
        Assert.AreEqual(0, imported.Body.GetProperty("links").GetArrayLength());
        Assert.AreEqual(expected.Note, imported.Body.GetProperty("tables")[0].GetProperty("note").GetString());
        Assert.IsFalse(imported.Text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(imported.Text.Contains("\"rows\"", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, host.Annotations.Commits);
        Assert.AreEqual(0, host.Caller.Count);
        Assert.AreEqual(0, connections.Available);
    }

    [TestMethod]
    public async Task Excel_draft_contract_rejection_keeps_the_draft()
    {
        var caller = new RecordingCaller();
        var annotations = new RecordingAnnotations
        {
            Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" },
        };
        await using (var context = Station(caller, annotations))
        {
            var cut = context.Render<Workbench.Web.Components.Governance>();
            cut.WaitForAssertion(() => Assert.AreEqual("Kept", cut.Find(".package-bar .code").TextContent.Trim()));
            cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([9, 8, 7], "broken.xlsx"));
            cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".status-message").Count));
            var alert = cut.Find(".status-message");
            Assert.AreEqual("alert", alert.GetAttribute("role"));
            Assert.IsTrue(alert.InnerHtml.IndexOf(ImportRejected, StringComparison.Ordinal) < alert.InnerHtml.IndexOf("import_rejected", StringComparison.Ordinal));
            Assert.AreEqual("Kept", cut.Find(".package-bar .code").TextContent.Trim());
            Assert.AreEqual("原說明", annotations.Draft!.Purpose);
            Assert.AreEqual(0, annotations.Attempts);
            Assert.AreEqual(0, caller.Count);
        }

        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft", Purpose = "原說明" };
        var before = JsonSerializer.Serialize(host.Annotations.Draft);
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        foreach (var (name, bytes) in new (string, byte[])[]
        {
            ("broken.xlsx", [1, 2, 3]),
            ("header.xlsx", HeaderWorkbook()),
            ("sheets.xlsx", TwoSheetWorkbook()),
        })
        {
            var rejected = await PostAsync(person, bytes, name);
            Assert.AreEqual(400, rejected.Status, rejected.Text);
            Assert.AreEqual("import_rejected", rejected.Body.GetProperty("code").GetString());
            Assert.AreEqual(ImportRejected, rejected.Body.GetProperty("message").GetString());
        }

        Assert.AreEqual(before, JsonSerializer.Serialize(host.Annotations.Draft));
        Assert.AreEqual(0, host.Annotations.Attempts);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public void Excel_draft_missing_catalog_file_creates_one_draft()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-new-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "catalog", "annotations.json");
        Assert.IsFalse(File.Exists(path));
        try
        {
            var catalog = new DbServices.Mcp.AnnotationCatalog(path);
            Assert.IsNull(catalog.Draft());
            var saved = catalog.Import(new DbServices.Mcp.AnnotationPackage { Id = "first", Title = "Samples" });
            Assert.IsTrue(saved.Ok);
            Assert.AreEqual("draft", saved.Package!.Status);
            Assert.IsFalse(saved.Package.Active);
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("first", catalog.Draft()!.Id);
            Assert.AreEqual("Samples", catalog.Draft()!.Title);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task Excel_draft_existing_draft_file_bytes_stay()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            await File.WriteAllTextAsync(path, """
            [
              {
                "id": "old",
                "title": "Old",
                "purpose": "已確認",
                "status": "confirmed",
                "active": true,
                "tables": [],
                "links": [],
                "dbml": ""
              }
            ]
            """);
            var catalog = new DbServices.Mcp.AnnotationCatalog(path);
            var first = catalog.Import(new DbServices.Mcp.AnnotationPackage
            {
                Id = "new",
                Title = "Samples",
                Status = "confirmed",
                Active = true,
                Purpose = "",
            });
            Assert.IsTrue(first.Ok);
            Assert.AreEqual("draft", first.Package!.Status);
            Assert.IsFalse(first.Package.Active);
            Assert.AreEqual("new", catalog.Draft()!.Id);
            var text = await File.ReadAllTextAsync(path);
            using (var saved = JsonDocument.Parse(text))
            {
                var packages = saved.RootElement.EnumerateArray().ToList();
                Assert.AreEqual(2, packages.Count);
                var old = packages.Single(item => item.GetProperty("id").GetString() == "old");
                Assert.AreEqual("confirmed", old.GetProperty("status").GetString());
                Assert.IsTrue(old.GetProperty("active").GetBoolean());
                Assert.AreEqual("已確認", old.GetProperty("purpose").GetString());
                var fresh = packages.Single(item => item.GetProperty("id").GetString() == "new");
                Assert.AreEqual("draft", fresh.GetProperty("status").GetString());
                Assert.IsFalse(fresh.GetProperty("active").GetBoolean());
            }

            Assert.IsFalse(text.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
            var bytes = await File.ReadAllBytesAsync(path);
            var second = catalog.Import(new DbServices.Mcp.AnnotationPackage { Id = "other", Title = "Other" });
            Assert.IsFalse(second.Ok);
            Assert.AreEqual("draft_exists", second.Code);
            Assert.AreEqual(DraftExists, second.Message);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (IOException) { }
        }

        await using var host = new WorkbenchHost();
        host.Annotations.Draft = new AnnotationPackageDto { Id = "kept", Title = "Kept", Status = "draft" };
        var person = await PersonAsync(host, "方晴", EntryNames.Governance);
        var again = await PostAsync(person, File.ReadAllBytes(Sample("Samples.xlsx")), "Samples.xlsx");
        Assert.AreEqual(400, again.Status, again.Text);
        Assert.AreEqual("draft_exists", again.Body.GetProperty("code").GetString());
        Assert.AreEqual(DraftExists, again.Body.GetProperty("message").GetString());
        Assert.AreEqual("kept", host.Annotations.Draft.Id);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.AreEqual(1, host.Annotations.Attempts);
        Assert.AreEqual(0, host.Caller.Count);
    }

    [TestMethod]
    public async Task Excel_draft_without_governance_does_not_write()
    {
        await using var host = new WorkbenchHost();
        var person = await PersonAsync(host, "林可", EntryNames.Analyst);
        var rejected = await PostAsync(person, File.ReadAllBytes(Sample("Samples.xlsx")), "Samples.xlsx");
        Assert.AreEqual(403, rejected.Status, rejected.Text);
        Assert.AreEqual("forbidden", rejected.Body.GetProperty("code").GetString());
        Assert.AreEqual("沒有權限。", rejected.Body.GetProperty("message").GetString());
        Assert.AreEqual(0, host.Annotations.Attempts);
        Assert.AreEqual(0, host.Annotations.Commits);
        Assert.IsNull(host.Annotations.Draft);
        Assert.AreEqual(0, host.Caller.Count);
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

    private static async Task<(int Status, string Text, JsonElement Body)> PostAsync(Session session, byte[] bytes, string name)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", name);
        using var response = await session.Client.PostAsync("api/annotations/draft", content);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
        return ((int)response.StatusCode, text, document.RootElement.Clone());
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

    private static ExpectedShape Expected(string dbml)
    {
        var columns = new List<ExpectedColumn>();
        foreach (var raw in dbml.Split('\n'))
        {
            var line = raw.Trim();
            var match = Regex.Match(line, @"^(?<name>[A-Z0-9_]+) (?<type>[a-z0-9()]+) \[(?<body>.*)\]$");
            if (!match.Success)
                continue;
            var body = match.Groups["body"].Value;
            var note = Regex.Match(body, @"note: '(?<note>.*)'").Groups["note"].Value;
            columns.Add(new ExpectedColumn(match.Groups["name"].Value, match.Groups["type"].Value, body.Contains("not null", StringComparison.Ordinal), note));
        }

        var tableNote = Regex.Match(dbml, @"Note: '(?<note>[^']*)'").Groups["note"].Value;
        var index = Regex.Match(dbml, @"\((?<columns>[^)]+)\) \[name: '(?<name>[^']+)', note: '(?<note>[^']+)'\]");
        return new ExpectedShape(
            columns,
            tableNote,
            index.Groups["name"].Value,
            index.Groups["note"].Value,
            index.Groups["columns"].Value.Split(',').Select(name => name.Trim()).ToArray());
    }

    private static byte[] HeaderWorkbook()
        => Zip(
            ("xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Only" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """),
            ("xl/worksheets/sheet1.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData><row r="1"><c r="A1" t="inlineStr"><is><t>不是表頭</t></is></c></row></sheetData>
            </worksheet>
            """));

    private static byte[] TwoSheetWorkbook()
        => Zip(("xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="One" sheetId="1" r:id="rId1"/>
                <sheet name="Two" sheetId="2" r:id="rId2"/>
              </sheets>
            </workbook>
            """));

    private static byte[] Zip(params (string Path, string Text)[] entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in entries)
            {
                var entry = zip.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(text);
            }
        }

        return memory.ToArray();
    }

    private sealed record ExpectedColumn(string Name, string Type, bool NotNull, string Note);

    private sealed record ExpectedShape(
        List<ExpectedColumn> Columns,
        string Note,
        string IndexName,
        string IndexNote,
        string[] IndexColumns);

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
