using DbServices.Api.Shared;
using GraphQLService.Schema;

namespace GraphQLService.Services;

public sealed class DbConnectionSession
{
    private readonly ILogger<DbConnectionSession> _logger;
    private readonly ILogger<DbSchemaRegistry> _registryLogger;
    private readonly object _lock = new();

    public DbConnectionSession(
        ILogger<DbConnectionSession> logger,
        ILogger<DbSchemaRegistry> registryLogger)
    {
        _logger = logger;
        _registryLogger = registryLogger;
        Holder = new DbServiceHolder();
    }

    public DbServiceHolder Holder { get; }

    public DbTableOperations? Operations { get; private set; }

    public DbSchemaRegistry? Registry { get; private set; }

    public string? Provider { get; private set; }

    public string? ConnectionString { get; private set; }

    public bool IsConnected => Holder.IsInitialized;

    public async Task ConnectAsync(string provider, string connectionString, CancellationToken cancellationToken = default)
    {
        var db = DbServiceFactory.Create(connectionString, provider)
            ?? throw new InvalidOperationException($"不支援的資料庫類型: {provider}");

        lock (_lock)
        {
            Holder.Initialize(db);
            Operations = new DbTableOperations(Holder);
            Registry = new DbSchemaRegistry(Operations, _registryLogger);
            Provider = provider;
            ConnectionString = connectionString;
        }

        await Registry.InitializeAsync(cancellationToken);
        _logger.LogInformation("已連線至 {Provider}", provider);
    }
}
