using DbServices.Core.Configuration;

namespace DbServices.Core.Helpers
{
    /// <summary>
    /// 連線字串輔助類別
    /// 用於動態調整連線字串中的連線池設定
    /// </summary>
    public static class ConnectionStringHelper
    {
        /// <summary>
        /// 為連線字串加入或更新連線池設定
        /// </summary>
        /// <param name="connectionString">原始連線字串</param>
        /// <param name="options">資料庫服務選項</param>
        /// <returns>更新後的連線字串</returns>
        public static string ApplyConnectionPoolSettings(string connectionString, DbServiceOptions options)
        {
            if (string.IsNullOrEmpty(connectionString) || options == null)
                return connectionString;

            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };

            // 根據資料庫類型設定連線池參數
            var provider = DetectDatabaseProvider(connectionString);
            
            switch (provider)
            {
                case DatabaseProviderType.SqlServer:
                    builder["Min Pool Size"] = options.MinPoolSize;
                    builder["Max Pool Size"] = options.MaxPoolSize;
                    builder["Pooling"] = true;
                    break;

                case DatabaseProviderType.PostgreSQL:
                    builder["Minimum Pool Size"] = options.MinPoolSize;
                    builder["Maximum Pool Size"] = options.MaxPoolSize;
                    builder["Pooling"] = true;
                    break;

                case DatabaseProviderType.MySQL:
                    builder["MinimumPoolSize"] = options.MinPoolSize;
                    builder["MaximumPoolSize"] = options.MaxPoolSize;
                    builder["Pooling"] = true;
                    break;

                case DatabaseProviderType.Oracle:
                    builder["Min Pool Size"] = options.MinPoolSize;
                    builder["Max Pool Size"] = options.MaxPoolSize;
                    builder["Pooling"] = true;
                    break;

                case DatabaseProviderType.SQLite:
                    // SQLite 不支援傳統連線池，但可以設定快取模式
                    builder["Cache"] = "Shared";
                    break;
            }

            return builder.ConnectionString;
        }

        /// <summary>
        /// 偵測連線字串中的資料庫類型
        /// </summary>
        /// <param name="connectionString">連線字串</param>
        /// <returns>資料庫類型</returns>
        private static DatabaseProviderType DetectDatabaseProvider(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return DatabaseProviderType.Unknown;

            var lower = connectionString.ToLowerInvariant();

            if (lower.Contains("server=") || lower.Contains("data source=") || lower.Contains("initial catalog="))
                return DatabaseProviderType.SqlServer;

            if (lower.Contains("host=") && (lower.Contains("database=") || lower.Contains("db=")))
            {
                if (lower.Contains("provider=") && lower.Contains("postgresql"))
                    return DatabaseProviderType.PostgreSQL;
                return DatabaseProviderType.PostgreSQL; // 預設為 PostgreSQL
            }

            if (lower.Contains("server=") && (lower.Contains("database=") || lower.Contains("uid=") || lower.Contains("user id=")))
            {
                if (lower.Contains("provider=") && lower.Contains("mysql"))
                    return DatabaseProviderType.MySQL;
                return DatabaseProviderType.MySQL; // 預設為 MySQL
            }

            if (lower.Contains("data source=") && lower.Contains(".db"))
                return DatabaseProviderType.SQLite;

            if (lower.Contains("oracle") || lower.Contains("tns="))
                return DatabaseProviderType.Oracle;

            return DatabaseProviderType.Unknown;
        }

        /// <summary>
        /// 資料庫提供者類型
        /// </summary>
        private enum DatabaseProviderType
        {
            Unknown,
            SqlServer,
            PostgreSQL,
            MySQL,
            Oracle,
            SQLite
        }
    }
}

