using DbServices.Core.Configuration;
using DbServices.Core.Models.Interface;
using DbServices.Core.Services;
using Microsoft.Extensions.Logging;

namespace DBServices
{
    /// <summary>
    /// 資料庫服務建構器介面
    /// </summary>
    public interface IDbServiceBuilder
    {
        /// <summary>
        /// 設定日誌服務
        /// </summary>
        /// <param name="logger">日誌服務</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithLogging(ILogger logger);

        /// <summary>
        /// 設定驗證服務
        /// </summary>
        /// <param name="validationService">驗證服務</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithValidation(IValidationService validationService);

        /// <summary>
        /// 設定重試政策服務
        /// </summary>
        /// <param name="retryService">重試政策服務</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithRetryPolicy(IRetryPolicyService retryService);

        /// <summary>
        /// 設定命令逾時
        /// </summary>
        /// <param name="timeoutSeconds">逾時秒數</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithTimeout(int timeoutSeconds);

        /// <summary>
        /// 啟用查詢快取
        /// </summary>
        /// <param name="enabled">是否啟用</param>
        /// <param name="expirationMinutes">快取過期時間（分鐘）</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithQueryCache(bool enabled = true, int expirationMinutes = 5);

        /// <summary>
        /// 設定連線池
        /// </summary>
        /// <param name="minPoolSize">最小連線池大小</param>
        /// <param name="maxPoolSize">最大連線池大小</param>
        /// <returns>建構器</returns>
        IDbServiceBuilder WithConnectionPool(int minPoolSize = 5, int maxPoolSize = 100);

        /// <summary>
        /// 建立 SQLite 資料庫服務
        /// </summary>
        /// <returns>資料庫服務</returns>
        IDbService BuildSQLite();

        /// <summary>
        /// 建立 SQL Server 資料庫服務
        /// </summary>
        /// <returns>資料庫服務</returns>
        IDbService BuildSqlServer();

        /// <summary>
        /// 建立 MySQL 資料庫服務
        /// </summary>
        /// <returns>資料庫服務</returns>
        IDbService BuildMySQL();

        /// <summary>
        /// 建立 Oracle 資料庫服務
        /// </summary>
        /// <returns>資料庫服務</returns>
        IDbService BuildOracle();
    }

    /// <summary>
    /// 資料庫服務建構器實作
    /// </summary>
    public class DbServiceBuilder : IDbServiceBuilder
    {
        private readonly DbServiceOptions _options;
        private ILogger? _logger;
        private IValidationService? _validationService;
        private IRetryPolicyService? _retryPolicyService;

        public DbServiceBuilder(string connectionString)
        {
            _options = new DbServiceOptions
            {
                ConnectionString = connectionString
            };
        }

        public DbServiceBuilder(DbServiceOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IDbServiceBuilder WithLogging(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public IDbServiceBuilder WithValidation(IValidationService validationService)
        {
            _validationService = validationService;
            return this;
        }

        public IDbServiceBuilder WithRetryPolicy(IRetryPolicyService retryService)
        {
            _retryPolicyService = retryService;
            return this;
        }

        public IDbServiceBuilder WithTimeout(int timeoutSeconds)
        {
            _options.CommandTimeout = timeoutSeconds;
            return this;
        }

        public IDbServiceBuilder WithQueryCache(bool enabled = true, int expirationMinutes = 5)
        {
            _options.EnableQueryCache = enabled;
            _options.CacheExpirationMinutes = expirationMinutes;
            return this;
        }

        public IDbServiceBuilder WithConnectionPool(int minPoolSize = 5, int maxPoolSize = 100)
        {
            _options.MinPoolSize = minPoolSize;
            _options.MaxPoolSize = maxPoolSize;
            return this;
        }

        public IDbService BuildSQLite()
        {
            if (_logger is ILogger<DbServices.Core.DataBaseService> typedLogger)
            {
                return new DbServices.Provider.Sqlite.ProviderService(_options, typedLogger, _validationService, _retryPolicyService);
            }
            else
            {
                return new DbServices.Provider.Sqlite.ProviderService(_options.ConnectionString);
            }
        }

        public IDbService BuildSqlServer()
        {
            if (_logger is ILogger<DbServices.Core.DataBaseService> typedLogger)
            {
                return new DbServices.Provider.MsSql.ProviderService(_options, typedLogger, _validationService, _retryPolicyService);
            }
            else
            {
                return new DbServices.Provider.MsSql.ProviderService(_options.ConnectionString);
            }
        }

        public IDbService BuildMySQL()
        {
            if (_logger is ILogger<DbServices.Core.DataBaseService> typedLogger)
            {
                return new DbServices.Provider.MySql.ProviderService(_options, typedLogger, _validationService, _retryPolicyService);
            }
            else
            {
                return new DbServices.Provider.MySql.ProviderService(_options.ConnectionString);
            }
        }

        public IDbService BuildOracle()
        {
            if (_logger is ILogger<DbServices.Core.DataBaseService> typedLogger)
            {
                return new DbServices.Provider.Oracle.ProviderService(_options, typedLogger, _validationService, _retryPolicyService);
            }
            else
            {
                return new DbServices.Provider.Oracle.ProviderService(_options.ConnectionString);
            }
        }
    }
}
