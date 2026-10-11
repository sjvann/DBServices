namespace GraphQLService.Models;

public sealed class TreeNodeDto
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Kind { get; init; }
    public string? TableName { get; init; }
    public string? FieldName { get; init; }
    public string? GraphQlTypeName { get; init; }
    public string? ListFieldName { get; init; }
    public string? SingularFieldName { get; init; }
    public string? ByFieldName { get; init; }
    public string? ScalarKind { get; init; }
    public bool Expanded { get; init; }
    public IReadOnlyList<TreeNodeDto>? Children { get; init; }
}

public sealed class ProviderInfoDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string DefaultConnectionString { get; init; }
    public string? ConnectionHint { get; init; }
}

public sealed class ConnectionRequestDto
{
    public string Provider { get; set; } = "Sqlite";
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class ConnectionStatusDto
{
    public bool Connected { get; init; }
    public string? Provider { get; init; }
    public string? ConnectionString { get; init; }
    public int TableCount { get; init; }
}
