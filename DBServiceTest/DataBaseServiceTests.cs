using DBServices;
using DBServices.Models.Interface;
using DBServices.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DBServiceTest.Models;
using DBServices.Test;
using System.ComponentModel.Design;
using Org.BouncyCastle.Asn1.Cms;

namespace DBServices.Tests
{
    [TestClass()]
    public abstract class DataBaseServiceTests(IDbService _db) : IMetaTest
    {
        #region DCL
        [TestMethod()]
        public virtual void SetCurrentTableNameTest()
        {
            _db.SetCurrentTableName("PersonTable");
            Assert.AreEqual("PersonTable", _db.GetCurrentTableName());
        }

        [TestMethod()]
        public virtual void SetCurrentTableTest()
        {
            _db.SetCurrentTable<PersonTable>();
            Assert.AreEqual("PersonTable", _db.GetCurrentTableName());

        }
        [TestMethod()]
        public virtual void GetAllTableNamesTest()
        {
            string[]? allTableName = _db.GetAllTableNames();
            int result = allTableName?.Length ?? 0;
           
            Assert.IsTrue(result > 0);
            
        }
        [TestMethod()]
        public virtual void GetFieldsByTableNameTest()
        {

            var result = _db.GetFieldsByTableName("PersonTable");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any());

        }

        #endregion
        #region DDL
        [TestMethod]
        public virtual void CreateTableForNormalClass()
        {
            int result = CreateNewTable<PersonTable>();
            Assert.IsTrue(result >= 0);
        }
        [TestMethod]
        public virtual void CreateTableForForeignClass()
        {
            int result = CreateNewTable<EncounterTable>();
            Assert.IsTrue(result >= 0);
        }

        [TestMethod()]
        public virtual void DropTableTest()
        {
            _db.CreateNewTable<TestTable>();
            if (_db.HasTable("TestTable"))
            {
                _db.DropTable("TestTable");
                Assert.IsFalse(_db.HasTable("TestTable"));
            }
            else
            {
                Assert.Fail("TestTable not created");
            }
        }
        #endregion

        #region DQL
        [TestMethod()]
        public virtual void GetRecordByIdTest()
        {

            if (_db.HasRecord("PersonTable"))
            {
                var result = _db.GetRecordById(1, "PersonTable");
                Assert.IsNotNull(result);
            }
            else
            {
                Assert.Fail("No record in PersonTable");
            }
        }

        [TestMethod()]
        public virtual void GetRecordByKeyValueTest()
        {

            KeyValuePair<string, object?> newValue = new("Name", "BB");
            PersonTable target = new ()
            {
                Name = "EE",
                Age = 11
            };
            
            _db.InsertRecord(target.GetKeyValuePairs(), "PersonTable");
            var result = _db.GetRecordByKeyValue(newValue, null, "PersonTable");

            Assert.IsNotNull(result);

        }
        [TestMethod()]
        public virtual void GetRecordByKeyValuesTest()
        {
            PersonTable target = new()
            {
                Name = "QQ",
                Age = 88
            };
            _db.InsertRecord(target.GetKeyValuePairs(), "PersonTable");
            var result = _db.GetRecordByKeyValues(target.GetKeyValuePairs(), "PersonTable");
            Assert.IsNotNull(result);
           
        }

        [TestMethod()]
        public virtual void GetRecordByTableNameTest()
        {
            var result = _db.GetRecordByTableName("PersonTable");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Records?.Any());
        }
        [TestMethod()]
        public virtual void GetRecordForFieldTest()
        {
            string[] needField = ["id", "Name"];
            var result = _db.GetRecordForField(needField, null, nameof(PersonTable));
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Records?.Any());
           
        }

        [TestMethod()]
        public virtual void GetRecordByForeignKeyTest()
        {
            var result = _db.GetRecordByForeignKey("PersonFK", 1, "EncounterTable");
            Assert.IsNotNull(result);
        }
        #endregion


        #region DML
        [TestMethod]
        public virtual void InsertDataTest()
        {
            if (_db == null)
            {
                Assert.Fail("_db is null");
            }

            PersonTable p = new()
            {
                Name = "DD",
                Age = 87
            };
            _db.SetCurrentTableName("PersonTable");
            var result = _db.InsertRecord(p.GetKeyValuePairs());
            Assert.IsNotNull(result);
           
        }
        [TestMethod]
        public virtual void InsertFKDataTest()
        {
            if (_db == null)
            {
                Assert.Fail("_db is null");
            }

            EncounterTable e = new()
            {
                PersonFK = 1,
                EncounterType = "Inpatient"
            };
            _db.SetCurrentTableName("EncounterTable");
            var result = _db.InsertRecord(e.GetKeyValuePairs());

            Assert.IsNotNull(result);
        }

        [TestMethod()]
        public virtual void UpdateRecordByIdTest()
        {

            var updateObject = new PersonTable()
            {
                Name = "EE",
                Age = 99
            };
            var result = _db.UpdateRecordById(1, updateObject.GetKeyValuePairs(), "PersonTable");
            var record = _db.GetRecordById(1, "PersonTable");
            Assert.IsNotNull(result);
            Assert.AreEqual("EE", record?.GetObject<PersonTable>()?.Name);

        }
        [TestMethod()]
        public virtual void UpdateRecordByKeyValueTest()
        {
            var targetObject = new KeyValuePair<string, object?>("Name", "EE");
            var updateObject = new PersonTable()
            {
                Name = "GG",
                Age = 99
            };
            var result = _db.UpdateRecordByKeyValue(targetObject, updateObject.GetKeyValuePairs(), "PersonTable");
            var record = _db.GetRecordById(1, "PersonTable");
            Assert.IsNotNull(result);
            Assert.AreEqual("GG", record?.GetObject<PersonTable>()?.Name);
        }
        [TestMethod()]
        public virtual void DeleteRecordByIdTest()
        {
            var insertObject = new PersonTable()
            {
                Name = "HH",
                Age = 66
            };
            var newRecord = _db.InsertRecord(insertObject.GetKeyValuePairs(), "PersonTable");
            var id = newRecord?.GetObject<PersonTable>()?.Id ?? 0;
            if(id > 0)
            {
                Assert.IsTrue(_db.DeleteRecordById(id, "PersonTable"));
            }
            else
            {
                Assert.Fail("Insert failed");
            }

        }
        #endregion

        #region Private Methods
        private int CreateNewTable<T>() where T : TableDefBaseModel
        {
            string tableName = typeof(T).Name;
            if (_db.HasTable(tableName))
            {
                _db.DropTable(tableName);
            }
            return _db.CreateNewTable<T>();
        }


        #endregion


    }
}