
using DBServices.SqlStringGenerator.MsSql;
using Microsoft.Data.SqlClient;

namespace DBServices
{
    public class MsSqlService : DataBaseService
    {
        public MsSqlService(string connectionString) : base(connectionString)
        {
            _conn = new SqlConnection(connectionString);
            _sqlProvider = new SqlProviderForMsSql();
            _tableNameList = GetAllTableNames();
        }

    }
}
