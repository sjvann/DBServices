
using DBServices.SqlStringGenerator.MySql;
using MySql.Data.MySqlClient;


namespace DBServices
{
    public class MySqlService : DataBaseService
    {
        public MySqlService(string connectionString) : base(connectionString)
        {
            _conn = new MySqlConnection(connectionString);
            _sqlProvider = new SqlProviderForMySql();
            _tableNameList = GetAllTableNames();
        }


    }
}
