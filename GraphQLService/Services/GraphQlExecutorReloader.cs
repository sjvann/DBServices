namespace GraphQLService.Services;

/// <summary>
/// 連線變更後的 Schema 更新占位（完整 GraphQL Schema 重建需重新啟動服務）。
/// </summary>
public sealed class GraphQlExecutorReloader
{
    public Task EvictAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
