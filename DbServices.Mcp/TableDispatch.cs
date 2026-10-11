using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Core.Session;

namespace DbServices.Mcp;

/// <summary>
/// 依已登記連線建立服務，並執行 Session 的 tables 或 fields。不參考 MCP。
/// </summary>
public static class TableDispatch
{
    public static TableDispatchResult Execute(ToolCatalog catalog, string connectionId, object? includeView)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var snapshot = catalog.Connections[connectionId];
        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (includeView is not null)
                arguments["includeView"] = includeView;

            var result = service!.GetSession().Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call,
                Arguments = arguments,
            });

            if (result.Kind == QueryKind.Error)
                return TableDispatchResult.ExecutionFailed();

            return TableDispatchResult.Rows(result.Columns, result.Rows);
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 同一服務先探表（includeView 為 true）。命中才把 table 送進 fields。
    /// 缺表名不建立服務。
    /// </summary>
    public static TableDispatchResult ExecuteFields(ToolCatalog catalog, string connectionId, string? table)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(table))
            return TableDispatchResult.InvalidArguments();

        var snapshot = catalog.Connections[connectionId];
        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        try
        {
            var session = service!.GetSession();
            var probe = session.Execute(new QueryRequest
            {
                Text = "tables",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["includeView"] = true,
                },
            });

            return FieldsAfterTables(probe, table, () => session.Execute(new QueryRequest
            {
                Text = "fields",
                Opcode = QueryOpcode.Call,
                Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["table"] = table,
                },
            }));
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// 依送入的探表結果決定是否執行 fields。探表失敗或未命中都不呼叫 fields。
    /// </summary>
    public static TableDispatchResult FieldsAfterTables(QueryResult tables, string table, Func<QueryResult> fields)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(fields);

        if (tables.Kind == QueryKind.Error)
            return TableDispatchResult.FieldsExecutionFailed();

        if (!ContainsTable(tables, table))
            return TableDispatchResult.UnknownTable();

        var result = fields();
        if (result.Kind == QueryKind.Error)
            return TableDispatchResult.FieldsExecutionFailed();

        return TableDispatchResult.Rows(result.Columns, result.Rows);
    }

    private static bool ContainsTable(QueryResult tables, string table)
    {
        var nameIndex = -1;
        for (var index = 0; index < tables.Columns.Count; index++)
        {
            if (string.Equals(tables.Columns[index], "name", StringComparison.OrdinalIgnoreCase))
            {
                nameIndex = index;
                break;
            }
        }

        if (nameIndex < 0)
            return false;

        foreach (var row in tables.Rows)
        {
            if (nameIndex >= row.Count || row[nameIndex] is null)
                continue;

            if (string.Equals(Convert.ToString(row[nameIndex]), table, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// 呼叫結果。成功是 columns 與 rows；失敗是 code 與 message。
/// </summary>
public sealed class TableDispatchResult
{
    private TableDispatchResult(bool isError, string json)
    {
        IsError = isError;
        Json = json;
    }

    public bool IsError { get; }

    public string Json { get; }

    public static TableDispatchResult Rows(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> rows)
        => new(false, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["columns"] = columns,
            ["rows"] = rows.Select(static row => row.ToArray()).ToArray(),
        }));

    public static TableDispatchResult ExecutionFailed()
        => Error("execution_failed", "列出資料表失敗。");

    public static TableDispatchResult FieldsExecutionFailed()
        => Error("execution_failed", "列出欄位失敗。");

    public static TableDispatchResult UnknownTable()
        => Error("unknown_table", "找不到資料表。");

    public static TableDispatchResult InvalidArguments()
        => Error("invalid_arguments", "未提供資料表名稱。");

    private static TableDispatchResult Error(string code, string message)
        => new(true, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["code"] = code,
            ["message"] = message,
        }));
}
