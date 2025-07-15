using DbServices.Core.Configuration;
using DbServices.Core.Extensions;
using DbServices.Core.Models.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace DBServices.Extensions
{
    /// <summary>
    /// DBServices 依賴注入擴充方法
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 註冊 SQLite 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <param name="configureOptions">設定選項委派</param>
        /// <returns>服務集合</returns>
        public static IServiceCollection AddSQLiteDbService(this IServiceCollection services, 
            string connectionString, 
            Action<DbServiceOptions>? configureOptions = null)
        {
            // 註冊核心服務
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
                configureOptions?.Invoke(options);
            });

            // 註冊 SQLite 特定服務
            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                configureOptions?.Invoke(options);
                
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<DbServices.Core.DataBaseService>>();
                var validation = provider.GetService<DbServices.Core.Services.IValidationService>();
                var retry = provider.GetService<DbServices.Core.Services.IRetryPolicyService>();
                
                return new DbServices.Provider.Sqlite.ProviderService(options, logger, validation, retry);
            });

            return services;
        }

        /// <summary>
        /// 註冊 SQL Server 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <param name="configureOptions">設定選項委派</param>
        /// <returns>服務集合</returns>
        public static IServiceCollection AddSqlServerDbService(this IServiceCollection services, 
            string connectionString, 
            Action<DbServiceOptions>? configureOptions = null)
        {
            // 註冊核心服務
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
                configureOptions?.Invoke(options);
            });

            // 註冊 SQL Server 特定服務
            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                configureOptions?.Invoke(options);
                
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<DbServices.Core.DataBaseService>>();
                var validation = provider.GetService<DbServices.Core.Services.IValidationService>();
                var retry = provider.GetService<DbServices.Core.Services.IRetryPolicyService>();
                
                return new DbServices.Provider.MsSql.ProviderService(options, logger, validation, retry);
            });

            return services;
        }

        /// <summary>
        /// 註冊 MySQL 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <param name="configureOptions">設定選項委派</param>
        /// <returns>服務集合</returns>
        public static IServiceCollection AddMySQLDbService(this IServiceCollection services, 
            string connectionString, 
            Action<DbServiceOptions>? configureOptions = null)
        {
            // 註冊核心服務
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
                configureOptions?.Invoke(options);
            });

            // 註冊 MySQL 特定服務
            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                configureOptions?.Invoke(options);
                
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<DbServices.Core.DataBaseService>>();
                var validation = provider.GetService<DbServices.Core.Services.IValidationService>();
                var retry = provider.GetService<DbServices.Core.Services.IRetryPolicyService>();
                
                return new DbServices.Provider.MySql.ProviderService(options, logger, validation, retry);
            });

            return services;
        }

        /// <summary>
        /// 註冊 Oracle 資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="connectionString">連線字串</param>
        /// <param name="configureOptions">設定選項委派</param>
        /// <returns>服務集合</returns>
        public static IServiceCollection AddOracleDbService(this IServiceCollection services, 
            string connectionString, 
            Action<DbServiceOptions>? configureOptions = null)
        {
            // 註冊核心服務
            services.AddDbServices(options =>
            {
                options.ConnectionString = connectionString;
                configureOptions?.Invoke(options);
            });

            // 註冊 Oracle 特定服務
            services.AddScoped<IDbService>(provider =>
            {
                var options = new DbServiceOptions { ConnectionString = connectionString };
                configureOptions?.Invoke(options);
                
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<DbServices.Core.DataBaseService>>();
                var validation = provider.GetService<DbServices.Core.Services.IValidationService>();
                var retry = provider.GetService<DbServices.Core.Services.IRetryPolicyService>();
                
                return new DbServices.Provider.Oracle.ProviderService(options, logger, validation, retry);
            });

            return services;
        }

        /// <summary>
        /// 註冊多個資料庫服務
        /// </summary>
        /// <param name="services">服務集合</param>
        /// <param name="databases">資料庫設定</param>
        /// <returns>服務集合</returns>
        public static IServiceCollection AddMultipleDbServices(this IServiceCollection services, 
            params (string name, DatabaseProvider provider, string connectionString, Action<DbServiceOptions>? options)[] databases)
        {
            // 註冊核心服務
            services.AddDbServices();

            // 註冊多個資料庫服務
            foreach (var (name, provider, connectionString, configureOptions) in databases)
            {
                services.AddKeyedScoped<IDbService>(name, (serviceProvider, key) =>
                {
                    var options = new DbServiceOptions { ConnectionString = connectionString };
                    configureOptions?.Invoke(options);
                    
                    var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DbServices.Core.DataBaseService>>();
                    var validation = serviceProvider.GetService<DbServices.Core.Services.IValidationService>();
                    var retry = serviceProvider.GetService<DbServices.Core.Services.IRetryPolicyService>();

                    return provider switch
                    {
                        DatabaseProvider.SQLite => new DbServices.Provider.Sqlite.ProviderService(options, logger, validation, retry),
                        DatabaseProvider.SqlServer => new DbServices.Provider.MsSql.ProviderService(options, logger, validation, retry),
                        DatabaseProvider.MySQL => new DbServices.Provider.MySql.ProviderService(options, logger, validation, retry),
                        DatabaseProvider.Oracle => new DbServices.Provider.Oracle.ProviderService(options, logger, validation, retry),
                        _ => throw new ArgumentException($"不支援的資料庫類型: {provider}")
                    };
                });
            }

            return services;
        }
    }
}
