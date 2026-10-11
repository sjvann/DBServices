using DbServices.Api.Shared;
using Microsoft.Extensions.Logging;

namespace GraphQLService.Schema;

public sealed class DbSchemaRegistry
{
    private readonly DbTableOperations _operations;
    private readonly ILogger<DbSchemaRegistry> _logger;

    public DbSchemaRegistry(DbTableOperations operations, ILogger<DbSchemaRegistry> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    public IReadOnlyList<TableDescriptor> Tables { get; private set; } = Array.Empty<TableDescriptor>();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var tableNames = await _operations.GetAllTableNamesAsync(cancellationToken);
        if (tableNames is null || tableNames.Length == 0)
        {
            _logger.LogWarning("資料庫中沒有可用的資料表");
            Tables = Array.Empty<TableDescriptor>();
            return;
        }

        var descriptors = new List<TableDescriptor>();
        foreach (var tableName in tableNames)
        {
            var fields = await _operations.GetFieldsAsync(tableName, cancellationToken);
            descriptors.Add(TableDescriptor.Create(tableName, fields, _logger));
            _logger.LogInformation("已註冊 GraphQL 型別: {TypeName} (資料表: {Table})", descriptors[^1].GraphQlTypeName, tableName);
        }

        Tables = descriptors;
    }
}
