using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.PostgreSQL.SqlStringGenerator;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace DbServices.Provider.PostgreSQL
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new NpgsqlConnection(connectionString);
            _sqlProvider = new SqlProviderForPostgreSQL();
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
            
            _conn = new NpgsqlConnection(connectionString);
            _sqlProvider = new SqlProviderForPostgreSQL();
            _tableNameList = GetAllTableNames();
        }
    }
}

