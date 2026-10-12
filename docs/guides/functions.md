# 函數

`IFunctionRegistry` 保存名稱 → `DbFunctionDescriptor`（不分大小寫）。`FunctionRegistry.CreateDefault(IDbService)` 會註冊一組對應 CRUD 的函數。

## 內建

| 名稱 | 參數 | 說明 |
|------|------|------|
| `tables` | `includeView`（預設 true） | 列出資料表 |
| `fields` | `table` | 欄位：name、type、pk、notNull |
| `get` | `table`、`id` | 依 Id 一列 |
| `query` | `table` + 其他鍵 | `GetRecordByKeyValues` |
| `insert` | `table` + 欄位 | 新增 |
| `update` | `table`、`id` + 欄位 | 依 Id 更新 |
| `delete` | `table`、`id` | 依 Id 刪除 |
| `count` | `table` + 可選條件 | 計數 |
| `exists` | `table` + 可選條件 | 是否存在 |
| `sql` | `sql` + 其餘當參數 | 參數化 SQL |

## 呼叫

```csharp
var session = db.GetSession();
session.Execute(new QueryRequest
{
    Text = "insert",
    Opcode = QueryOpcode.Call,
    Arguments = new Dictionary<string, object?>
    {
        ["table"] = "Users",
        ["Name"] = "Ada",
        ["Age"] = 36
    }
});
```

CLI：

```text
\call get Users 1
\fn query --table Users --Name Ada
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=app.db" --fn get --arg table=Users --arg id=1 --format json
```

## 自訂函數

```csharp
db.Functions.Register(new DbFunctionDescriptor
{
    Name = "ping_table",
    Description = "資料表是否存在",
    Handler = args =>
    {
        var table = Convert.ToString(args["table"])!;
        return QueryResult.FromRows("ping_table", ["ok"], [[db.HasTable(table)]]);
    }
});
```

`GetSession()` 與 `Functions` 共用同一 registry，先註冊再 Call。

## MCP 只露出唯讀子集

[MCP 宿主](../mcp/index.md) 廣告的是 `tables`、`fields`、`get`、`query`、`count`、`exists`。`insert`、`update`、`delete` 與 `sql` 仍可在 SDK 與 CLI 使用，但不會出現在該宿主的工具清單，也不會被它執行。等值條件在 MCP 裡放進 `filters`，不與 `table` 放在同一層。
