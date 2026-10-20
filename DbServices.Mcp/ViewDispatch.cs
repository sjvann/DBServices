using System.Text.Json;
using DbServices.Api.Shared;

namespace DbServices.Mcp;

/// <summary>
/// 依已發布檢視呼叫既有 query。不組自由 SQL，不呼叫 count。
/// </summary>
public static class ViewDispatch
{
    public static TableDispatchResult Execute(
        ToolCatalog catalog,
        string connectionId,
        ViewDocument view,
        IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(parameters);

        if (RejectParameters(view, parameters) is { } rejected)
            return rejected;

        var sources = view.Sources ?? [];
        if (sources.Count == 0)
            return TableDispatchResult.QueryExecutionFailed();

        if (sources.Count > 1 && !ReadyToJoin(view))
            return TableDispatchResult.QueryExecutionFailed();

        if (sources.Count == 1)
            return Project(Query(catalog, connectionId, sources[0], ParametersFor(view, sources[0], parameters, single: true)), view);

        var tables = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var outcome = Query(catalog, connectionId, source, ParametersFor(view, source, parameters, single: false));
            if (outcome.IsError)
                return outcome;
            if (!TryRead(outcome.Json, out var columns, out var rows))
                return TableDispatchResult.QueryExecutionFailed();
            tables[source] = ToMaps(columns, rows);
        }

        var order = JoinOrder(view);
        if (order is null)
            return TableDispatchResult.QueryExecutionFailed();

        var current = tables[order[0].Source].Select(row => Prefix(order[0].Source, row)).ToList();
        for (var index = 1; index < order.Count; index++)
        {
            var step = order[index];
            var next = tables[step.Source];
            var joined = new List<Dictionary<string, string>>();
            foreach (var left in current)
            {
                foreach (var right in next)
                {
                    var fromKey = step.Link.From + "." + step.Link.FromColumn;
                    var rightText = Text(right, step.Link.ToColumn);
                    if (!left.TryGetValue(fromKey, out var leftText) || !string.Equals(leftText, rightText, StringComparison.Ordinal))
                        continue;
                    var copy = new Dictionary<string, string>(left, StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in right)
                        copy[step.Source + "." + pair.Key] = pair.Value;
                    joined.Add(copy);
                }
            }

            current = joined;
        }

