using DbServices.Core.Models;

namespace DbServices.Core.Session
{
    public static class QueryResultMapper
    {
        public static QueryResult FromTable(TableBaseModel? table, string commandTag)
        {
            if (table?.Records == null)
                return QueryResult.Empty(commandTag);

            var columns = table.Fields?
                .Where(f => !string.IsNullOrEmpty(f.FieldName))
                .Select(f => f.FieldName!)
                .ToList() ?? [];

            if (columns.Count == 0)
            {
                columns = table.Records
                    .SelectMany(r => r.FieldValue?.Select(kv => kv.Key) ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var rows = new List<IReadOnlyList<object?>>();
            foreach (var record in table.Records)
            {
                var row = columns.Select(col => record.GetFieldValue(col)).ToList();
                rows.Add(row);
            }

            return QueryResult.FromRows(commandTag, columns, rows);
        }

        public static QueryResult FromDynamics(IEnumerable<dynamic> rows, string commandTag)
        {
            var list = rows.ToList();
            if (list.Count == 0)
                return QueryResult.Empty(commandTag);

            var columns = new List<string>();
            var resultRows = new List<IReadOnlyList<object?>>();
            foreach (var item in list)
            {
                if (item is IDictionary<string, object?> dict)
                {
                    if (columns.Count == 0)
                        columns.AddRange(dict.Keys);
                    resultRows.Add(columns.Select(c => dict.TryGetValue(c, out var v) ? v : null).ToList());
                }
                else if (item is IDictionary<string, object> dict2)
                {
                    if (columns.Count == 0)
                        columns.AddRange(dict2.Keys);
                    resultRows.Add(columns.Select(c => dict2.TryGetValue(c, out var v) ? v : null).ToList());
                }
            }

            return QueryResult.FromRows(commandTag, columns, resultRows);
        }
    }
}
