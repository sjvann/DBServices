namespace DbServices.Core.Session
{
    public sealed class QueryResult
    {
        public QueryKind Kind { get; init; }
        public IReadOnlyList<string> Columns { get; init; } = [];
        public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = [];
        public int AffectedRows { get; init; }
        public string? CommandTag { get; init; }
        public string? Error { get; init; }

        public static QueryResult FromRows(string commandTag, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<object?>> rows)
            => new()
            {
                Kind = QueryKind.Rows,
                CommandTag = commandTag,
                Columns = columns,
                Rows = rows,
                AffectedRows = rows.Count
            };

        public static QueryResult FromNonQuery(string commandTag, int affectedRows)
            => new()
            {
                Kind = QueryKind.NonQuery,
                CommandTag = commandTag,
                AffectedRows = affectedRows
            };

        public static QueryResult FromError(string error)
            => new()
            {
                Kind = QueryKind.Error,
                Error = error,
                CommandTag = "ERROR"
            };

        public static QueryResult Empty(string commandTag)
            => new() { Kind = QueryKind.Rows, CommandTag = commandTag };
    }
}
