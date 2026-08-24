using DbServices.Core;
using DbServices.Core.Models;
using DbServices.Core.Services;
using DbServices.Core.Session;
using DbServices.Provider.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{
    [TestClass]
    public class SessionAndConnectionTests
    {
        private string _dbPath = string.Empty;
        private DataBaseService _db = null!;

        [TestInitialize]
        public void Init()
        {
            var dir = Path.Combine(Path.GetTempPath(), "DBServicesTests");
            Directory.CreateDirectory(dir);
            _dbPath = Path.Combine(dir, $"{Guid.NewGuid():N}.db");
            _db = new ProviderService($"Data Source={_dbPath};");
            _db.CreateNewTable(
            [
                new FieldBaseModel { FieldName = "Id", FieldType = "Int64", IsPrimaryKey = true },
                new FieldBaseModel { FieldName = "Name", FieldType = "String", IsNotNull = true }
            ], "Users");
            _db.InsertRecord([new KeyValuePair<string, object?>("Name", "Ada")], "Users");
            _db.InsertRecord([new KeyValuePair<string, object?>("Name", "Bob")], "Users");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _db.Dispose();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* ignore */ }
        }

        [TestMethod]
        public async Task ParallelQueries_OnSameInstance_Succeed()
        {
            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                var result = _db.QuerySql("SELECT Name FROM Users");
                Assert.AreEqual(QueryKind.Rows, result.Kind);
                Assert.IsGreaterThanOrEqualTo(2, result.Rows.Count);
            }));
            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public void QuerySql_ReturnsRows()
        {
            var result = _db.QuerySql("SELECT Name FROM Users WHERE Name = @Name", new { Name = "Ada" });
            Assert.AreEqual(QueryKind.Rows, result.Kind);
            Assert.HasCount(1, result.Rows);
            Assert.AreEqual("Ada", Convert.ToString(result.Rows[0][0]));
        }

        [TestMethod]
        public void ExecuteSql_Parameterized_AffectsRows()
        {
            var affected = _db.ExecuteSql("UPDATE Users SET Name = @Name WHERE Name = @Old", new { Name = "Ada2", Old = "Ada" });
            Assert.IsGreaterThanOrEqualTo(1, affected);
        }

        [TestMethod]
        public void Session_Call_Tables()
        {
            var session = _db.GetSession();
            var result = session.Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call
            });
            Assert.AreEqual(QueryKind.Rows, result.Kind);
            Assert.IsTrue(result.Rows.Any(r => Convert.ToString(r[0]) == "Users"));
        }

        [TestMethod]
        public void Session_Call_Get()
        {
            var session = _db.GetSession();
            var result = session.Execute(new QueryRequest
            {
                Text = "get",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>
                {
                    ["table"] = "Users",
                    ["id"] = 1L
                }
            });
            Assert.AreEqual(QueryKind.Rows, result.Kind);
            Assert.IsGreaterThanOrEqualTo(1, result.Rows.Count);
        }

        [TestMethod]
        public void Transaction_Rollback_RemovesInsertedRow()
        {
            var before = _db.QuerySql("SELECT COUNT(*) AS Count FROM Users").Rows[0][0];
            try
            {
                using var tx = new TransactionService(_db);
                tx.ExecuteInTransaction(() =>
                {
                    _db.InsertRecord([new KeyValuePair<string, object?>("Name", "Temp")], "Users");
                    throw new InvalidOperationException("force rollback");
                });
            }
            catch (InvalidOperationException)
            {
                // expected
            }

            var after = _db.QuerySql("SELECT COUNT(*) AS Count FROM Users").Rows[0][0];
            Assert.AreEqual(Convert.ToInt64(before), Convert.ToInt64(after));
        }

        [TestMethod]
        public void ApplyPaging_Sqlite_UsesLimitOffset()
        {
            var sql = _db.GetRecordsWithOptions(null, new QueryOptions { Take = 1, Skip = 1 }, "Users");
            Assert.IsNotNull(sql);
            Assert.AreEqual(1, sql!.Records?.Count() ?? 0);
        }
    }
}
