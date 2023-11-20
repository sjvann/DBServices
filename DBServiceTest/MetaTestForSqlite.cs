using DBService;
using DBService.Models;
using DBService.Models.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{

    [TestClass]
    public class MetaTestForSqlite
    {
        private readonly string connString = new (@"Data Source=c:\\temp\\Test.db;");
        private readonly MainService db;
        private readonly string  _tableName = "OrderItemTable";

        public MetaTestForSqlite()
        {
            db = MainService.GetInstance(connString).UseSQLite();

        }
        #region Query
        [TestMethod]
        public void GetTableList()
        {
            var tableList = db.GetTableNamesFromDb();
            Assert.IsTrue(tableList?.Count() > 0);
            Console.WriteLine(string.Join(',', tableList));

        }

        [TestMethod]
        public void GetAllFromDb()
        {
            TableBaseModel? result = db.GetRecordByTable(_tableName);
            if (result != null)
            {
                Console.WriteLine(result.ToFullJsonObject()?.ToJsonString());
            }
            Assert.IsNotNull(result);
          
        }
        [TestMethod]
        public void GetRecordById()
        {
            TableBaseModel? result = db.GetRecordById(1);
            if (result != null)
            {
                Console.WriteLine(result.GetRecordsJsonObject());
            }
            Assert.IsNotNull(result);
          
        }
        [TestMethod]
        public void GetRecordByKeyValue()
        {

            TableBaseModel? result = db.GetRecordByKeyValue(new KeyValuePair<string, object?>("Name", "BB"));
            if (result != null)
            {
                Console.WriteLine(result.GetRecordsJsonObject());
            }
            Assert.IsNotNull(result);
          
        }
        [TestMethod]
        public void GetMetaData()
        {

           db.SetCurrentTable("Demo") ;
            TableBaseModel? result = db.GetCurrentTable();
            if (result != null)
            {
                Console.Write(result.GetMetaJsonObject());
            }
            Assert.IsNotNull(result);
          
        }

        #endregion

    }
}