using Workbench.Contracts;

namespace Workbench.Core;

/// <summary>
/// 由外鍵往外找第一層與第二層資料表。加入較遠的表時，沿途的表會一起補上，查詢才連得起來。
/// </summary>
public static class SchemaNeighborhood
{
    public sealed record Suggestion(
        string Table,
        int Depth,
        string Parent,
        string From,
        string FromColumn,
        string To,
        string ToColumn);

    public static IReadOnlyList<Suggestion> Suggest(IReadOnlyList<SchemaTableDto>? tables, string? root, int depth)
    {
        if (tables is null || tables.Count == 0 || string.IsNullOrWhiteSpace(root) || depth < 1)
            return [];

        var byName = new Dictionary<string, SchemaTableDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            if (!string.IsNullOrWhiteSpace(table.Name))
                byName[table.Name] = table;
        }

        if (!byName.TryGetValue(root, out var start))
            return [];

        var suggestions = new List<Suggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.Name };
        var queue = new Queue<(string Name, int Depth)>();
        queue.Enqueue((start.Name, 0));

        while (queue.Count > 0)
        {
            var (name, currentDepth) = queue.Dequeue();
            if (currentDepth >= depth || !byName.TryGetValue(name, out var table))
                continue;

            foreach (var hop in Edges(byName, table))
            {
                if (!byName.TryGetValue(hop.Neighbor, out var neighbor) || !seen.Add(neighbor.Name))
                    continue;

                suggestions.Add(new Suggestion(
                    neighbor.Name,
                    currentDepth + 1,
                    table.Name,
                    hop.From,
                    hop.FromColumn,
                    hop.To,
                    hop.ToColumn));
                queue.Enqueue((neighbor.Name, currentDepth + 1));
            }
        }

        return suggestions;
    }

    private readonly record struct Edge(string Neighbor, string From, string FromColumn, string To, string ToColumn);

    private static IEnumerable<Edge> Edges(Dictionary<string, SchemaTableDto> byName, SchemaTableDto table)
    {
        foreach (var column in table.Columns ?? [])
        {
            if (!Linked(column, out var targetName, out var targetColumn))
                continue;
            if (!byName.TryGetValue(targetName, out var target))
                continue;
            if (string.Equals(target.Name, table.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return new Edge(target.Name, table.Name, column.Name, target.Name, targetColumn);
        }

        foreach (var other in byName.Values)
        {
            if (string.Equals(other.Name, table.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var column in other.Columns ?? [])
            {
                if (!Linked(column, out var targetName, out var targetColumn))
                    continue;
                if (!string.Equals(targetName, table.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return new Edge(other.Name, other.Name, column.Name, table.Name, targetColumn);
            }
        }
    }

    private static bool Linked(SchemaColumnDto column, out string table, out string targetColumn)
    {
        table = column.ReferencesTable?.Trim() ?? "";
        targetColumn = column.ReferencesColumn?.Trim() ?? "";
        return column.Name.Trim().Length > 0 && table.Length > 0 && targetColumn.Length > 0;
    }
}
