# MCP 資料平面

`McpService` 是給外部 Agent 用的唯讀宿主。它用 [Model Context Protocol](https://modelcontextprotocol.io) 的 Streamable HTTP。Agent 的 Discovery 只列出已發布模組視圖。沒有已發布視圖時清單是空的，不回退成表級工具。查詢仍由既有的 `IQuerySession` 執行，公開的 `IDbService` 簽名不變。

寫入與自由 SQL 不在廣告清單裡。`insert`、`update`、`delete`、`sql` 不會被這個宿主執行。

## 啟動

```bash
dotnet run --project McpService
```

預設位址是 `http://localhost:5291`，工具端點是 `POST /mcp`。工作階段是無狀態的 Streamable HTTP。

## 具名連線

連線字串只放在伺服器組態。工具參數沒有連線字串欄位，呼叫端用 `connectionId` 選定資料來源。

`McpService/appsettings.json`：

```json
{
  "Connections": {
    "inventory": {
      "ServerType": "Sqlite",
      "ConnectionString": "Data Source=app.db"
    }
  },
  "Audit": {
    "Path": "audit/mcp.jsonl"
  }
}
```

`ServerType` 必須是下列其中一個，大小寫要一致：

| 值 | 引擎 |
|----|------|
| `Sqlite` | SQLite |
| `SqlServer` | SQL Server |
| `MySQL` | MySQL |
| `Oracle` | Oracle |
| `PostgreSQL` | PostgreSQL |

其他寫法（例如 `sqlite`、`Postgress`）不會建立連線。該次呼叫得到 `connection_failed`，訊息是「無法建立連線。」宿主與其他連線仍可繼續接受呼叫。沒有任何可用連線時，宿主仍會啟動。Discovery 廣告的是已發布模組視圖，可以是空清單。

`connectionId` 與組態鍵的比對不分大小寫。請傳已登記的名稱。

## 營運除錯：表級工具

下列六個名稱不是 Agent 的 Discovery。若宿主仍實作它們，只在與 Discovery 分開的營運除錯路徑。除錯未開啟時，公開端點對這些名稱回 `unknown_tool`。

名稱比對不分大小寫。每個工具都標成唯讀、非破壞性。說明不承諾接受 SQL，參數裡也不出現引擎名稱。

| 工具 | 參數 | 成功時 |
|------|------|--------|
| `tables` | `connectionId`；可省略 `includeView` | 資料表名稱。省略 `includeView` 時包含檢視 |
| `fields` | `connectionId`、`table` | 欄位，與 Session `fields` 相同（`name`、`type`、`pk`、`notNull`） |
| `get` | `connectionId`、`table`、整數 `id` | 該列。沒有該列時是空結果 |
| `query` | `connectionId`、`table`；可省略 `filters` | 符合等值條件的列。沒有符合列時是空結果 |
| `count` | 同 `query` | JSON 數字。沒有符合列時是 `0` |
| `exists` | 同 `query` | JSON 布林 |

`filters` 是物件，鍵是欄位名，值是純量（字串、數字、布林或 null）。條件以等值送進 Session，不拼進 SQL。省略 `filters` 或傳空物件時，只依資料表查詢。

`fields` 會先對照包含檢視的資料表清單。表不存在時是 `unknown_table`，訊息是「找不到資料表。」`get`、`query`、`count`、`exists` 不先查表名；缺表沿用 Session 的結果，不會改成 `unknown_table`。

## 呼叫

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "query",
    "arguments": {
      "connectionId": "inventory",
      "table": "products",
      "filters": { "sku": "A-1" }
    }
  }
}
```

請求要帶 `MCP-Protocol-Version`（測試使用 `2025-11-25`）。選擇性的 `X-DBServices-Caller` 會寫進稽核；沒有這個標頭時，呼叫者記為 `anonymous`，呼叫仍會執行。

列結果是一個 JSON 物件，鍵為 `columns` 與 `rows`。`count` 與 `exists` 的文字內容分別是 JSON 數字與 JSON 布林。

## 錯誤

失敗時 MCP 的 `isError` 為 true。唯一的文字內容是 JSON，鍵恰好是 `code` 與 `message`。`message` 是中文，不含連線字串。

| code | 何時出現 |
|------|----------|
| `invalid_arguments` | 缺少或空白的 `table`；`get` 的 `id` 不是整數 |
| `unknown_table` | `fields` 的表名不在資料表清單中 |
| `connection_failed` | `ServerType` 不是上表的五個精確字串 |
| `execution_failed` | Session 回報錯誤，或稽核寫不進去 |

稽核寫不進去時，客戶端只得到 `execution_failed`，不會同時拿到這次的列、數字或布林。

## 稽核

每一次進入調度的呼叫會在內容根目錄追加一行 JSON。預設路徑是 `audit/mcp.jsonl`。`Audit:Path` 若是相對路徑，會接在內容根目錄之後。

每一行的鍵是 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。時間是 UTC。成功的列查詢只記 `rowCount`，不記結果列。`count` 與 `exists` 把結果記在 `value`。失敗時 `error` 是回給客戶端的 `code`。

稽核與宿主日誌都不寫連線字串、密碼或結果列。參數裡若出現 `connectionString` 或 `connectString`，寫入稽核前會略過。
