# 存放與語句形狀

欄位是契約，不是資料庫綱要。名稱可以不同。不得多出連線字串、模型金鑰或明文密碼。業務列只允許出現在生成紀錄與尚未保存的動態報表。

## 操作者

| 欄位 | 語意 |
| --- | --- |
| `id` | 穩定識別。呼叫 MCP 時寫入 `X-DBServices-Caller` |
| `displayName` | 畫面上的名稱 |
| `passwordVerifier` | 密碼驗證材料。不是明文密碼 |
| `admin` | 是否為管理員 |
| `entries` | `analyst`、`designer`、`enduser` 的子集 |

沒有註冊 API。沒有群組，也沒有 `design` 或 `view` 角色欄位。儲存裡零個操作者時，宿主讀 `Workbench:InitialAdmin:DisplayName` 與 `Workbench:InitialAdmin:Password`，寫入恰好一個 `admin`。已有操作者之後這兩個鍵不再使用。明文密碼不寫入 SQLite 與日誌。

## 檢視

```json
{
  "id": "view-sales-month",
  "name": "銷貨月檢視",
  "purpose": "依年月回答銷貨",
  "published": true,
  "sources": ["sales", "product"],
  "links": [{ "from": "sales", "to": "product", "by": "productId", "kind": "foreign-key" }],
  "parameters": [{ "field": "yearMonth", "label": "年月" }],
  "columns": ["yearMonth", "sku", "qty"]
}
```

沒有 `connectionString`，也沒有 `rows`。`id` 穩定。`name` 是 MCP 工具名稱，目錄內不重複，比對不區分大小寫。`published` 為 false 時，目錄不含這筆。`sources` 只有一張表時，`links` 為空陣列。兩張以上時，每一段的 `kind` 只許 `foreign-key` 或 `accepted`；否則不能保存。仍被樣式的 `viewId` 引用時，不能刪除、不能取消發布，也不能改 `name`、`columns`、`parameters`、`sources` 或 `links`。`purpose` 可以改。

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
| --- | --- | --- |
| `title` | `text` | 不呼叫。文字進動態報表與匯出 |
| `text` | `text` | 不呼叫 |
| `parameter` | `field`、`label` | 不單獨呼叫。其值組成該檢視的參數 |
| `table` | `columns` | 不單獨呼叫。欄來自所綁檢視的輸出欄 |

一筆樣式恰好一個 `viewId`。參數值由就緒的交談提供，不寫在區塊上。

設計建議的最小集合是一個 `table`，其 `columns` 等於該檢視的輸出欄，再加上每個已宣告參數各一個 `parameter`。建議裡沒有參數值與業務列。未知欄名不能接受。

## 報表生成紀錄

```json
{
  "id": "gen-1",
  "ownerId": "op-chen",
  "visibility": "private",
  "templateId": "tmpl-sales-month",
  "viewId": "view-sales-month",
  "parameters": { "yearMonth": "2026-10" },
  "components": [],
  "columns": ["yearMonth", "sku", "qty"],
  "rows": []
}
```

`components` 是當時的區塊順序與文字。`columns` 與 `rows` 是該次呼叫的結果。`parameters` 是純量。沒有 `connectionString`。再開讀這幾欄，不回讀樣式，也不呼叫 MCP。`private` 只對 `ownerId` 可讀。`shared` 對持有 `enduser` 的其他人可讀。產品介面不提供管理員跨人讀取私有紀錄。

## 語句

輸入只有：

| 輸入 | 內容 |
| --- | --- |
| `utterance` | 語句原文 |
| `templates` | 可讀樣式的 `id`、`name`、`viewId`，以及參數的 `field` 與 `label` |

不傳業務列、連線字串、連線清單、密碼或其他人的私有紀錄。

輸出的 `status` 只會是 `ready`、`ambiguous`、`incomplete`。`ready` 是恰好一筆樣式且每個參數都有純量值。`ambiguous` 是樣式多於一筆。`incomplete` 是沒有樣式，或缺少參數值。宿主重算這個狀態。候選若含輸入清單以外的樣式，整份作廢。

比對規則：語句包含某個樣式 `name` 才讓該樣式進入候選。包含某個參數的 `field` 或 `label` 時，其後第一個不以空白拆開的詞是值；全由數字組成可作數字。對不到的不補猜。範圍詞只有在同一個詞也能當參數值時才採用。不從語句擷取連線名稱。

## 工作階段

宿主記憶體的一筆是 `sessionId` 與操作者 `id`。尚未寫入生成紀錄的動態報表也在這個進程的記憶體，鍵是操作者 `id`。離開該次報表，或同一人產生下一份，這塊記憶體刪除。
