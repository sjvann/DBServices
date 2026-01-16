
using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.Oracle.SqlStringGenerator;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Logging;

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

        public ProviderService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null,
            ITableStructureCacheService? cacheService = null) 
            : base(options, logger, validationService, retryPolicyService, cacheService)
        {
            // 套用連線池設定到連線字串
            var connectionString = DbServices.Core.Helpers.ConnectionStringHelper
                .ApplyConnectionPoolSettings(options.ConnectionString, options);
            
            _conn = new OracleConnection(connectionString);
            _sqlProvider = new SqlProviderForOracle();
            _tableNameList = GetAllTableNames();
        }
    }
}
