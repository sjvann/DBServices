
using DBServices.SqlStringGenerator.Sqlite;
using Microsoft.Data.Sqlite;


namespace DBServices
{
    public class SqliteService : DataBaseService
    {
        public SqliteService(string connectionString) : base(connectionString)
        {
            _conn = new SqliteConnection(connectionString);
            _sqlProvider = new SqlProviderForSqlite();
            _tableNameList = GetAllTableNames();
        }
    }
}
