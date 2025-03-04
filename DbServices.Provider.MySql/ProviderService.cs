
using DbServices.Core;
using DBServices.SqlStringGenerator.MySql;
using MySql.Data.MySqlClient;



namespace DbServices.Provider.MySql
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new MySqlConnection(connectionString);
            _sqlProvider = new SqlProviderForMySql();
            _tableNameList = GetAllTableNames();
        }


    }
}
