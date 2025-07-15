
using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.MySql.SqlStringGenerator;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Logging;

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

        public ProviderService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null) 
            : base(options, logger, validationService, retryPolicyService)
        {
            _conn = new MySqlConnection(options.ConnectionString);
            _sqlProvider = new SqlProviderForMySql();
            _tableNameList = GetAllTableNames();
        }
    }
}
