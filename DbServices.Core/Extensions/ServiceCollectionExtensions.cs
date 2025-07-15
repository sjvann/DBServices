using DbServices.Core.Configuration;
using DbServices.Core.Models.Interface;
using DbServices.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DbServices.Core.Extensions
{
    /// <summary>
    /// 依賴注入擴充方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 加入 DbServices 核心服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="configureOptions">設定選項委派</param>
        /// <returns></returns>
        public static IServiceCollection AddDbServices(this IServiceCollection services, Action<DbServiceOptions>? configureOptions = null)
        {
            // 註冊設定選項
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            // 註冊核心服務
            services.AddScoped<IValidationService, ValidationService>();
            services.AddScoped<IRetryPolicyService, RetryPolicyService>();

            // 註冊日誌
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            return services;
        }

        /// <summary>
        /// 加入 SQLite 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <returns></returns>
        public static IServiceCollection AddSQLiteDbService(this IServiceCollection services, string connectionString)
        {
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
            });

            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                var logger = provider.GetService<ILogger<DataBaseService>>();
                var validation = provider.GetService<IValidationService>();
                var retry = provider.GetService<IRetryPolicyService>();
                
                // 這裡需要根據實際的 SQLite Provider 實作來調整
                // return new SqliteProviderService(options, logger, validation, retry);
                throw new NotImplementedException("需要實作 SQLite Provider 服務");
            });

            return services;
        }

        /// <summary>
        /// 加入 SQL Server 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <returns></returns>
        public static IServiceCollection AddSqlServerDbService(this IServiceCollection services, string connectionString)
        {
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
            });

            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                var logger = provider.GetService<ILogger<DataBaseService>>();
                var validation = provider.GetService<IValidationService>();
                var retry = provider.GetService<IRetryPolicyService>();
                
                // 這裡需要根據實際的 SQL Server Provider 實作來調整
                // return new MsSqlProviderService(options, logger, validation, retry);
                throw new NotImplementedException("需要實作 SQL Server Provider 服務");
            });

            return services;
        }
    }
}
