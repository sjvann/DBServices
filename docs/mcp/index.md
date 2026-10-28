# MCP 資料平面

`McpService` 是給外部 Agent 與報表工作台用的宿主。Agent 走 [Model Context Protocol](https://modelcontextprotocol.io) 的 Streamable HTTP。Discovery 只列出**已發布檢視**，工具名稱就是檢視名稱。沒有已發布檢視時清單是空的，不回退成表級工具。

查詢仍由既有的 `IQuerySession` 執行，公開的 `IDbService` 簽名不變。寫入與自由 SQL 不在這個宿主裡。`insert`、`update`、`delete`、`sql`，以及 `tables`、`fields`、`get`、`query`、`count`、`exists`，都不會被 `tools/list` 或 `tools/call` 執行。

報表工作台如何消費這些檢視，見 [動態報表工作台](../workbench/index.md)。

## 啟動

```bash
dotnet run --project McpService
```

預設位址是 `http://localhost:5291`。工具端點是 `POST /mcp`。工作階段是無狀態的 Streamable HTTP。

## 具名連線

連線字串只放在伺服器。工具參數沒有連線字串欄位，呼叫端用 `connectionId` 選定資料來源。

連線可以寫在 `McpService/appsettings.json` 的 `Connections`，也可以由工作台經目錄 API 寫入內容根目錄的 `catalog/connections.json`。兩處合併後才是執行期目錄。

```json
{
  "Connections": {
    "inventory": {
      "ServerType": "Sqlite",
      "ConnectionString": "Data Source=app.db"
    }
  },
  "Catalog": {
    "Path": "catalog/views.json"
  },
  "Workbench": {
    "CatalogKey": "change-me"
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

其他寫法不會建立連線。該次呼叫得到 `connection_failed`，訊息是「無法建立連線。」宿主與其他連線仍可繼續接受呼叫。沒有任何可用連線時，宿主仍會啟動，Discovery 可以是空清單。

`connectionId` 與目錄鍵的比對不分大小寫。

## 已發布檢視就是工具

檢視文件放在 `Catalog:Path`（預設 `catalog/views.json`）。只有 `published` 為 true、且名稱不是空白的檢視會進入 Discovery。

| 工具欄位 | 來源 |
|----------|------|
| 名稱 | 檢視 `name`。比對不分大小寫 |
| 說明 | 檢視 `purpose`；空白時沿用名稱 |
| `connectionId` | 必填字串 |
| 其餘參數 | 檢視宣告的每個參數名，型別為字串 |

工具標成唯讀、非破壞性。參數裡不出現引擎名稱，也不接受自由 SQL。`filters` 會被略過，條件只走檢視自己的參數。

## 呼叫

請求要帶 `MCP-Protocol-Version`（測試使用 `2025-11-25`）。選擇性的 `X-DBServices-Caller` 會寫進稽核；沒有這個標頭時，呼叫者記為 `anonymous`，呼叫仍會執行。工具路由不讀目錄金鑰。

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "月銷貨",
    "arguments": {
      "connectionId": "inventory",
      "年月": "2026-10"
    }
  }
}
```

成功時，唯一的文字內容是 JSON，鍵為 `columns` 與 `rows`。沒有符合列時仍是成功，`rows` 為空。

名稱對不到已發布檢視、檢視沒有來源表，或多張來源表的關聯還沒接好時，結果是 `execution_failed`。檢視宣告的參數缺漏或不是純量時，結果是 `invalid_arguments`。

## 錯誤

失敗時 MCP 的 `isError` 為 true。唯一的文字內容是 JSON，鍵恰好是 `code` 與 `message`。`message` 是中文，不含連線字串。

| code | 何時出現 |
|------|----------|
| `invalid_arguments` | 已發布檢視的參數缺漏，或不是純量 |
| `connection_failed` | `connectionId` 不存在，或 `ServerType` 不是上表的五個精確字串 |
| `execution_failed` | 工具名稱不是已發布檢視、檢視無法執行、Session 回報錯誤，或稽核寫不進去 |

稽核寫不進去時，客戶端只得到 `execution_failed`，不會同時拿到這次的列。

## 目錄 API

檢視、連線與表結構的讀寫不走 `POST /mcp`。這些 HTTP 路由給報表工作台使用，每次都要同時帶：

| 標頭 | 要求 |
|------|------|
| `X-DBServices-Caller` | 不可空白。工作台送操作者 id |
| `X-DBServices-Workbench` | 必須與兩邊的 `Workbench:CatalogKey` 完全相同 |

不符時是 `forbidden`（403）。呼叫者空白時是 `invalid_arguments`（400）。回應不含連線字串。

| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/connections` | 可用連線的 `name` 與 `serverType` |
| POST | `/connections` | 新增或覆寫一筆連線 |
| DELETE | `/connections/{name}` | 移除連線 |
| POST | `/connections/databases` | 依送出的連線列出資料庫名稱。不寫入目錄 |
| POST | `/connections/probe` | 測試連線。不寫入目錄 |
| GET | `/views` | 全部檢視，含未發布 |
| GET | `/views/{id}` | 單筆檢視 |
| PUT | `/views/{id}` | 建立或覆寫。名稱在目錄內不可重複，且不分大小寫 |
| DELETE | `/views/{id}` | 刪除 |
| POST | `/views/{id}/publish` | 發布。之後才出現在 Discovery |
| POST | `/views/{id}/unpublish` | 取消發布 |
| GET | `/schema/graph?connectionId=` | 該連線的資料表與欄位 |
| POST | `/schema/preview` | 依一份檢視定義預覽列 |

發布不會在來源資料庫建立檢視。多張來源表時，每一段關聯必須是外鍵或分析師接受的關聯，否則呼叫不會產生笛卡兒積。

## 稽核

每一次進入調度的工具呼叫會在內容根目錄追加一行 JSON。預設路徑是 `audit/mcp.jsonl`。`Audit:Path` 若是相對路徑，會接在內容根目錄之後。

每一行的鍵是 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。時間是 UTC。成功的列查詢只記 `rowCount`，不記結果列。失敗時 `error` 是回給客戶端的 `code`。

稽核與宿主日誌都不寫連線字串、密碼或結果列。
