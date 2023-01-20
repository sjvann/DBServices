using DBService;
using DBService.Models;
using DBService.Models.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{

    [TestClass]
    public class MetaTestForSqlite
    {
        private readonly string connString = "Data Source=c://temp//Test.db;Version=3;";
        private readonly IDbService db;

        public MetaTestForSqlite()
        {
            db = new MainService(connString).UseSQLite("Demo");

        }
        #region Query
        [TestMethod]
        public void GetTableList()
        {
            var tableList = db.GetTableNameList();
            Assert.IsTrue(tableList?.Count() > 0);
            Console.WriteLine(string.Join(',', tableList));

        }
        [TestMethod]
        public void GetFieldList()
        {
            var fieldList = db.GetFieldObjectByName("Demo");
            Assert.IsTrue(fieldList?.Count() > 0);
            Console.WriteLine(string.Join(',', fieldList.Select(x => x.FieldName)));

        }
        [TestMethod]
        public void GetAllFieldList()
        {
            var allFieldList = db.GetAllFieldList();
            Assert.IsTrue(allFieldList?.Count > 0);
            Console.WriteLine(string.Join(',', allFieldList.Where(x => x.Key == "Demo").Select(x => x.Value).First().Select(x => x.FieldName)));

        }
        [TestMethod]
        public void CheckHasRecord()
        {
            Assert.IsTrue(db.CheckHasRecord());

        }
        [TestMethod]
        public void GetAllFromDb()
        {
            TableBaseModel? result = db.GetRecordByTable();
            Assert.IsNotNull(result);
            if (result != null)
            {
                Console.WriteLine(result.ToJsonObject()?.ToJsonString());

                 Console.WriteLine(result.ToJsonObject(true)?.ToJsonString());
            }
        }
        [TestMethod]
        public void GetRecordById()
        {
            TableBaseModel? result = db.GetRecordById(1);
            Assert.IsNotNull(result);
            if (result != null)
            {
                Console.WriteLine(result.ToJsonObject(true)?.ToJsonString());
            }
        }
        [TestMethod]
        public void GetRecordByKeyValue()
        {

            TableBaseModel? result = db.GetRecordByKeyValue(new KeyValuePair<string, object>("Name", "BB"));
            Assert.IsNotNull(result);
            if (result != null)
            {
                Console.WriteLine(result.ToJsonObject(true)?.ToJsonString());
            }
        }
        #endregion

    }
}