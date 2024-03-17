using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServices.Tests
{

    [TestClass]
    public class TesterForSqlite : DataBaseServiceTests
    {
        private const string connString = @"Data Source=C:\temp\Test.db;";
        public TesterForSqlite() : base(MainService.UseSQLite(connString))
        {
        }
    }
}