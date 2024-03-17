
using DBServices.SqlStringGenerator.Oracle;
using Oracle.ManagedDataAccess.Client;


namespace DBServices
{
    public class OracleService : DataBaseService
    {
        public OracleService(string connectionString) : base(connectionString)
        {
            _conn = new OracleConnection(connectionString);
            _sqlProvider = new SqlProviderForOracle();
            _tableNameList = GetAllTableNames();
        }


    }
}
