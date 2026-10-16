---
title: DBServices 報表工作台 — 技術附錄
status: draft
created: 2026-10-12
updated: 2026-10-15
---

# Addendum: 報表工作台的技術邊界

本附錄承接 [prd.md](prd.md)。它記錄儲存形狀、解析契約、元件欄位，以及兩條取數表面的對應。它不是畫面設計，也不是專案配置。架構技能可以改媒介與行程，不能改這裡的約束。

> **2026-10-15。** 工作台的 MCP 只轉發已發布模組視圖。表級瀏覽走資料表 Web API。下文的工具表與確認清單已改成一次視圖呼叫。拖拉設計器與共用關聯程式庫不在本附錄。

資料平面的既有約束仍有效，見 [docs/planning/agent-data-plane/addendum.md](../../../../docs/planning/agent-data-plane/addendum.md)。本工作台不修改 `IQuerySession`、Provider、公開的 `IDbService`／`IDbServiceAsync`，也不把 SQL 放進瀏覽器或解析器。

## 已決定的約束

1. **範本與常用項目存在工作台儲存。** 不在業務庫建表來放版面、帳號或常用項目。媒介（檔案、SQLite 或其他）未指定。
2. **連線字串不進入工作台。** 列出連線時只拿名稱與 `ServerType`。請求、檔案、匯出與日誌都不新增連線字串欄位。
3. **填數只轉發既有能力。** `MCP` 只轉發已發布模組視圖。表級瀏覽不走 `tables`、`fields`、`query`、`count`。`Web API` 只發〈Web API〉一節列出的 GET。工作台不呼叫 `get`、`exists`、`insert`、`update`、`delete`、`sql`。
4. **解析器沒有資料庫驅動。** 關鍵字規則與 Python 側車收到的都是〈解析契約〉的輸入。工作台後端在執行前核對輸出。核不通過就不發填數呼叫。
5. **畫布是順序，不是座標。** 元件以 `order` 由上到下排列。不保存 x、y、寬高。列印依這個順序、以元件為界換頁。

## 工作台儲存

下列欄位是契約，不是資料庫綱要。名稱可以不同，語意不能少，也不能多出連線字串或業務列。

### 操作者

| 欄位 | 語意 |
|------|------|
| `id` | 穩定識別。呼叫 MCP 時寫入 `X-DBServices-Caller` |
| `displayName` | 畫面上的名稱 |
| `passwordVerifier` | 密碼驗證材料。不是明文密碼 |
| `admin` | 是否為管理員 |

沒有公開註冊 API。第一個管理員來自安裝設定，格式留給架構。

### 範本群組與角色

| 欄位 | 語意 |
|------|------|
| 群組 `id`、`name` | 群組識別與名稱 |
| 指派 `operatorId`、`groupId`、`role` | `role` 只會是 `design` 或 `view` |

`design` 在讀取時視同也具有 `view`。一個操作者對一個群組至多一筆指派。

### 報表範本

```json
{
  "id": "tmpl-sales-month",
  "groupId": "grp-sales",
  "name": "銷貨月報",
  "components": []
}
```

`name` 在同一個 `groupId` 內唯一。沒有 `connectionId`、`connectionString`、參數值或 `rows`。

### 常用項目

```json
{
  "id": "fav-1",
  "ownerId": "op-chen",
  "name": "十月銷貨",
  "templateId": "tmpl-sales-month",
  "surface": "mcp",
  "connectionId": "inventory",
  "parameters": { "yearMonth": "2026-10" }
}
```

`surface` 只會是 `mcp` 或 `web`。`parameters` 的鍵是參數元件的欄位名稱，值是純量。沒有 `components` 複本，也沒有 `rows`。讀取清單時以 `ownerId` 過濾。管理員的維運備份可以接觸檔案，產品介面不提供跨人讀取。

### 預覽快照

快照不是工作台儲存裡的紀錄。它只存在於該次已確認預覽的回應或工作階段畫面。離開預覽後，匯出端點拒絕使用舊的快照識別。快照裡可以有該次業務列，因為匯出必須等於畫面；它不寫入範本與常用項目。

