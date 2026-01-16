
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DbServices.Core.Models.Interface;
using DbServices.Core.Models.Enum;
using DBServiceTest.Models;
using DbServices.Core.Models;


namespace DBServiceTest
{
    [TestClass()]
    public abstract class DataBaseServiceTests(IDbService _db) : IMetaTest
    {
		[TestInitialize]
		public void TestInit()
		{
			// Make each test independent from any pre-existing Test.db content.
			// Create deterministic schema + seed data used by DQL/DML tests.
			_db.DropTable(nameof(EncounterTable));
			_db.DropTable(nameof(PersonTable));
			_db.CreateNewTable<PersonTable>();
			_db.CreateNewTable<EncounterTable>();

			var p = new PersonTable { Name = "EE", Age = 20 };
			_db.InsertRecord(p.GetKeyValuePairs(), nameof(PersonTable));

			var e = new EncounterTable { PersonFK = 1, EncounterType = "Seed" };
			_db.InsertRecord(e.GetKeyValuePairs(), nameof(EncounterTable));
		}

        #region DCL
        [TestMethod()]
        public virtual void SetCurrentTableNameTest()
        {
            _db.SetCurrentTableName("PersonTable");
            Assert.AreEqual("PersonTable", _db.GetCurrentTableName());
            Console.WriteLine($"Current Table: {_db.GetCurrentTableName()}");
        }

        [TestMethod()]
        public virtual void SetCurrentTableTest()
        {
            _db.SetCurrentTable<PersonTable>();
            Assert.AreEqual("PersonTable", _db.GetCurrentTableName());
            Console.WriteLine($"Current Table: {_db.GetCurrentTableName()}");
        }
        [TestMethod()]
        public virtual void GetAllTableNamesTest()
        {
            string[]? allTableName = _db.GetAllTableNames();

            Assert.IsNotNull(allTableName);
            Assert.IsTrue(allTableName.Length > 0);
            Console.WriteLine($"All Table Name: {string.Join("\r\n", allTableName)}");
        }
        [TestMethod()]
        public virtual void GetFieldsByTableNameTest()
        {

            var result = _db.GetFieldsByTableName("PersonTable");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any());

            var nameList = from item in result
                           select item.FieldName;
            Console.WriteLine($"Fields:\r\n{string.Join("\r\n", nameList)}");

        }

        #endregion
        #region DDL
        [TestMethod]
        public virtual void CreateTableForNormalClass()
        {
            Console.WriteLine($"Before Create PersonTable Table: {_db.HasTable("PersonTable")}");
            int result = CreateNewTable<PersonTable>();
            Assert.IsTrue(result >= 0);
            Console.WriteLine($"After Create PersonTable Table: {_db.HasTable("PersonTable")}");
        }
        [TestMethod]
        public virtual void CreateTableForForeignClass()
        {
            Console.WriteLine($"Before Create EncounterTable Table: {_db.HasTable("EncounterTable")}");
            int result = CreateNewTable<EncounterTable>();
            Assert.IsTrue(result >= 0);
            Console.WriteLine($"After Create EncounterTable Table: {_db.HasTable("EncounterTable")}");
        }

