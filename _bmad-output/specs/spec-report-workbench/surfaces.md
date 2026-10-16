# 取數表面

工作台只轉發下列呼叫。不改寫查詢，不重算列，不把空列改成錯誤。

## MCP

端點是 `McpService` 的 `POST /mcp`。客戶端套件 ModelContextProtocol 2.2.0。每個請求帶 `X-DBServices-Caller`，值是操作者 `id`。未登入不以 `anonymous` 呼叫。

| 工具 | 何時 | 引數 |
| --- | --- | --- |
| `tables` | 結構 | `connectionId`。省略 `includeView` 時與 MCP 宿主相同 |
| `fields` | 結構 | `connectionId`、`table` |
| `query` | 確認後的資料表元件，或活資料預覽 | `connectionId`、`table`、`filters` |
| `count` | 確認後的計數元件，或活資料預覽 | 同 `query` |

`filters` 的鍵是欄位名，值是純量，巢狀傳遞，不拼進 SQL。`fields` 每一欄至少保留名稱、型別、是否主鍵、是否不可為空。成功形狀原樣進入預覽。

不呼叫 `get`、`exists`、`insert`、`update`、`delete`、`sql`。這四個關閉工具維持關閉。

錯誤碼沿用宿主：`invalid_arguments`、`unknown_table`、`connection_failed`、`execution_failed`。顯示宿主的 `code` 與 `message`，不重包成另一套代碼。`unknown_table` 只出現在 `fields` 探表。`query` 與 `count` 不另造缺表碼。

## 連線中繼資料

`McpService` 提供 `GET /connections`。它不是 MCP 工具，不掛在 `/mcp` 之下，也不進入 Discovery。

回應是 JSON 陣列。每筆只有 `name` 與 `serverType`，且只含組態完整的連線。不回傳連線字串，也不回傳是否可用。

## Web API

只使用既有路由，不修改 WebService 的路由與 JSON。工作台不讀 WebService 的組態檔。

| 需求 | 請求 |
| --- | --- |
| 資料表清單 | `GET /meta` |
| 欄位 | `GET /{resourceName}/fieldSet` |
| 零個篩選的列 | `GET /{resourceName}` |
| 一個篩選的列 | `GET /{resourceName}?Key=&Value=` |

顯示名稱固定為 `default`。`ServerType` 來自工作台組態 `Surfaces:Web:ServerType`。基底網址來自 `Surfaces:Web:BaseUrl`。連線字串留在 WebService。

兩個以上篩選時拒絕確認，說明此表面只接受單鍵，並且不發送該請求。計數、POST、PUT、DELETE 都不發送。範本含計數元件且表面是 Web API 時，整次確認拒絕，填數次數為 0。工作台不以讀完全表的方式自己計算筆數。

此表面沒有 MCP 那份稽核檔。軌跡仍用操作者、連線名稱、請求種類、篩選與列數或錯誤，由工作台顯示。

## 不連接的表面

GraphQL 的 `/graphql` 與操作台不在填數路徑。CLI 也不轉發。
