using System.Text;
using Workbench.Contracts;

namespace Workbench.Core;

/// <summary>
/// 給提案模型按需讀取的註記。名單只有表名與表註解；欄位與連接要另外要。
/// </summary>
public static class AnnotationProposalTools
{
    public static string ListTables(AnnotationPackageDto? package, IReadOnlyList<string>? scope)
    {
        if (package is null)
            return "沒有啟用中的註記包。";
        var tables = Visible(package, scope);
        if (tables.Count == 0)
            return "這次範圍沒有資料表。";

        var builder = new StringBuilder();
        foreach (var table in tables)
        {
            builder.Append(table.Name.Trim());
            if (!string.IsNullOrWhiteSpace(table.Note))
                builder.Append(' ').Append(table.Note.Trim());
            builder.Append('\n');
        }

        return builder.ToString().TrimEnd();
    }

    public static string TableDetails(AnnotationPackageDto? package, IReadOnlyList<string>? scope, string? tableNames, bool includeLinks)
    {
        if (package is null)
            return "沒有啟用中的註記包。";
        var requested = Split(tableNames);
        if (requested.Count == 0)
            return "要指定表名。";

        var visible = Visible(package, scope).ToDictionary(table => table.Name.Trim(), StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        var resolved = new List<string>();
        foreach (var name in requested)
        {
            if (!visible.TryGetValue(name, out var table))
            {
                builder.Append(name).Append("：不在這次範圍。").Append('\n');
                continue;
            }

            resolved.Add(table.Name.Trim());
            builder.Append(table.Name.Trim());
            if (!string.IsNullOrWhiteSpace(table.Note))
                builder.Append(' ').Append(table.Note.Trim());
            builder.Append('\n');
            foreach (var column in table.Columns ?? [])
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                    continue;
                builder.Append("  ").Append(column.Name.Trim());
                if (!string.IsNullOrWhiteSpace(column.Type))
                    builder.Append(' ').Append(column.Type.Trim());
                if (!string.IsNullOrWhiteSpace(column.Note))
                    builder.Append(' ').Append(column.Note.Trim());
                builder.Append('\n');
            }
        }

        if (includeLinks)
        {
            var ends = new HashSet<string>(resolved, StringComparer.OrdinalIgnoreCase);
            foreach (var link in package.Links ?? [])
            {
                var origin = link.Origin?.Trim() ?? "";
                if (origin is not ("foreign-key" or "declared"))
                    continue;
                var from = link.From?.Trim() ?? "";
                var to = link.To?.Trim() ?? "";
                if (!ends.Contains(from) || !ends.Contains(to))
                    continue;
                builder.Append("連接 ").Append(from).Append('.').Append(link.FromColumn?.Trim())
                    .Append(" -> ").Append(to).Append('.').Append(link.ToColumn?.Trim()).Append('\n');
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static List<string> CanvasScope(AnnotationPackageDto? package, IReadOnlyList<string>? sources)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources ?? [])
        {
            var name = source?.Trim() ?? "";
            if (name.Length == 0 || !seen.Add(name))
                continue;
            names.Add(name);
        }

        if (names.Count == 0 || package is null)
            return names;

        var origin = new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase);
        foreach (var link in package.Links ?? [])
        {
            var kind = link.Origin?.Trim() ?? "";
            if (kind is not ("foreign-key" or "declared"))
                continue;
            var from = link.From?.Trim() ?? "";
            var to = link.To?.Trim() ?? "";
            if (origin.Contains(from) && to.Length > 0 && seen.Add(to))
                names.Add(to);
            if (origin.Contains(to) && from.Length > 0 && seen.Add(from))
                names.Add(from);
        }

        return names;
    }

    private static List<AnnotationTableDto> Visible(AnnotationPackageDto package, IReadOnlyList<string>? scope)
    {
        var tables = (package.Tables ?? []).Where(table => !string.IsNullOrWhiteSpace(table.Name)).ToList();
        if (scope is null || scope.All(string.IsNullOrWhiteSpace))
            return tables;

        var wanted = new HashSet<string>(scope.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);
        return tables.Where(table => wanted.Contains(table.Name.Trim())).ToList();
    }

    private static List<string> Split(string? tableNames)
    {
        if (string.IsNullOrWhiteSpace(tableNames))
            return [];
        return tableNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
