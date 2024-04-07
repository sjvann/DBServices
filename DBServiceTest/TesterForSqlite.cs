using DbServices.Provider.Sqlite;
using DBServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBServiceTest
{

    [TestClass]
    public class TesterForSqlite : DataBaseServiceTests
    {
        private const string connString = @"Data Source=C:\\temp\\Test.db;";
        public TesterForSqlite() : base(MainService.UseDataBase(connString,
            (connString) =>
            {
                return new ProviderService(connString);
            }))
        { }
    }
}