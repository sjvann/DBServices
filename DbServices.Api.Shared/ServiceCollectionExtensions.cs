using DbServices.Core.Models.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace DbServices.Api.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDbApiServices(
        this IServiceCollection services,
        string connectionString,
        string serverTypeName)
    {
        services.AddSingleton<DbServiceHolder>(sp =>
        {
            var holder = new DbServiceHolder();
            var db = DbServiceFactory.Create(connectionString, serverTypeName)
                ?? throw new InvalidOperationException($"無法初始化資料庫服務，伺服器類型: {serverTypeName}");
            holder.Initialize(db);
            return holder;
        });
        services.AddSingleton<DbTableOperations>();
        return services;
    }

    public static IServiceCollection AddDbApiServices(
        this IServiceCollection services,
        Func<IServiceProvider, IDbService> dbFactory)
    {
        services.AddSingleton<DbServiceHolder>(sp =>
        {
            var holder = new DbServiceHolder();
            holder.Initialize(dbFactory(sp));
            return holder;
        });
        services.AddSingleton<DbTableOperations>();
        return services;
    }
}
