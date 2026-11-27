using System.Globalization;
using System.Text;
using System.Text.Json;
using DbServices.Api.Shared;

namespace DbServices.Mcp;

/// <summary>
/// 依檢視結構編成單句 SELECT。不接受外來 SQL 字串。
/// </summary>
public static class SelectCompiler
{
    private static readonly JsonSerializerOptions ShapeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] Operators = ["=", "<>", ">", "<", ">=", "<=", "LIKE", "IN", "IS NULL", "IS NOT NULL"];
    private static readonly string[] Aggregates = ["COUNT", "SUM", "AVG", "MIN", "MAX"];

    public static SelectCompileResult Compile(
        ViewDocument view,
        string? serverType,
        int? rowLimit,
        IReadOnlyDictionary<string, object?>? supplied,
        bool omitMissing)
    {
        ArgumentNullException.ThrowIfNull(view);
        var dialect = string.IsNullOrWhiteSpace(serverType) ? "Sqlite" : serverType.Trim();
        var sources = (view.Sources ?? []).Select(source => source?.Trim() ?? "").Where(source => source.Length > 0).ToList();
        if (sources.Count == 0)
            return SelectCompileResult.Fail("disconnected", "來源連不起來。");
        if (!TryOrder(view, sources, out var steps, out var extra, out var orderError))
            return SelectCompileResult.Fail("disconnected", orderError ?? "來源連不起來。");

        var chosen = Chosen(view, rowLimit);
        var bindings = new List<SelectBinding>();
        var groupBy = view.GroupBy ?? [];
        var aggregates = view.Aggregates ?? [];
        var having = view.Having ?? [];
        if ((aggregates.Count > 0 || having.Count > 0) && groupBy.Count == 0)
            return SelectCompileResult.Fail("invalid_arguments", "沒有 GROUP BY 不能使用彙總或 HAVING。");

        var aggregateAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var aggregate in aggregates)
        {
            var alias = aggregate.Alias?.Trim() ?? "";
            if (!Safe(alias) || !aggregateAliases.Add(alias))
                return SelectCompileResult.Fail("invalid_arguments", "彙總名稱無法使用。");
            var function = (aggregate.Function ?? "").Trim().ToUpperInvariant();
            if (!Aggregates.Contains(function, StringComparer.Ordinal))
                return SelectCompileResult.Fail("invalid_arguments", "彙總函數無法使用。");
            if (function != "COUNT" || !string.IsNullOrWhiteSpace(aggregate.Column))
            {
                if (!Safe(aggregate.Source) || !Safe(aggregate.Column))
                    return SelectCompileResult.Fail("invalid_arguments", "識別字無法使用。");
            }
        }

        foreach (var filter in having)
        {
            if (!aggregateAliases.Contains(filter.Column?.Trim() ?? ""))
                return SelectCompileResult.Fail("invalid_arguments", "彙總條件對不上。");
        }

        var sql = new StringBuilder();
        sql.Append("SELECT");
        if (view.Distinct)
            sql.Append(" DISTINCT");
        if (chosen is int top && dialect.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            sql.Append(" TOP (").Append(top.ToString(CultureInfo.InvariantCulture)).Append(')');

        var projection = Projection(view, sources, aggregateAliases, dialect);
        if (projection is null)
            return SelectCompileResult.Fail("invalid_arguments", "識別字無法使用。");
        sql.Append(' ').Append(projection);
        sql.Append(" FROM ").Append(Quote(steps[0].Source, dialect));
        for (var index = 1; index < steps.Count; index++)
        {
            var step = steps[index];
            sql.Append(IsLeft(step.Link) ? " LEFT JOIN " : " INNER JOIN ");
            sql.Append(Quote(step.Source, dialect));
            sql.Append(" ON ");
            sql.Append(Quote(step.Link.From, dialect)).Append('.').Append(Quote(step.Link.FromColumn, dialect));
            sql.Append(" = ");
            sql.Append(Quote(step.Link.To, dialect)).Append('.').Append(Quote(step.Link.ToColumn, dialect));
        }

        if (!AppendWhere(sql, view, sources, extra, dialect, bindings, supplied, omitMissing, out var whereError))
            return SelectCompileResult.Fail("invalid_arguments", whereError!);

        if (groupBy.Count > 0)
        {
            sql.Append(" GROUP BY ");
            for (var index = 0; index < groupBy.Count; index++)
            {
                if (index > 0)
                    sql.Append(", ");
                if (!Safe(groupBy[index].Source) || !Safe(groupBy[index].Column))
                    return SelectCompileResult.Fail("invalid_arguments", "識別字無法使用。");
                sql.Append(Quote(groupBy[index].Source, dialect)).Append('.').Append(Quote(groupBy[index].Column, dialect));
            }
        }

        if (!AppendHaving(sql, having, dialect, bindings, supplied, omitMissing, out var havingError))
            return SelectCompileResult.Fail("invalid_arguments", havingError!);

        var orderBy = view.OrderBy ?? [];
        if (orderBy.Count > 0)
        {
            sql.Append(" ORDER BY ");
            for (var index = 0; index < orderBy.Count; index++)
            {
                if (index > 0)
                    sql.Append(", ");
                var item = orderBy[index];
                var direction = (item.Direction ?? "").Trim();
                if (!direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                    && !direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
                    return SelectCompileResult.Fail("invalid_arguments", "排序方向無法使用。");
                var orderColumn = item.Column?.Trim() ?? "";
                var orderSource = item.Source?.Trim() ?? "";
                if (aggregateAliases.Contains(orderColumn) && orderSource.Length == 0)
                {
                    if (!Safe(orderColumn))
                        return SelectCompileResult.Fail("invalid_arguments", "識別字無法使用。");
                    sql.Append(Quote(orderColumn, dialect));
                }
                else
                {
                    if (!Safe(orderSource) || !Safe(orderColumn))
                        return SelectCompileResult.Fail("invalid_arguments", "識別字無法使用。");
                    sql.Append(Quote(orderSource, dialect)).Append('.').Append(Quote(orderColumn, dialect));
                }

                sql.Append(direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? " DESC" : " ASC");
            }
        }

        if (chosen is int limit && !dialect.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var text = limit.ToString(CultureInfo.InvariantCulture);
            if (dialect.Equals("Oracle", StringComparison.OrdinalIgnoreCase))
                sql.Append(" FETCH FIRST ").Append(text).Append(" ROWS ONLY");
            else
                sql.Append(" LIMIT ").Append(text);
        }

        var textSql = sql.ToString();
        if (textSql.Contains(';'))
            return SelectCompileResult.Fail("invalid_arguments", "只能是一句 SELECT。");
        return SelectCompileResult.Success(textSql, bindings);
    }

    public static bool SameQuery(ViewDocument left, ViewDocument right)
        => string.Equals(Fingerprint(left), Fingerprint(right), StringComparison.Ordinal);

    private static int? Chosen(ViewDocument view, int? rowLimit)
    {
        var cap = rowLimit is int requested && requested > 0 ? requested : (int?)null;
        var set = view.Limit is int owned && owned > 0 ? owned : (int?)null;
        if (cap is int high && set is int low)
            return Math.Min(high, low);
        return cap ?? set;
    }

    public static int DisplayLimit(ViewDocument view, int ceiling)
    {
        if (view.Limit is int set && set > 0)
            return Math.Min(set, ceiling);
        return ceiling;
    }

    private static string Fingerprint(ViewDocument view)
    {
        var shape = new
        {
            sources = view.Sources ?? [],
            links = (view.Links ?? []).Select(link => new
            {
                link.Kind,
                link.From,
                link.FromColumn,
                link.To,
                link.ToColumn,
                link.Join,
                link.Reason,
                link.ViewOnly,
            }),
            columns = (view.Columns ?? []).Select(column => new { column.Name, column.Source }),
            parameters = (view.Parameters ?? []).Select(item => new { item.Name, item.Column, item.Source }),
            filters = view.Filters ?? [],
            groupBy = view.GroupBy ?? [],
            aggregates = view.Aggregates ?? [],
            having = view.Having ?? [],
            orderBy = view.OrderBy ?? [],
            limit = view.Limit,
            distinct = view.Distinct,
        };
        return JsonSerializer.Serialize(shape, ShapeJson);
    }

    private static string? Projection(ViewDocument view, List<string> sources, HashSet<string> aggregateAliases, string dialect)
    {
        var parts = new List<string>();
        var used = new HashSet<string>(aggregateAliases, StringComparer.OrdinalIgnoreCase);
        foreach (var column in view.Columns ?? [])
        {
            var source = string.IsNullOrWhiteSpace(column.Source) && sources.Count == 1 ? sources[0] : column.Source?.Trim() ?? "";
            var name = column.Name?.Trim() ?? "";
            if (!Safe(source) || !Safe(name))
                return null;
            var alias = name;
            if (!used.Add(alias))
            {
                alias = source + "_" + name;
                if (!Safe(alias) || !used.Add(alias))
                    return null;
            }

            parts.Add(Quote(source, dialect) + "." + Quote(name, dialect) + " AS " + Quote(alias, dialect));
        }

        foreach (var aggregate in view.Aggregates ?? [])
        {
            var function = aggregate.Function.Trim().ToUpperInvariant();
            var alias = aggregate.Alias.Trim();
            string target;
            if (function == "COUNT" && string.IsNullOrWhiteSpace(aggregate.Column))
                target = "*";
            else
                target = Quote(aggregate.Source.Trim(), dialect) + "." + Quote(aggregate.Column.Trim(), dialect);
            parts.Add(function + "(" + target + ") AS " + Quote(alias, dialect));
        }

        return parts.Count == 0 ? "*" : string.Join(", ", parts);
    }

    private static bool AppendWhere(
        StringBuilder sql,
        ViewDocument view,
        List<string> sources,
        List<ViewLink> extra,
        string dialect,
        List<SelectBinding> bindings,
        IReadOnlyDictionary<string, object?>? supplied,
        bool omitMissing,
        out string? error)
    {
        var pieces = new List<(string Combiner, string Text)>();
        foreach (var link in extra)
        {
            if (!Safe(link.From) || !Safe(link.FromColumn) || !Safe(link.To) || !Safe(link.ToColumn))
            {
                error = "識別字無法使用。";
                return false;
            }

            var text = Quote(link.From, dialect) + "." + Quote(link.FromColumn, dialect)
                + " = " + Quote(link.To, dialect) + "." + Quote(link.ToColumn, dialect);
            pieces.Add(("AND", text));
        }

        foreach (var filter in view.Filters ?? [])
        {
            if (!TryPiece(filter, dialect, bindings, supplied, omitMissing, alias: false, out var piece, out error))
                return false;
            if (piece is null)
                continue;
            pieces.Add((Combiner(filter.Combiner), piece));
        }

        foreach (var parameter in view.Parameters ?? [])
        {
            if (string.IsNullOrWhiteSpace(parameter.Name) || string.IsNullOrWhiteSpace(parameter.Column))
                continue;
            if (omitMissing && (supplied is null || !supplied.ContainsKey(parameter.Name)))
                continue;
            var source = parameter.Source?.Trim() ?? "";
            if (source.Length == 0)
            {
                if (sources.Count != 1)
                    continue;
                source = sources[0];
            }

            if (!Safe(source) || !Safe(parameter.Column) || !Safe(parameter.Name))
            {
                error = "識別字無法使用。";
                return false;
            }

            var slot = Next(bindings, parameter.Name, null);
            pieces.Add(("AND", Quote(source, dialect) + "." + Quote(parameter.Column.Trim(), dialect) + " = " + Placeholder(slot, dialect)));
        }

        error = null;
        AppendChain(sql, "WHERE", pieces);
        return true;
    }

    private static bool AppendHaving(
        StringBuilder sql,
        List<ViewFilter> having,
        string dialect,
        List<SelectBinding> bindings,
        IReadOnlyDictionary<string, object?>? supplied,
        bool omitMissing,
        out string? error)
    {
        var pieces = new List<(string Combiner, string Text)>();
        foreach (var filter in having)
        {
            if (!TryPiece(filter, dialect, bindings, supplied, omitMissing, alias: true, out var piece, out error))
                return false;
            if (piece is null)
                continue;
            pieces.Add((Combiner(filter.Combiner), piece));
        }

        error = null;
        AppendChain(sql, "HAVING", pieces);
        return true;
    }

    private static void AppendChain(StringBuilder sql, string keyword, List<(string Combiner, string Text)> pieces)
    {
        string? chain = null;
        foreach (var (combiner, text) in pieces)
            chain = chain is null ? "(" + text + ")" : "(" + chain + " " + combiner + " (" + text + "))";
        if (chain is not null)
            sql.Append(' ').Append(keyword).Append(' ').Append(chain);
    }

    private static bool TryPiece(
        ViewFilter filter,
        string dialect,
        List<SelectBinding> bindings,
        IReadOnlyDictionary<string, object?>? supplied,
        bool omitMissing,
        bool alias,
        out string? piece,
        out string? error)
    {
        piece = null;
        error = null;
        var op = Normalize(filter.Operator);
        if (op is null)
        {
            error = "條件運算子無法使用。";
            return false;
        }

        var parameter = filter.Parameter?.Trim() ?? "";
        if (parameter.Length > 0 && omitMissing && (supplied is null || !supplied.ContainsKey(parameter)))
            return true;

        string left;
        if (alias)
        {
            if (!Safe(filter.Column))
            {
                error = "識別字無法使用。";
                return false;
            }

            left = Quote(filter.Column.Trim(), dialect);
        }
        else
        {
            if (!Safe(filter.Source) || !Safe(filter.Column))
            {
                error = "識別字無法使用。";
                return false;
            }

            left = Quote(filter.Source.Trim(), dialect) + "." + Quote(filter.Column.Trim(), dialect);
        }

        if (op is "IS NULL" or "IS NOT NULL")
        {
            piece = left + " " + op;
            return true;
        }

        if (op == "IN")
        {
            if (parameter.Length > 0)
            {
                error = "這個條件不能使用參數。";
                return false;
            }

            var values = (filter.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length == 0)
            {
                error = "條件沒有值。";
                return false;
            }

            var slots = values.Select(value => Placeholder(Next(bindings, null, value), dialect)).ToArray();
            piece = left + " IN (" + string.Join(", ", slots) + ")";
            return true;
        }

        if (parameter.Length == 0 && string.IsNullOrWhiteSpace(filter.Value))
        {
            error = "條件沒有值。";
            return false;
        }

        var slot = parameter.Length > 0
            ? Next(bindings, parameter, null)
            : Next(bindings, null, filter.Value ?? "");
        piece = left + " " + op + " " + Placeholder(slot, dialect);
        return true;
    }

    private static string Next(List<SelectBinding> bindings, string? parameter, string? literal)
    {
        var slot = "p" + bindings.Count.ToString(CultureInfo.InvariantCulture);
        bindings.Add(new SelectBinding(slot, parameter, literal));
        return slot;
    }

    private static string Placeholder(string slot, string dialect)
        => dialect.Equals("Oracle", StringComparison.OrdinalIgnoreCase) ? ":" + slot : "@" + slot;

    private static string? Normalize(string? op)
    {
        var text = (op ?? "").Trim();
        if (text.Equals("!=", StringComparison.Ordinal))
            text = "<>";
        foreach (var candidate in Operators)
        {
            if (candidate.Equals(text, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static string Combiner(string? combiner)
        => (combiner ?? "").Trim().Equals("or", StringComparison.OrdinalIgnoreCase) ? "OR" : "AND";

    private static bool TryOrder(
        ViewDocument view,
        List<string> sources,
        out List<JoinStep> steps,
        out List<ViewLink> extra,
        out string? error)
    {
        steps = [];
        extra = [];
        error = null;
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!Safe(source) || !unique.Add(source))
            {
                error = "來源連不起來。";
                return false;
            }
        }

        var pending = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        steps.Add(new JoinStep(sources[0], new ViewLink()));
        pending.Remove(sources[0]);
        var links = (view.Links ?? []).Select(CopyLink).ToList();
        while (pending.Count > 0)
        {
            var index = -1;
            for (var cursor = 0; cursor < links.Count; cursor++)
            {
                var candidate = links[cursor];
                var fromIn = !pending.Contains(candidate.From);
                var toIn = !pending.Contains(candidate.To);
                if (fromIn == toIn)
                    continue;
                if (!fromIn && IsLeft(candidate))
                    continue;
                index = cursor;
                break;
            }

            if (index < 0)
            {
                if (links.Any(link => IsLeft(link) && (pending.Contains(link.From) ^ pending.Contains(link.To))))
                    error = "左連接的保留端要先出現在來源表。";
                else
                    error = "來源連不起來。";
                return false;
            }

            var link = links[index];
            links.RemoveAt(index);
            if (!Safe(link.FromColumn) || !Safe(link.ToColumn))
            {
                error = "識別字無法使用。";
                return false;
            }

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
                    Join = link.Join,
                    Reason = link.Reason,
                    ViewOnly = link.ViewOnly,
                };
            steps.Add(new JoinStep(source, oriented));
            pending.Remove(source);
        }

        extra = links;
        return true;
    }

    private static ViewLink CopyLink(ViewLink link) => new()
    {
        Kind = link.Kind,
        From = link.From?.Trim() ?? "",
        FromColumn = link.FromColumn?.Trim() ?? "",
        To = link.To?.Trim() ?? "",
        ToColumn = link.ToColumn?.Trim() ?? "",
        Join = link.Join,
        Reason = link.Reason,
        ViewOnly = link.ViewOnly,
    };

    private static bool IsLeft(ViewLink link)
        => (link.Join ?? "").Trim().Equals("left", StringComparison.OrdinalIgnoreCase);

    private static bool Safe(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        foreach (var ch in name)
        {
            if (ch is '"' or '`' or '[' or ']' or ';' or '\0' or '\r' or '\n')
                return false;
        }

        return true;
    }

    private static string Quote(string name, string dialect)
    {
        if (dialect.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
            return "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
        if (dialect.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            return "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
        return "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private sealed record JoinStep(string Source, ViewLink Link);
}

public sealed class SelectBinding
{
    public SelectBinding(string slot, string? parameter, string? literal)
    {
        Slot = slot;
        Parameter = parameter;
        Literal = literal;
    }

    public string Slot { get; }
    public string? Parameter { get; }
    public string? Literal { get; }
}

public sealed class SelectCompileResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Sql { get; init; } = "";
    public List<SelectBinding> Bindings { get; init; } = [];

    public static SelectCompileResult Success(string sql, List<SelectBinding> bindings)
        => new() { Ok = true, Sql = sql, Bindings = bindings };

    public static SelectCompileResult Fail(string code, string message)
        => new() { Code = code, Message = message };
}

public static class SelectRunner
{
    public static SchemaPreviewResult Preview(
        ToolCatalog catalog,
        string? connectionId,
        ViewDocument? view,
        IReadOnlyDictionary<string, string>? parameters)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (view is null)
            return SchemaPreviewResult.Fail("invalid_arguments", "請先選擇資料表。");
        if (!TrySnapshot(catalog, connectionId, out var snapshot, out var failed))
            return failed;

        var supplied = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is not null)
        {
            foreach (var pair in parameters)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    supplied[pair.Key] = pair.Value.Trim();
            }
        }

        var display = SelectCompiler.DisplayLimit(view, SchemaGraph.PreviewLimit);
        var shown = SelectCompiler.Compile(view, snapshot.ServerType, display, supplied, omitMissing: true);
        if (!shown.Ok)
            return SchemaPreviewResult.Fail(shown.Code, shown.Message);
        var probe = SelectCompiler.Compile(view, snapshot.ServerType, display + 1, supplied, omitMissing: true);
        if (!probe.Ok)
            return SchemaPreviewResult.Fail(probe.Code, probe.Message);

        var result = Run(snapshot, probe, supplied);
        if (result is null)
            return SchemaPreviewResult.Fail("sample_failed", "查詢沒有完成。");

        var truncated = result.Rows.Count > display;
        var rows = new List<List<string>>();
        foreach (var row in result.Rows.Take(display))
            rows.Add(row.Select(Cell).ToList());
        return SchemaPreviewResult.Success(result.Columns.ToList(), rows, truncated, shown.Sql);
    }

    public static TableDispatchResult Dispatch(
        ToolCatalog catalog,
        string connectionId,
        ViewDocument view,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (!TrySnapshot(catalog, connectionId, out var snapshot, out _))
            return TableDispatchResult.QueryExecutionFailed();

        var compiled = SelectCompiler.Compile(view, snapshot.ServerType, view.Limit is int set && set > 0 ? set : null, parameters, omitMissing: false);
        if (!compiled.Ok)
            return TableDispatchResult.QueryExecutionFailed();
        var result = Run(snapshot, compiled, parameters);
        if (result is null)
            return TableDispatchResult.QueryExecutionFailed();

        var rows = result.Rows.Select(row => (IReadOnlyList<object?>)row.Select(Cell).Cast<object?>().ToList()).ToList();
        return TableDispatchResult.Rows(result.Columns, rows);
    }

    private static DbServices.Core.Session.QueryResult? Run(
        ConnectionSnapshot snapshot,
        SelectCompileResult compiled,
        IReadOnlyDictionary<string, object?> supplied)
    {
        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        if (service is null)
            return null;
        try
        {
            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var binding in compiled.Bindings)
            {
                if (!string.IsNullOrEmpty(binding.Parameter))
                {
                    if (!supplied.TryGetValue(binding.Parameter, out var value))
                        return null;
                    arguments[binding.Slot] = value;
                }
                else
                {
                    arguments[binding.Slot] = binding.Literal;
                }
            }

            var result = service.QuerySql(compiled.Sql, arguments.Count == 0 ? null : arguments);
            return result.Kind == DbServices.Core.Session.QueryKind.Rows ? result : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private static bool TrySnapshot(ToolCatalog catalog, string? connectionId, out ConnectionSnapshot snapshot, out SchemaPreviewResult failed)
    {
        snapshot = null!;
        if (string.IsNullOrWhiteSpace(connectionId) || !catalog.Connections.TryGetValue(connectionId, out var found) || !found.Available)
        {
            failed = SchemaPreviewResult.Fail("connection_failed", "無法建立連線。");
            return false;
        }

        snapshot = found;
        failed = null!;
        return true;
    }

    private static string Cell(object? value) => value switch
    {
        null => "",
        string text => text,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };
}
