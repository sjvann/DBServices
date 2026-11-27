using DbServices.Api.Shared;
using DbServices.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
public class SourceIdentityTests
{
    [TestMethod]
    public void Source_identity_excel_confirm_keeps_its_own_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ann-excel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var catalog = new AnnotationCatalog(path);
            var imported = catalog.Import(Excel("Samples.xlsx", "T", ("Id", "局號")));
            Assert.IsTrue(imported.Ok, imported.Message);
            Assert.AreEqual("excel", imported.Package!.SourceKind);
            Assert.AreEqual("Samples.xlsx", imported.Package.SourceKey);

            var confirmed = catalog.Confirm(EmptyCatalog(), "", null, imported.Package.Id);
            Assert.IsTrue(confirmed.Ok, confirmed.Message);
            Assert.AreEqual("confirmed", confirmed.Package!.Status);
            Assert.IsFalse(confirmed.Package.Active);
            Assert.AreEqual("局號", confirmed.Package.Tables[0].Columns[0].Note);
            var kept = confirmed.Package.Id;

            var again = catalog.Import(Excel("samples.xlsx", "T", ("Id", "新局號")));
            Assert.IsTrue(again.Ok, again.Message);
            var replaced = catalog.Confirm(EmptyCatalog(), "", null, again.Package!.Id);
            Assert.IsTrue(replaced.Ok, replaced.Message);
            Assert.AreEqual(kept, replaced.Package!.Id);
            Assert.AreEqual("新局號", replaced.Package.Tables[0].Columns[0].Note);
            Assert.AreEqual(1, catalog.Active().Others.Count(package => package.SourceKind == "excel"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Source_identity_connection_takes_one_excel_note_and_keeps_user_text()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-conn-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "ann-conn-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var service = DbServiceFactory.Create("Data Source=" + dbPath, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE T (Id INTEGER PRIMARY KEY, Name TEXT)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }
            var tools = Tools(dbPath);

            var catalog = new AnnotationCatalog(path);
            Assert.IsTrue(catalog.Import(Excel("A.xlsx", "T", ("Id", "局號"), ("Name", "名稱"))).Ok);
            Assert.IsTrue(catalog.Confirm(EmptyCatalog(), "", null).Ok);
            Assert.IsTrue(catalog.Import(Excel("B.xlsx", "T", ("Id", "另一句"))).Ok);
            Assert.IsTrue(catalog.Confirm(EmptyCatalog(), "", null).Ok);

            var read = catalog.ReadStructure(tools, "Warehouse");
            Assert.IsTrue(read.Ok, read.Message);
            var table = read.Package!.Tables.Single(item => item.Name == "T");
            Assert.AreEqual("", table.Columns.Single(column => column.Name == "Id").Note);
            var name = table.Columns.Single(column => column.Name == "Name");
            Assert.AreEqual("名稱", name.Note);
            Assert.AreEqual("excel", name.NoteOrigin);
            Assert.AreEqual("A.xlsx", name.NoteSource);

            name.Note = "人手";
            name.NoteOrigin = "user";
            name.NoteSource = "";
            read.Package.Purpose = "說明";
            Assert.IsTrue(catalog.Save(read.Package).Ok);
            var revised = catalog.Import(Excel("A.xlsx", "T", ("Id", "局號"), ("Name", "改過的名稱")));
            Assert.IsTrue(catalog.Confirm(EmptyCatalog(), "", null, revised.Package!.Id).Ok);
            var again = catalog.ReadStructure(tools, "Warehouse");
            Assert.IsTrue(again.Ok, again.Message);
            var kept = again.Package!.Tables.Single(item => item.Name == "T").Columns.Single(column => column.Name == "Name");
            Assert.AreEqual("人手", kept.Note);
            Assert.AreEqual("user", kept.NoteOrigin);
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete(root);
        }
    }

