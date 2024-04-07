
using DbServices.Core;
using DbServices.Provider.MsSql.SqlStringGenerator;
using Microsoft.Data.SqlClient;

namespace DbServices.Provider.MsSql
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new SqlConnection(connectionString);
            _sqlProvider = new SqlProviderForMsSql();
            _tableNameList = GetAllTableNames();
        }

    }
}
