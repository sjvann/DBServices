
using DbServices.Core;
using DbServices.Provider.Oracle.SqlStringGenerator;
using Oracle.ManagedDataAccess.Client;


namespace DbServices.Provider.Oracle
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new OracleConnection(connectionString);
            _sqlProvider = new SqlProviderForOracle();
            _tableNameList = GetAllTableNames();
        }


    }
}
