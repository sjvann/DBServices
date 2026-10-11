using DbServices.Core.Models.Interface;

namespace DbServices.Api.Shared;

/// <summary>
/// 以 Singleton 持有已初始化的 <see cref="IDbService"/>。
/// </summary>
public sealed class DbServiceHolder
{
    private IDbService? _dbService;

    public IDbService Db => _dbService
        ?? throw new InvalidOperationException("資料庫服務尚未初始化。請先呼叫 Initialize。");

    public bool IsInitialized => _dbService is not null;

    public void Initialize(IDbService dbService)
    {
        _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
    }
}