        return TableDispatchResult.Rows(OutputColumns(view), ProjectRows(view, current));
    }

    private static bool ReadyToJoin(ViewDocument view)
    {
        foreach (var link in view.Links ?? [])
        {
            if (link.Kind is not ("foreign-key" or "accepted"))
                return false;
            if (string.IsNullOrWhiteSpace(link.From) || string.IsNullOrWhiteSpace(link.To)
                || string.IsNullOrWhiteSpace(link.FromColumn) || string.IsNullOrWhiteSpace(link.ToColumn))
                return false;
        }

        return JoinOrder(view) is not null;
    }

    private static List<JoinStep>? JoinOrder(ViewDocument view)
    {
        var sources = view.Sources;
        var pending = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        var steps = new List<JoinStep> { new(sources[0], new ViewLink()) };
        pending.Remove(sources[0]);
        var links = (view.Links ?? []).ToList();
        while (pending.Count > 0)
        {
            var link = links.FirstOrDefault(candidate =>
                !pending.Contains(candidate.From) && pending.Contains(candidate.To)
                || !pending.Contains(candidate.To) && pending.Contains(candidate.From));
            if (link is null)
                return null;

            var forward = !pending.Contains(link.From);
            var source = forward ? link.To : link.From;
            var oriented = forward
                ? link
                : new ViewLink
                {
                    Kind = link.Kind,
                    From = link.To,
                    FromColumn = link.ToColumn,
                    To = link.From,
                    ToColumn = link.FromColumn,
                };
            steps.Add(new JoinStep(source, oriented));
            pending.Remove(source);
            links.Remove(link);
        }

        return steps;
    }

    private static TableDispatchResult? RejectParameters(ViewDocument view, IReadOnlyDictionary<string, object?> parameters)
    {
        var sources = view.Sources ?? [];
        var known = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        var multi = sources.Count > 1;
        foreach (var parameter in view.Parameters ?? [])
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            if (multi && (string.IsNullOrWhiteSpace(parameter.Source) || !known.Contains(parameter.Source)))
                return TableDispatchResult.InvalidArguments();
            if (!parameters.TryGetValue(parameter.Name, out var value) || !IsScalar(value))
                return TableDispatchResult.InvalidArguments();
        }

        return null;
    }

    private static bool IsScalar(object? value)
        => value is null or string or bool
            or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;

    private static Dictionary<string, object?> ParametersFor(
        ViewDocument view,
        string source,
        IReadOnlyDictionary<string, object?> parameters,
        bool single)
    {
        var filters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in view.Parameters ?? [])
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Column))
                continue;
            if (!string.IsNullOrWhiteSpace(parameter.Source)
                && !string.Equals(parameter.Source, source, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!single && string.IsNullOrWhiteSpace(parameter.Source))
                continue;
            if (!parameters.TryGetValue(parameter.Name, out var value))
                continue;
            filters[parameter.Column] = value;
        }

        return filters;
    }

    private static TableDispatchResult Query(
        ToolCatalog catalog,
        string connectionId,
        string table,
        IReadOnlyDictionary<string, object?> filters)
    {
        JsonElement? element = filters.Count == 0 ? null : JsonSerializer.SerializeToElement(filters);
        return TableDispatch.ExecuteQuery(catalog, connectionId, table, element);
    }

    private static TableDispatchResult Project(TableDispatchResult outcome, ViewDocument view)
    {
        if (outcome.IsError)
            return outcome;
        if (!TryRead(outcome.Json, out var columns, out var rows))
            return TableDispatchResult.QueryExecutionFailed();

        var declared = view.Columns ?? [];
        if (declared.Count == 0)
            return TableDispatchResult.Rows(columns, rows.Select(row => (IReadOnlyList<object?>)row).ToList());

        var projected = new List<IReadOnlyList<object?>>();
        foreach (var row in rows)
        {
            var line = new List<object?>();
            foreach (var column in declared)
            {
                var index = IndexOf(columns, column.Name);
                line.Add(index >= 0 && index < row.Count ? row[index] : null);
            }

            projected.Add(line);
        }

        return TableDispatchResult.Rows(declared.Select(column => column.Name).ToList(), projected);
    }

    private static List<string> OutputColumns(ViewDocument view)
    {
        var declared = view.Columns ?? [];
        if (declared.Count > 0)
            return declared.Select(column => column.Name).ToList();
        return [];
    }

    private static List<IReadOnlyList<object?>> ProjectRows(ViewDocument view, List<Dictionary<string, string>> rows)
    {
        var declared = view.Columns ?? [];
        var projected = new List<IReadOnlyList<object?>>();
        foreach (var row in rows)
        {
            if (declared.Count == 0)
            {
                projected.Add(row.Values.Cast<object?>().ToList());
                continue;
            }

            var line = new List<object?>();
            foreach (var column in declared)
            {
                var key = string.IsNullOrWhiteSpace(column.Source) ? column.Name : column.Source + "." + column.Name;
                line.Add(row.TryGetValue(key, out var value) ? value : "");
            }

            projected.Add(line);
        }

        return projected;
    }

    private static List<Dictionary<string, string>> ToMaps(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var maps = new List<Dictionary<string, string>>();
        foreach (var row in rows)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Count; index++)
                map[columns[index]] = index < row.Count ? row[index] : "";
            maps.Add(map);
        }

        return maps;
    }

    private static Dictionary<string, string> Prefix(string source, Dictionary<string, string> row)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in row)
            map[source + "." + pair.Key] = pair.Value;
        return map;
    }

    private static string Text(Dictionary<string, string> row, string column)
        => row.TryGetValue(column, out var value) ? value : "";

    private static bool TryRead(string json, out List<string> columns, out List<List<string>> rows)
    {
        columns = [];
        rows = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("columns", out var columnElement)
                || !document.RootElement.TryGetProperty("rows", out var rowElement))
                return false;

            columns = columnElement.EnumerateArray().Select(item => item.GetString() ?? "").ToList();
            foreach (var row in rowElement.EnumerateArray())
                rows.Add(row.EnumerateArray().Select(Cell).ToList());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Cell(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => element.GetRawText(),
    };

    private static int IndexOf(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index], name, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private sealed record JoinStep(string Source, ViewLink Link);
}
