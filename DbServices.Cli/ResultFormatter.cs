using System.Text;
using System.Text.Json;
using DbServices.Core.Session;

namespace DbServices.Cli
{
    public static class ResultFormatter
    {
        public static string Format(QueryResult result, ResultFormat format, bool expanded)
        {
            if (result.Kind == QueryKind.Error)
                return $"ERROR: {result.Error}";
            if (result.Kind == QueryKind.NonQuery)
                return $"{result.CommandTag} {result.AffectedRows}";
            if (result.Rows.Count == 0)
                return "(0 rows)";

            return format switch
            {
                ResultFormat.Json => ToJson(result),
                ResultFormat.Csv => ToCsv(result),
                _ => expanded ? ToExpanded(result) : ToTable(result)
            };
        }

        public static string ToTable(QueryResult result)
        {
            var widths = result.Columns.Select((c, i) =>
            {
                var max = c.Length;
                foreach (var row in result.Rows)
                {
                    var len = Convert.ToString(row.ElementAtOrDefault(i))?.Length ?? 0;
                    if (len > max) max = len;
                }
                return Math.Min(max, 60);
            }).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" | ", result.Columns.Select((c, i) => c.PadRight(widths[i]))));
            sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));
            foreach (var row in result.Rows)
            {
                sb.AppendLine(string.Join(" | ", result.Columns.Select((_, i) =>
                    (Convert.ToString(row.ElementAtOrDefault(i)) ?? "").PadRight(widths[i]))));
            }
            sb.Append($"({result.Rows.Count} rows)");
            return sb.ToString();
        }

        public static string ToExpanded(QueryResult result)
        {
            var sb = new StringBuilder();
            var n = 1;
            foreach (var row in result.Rows)
            {
                sb.AppendLine($"-[ RECORD {n} ]-");
                for (var i = 0; i < result.Columns.Count; i++)
                    sb.AppendLine($"{result.Columns[i]} | {row.ElementAtOrDefault(i)}");
                n++;
            }
            return sb.ToString().TrimEnd();
        }

        public static string ToCsv(QueryResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", result.Columns.Select(EscapeCsv)));
            foreach (var row in result.Rows)
                sb.AppendLine(string.Join(",", result.Columns.Select((_, i) => EscapeCsv(Convert.ToString(row.ElementAtOrDefault(i))))));
            return sb.ToString().TrimEnd();
        }

        public static string ToJson(QueryResult result)
        {
            var list = result.Rows.Select(row =>
            {
                var dict = new Dictionary<string, object?>();
                for (var i = 0; i < result.Columns.Count; i++)
                    dict[result.Columns[i]] = row.ElementAtOrDefault(i);
                return dict;
            });
            return JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string EscapeCsv(string? value)
        {
            value ??= "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
