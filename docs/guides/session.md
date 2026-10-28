# Session

`IQuerySession` 是 CLI、REST、GraphQL、MCP 與程式內「一條請求」的統一入口。從 `IDbService.GetSession()` 取得（每個服務實例快取一份）。MCP 的 Discovery 只列出已發布檢視，見 [MCP 資料平面](../mcp/index.md)。報表工作台取列也只經該檢視，見 [動態報表工作台](../workbench/index.md)。

## 請求

```csharp
public sealed class QueryRequest
{
    public string Text { get; init; }
    public QueryOpcode Opcode { get; init; }      // Auto | Query | Call | Utility
    public IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public ResultFormat Format { get; init; }     // Table | Json | Csv
}
```

## 三路分派

| Opcode | 行為 |
|--------|------|
| `Query` | 當 SQL。`SELECT`／`WITH`／`SHOW`／`EXPLAIN`／`PRAGMA`／`DESC` 走 `QuerySql`；其餘走 `ExecuteSql` |
| `Call` | 以 `Text` 第一個 token 當函數名，呼叫 `IFunctionRegistry` |
| `Utility` | `ping`／`test`（探測 catalog）、`reload`（`ICatalogService.Reload`） |
| `Auto` | 空 → Query；utility 關鍵字 → Utility；已註冊函數名 → Call；否則 Query |

反斜線指令（`\dt`、`\d`）**只存在於 dbsql 客戶端**，展開成 Call 後才進 Session。見 [CLI](../cli/index.md)。

## 範例

```csharp
var session = db.GetSession();

session.Execute(new QueryRequest { Text = "ping", Opcode = QueryOpcode.Utility });
session.Execute(new QueryRequest { Text = "tables", Opcode = QueryOpcode.Call });
session.Execute(new QueryRequest
{
    Text = "get",
    Opcode = QueryOpcode.Call,
    Arguments = new Dictionary<string, object?> { ["table"] = "Users", ["id"] = 1 }
});
session.Execute(new QueryRequest
{
    Text = "SELECT 1 AS n",
    Opcode = QueryOpcode.Query
});
```

非同步：`ExecuteAsync`。實作內部目前多半仍同步轉 `Task.FromResult`，但 API 已預留取消權杖。
