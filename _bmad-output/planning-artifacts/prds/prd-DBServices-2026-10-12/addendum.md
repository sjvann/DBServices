---
title: DBServices 檢視與報表 — 技術附錄
status: final
created: 2026-10-12
updated: 2026-10-20
---

# Addendum: 檢視與報表的技術邊界

本附錄承接 [prd.md](prd.md)。它記錄存放形狀、關鍵字契約，以及 web API 與 MCP 的分工。它不是畫面設計，也不是專案配置。架構可以改媒介與行程，不能改這裡的約束。

> **2026-10-18。** 單一報表工作台、人選擇取數表面、範本群組上的 design/view 角色、常用項目，以及「先等人手寫模組視圖再做報表」，都已由 PRD 第 0 節覆寫。下文只保留與新契約相容的約束。
>
> **2026-10-20。** 架構已收束：這一輪讀列是一次已發布檢視呼叫，不先落地；比對只走關鍵字，不啟動側車；檢視定義在 `DbServices.Mcp`；工作台 SQLite 只放操作者、入口、樣式與生成紀錄。規劃期表結構通道、CSV 寫法、稽核保留天數，以及零筆或多筆連線的錯誤碼拼法，仍未定。

資料平面的既有約束仍有效，見 [docs/planning/agent-data-plane/addendum.md](../../../../docs/planning/agent-data-plane/addendum.md)。本產品不修改 `IQuerySession`、Provider、公開的 `IDbService`／`IDbServiceAsync`，也不把 SQL 放進瀏覽器或關鍵字步驟。資料平面所稱的模組視圖，就是本 PRD 的已發布檢視。

## 已決定的約束

