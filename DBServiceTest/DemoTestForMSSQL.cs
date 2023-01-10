using DBService;
using DBService.Models.Enum;
using DBServiceTest.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{

    [TestClass]
    public class DemoTestForMsSql
    {
        private readonly string connString = "Data Source=localhost;Initial Catalog=DbService;User ID=dbserviceadm;Password=fmjcjjmf5$QQ;";
        private readonly DbServiceGeneric<Demo> db;

        public DemoTestForMsSql()
        {
            db = new DbServiceGeneric<Demo>(EnumSupportedServer.SqlServer, connString);
        }
        #region Query
        [TestMethod]
        public void GetDemoTableAcount()
        {
            var AllItems = db.GetRecordbyTable();
            Assert.IsTrue(AllItems?.Count() > 0);

        }

        [TestMethod]
        public void GetDemoTableId()
        {
            Demo? item = db.GetRecord(1);
            Assert.IsNotNull(item);
        }
        [TestMethod]
        public void GetDemoTableByKey()
        {
            var item = db.GetRecordByKey("Name", "AA");
            Assert.IsNotNull(item);
        }
        [TestMethod]
        public void GetDemoFields()
        {
            string[] fields = new string[] { "Id", "Name" };
            var item = db.GetRecords(fields);
            Assert.IsNotNull(item);
            Assert.IsTrue(item.Any());
        }
        #endregion
        #region DML
        [TestMethod]
        public void CreateDemoRecord()
        {
            string newName = $"New_{DateTime.Now:HHmmss}";
            List<Demo> records = new();
            var oneRecord = new Demo() {
                Name = newName,
                LoginTimes = 3,
                BirthOfDay = "20220829"
            };
            records.Add(oneRecord);
            var resultId = db.AddRecord<int>(oneRecord);
            var newRecord = db.GetRecord(resultId);
            Assert.IsNotNull(resultId);

            Assert.AreEqual(newName, newRecord?.Name);
        }
        [TestMethod]
        public void UpdateDemoRecord()
        {
            string newName = $"New_{DateTime.Now:HHmmss}";
            int currentId = db.GetRecordbyTable()?.Last().Id ?? 2;
            Demo newOne = new()
            {
                Name = newName,
                LoginTimes = 3,
                BirthOfDay = "20100303"
            };
            var result = db.UpdateRecord(currentId, newOne);
            Assert.IsNotNull(result);
            Assert.AreEqual(newName, result.Name);
        }
        [TestMethod]
        public void UpdateDemoRecordByKey()
        {
            string newName = $"New_{DateTime.Now:HHmmss}";
            Demo? current = db.GetRecordbyTable()?.Last();
            if (current != null && current.Id != null)
            {
                Demo newOne = new()
                {
                    Id = current.Id,
                    Name = newName,
                    LoginTimes = 3,
                   BirthOfDay = new DateTime(2022,8,29).ToString("yyyyMMdd")
                };
                if (current.Name != null)
                {
                    current = db.UpdateRecordByKey("Name", current.Name, newOne)?.FirstOrDefault();
                }

            }
            Assert.IsNotNull(current);
            Assert.AreEqual(newName, current.Name);

        }
        [TestMethod]
        public void DeleteDemoRecord()
        {
            int deleteId = db.GetRecordbyTable()?.Last().Id ?? 2;
            var result = db.DeleteRecord(deleteId);
            Assert.IsTrue(result);
        }
        [TestMethod]
        public void DeleteDemoRecordByKey()
        {
            var deleteOne = db.GetRecordbyTable()?.Last();
            bool result = (deleteOne != null && deleteOne.Name != null) && db.DeleteRecordByKey("Name", deleteOne.Name);
            Assert.IsTrue(result);
        }
        [TestMethod]
        public void CheckRecords()
        {
            int result = db.CheckHasRecord();
            Assert.IsTrue(result > 0);
        }
        #endregion
        #region DCL

        #endregion
    }
}