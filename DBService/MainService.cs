using DbServices.Core.Models.Interface;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DBServices
{
    /// <summary>
    /// 起始服務物件。透過此物件來設定資料庫類別。
    /// </summary>
    public static class MainService
    {
        #region 設定資料庫廠商

        public static IDbService UseDataBase(string connectString, Func<string, IDbService> providerService)
        {
            return providerService(connectString);
        }

        /// <summary>
        /// 使用SQLite資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseSQLite(string connectString)
        {
           return  new DbServices.Provider.Sqlite.ProviderService(connectString);
          
        }

        /// <summary>
        /// 使用SQLite資料庫（進階版）
        /// </summary>
        /// <param name="options">資料庫服務設定選項</param>
        /// <param name="serviceProvider">服務提供者（用於取得日誌和其他服務）</param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseSQLite(DbServiceOptions options, IServiceProvider? serviceProvider = null)
        {
            var logger = serviceProvider?.GetService<ILogger<DbServices.Core.DataBaseService>>();
            var validation = serviceProvider?.GetService<IValidationService>();
            var retry = serviceProvider?.GetService<IRetryPolicyService>();
            
            return new DbServices.Provider.Sqlite.ProviderService(options, logger, validation, retry);
        }
        
        /// <summary>
        /// 使用MsSQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMsSQL(string connectString)
        {
            return new DbServices.Provider.MsSql.ProviderService(connectString);
        }

        /// <summary>
        /// 使用MsSQL資料庫（進階版）
        /// </summary>
        /// <param name="options">資料庫服務設定選項</param>
        /// <param name="serviceProvider">服務提供者（用於取得日誌和其他服務）</param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMsSQL(DbServiceOptions options, IServiceProvider? serviceProvider = null)
        {
            var logger = serviceProvider?.GetService<ILogger<DbServices.Core.DataBaseService>>();
            var validation = serviceProvider?.GetService<IValidationService>();
            var retry = serviceProvider?.GetService<IRetryPolicyService>();
            
            return new DbServices.Provider.MsSql.ProviderService(options, logger, validation, retry);
        }

        /// <summary>
        /// 使用MySQL資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMySQL(string connectString)
        {
            return new DbServices.Provider.MySql.ProviderService(connectString);
        }

        /// <summary>
        /// 使用MySQL資料庫（進階版）
        /// </summary>
        /// <param name="options">資料庫服務設定選項</param>
        /// <param name="serviceProvider">服務提供者（用於取得日誌和其他服務）</param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseMySQL(DbServiceOptions options, IServiceProvider? serviceProvider = null)
        {
            var logger = serviceProvider?.GetService<ILogger<DbServices.Core.DataBaseService>>();
            var validation = serviceProvider?.GetService<IValidationService>();
            var retry = serviceProvider?.GetService<IRetryPolicyService>();
            
            return new DbServices.Provider.MySql.ProviderService(options, logger, validation, retry);
        }
        
        /// <summary>
        /// 使用Oracle資料庫
        /// </summary>
        /// <param name="connectString"></param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseOracle(string connectString)
        {
            return new DbServices.Provider.Oracle.ProviderService(connectString);
        }

        /// <summary>
        /// 使用Oracle資料庫（進階版）
        /// </summary>
        /// <param name="options">資料庫服務設定選項</param>
        /// <param name="serviceProvider">服務提供者（用於取得日誌和其他服務）</param>
        /// <returns>DataBaseService</returns>
        public static IDbService UseOracle(DbServiceOptions options, IServiceProvider? serviceProvider = null)
        {
            var logger = serviceProvider?.GetService<ILogger<DbServices.Core.DataBaseService>>();
            var validation = serviceProvider?.GetService<IValidationService>();
            var retry = serviceProvider?.GetService<IRetryPolicyService>();
            
            return new DbServices.Provider.Oracle.ProviderService(options, logger, validation, retry);
        }
        #endregion

        #region 非同步工廠方法

        /// <summary>
        /// 非同步建立並測試資料庫連線
        /// </summary>
        /// <param name="connectionString">連線字串</param>
        /// <param name="providerType">Provider 類型</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>資料庫服務</returns>
        public static async Task<IDbService> CreateAndTestAsync(string connectionString, DatabaseProvider providerType, CancellationToken cancellationToken = default)
        {
            var service = providerType switch
            {
                DatabaseProvider.SQLite => UseSQLite(connectionString),
                DatabaseProvider.SqlServer => UseMsSQL(connectionString),
                DatabaseProvider.MySQL => UseMySQL(connectionString),
                DatabaseProvider.Oracle => UseOracle(connectionString),
                _ => throw new ArgumentException($"不支援的資料庫類型: {providerType}")
            };

            // 測試連線
            if (service is IDbServiceAsync asyncService)
            {
                await asyncService.GetAllTableNamesAsync(cancellationToken: cancellationToken);
            }
            else
            {
                service.GetAllTableNames();
            }

            return service;
        }

        #endregion

        #region 建構器模式支援

        /// <summary>
        /// 建立資料庫服務建構器
        /// </summary>
        /// <param name="connectionString">連線字串</param>
        /// <returns>資料庫服務建構器</returns>
        public static IDbServiceBuilder CreateBuilder(string connectionString)
        {
            return new DbServiceBuilder(connectionString);
        }

        /// <summary>
        /// 建立資料庫服務建構器
        /// </summary>
        /// <param name="options">資料庫服務設定選項</param>
        /// <returns>資料庫服務建構器</returns>
        public static IDbServiceBuilder CreateBuilder(DbServiceOptions options)
        {
            return new DbServiceBuilder(options);
        }

        #endregion
    }

    /// <summary>
    /// 資料庫 Provider 類型
    /// </summary>
    public enum DatabaseProvider
    {
        SQLite,
        SqlServer,
        MySQL,
        Oracle
    }
}
