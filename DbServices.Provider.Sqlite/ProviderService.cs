using DbServices.Core;
using DbServices.Provider.Sqlite.SqlStringGenerator;
using Microsoft.Data.Sqlite;


namespace DbServices.Provider.Sqlite
{
    public  class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new SqliteConnection(connectionString);
            _sqlProvider = new SqlProviderForSqlite();
            _tableNameList = GetAllTableNames();
        }
    }
}
