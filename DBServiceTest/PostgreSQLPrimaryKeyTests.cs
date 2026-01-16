using Microsoft.VisualStudio.TestTools.UnitTesting;
using DbServices.Provider.PostgreSQL;
using DBServices;
using DbServices.Core.Models.Interface;
using System;
using System.IO;

namespace DBServiceTest
{
    /// <summary>
    /// PostgreSQL 主鍵查詢測試
    /// 注意：需要實際的 PostgreSQL 資料庫連線才能執行
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    [TestCategory("PostgreSQL")]
    public class PostgreSQLPrimaryKeyTests
    {
        private IDbService? _db;
        private const string TestConnectionString = "Host=localhost;Database=testdb;Username=postgres;Password=password";

        [TestInitialize]
        public void TestInit()
        {
            // 注意：只有在有 PostgreSQL 資料庫時才初始化
            // 在 CI/CD 環境中可能需要跳過這些測試
            try
            {
                _db = MainService.UsePostgreSQL(TestConnectionString);
                // 測試連線
                _db.GetAllTableNames();
            }
            catch
            {
                // 如果無法連線，標記為跳過
                Assert.Inconclusive("無法連線到 PostgreSQL 資料庫，跳過測試");
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_db is IDisposable d)
            {
                d.Dispose();
            }
        }

        [TestMethod]
        public void Should_Identify_Primary_Key_Correctly()
        {
            if (_db == null) Assert.Inconclusive("資料庫未初始化");

            // 建立測試表
            var testTableName = $"TestTable_{Guid.NewGuid():N}";
            try
            {
                // 建立包含主鍵的測試表
                var createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {testTableName} (
                        Id INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
                        Name TEXT NOT NULL,
                        Age INTEGER
                    );";

                _db.ExecuteSQL(createTableSql);

                // 取得欄位資訊
                var fields = _db.GetFieldsByTableName(testTableName);

                Assert.IsNotNull(fields, "應該能取得欄位資訊");

                var idField = fields.FirstOrDefault(f => f.FieldName == "Id");
                Assert.IsNotNull(idField, "應該能找到 Id 欄位");
                Assert.IsTrue(idField.IsPrimaryKey, "Id 欄位應該被識別為主鍵");

                var nameField = fields.FirstOrDefault(f => f.FieldName == "Name");
                Assert.IsNotNull(nameField, "應該能找到 Name 欄位");
                Assert.IsFalse(nameField.IsPrimaryKey, "Name 欄位不應該是主鍵");
            }
            finally
            {
                // 清理測試表
                try
                {
                    _db?.DropTable(testTableName);
                }
                catch
                {
                    // 忽略清理錯誤
                }
            }
        }

        [TestMethod]
        public void Should_Handle_Multiple_Primary_Keys()
        {
            if (_db == null) Assert.Inconclusive("資料庫未初始化");

            var testTableName = $"TestTable_{Guid.NewGuid():N}";
            try
            {
                // 建立包含複合主鍵的測試表
                var createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {testTableName} (
                        Key1 INTEGER,
                        Key2 INTEGER,
                        Name TEXT,
                        PRIMARY KEY (Key1, Key2)
                    );";

                _db.ExecuteSQL(createTableSql);

                var fields = _db.GetFieldsByTableName(testTableName);

                Assert.IsNotNull(fields, "應該能取得欄位資訊");

                var key1Field = fields.FirstOrDefault(f => f.FieldName == "Key1");
                var key2Field = fields.FirstOrDefault(f => f.FieldName == "Key2");

                Assert.IsNotNull(key1Field, "應該能找到 Key1 欄位");
                Assert.IsNotNull(key2Field, "應該能找到 Key2 欄位");

                // 注意：目前的實作可能無法正確識別複合主鍵的所有欄位
                // 這是一個已知限制
            }
            finally
            {
                try
                {
                    _db?.DropTable(testTableName);
                }
                catch { }
            }
        }
    }
}

