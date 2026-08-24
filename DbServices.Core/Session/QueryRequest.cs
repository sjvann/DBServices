namespace DbServices.Core.Session
{
    public sealed class QueryRequest
    {
        public string Text { get; init; } = string.Empty;
        public QueryOpcode Opcode { get; init; } = QueryOpcode.Auto;
        public IReadOnlyDictionary<string, object?> Arguments { get; init; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        public ResultFormat Format { get; init; } = ResultFormat.Table;
    }
}
