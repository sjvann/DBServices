
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
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null) 
            : base(options, logger, validationService, retryPolicyService)
        {
            _conn = new SqlConnection(options.ConnectionString);
            _sqlProvider = new SqlProviderForMsSql();
            _tableNameList = GetAllTableNames();
        }
    }
}