        [TestMethod()]
        public virtual void DropTableTest()
        {
            _db.CreateNewTable<TestTable>();
            Console.WriteLine($"Before Drop TestTable: {_db.HasTable("TestTable")}");
            if (_db.HasTable("TestTable"))
            {
                _db.DropTable("TestTable");
                Assert.IsFalse(_db.HasTable("TestTable"));
            }
            else
            {
                Assert.Fail("TestTable not created");
            }
            Console.WriteLine($"After Drop TestTable: {_db.HasTable("TestTable")}");
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
                Console.WriteLine(result.GetRecordOnlyJsonString(true));
            }
            else
            {
                Assert.Fail("No record in PersonTable");
            }
        }
        [TestMethod]
        public virtual void GetRecordByWhereTest()
        {
            if (_db.HasRecord("PersonTable"))
            {
                var result = _db.GetRecordWithWhere("id = 1", "PersonTable");
                Assert.IsNotNull(result);
                Console.WriteLine(result.GetRecordOnlyJsonString(true));
            }
            else
            {
                Assert.Fail("No record in PersonTable");
            }
        }

        [TestMethod()]
        public virtual void GetRecordByKeyValueTest()
        {
            KeyValuePair<string, object?> newValue = new("Age", "10");
            var result = _db.GetRecordByKeyValue(newValue, EnumQueryOperator.GreaterThan, "PersonTable");
            Assert.IsNotNull(result);
            Console.WriteLine(result.GetRecordOnlyJsonString(true));
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
            Console.WriteLine(result.GetRecordOnlyJsonString(true));
        }

        [TestMethod()]
        public virtual void GetRecordByTableNameTest()
        {
            var result = _db.GetRecordByTableName("PersonTable");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Records?.Any());
            Console.WriteLine(result.GetFullyJsonString(true));
        }
        [TestMethod()]
        public virtual void GetRecordForFieldTest()
        {
            string[] needField = ["id", "Name"];
            var result = _db.GetRecordForField(needField, null, nameof(PersonTable));
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Records?.Any());
            Console.WriteLine(result.GetRecordOnlyJsonString(true));
        }

        [TestMethod()]
        public virtual void GetRecordByForeignKeyForOneSideTest()
        {
            var result = _db.GetRecordByForeignKeyForOneSide("PersonFK", 1, nameof(EncounterTable), nameof(PersonTable));
            Assert.IsNotNull(result);
            Console.WriteLine(result.GetRecordOnlyJsonString(true));
        }
        [TestMethod()]
        public virtual void GetRecordByForeignKeyForManySideTest()
        {
            var result = _db.GetRecordByForeignKeyForManySide(1, "PersonFK", nameof(EncounterTable));
            Assert.IsNotNull(result);
            Console.WriteLine(result.GetRecordOnlyJsonString(true));
        }
        #endregion
        #region DML
        [TestMethod]
        public virtual void InsertDataTest()
        {
            PersonTable p = new()
            {
                Name = "FF",
                Age = 38
            };

            Console.WriteLine("Before insert data:");
            var oldTable = _db.GetRecordWithWhere($"Name = '{p.Name}'", nameof(PersonTable));
            Console.WriteLine(oldTable?.GetRecordsJsonObjectString(true) ?? string.Empty);

            var target = _db.InsertRecord(p.GetKeyValuePairs(), nameof(PersonTable));
            Assert.IsNotNull(target);
            Console.WriteLine("After insert data:");
            var newTable = _db.GetRecordWithWhere($"Name = '{p.Name}'", nameof(PersonTable));
            Console.WriteLine(newTable?.GetRecordsJsonObjectString(true) ?? string.Empty);
        }
        [TestMethod]
        public virtual void InsertFKDataTest()
        {
            EncounterTable e = new()
            {
                PersonFK = 1,
                EncounterType = "Outpatient"
            };

            Console.WriteLine("Before insert data:");
            var oldTable = _db.GetRecordWithWhere($"EncounterType = '{e.EncounterType}'", nameof(EncounterTable));
            Console.WriteLine(oldTable?.GetRecordsJsonObjectString(true) ?? string.Empty);

            var target = _db.InsertRecord(e.GetKeyValuePairs(), nameof(EncounterTable));
            Assert.IsNotNull(target);

            Console.WriteLine("After insert data:");
            var newTable = _db.GetRecordWithWhere($"EncounterType = '{e.EncounterType}'", nameof(EncounterTable));
            Console.WriteLine(newTable?.GetRecordsJsonObjectString(true) ?? string.Empty);
        }

        [TestMethod()]
        public virtual void UpdateRecordByIdTest()
        {
            var updateObject = new PersonTable()
            {
                Name = "ZZ",
                Age = 99
            };
            Console.WriteLine("Before Update data:");
            var oldTable = _db.GetRecordById(1, nameof(PersonTable));
            Console.WriteLine(oldTable?.GetRecordsJsonObjectString(true) ?? string.Empty);

            var result = _db.UpdateRecordById(1, updateObject.GetKeyValuePairs(), nameof(PersonTable));
            Assert.IsNotNull(result);

            Console.WriteLine("After insert data:");
            var newTable = _db.GetRecordById(1, nameof(PersonTable));
            Console.WriteLine(newTable?.GetRecordsJsonObjectString(true) ?? string.Empty);
        }
        [TestMethod()]
        public virtual void UpdateRecordByKeyValueTest()
        {
            var targetObject = new KeyValuePair<string, object?>("Name", "EE");
            var updateObject = new PersonTable()
            {
                Age = 100
            };
            Console.WriteLine("Before Update data:");
            var oldTable = _db.GetRecordByKeyValue(targetObject, EnumQueryOperator.Like, nameof(PersonTable));
            Console.WriteLine(oldTable?.GetRecordsJsonObjectString(true) ?? string.Empty);

            var result = _db.UpdateRecordByKeyValue(targetObject, updateObject.GetKeyValuePairs(), nameof(PersonTable));
            Assert.IsNotNull(result);

            Console.WriteLine("After insert data:");
            var newTable = _db.GetRecordByKeyValue(targetObject, EnumQueryOperator.Like, nameof(PersonTable));
            Console.WriteLine(newTable?.GetRecordsJsonObjectString(true) ?? string.Empty);
        }
        [TestMethod()]
        public virtual void DeleteRecordByIdTest()
        {
            PersonTable p = new()
            {
                Name = "FF",
                Age = 38
            };

            var target = _db.InsertRecord(p.GetKeyValuePairs(), nameof(PersonTable));

            long pk = target?.GetObject<PersonTable>()?.Id?? 1;
            Console.WriteLine("Before Delete data:");
            var oldTable = _db.GetRecordById(pk, nameof(PersonTable));
            Console.WriteLine(oldTable?.GetRecordsJsonObjectString(true) ?? string.Empty);

            Assert.IsTrue(_db.DeleteRecordById(pk, "PersonTable"));

            Console.WriteLine("After Delete data:");
            var newTable = _db.GetRecordById(pk, nameof(PersonTable));
            Console.WriteLine(newTable?.GetRecordsJsonObjectString(true) ?? string.Empty);
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