1. **三座存放與帳號都在產品自己的環境。** 不在二代平台來源庫建表來放檢視定義、樣式範本、報表生成紀錄或帳號。媒介未指定。
2. **連線字串不進入三個入口。** 請求、檔案、匯出與日誌都不新增連線字串欄位。人不能選擇具名連線。
3. **Chat 只轉發已發布檢視。** MCP 目錄的名稱集合等於已發布檢視的名稱集合。不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`、`insert`、`update`、`delete`、`sql`。
4. **web API 只做結構與轉置。** 它不是人選擇的通道。不從三個入口發送 POST、PUT、DELETE。
5. **關鍵字步驟沒有資料庫驅動。** 它收到的是〈關鍵字契約〉的輸入。後端在執行前核對輸出。核不通過就不呼叫 MCP 讀列。
6. **樣式是順序，不是座標。** 區塊以 `order` 由上到下排列。不保存 x、y、寬高。列印依這個順序、以區塊為界換頁。
7. **不在二代平台來源庫執行 `CREATE VIEW` 或任何結構變更。** 檢視定義寫在模組資料庫。它在模組資料庫裡是 SQL 檢視還是伺服器解釋的定義，留給架構。

## 帳號與入口

| 欄位 | 語意 |
|------|------|
| `id` | 穩定識別。呼叫 MCP 時寫入 `X-DBServices-Caller` |
| `displayName` | 畫面上的名稱 |
| `passwordVerifier` | 密碼驗證材料。不是明文密碼 |
| `admin` | 是否為管理員 |
| `entries` | `analyst`、`designer`、`enduser` 的子集 |

沒有公開註冊 API。第一個管理員來自安裝設定，格式留給架構。沒有範本群組，也沒有 `design`／`view` 角色欄位。

## 檢視

```json
{
  "id": "view-sales-month",
  "name": "銷貨月檢視",
  "purpose": "依年月回答銷貨",
  "published": true,
  "sources": ["sales", "product"],
  "links": [{ "from": "sales", "to": "product", "by": "productId" }],
  "parameters": [{ "field": "yearMonth", "label": "年月" }],
  "columns": ["yearMonth", "sku", "qty"]
}
```

沒有 `connectionString`，也沒有 `rows`。`published` 為 false 時，MCP 目錄不含這筆檢視。`sources` 只有一張表時，`links` 為空陣列。兩張以上時，`links` 的每一段要麼對得上目錄外鍵，要麼標明是分析師接受的關聯；否則不能保存，也不產生笛卡兒積。仍被樣式範本的 `viewId` 引用時，不能刪除、不能取消發布，也不能改 `name`、`columns` 或 `parameters`。`purpose` 可以改。樣式範本繫結 `viewId`，不繫結顯示名稱。

## 樣式範本

```json
{
  "id": "tmpl-sales-month",
  "ownerId": "op-zhou",
  "visibility": "shared",
  "name": "銷貨月報",
  "viewId": "view-sales-month",
  "components": []
}
```

`visibility` 只會是 `private` 或 `shared`。沒有 `connectionId`、`connectionString`、參數值或 `rows`。`components` 依 `order` 升冪，從 0 起連續。`type` 只會是：

| type | 必要欄位 | 讀列時 |
|------|----------|--------|
| `title` | `text` | 不呼叫。文字進動態報表與匯出 |
| `text` | `text` | 不呼叫 |
| `parameter` | `field`、`label` | 不單獨呼叫。其值組成該檢視的參數 |
| `table` | `columns` | 不單獨呼叫。欄來自所綁檢視的輸出欄 |

一筆樣式範本恰好一個 `viewId`。參數值由就緒的交談提供，不寫在區塊上。對不齊該檢視已宣告的參數時，不呼叫 MCP。一次產生恰好呼叫該檢視一次。

設計建議的最小形狀：

```json
{
  "viewId": "view-sales-month",
  "components": [
    {
      "type": "table",
      "columns": ["yearMonth", "sku", "qty"]
    }
  ]
}
```

`columns` 必須等於該檢視的輸出欄。建議裡沒有參數值與業務列。

## 報表生成紀錄

```json
{
  "id": "gen-1",
  "ownerId": "op-chen",
  "visibility": "private",
  "templateId": "tmpl-sales-month",
  "viewId": "view-sales-month",
  "parameters": { "yearMonth": "2026-10" },
  "report": {}
}
```

`report` 是該次動態報表，可以含業務列，因為再開與共享要等於當時的報表。沒有 `connectionString`。再開不呼叫 MCP。`private` 只對 `ownerId` 可讀。`shared` 對持有 `enduser` 入口的其他人可讀。產品介面不提供管理員跨人讀取私有紀錄。

## 關鍵字契約

輸入只含這位操作者這次可讀的樣式：

| 輸入 | 內容 |
|------|------|
| `utterance` | 語句原文 |
| `templates` | 可讀樣式範本的摘要：`id`、`name`、`viewId`、參數的 `field` 與 `label` |

不傳業務列、連線字串、連線清單、密碼或其他人的私有紀錄。

輸出：

```json
{
  "status": "incomplete",
  "templates": ["tmpl-sales-month"],
  "parameters": [{ "field": "yearMonth", "value": "2026-10" }]
}
```

`status` 只會是：

| status | 條件 |
|--------|------|
| `ready` | 恰好一筆樣式範本，且該範本每個參數都有一個純量值 |
| `ambiguous` | 樣式範本候選多於一筆 |
| `incomplete` | 沒有樣式範本，或缺少參數值 |

後端重新計算這個狀態，不以關鍵字步驟自報的 `status` 當作可以呼叫 MCP 的唯一依據。候選若含有輸入清單以外的樣式範本，整份輸出作廢，MCP 讀列次數是 0。`ready` 之後直接呼叫該檢視，不再產生一張供人確認的呼叫清單。

### 關鍵字規則

v1 可以用下列規則通過 FR-11，而不部署模型：

- 若語句包含某個樣式範本 `name`，該範本進入候選。
- 若語句包含某個參數的 `field` 或 `label`，其後第一個不以空白拆開的詞作為值。值維持字串；全由數字組成時可作數字。
- 沒有對到的樣式範本或參數，不補猜。
- 語句中的範圍詞，只有在同一個詞也能當參數值時才採用。否則該參數保持缺少。
- 不從語句擷取連線名稱。

### 側車

這一輪三個入口不啟動側車。比對只使用上一節的關鍵字規則。`DbServices.ModelSidecar` 留在倉儲，不部署 Python。

若日後改回讓入口啟動側車，先改架構 AD-8 與 AD-11，以及本附錄。啟用之後的形狀仍是：一次請求、一次結果；不組 SQL、不開連線、不保存連線字串或業務列。失敗時交談得到可讀錯誤，檢視、樣式範本與報表生成紀錄不變，MCP 讀列次數是 0。

## 通道邊界

### MCP

Chat 報表生成器的讀列是已發布檢視：

| 呼叫 | 何時 | 引數 |
|------|------|------|
| 已發布檢視 | 意圖就緒之後，且恰有一筆組態完整的具名連線 | 頂層 `connectionId`，加上該檢視已宣告的純量參數。沒有 `filters`。人不選擇連線。零筆或多筆時不呼叫 |

請求帶 `X-DBServices-Caller`，值是操作者 `id`。參數值是純量，不把篩選串進 SQL。目錄不包含資料表工具，也不包含連線清單。

錯誤碼沿用宿主：`invalid_arguments`、`unknown_table`、`connection_failed`、`execution_failed`。入口顯示代碼與訊息，不重包成另一套代碼。

### 規劃期的表結構

模組設計工具在規劃期讀的是二代平台資料源的表與欄，不是待轉資料庫。通道留給架構。回應不含連線字串。這一讀可以發生在轉置之前。

### web API

這一輪不用這些 GET 產生動態報表，也不用 `GET /meta` 充當規劃期表結構。基底網址只留給尚未定案的轉置，不出現在畫面上。下表是日後若改成先落地時的既有路由，不是這一輪的讀列路徑：

| 需求 | 請求 |
|------|------|
| 資料表清單 | `GET /meta` |
| 欄位 | `GET /{resourceName}/fieldSet` |
| 轉置所需的列 | 既有的零鍵或單鍵 `GET /{resourceName}` |

人不能在三個入口改選這條通道。兩個以上的篩選若不是該檢視已宣告的參數，不在入口另造查詢。POST、PUT、DELETE 都不發送。連線字串留在 WebService 進程。

本附錄不要求為了轉置去改 WebService 的路徑。轉置的排程與是否先落地成模組資料庫中的資料表，留給架構；無論哪一種，MCP 讀到的都必須是該筆已發布檢視，而且來源庫結構不變。

### 不連接的表面

GraphQL 的 `/graphql` 與操作台不在這三個入口的路徑。CLI 也不轉發。

## 被拒絕的做法

- **一個左軌同時放檢視設計、樣式與交談。** 三個入口各自只做該站的事。
- **讓人選擇 MCP 或 Web API，或選擇具名連線。** 通道由站決定，不由人決定。
- **用範本群組的 design/view 角色表示誰能讀。** 讀取邊界是入口加上共享範圍。`view` 這個詞留給檢視。
- **常用項目只保存叫法、再開時重新查詢。** 報表生成紀錄保存該次動態報表；樣式的後續修改不改寫已保存的紀錄。
- **自由座標的版面。** 會把樣式做成套印引擎。
- **用語句直接生成 SQL 或新的篩選運算子。** 對不到等值參數就停在未就緒。
- **在二代平台來源庫建立 DDL 檢視來充當檢視。** 定義寫在模組資料庫。
- **讓關鍵字步驟持有連線組態或業務列。** 輸入表已足夠做候選。

## 留給架構的項目

2026-10-20 已收束，不再開放：

- 兩個殼是 Blazor 與 Avalonia。恰好一個 `Workbench` 進程開啟工作台 SQLite，並在記憶體持有工作階段與尚未保存的動態報表。Avalonia 只經工作台 HTTP 進入同一組端口。
- 檢視定義是 `DbServices.Mcp` 的目錄文件。這一輪讀列是即時呼叫，不先落地。
- 第一個管理員來自 `Workbench:InitialAdmin`，驗證材料是 `Pbkdf2PasswordHasher` 的 `passwordVerifier`。
- 這一輪不啟動側車。關鍵字規則必須獨立通過第 4.4 節。

仍未定：規劃期表結構通道、CSV 寫法、MCP 稽核保留天數、零筆或多筆連線時工作台自己的錯誤碼拼法。
