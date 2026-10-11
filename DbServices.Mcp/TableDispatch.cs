using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Core.Session;

namespace DbServices.Mcp;

/// <summary>
/// 依已登記連線建立服務，並執行 Session 的 tables。不參考 MCP。
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
}

/// <summary>
/// tables 的呼叫結果。成功是 columns 與 rows；失敗是 execution_failed。
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
        => new(true, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["code"] = "execution_failed",
            ["message"] = "列出資料表失敗。",
        }));
}
