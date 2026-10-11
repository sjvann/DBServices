using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.Sqlite.SqlStringGenerator;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DbServices.Provider.Sqlite
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
<<<<<<< HEAD
            _connectionFactory = () => new SqliteConnection(connectionString);
=======
            EnsureDatabaseExists(connectionString);
            _conn = new SqliteConnection(connectionString);
>>>>>>> ContentSeervices
            _sqlProvider = new SqlProviderForSqlite();
            _tableNameList = GetAllTableNames();
        }

        public ProviderService(DbServiceOptions options, ILogger<DataBaseService>? logger = null,
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null,
            ITableStructureCacheService? cacheService = null)
            : base(options, logger, validationService, retryPolicyService, cacheService)
        {
            var connectionString = DbServices.Core.Helpers.ConnectionStringHelper
                .ApplyConnectionPoolSettings(options.ConnectionString, options);

<<<<<<< HEAD
            _connectionFactory = () => new SqliteConnection(connectionString);
=======
            EnsureDatabaseExists(connectionString);
            _conn = new SqliteConnection(connectionString);
>>>>>>> ContentSeervices
            _sqlProvider = new SqlProviderForSqlite();
            _tableNameList = GetAllTableNames();
        }

        /// <summary>
        /// 確保數據庫文件存在，如果不存在則自動創建
        /// </summary>
        /// <param name="connectionString">連接字串</param>
        private static void EnsureDatabaseExists(string connectionString)
        {
            try
            {
                var builder = new SqliteConnectionStringBuilder(connectionString);
                var dataSource = builder.DataSource;

                if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:")
                {
                    // 記憶體數據庫或無效路徑，不需要創建文件
                    return;
                }

                var dbPath = Path.GetFullPath(dataSource);
                var directory = Path.GetDirectoryName(dbPath);

                // 確保目錄存在
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 如果數據庫文件不存在，創建一個空的數據庫
                if (!File.Exists(dbPath))
                {
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();
                    // 連接打開後會自動創建數據庫文件
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"無法創建 SQLite 數據庫: {ex.Message}", ex);
            }
        }
    }
}
