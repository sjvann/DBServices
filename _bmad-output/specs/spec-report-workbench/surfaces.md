# 通道

人在三個入口都不選擇通道，也不選擇具名連線。GraphQL 的 `/graphql` 與 CLI 不在路徑上。

## 檢視目錄

目錄 HTTP 在 `/mcp` 之外，不是 Discovery 裡的工具。`McpService` 不依賴工作台進程就能列出已發布檢視。

| 動作 | 請求 |
| --- | --- |
| 列出 | `GET /views` |
| 讀一筆 | `GET /views/{id}` |
| 保存 | `PUT /views/{id}` |
| 刪除 | `DELETE /views/{id}` |
| 發布 | `POST /views/{id}/publish` |
| 取消發布 | `POST /views/{id}/unpublish` |

這些路由要求 `X-DBServices-Workbench` 等於兩邊組態的 `Workbench:CatalogKey`。這個鍵不是密碼、不是連線字串，也不寫入 SQLite。空白的 `X-DBServices-Caller` 同樣拒絕。MCP 工具路由不讀 `X-DBServices-Workbench`。

未發布的檢視不在 Discovery。Discovery 的名稱集合等於已發布檢視的 `name`。目錄是空的時候，不改列 `tables` 或其他資料表工具。

名稱、輸出欄、參數、來源表或關聯的變更，以及取消發布與刪除，只在 `Workbench.Core` 確認沒有樣式的 `viewId` 指向該 `id` 之後才發送。

## 已發布檢視呼叫

Chat 的讀列是 `POST /mcp`，套件 `ModelContextProtocol` 2.2.0。工具名稱是該檢視的 `name`。引數是頂層的 `connectionId`，加上該檢視已宣告的參數鍵，值是純量。沒有 `filters`。

請求帶 `X-DBServices-Caller`，值是操作者 `id`。恰有一筆組態完整的具名連線時，適配器送出該 `connectionId`。零筆或多筆時不呼叫。

成功結果是 `columns` 與 `rows`。空列是成功。錯誤沿用宿主的 `code` 與 `message`，含 `invalid_arguments`、`unknown_table`、`connection_failed`、`execution_failed`。工作台不重算列，也不把這些碼改成另一套。

## 連線筆數

`GET /connections` 維持現況。它不是 MCP 工具，不掛在 `/mcp` 之下，也不進入 Discovery。回應是 JSON 陣列，每筆只有 `name` 與 `serverType`，且只含組態完整的連線，不回傳連線字串。三個入口都不渲染這份清單。適配器只用它決定筆數，以及在恰有一筆時要送的 `connectionId`。

## Web API

`Surfaces:Web:BaseUrl` 只留給尚未綁定的轉置，不出現在畫面上。這一輪不用 Web API 的列產生動態報表，也不用待轉資料庫的 `GET /meta` 充當規劃期的表結構。三個入口不發送 POST、PUT、DELETE。不修改 WebService 既有路由與 JSON。

規劃期表結構的通道見 SPEC 的開放問題。定案前，這一讀可以發生在任何轉置之前，但不得順手讀業務列，也不得改來源庫結構。