    [TestMethod]
    public void Source_identity_confirm_blocks_bound_view_and_keeps_other_connection()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "ann-block-" + Guid.NewGuid().ToString("N") + ".db");
        var otherPath = Path.Combine(Path.GetTempPath(), "ann-other-" + Guid.NewGuid().ToString("N") + ".db");
        var root = Path.Combine(Path.GetTempPath(), "ann-block-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "annotations.json");
        try
        {
            var warehouseService = DbServiceFactory.Create("Data Source=" + dbPath, "Sqlite");
            try
            {
                warehouseService!.ExecuteSQL("CREATE TABLE T (Id INTEGER PRIMARY KEY, Name TEXT)");
            }
            finally
            {
                (warehouseService as IDisposable)?.Dispose();
            }

            var otherService = DbServiceFactory.Create("Data Source=" + otherPath, "Sqlite");
            try
            {
                otherService!.ExecuteSQL("CREATE TABLE U (Id INTEGER PRIMARY KEY)");
            }
            finally
            {
                (otherService as IDisposable)?.Dispose();
            }

            var catalog = new AnnotationCatalog(path);
            var warehouse = catalog.ReadStructure(Tools(dbPath), "Warehouse");
            Assert.IsTrue(warehouse.Ok, warehouse.Message);
            warehouse.Package!.Purpose = "倉庫說明";
            Assert.IsTrue(catalog.Save(warehouse.Package).Ok);
            var confirmed = catalog.Confirm(Tools(dbPath), "Warehouse", [], warehouse.Package.Id);
            Assert.IsTrue(confirmed.Ok, confirmed.Message);
            Assert.IsTrue(confirmed.Package!.Active);

            var other = catalog.ReadStructure(OtherTools(otherPath), "Other");
            Assert.IsTrue(other.Ok, other.Message);
            other.Package!.Purpose = "另一座";
            Assert.IsTrue(catalog.Save(other.Package).Ok);
            var otherConfirmed = catalog.Confirm(OtherTools(otherPath), "Other", [], other.Package.Id);
            Assert.IsTrue(otherConfirmed.Ok, otherConfirmed.Message);
            Assert.IsTrue(otherConfirmed.Package!.Active);
            Assert.IsTrue(catalog.Bindable().Any(package => package.Id == confirmed.Package.Id));

            var altered = DbServiceFactory.Create("Data Source=" + dbPath, "Sqlite");
            try
            {
                altered!.ExecuteSQL("DROP TABLE T");
                altered.ExecuteSQL("CREATE TABLE T (Id INTEGER PRIMARY KEY)");
            }
            finally
            {
                (altered as IDisposable)?.Dispose();
            }

            var reread = catalog.ReadStructure(Tools(dbPath), "Warehouse");
            Assert.IsTrue(reread.Ok, reread.Message);
            reread.Package!.Purpose = "倉庫說明";
            Assert.IsTrue(catalog.Save(reread.Package).Ok);
            var view = new ViewDocument
            {
                Id = "view-1",
                Name = "清單",
                AnnotationId = confirmed.Package.Id,
                Sources = ["T"],
                Columns = [new ViewColumn { Name = "Name", Source = "T" }],
            };
            var blocked = catalog.Confirm(Tools(dbPath), "Warehouse", [view], reread.Package.Id);
            Assert.IsFalse(blocked.Ok);
            Assert.AreEqual("confirm_blocked", blocked.Code);
            Assert.AreEqual("無法確認。這些檢視會落到新包之外：清單。", blocked.Message);
            var still = catalog.Bindable().Single(package => package.SourceKey == "Warehouse");
            Assert.AreEqual(confirmed.Package.Id, still.Id);
            Assert.IsTrue(still.Tables.Single(table => table.Name == "T").Columns.Any(column => column.Name == "Name"));
            Assert.IsTrue(catalog.Bindable().Any(package => package.SourceKey == "Other"));
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete(otherPath);
            TryDelete(root);
        }
    }

    private static AnnotationPackage Excel(string fileName, string table, params (string Name, string Note)[] columns)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = Path.GetFileNameWithoutExtension(fileName),
            SourceKind = "excel",
            SourceKey = fileName,
            Status = "draft",
            Tables =
            [
                new AnnotationTable
                {
                    Name = table,
                    Columns = columns.Select(column => new AnnotationColumn
                    {
                        Name = column.Name,
                        Type = "text",
                        Note = column.Note,
                    }).ToList(),
                },
            ],
        };

    private static ToolCatalog EmptyCatalog() => new(new Dictionary<string, ConnectionSetting>());

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static ToolCatalog Tools(string dbPath)
        => new(new Dictionary<string, ConnectionSetting>
        {
            ["Warehouse"] = new() { ServerType = "Sqlite", ConnectionString = "Data Source=" + dbPath },
        });

    private static ToolCatalog OtherTools(string dbPath)
        => new(new Dictionary<string, ConnectionSetting>
        {
            ["Other"] = new() { ServerType = "Sqlite", ConnectionString = "Data Source=" + dbPath },
        });
}