## 五種元件

`components` 依 `order` 升冪。`order` 從 0 起連續。`type` 只會是下表之一。

| type | 必要欄位 | 填數時 |
|------|----------|--------|
| `title` | `text` | 不呼叫。文字進預覽與匯出 |
| `text` | `text` | 不呼叫 |
| `parameter` | `field`、`label` | 不單獨呼叫。其值組成等值篩選 |
| `table` | `columns` | 不單獨呼叫。欄來自所綁模組視圖的輸出欄。`Web API` 範本改為綁一張表的欄名 |
| `count` | `column` | 不另發呼叫。綁所綁模組視圖的一個輸出欄。`Web API` 上整份範本拒絕確認 |

參數值由預覽或常用項目提供，不寫在元件上。同一份 `MCP` 範本的參數值合成一個物件，必須屬於該視圖已宣告的篩選。對不齊時整次確認被拒絕，不發送任何填數呼叫。一次確認恰好呼叫該範本所綁的一個模組視圖，不是每個元件各發一次。

設計器建議的最小形狀：

```json
{
  "components": [
    {
      "type": "table",
      "view": "sales-month",
      "columns": ["yearMonth", "sku", "qty"]
    }
  ]
}
```

`columns` 必須等於該視圖的輸出欄。`Web API` 範本則等於該次 `fieldSet`。建議裡沒有參數值與業務列。操作者接受前可以刪改；接受時才寫入畫布。

## 解析契約

輸入只含操作者這次看得到的結構：

| 輸入 | 內容 |
|------|------|
| `utterance` | 語句原文 |
| `connections` | `{ "name", "serverType" }` 陣列。沒有連線字串 |
| `templates` | 有權限的範本摘要：`id`、`name`、`groupId`、參數的 `field` 與 `label` |
| `surfaces` | 這次允許的取數表面 |

不傳業務列、連線字串、密碼或其他操作者的常用項目。

輸出：

```json
{
  "status": "ambiguous",
  "surface": "mcp",
  "connections": ["inventory", "inventory_pg"],
  "templates": ["tmpl-sales-month"],
  "parameters": [{ "field": "yearMonth", "value": "2026-10" }]
}
```

`status` 只會是：

| status | 條件 |
|--------|------|
| `ready` | 恰好一個連線、恰好一個範本，且該範本每個參數都有一個純量值 |
| `ambiguous` | 連線或範本候選多於一個 |
| `incomplete` | 沒有連線、沒有範本，或缺少參數值 |

工作台後端重新計算這個狀態，不以解析器自報的 `status` 當作可以確認的唯一依據。候選若含有輸入清單以外的連線或範本，整份輸出作廢，填數呼叫次數是 0。

`ready` 之後，後端列出填數呼叫清單給操作者。確認請求必須帶回這份清單的識別。執行時再次核對清單與範本、權限、表面能力是否仍一致。不一致就拒絕，不部分執行。

### 關鍵字規則

v1 可以用下列規則通過 FR-18，而不部署模型：

- 若語句包含某個連線 `name`，該連線進入候選。
- 若語句包含某個範本 `name`，該範本進入候選。
- 若語句包含某個參數的 `field` 或 `label`，其後第一個不以空白拆開的詞作為值。值維持字串；全由數字組成時可作數字。
- 沒有對到的連線、範本或參數，不補猜。
- 語句中的「這個月」這類範圍詞，只有在同一個詞也能當參數值時才採用。否則該參數保持缺少。

### Python 側車

側車沿用資料平面附錄：編譯後由 C# 啟動，一次請求、一次結果。它可以產出上一節的 JSON。它不組 SQL、不開連線、不保存連線字串或業務列，也不在核對失敗時改走 `sql`。側車行程失敗時，交談得到可讀錯誤，範本與常用項目不變，填數呼叫次數是 0。

先做關鍵字規則或側車，本附錄不指定。

## 取數邊界

### MCP

