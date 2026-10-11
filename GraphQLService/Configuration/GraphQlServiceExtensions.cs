using GraphQLService.Resolvers;
using GraphQLService.Schema;
using GraphQLService.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GraphQLService.Configuration;

public static class GraphQlServiceExtensions
{
    public static IServiceCollection AddDbGraphQL(
        this IServiceCollection services,
        string connectionString,
        string serverTypeName,
        bool includeExceptionDetails = false)
    {
        services.TryAddSingleton<DbConnectionSession>();
        services.TryAddSingleton<DynamicTableResolver>();
        services.TryAddSingleton<GraphQlExecutorReloader>();

        var graphqlBuilder = services.AddGraphQLServer()
            .AddMaxExecutionDepthRule(15)
            .ModifyRequestOptions(o => o.IncludeExceptionDetails = includeExceptionDetails);

        using var sp = services.BuildServiceProvider();
        var session = sp.GetRequiredService<DbConnectionSession>();
        session.ConnectAsync(serverTypeName, connectionString).GetAwaiter().GetResult();

        DbGraphQlSchemaConfigurator.AddDynamicDbSchema(graphqlBuilder, session.Registry!);

        return services;
    }

    public static async Task ReconnectAsync(
        this WebApplication app,
        string provider,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var session = app.Services.GetRequiredService<DbConnectionSession>();
        await session.ConnectAsync(provider, connectionString, cancellationToken);

        var reloader = app.Services.GetRequiredService<GraphQlExecutorReloader>();
        await reloader.EvictAsync(cancellationToken);
    }

    public static async Task InitializeDefaultConnectionAsync(
        this WebApplication app,
        string connectionString,
        string serverTypeName,
        CancellationToken cancellationToken = default)
    {
        await app.ReconnectAsync(serverTypeName, connectionString, cancellationToken);
    }
}
