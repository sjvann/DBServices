
using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.MsSql.SqlStringGenerator;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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

        public ProviderService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null,
            ITableStructureCacheService? cacheService = null) 
            : base(options, logger, validationService, retryPolicyService, cacheService)
        {
            // 套用連線池設定到連線字串
            var connectionString = DbServices.Core.Helpers.ConnectionStringHelper
                .ApplyConnectionPoolSettings(options.ConnectionString, options);
            
            _conn = new SqlConnection(connectionString);
            _sqlProvider = new SqlProviderForMsSql();
            _tableNameList = GetAllTableNames();
        }
    }
}