工具端點沿用宿主現況（開發預設 `POST /mcp`）。工作台的 MCP 呼叫是已發布模組視圖，不是表級工具：

| 呼叫 | 何時 | 引數 |
|------|------|------|
| 已發布模組視圖 | 確認後的填數，或 MCP 活資料預覽 | `connectionId`，加上該視圖已宣告的參數 |

不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`。請求帶 `X-DBServices-Caller`，值是操作者 `id`。參數值是純量，不把篩選串進 SQL。

列出連線名稱與 `ServerType` 的讀取不是 Discovery 裡的工具。架構可以新增一個只回名稱與 `ServerType` 的中繼資料讀取，或讓工作台後端讀取與 MCP 相同的組態並剝掉連線字串。兩種都不能把連線字串放進回應，也不能把它做成可執行 SQL 的工具，也不能把它放進 Discovery。

錯誤碼沿用宿主：`invalid_arguments`、`unknown_table`、`connection_failed`、`execution_failed`。工作台顯示代碼與訊息，不重包成另一套會讓 UJ-1 對不上稽核的代碼。

### Web API

只使用既有路由：

| 需求 | 請求 |
|------|------|
| 資料表清單 | `GET /meta` |
| 欄位 | `GET /{resourceName}/fieldSet` |
| 零個篩選的列 | `GET /{resourceName}` |
| 一個篩選的列 | `GET /{resourceName}?Key=&Value=` |

兩個以上篩選、計數元件、POST、PUT、DELETE 都不發送。`default` 這筆連線的 `ServerType` 來自 WebService 組態的 `ServerType:Name`，連線字串留在該進程。

Web API 今天沒有與 MCP 相同的稽核檔。此表面的取數軌跡由工作台自己顯示，欄位仍是操作者、連線名稱、請求種類、篩選與列數或錯誤。不因此要求本 PRD 去改 WebService。

### 不連接的表面

GraphQL 的 `/graphql` 與操作台不在工作台的填數路徑。CLI 也不轉發。

## 確認與呼叫清單

一次就緒預覽的清單由後端產生，例如：

```json
{
  "calls": [
    {
      "surface": "mcp",
      "name": "sales-month",
      "connectionId": "inventory",
      "parameters": { "yearMonth": "2026-10" }
    }
  ]
}
```

操作者確認時帶回這份清單。執行階段發送這一筆，紀錄實際發送的視圖名稱與參數。比對 SM-3 時，實際集合必須與這份清單相同。

MCP 活資料預覽的確認同樣產生只含一個已發布模組視圖的清單。結構瀏覽不產生填數清單。`Web API` 的清單是零鍵或單鍵 GET，不是模組視圖。

## 被拒絕的做法

- **自由座標的畫布。** 會把工作台做成套印引擎，和 FR-13、FR-22 的順序換頁衝突。
- **在瀏覽器把全表讀回來算筆數，以補上 Web API 沒有 `count`。** 這是第二次彙總，也會把 SM-C1 做成假完成。
- **常用項目複製一份版面。** 再次叫用必須看見範本後來的修改；權限也必須重新檢查。
- **用語句直接生成 SQL 或新的篩選運算子。** 對不到等值欄位就停在 `incomplete`。
- **讓解析器持有連線組態或業務列來「回答得更準」。** 輸入表已足夠做候選。準度不能靠擴大秘密範圍。
- **工作台為了預覽去呼叫已關閉的寫入工具。** P0 的關閉清單維持關閉。

## 留給架構技能的項目

- 工作台是哪一個前端專案、由哪個進程保存工作台儲存，以及它如何代理 MCP 與 Web API。
- 連線中繼資料的讀取方式（新的唯讀中繼資料，或後端讀組態後剝除秘密）。
- 第一個管理員的安裝設定格式，以及密碼驗證演算法。
- 預覽快照識別如何在該次畫面記憶體中對上匯出，離開後如何失效。
- 側車若啟用：編譯產物、呼叫方式、逾時與版本。未啟用時，關鍵字規則必須仍能獨立通過第 4.4 節。
