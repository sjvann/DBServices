namespace GraphQLService.Schema;

public sealed class MetaPayload
{
    public IReadOnlyList<string> Tables { get; init; } = Array.Empty<string>();
}
