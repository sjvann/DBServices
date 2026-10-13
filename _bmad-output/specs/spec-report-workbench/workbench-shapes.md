# 工作台形狀

欄位是契約，不是資料庫綱要。名稱可以不同。不得多出連線字串、模型金鑰、明文密碼或業務列，除非該節明文允許快照在記憶體中持有該次列。

## 操作者

| 欄位 | 語意 |
| --- | --- |
| `id` | 穩定識別。呼叫 MCP 時寫入 `X-DBServices-Caller` |
| `displayName` | 畫面上的名稱 |
| `passwordVerifier` | `PasswordHasher` 驗證材料。不是明文密碼 |
| `admin` | 是否為管理員 |

沒有註冊 API。儲存裡零個操作者時，宿主讀 `Workbench:InitialAdmin:DisplayName` 與 `Workbench:InitialAdmin:Password`，寫入恰好一個管理員。已有操作者之後這兩個鍵不再使用。

## 群組與角色

| 欄位 | 語意 |
| --- | --- |
| 群組 `id`、`name` | 群組識別與名稱 |
| 指派 `operatorId`、`groupId`、`role` | `role` 只會是 `design` 或 `view` |

一個操作者對一個群組至多一筆指派。`design` 在讀取時視同 `view`。

## 報表範本

```json
{
  "id": "tmpl-sales-month",
  "groupId": "grp-sales",
  "name": "銷貨月報",
  "components": []
}
```

`name` 在同一個 `groupId` 內唯一。沒有連線名稱、連線字串、參數值或資料列。

## 常用項目

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

`surface` 只會是 `mcp` 或 `web`。`parameters` 的鍵是參數元件的欄位名稱，值是純量。沒有版面複本，也沒有資料列。參數值可能是業務鍵值，與業務列分開存放。清單以 `ownerId` 過濾。

## 預覽快照與確認清單

快照與清單只在那一個宿主進程的記憶體。鍵是操作者 `id` 加上清單識別。快照的值是該次畫面的數字與列。不寫入 SQLite。離開該次預覽，或同一操作者的下一次確認，兩份都刪除。沒有閒置逾時。

## 五種元件

`components` 依 `order` 升冪。`order` 從 0 起連續。`type` 只會是下表之一。不保存 x、y、寬與高。

| type | 必要欄位 | 填數時 |
| --- | --- | --- |
| `title` | `text` | 不呼叫。文字進預覽與匯出 |
| `text` | `text` | 不呼叫 |
| `parameter` | `field`、`label` | 不單獨呼叫。其值組成等值篩選。元件上不存值 |
| `table` | `table`、`columns` | 一次列查詢。`columns` 至少一個欄名 |
| `count` | `table` | MCP 上一次 `count`。Web API 上整份範本拒絕確認 |

同一份範本的參數值合成一個物件，每個 `table` 與 `count` 都帶上這整個物件。任一資料表缺少某個參數欄位時，整次確認被拒絕，不發送任何填數呼叫。

設計器建議的最小形狀是一個 `table`，其 `columns` 等於該次 `fields` 的欄名集合。建議裡沒有參數值與業務列。類型只會是上表五種。

## 解析輸入與狀態

送給側車的輸入只有：

| 輸入 | 內容 |
| --- | --- |
| `kind` | 解析時為 `parse` |
| `utterance` | 語句原文 |
| `connections` | `name` 與 `serverType`。沒有連線字串 |
| `templates` | 有權限範本的 `id`、`name`、`groupId`，以及參數的 `field` 與 `label` |
| `surfaces` | 這次允許的取數表面 |

不傳業務列、連線字串、密碼、詞庫或其他操作者的常用項目。

宿主計算的 `status` 只會是：

| status | 條件 |
| --- | --- |
| `ready` | 恰好一個連線、恰好一個範本，且該範本每個參數都有一個純量值 |
| `ambiguous` | 連線或範本候選多於一個 |
| `incomplete` | 沒有連線、沒有範本，或缺少參數值 |

不以解析器自報的狀態作為可以確認的依據。候選若含有本次輸入以外的連線或範本，整份作廢，填數次數為 0。等值篩選的欄必須是該範本的參數欄。

關鍵字比對在核心完成，不啟動側車：

- 語句含連線 `name`，該連線入候選。
- 語句含範本 `name`，該範本入候選。
- 語句含參數 `field` 或 `label` 時，其後第一個不以空白拆開的詞為值。值維持字串；全由數字組成可作數字。
- 對不到的不補猜。
- 時間範圍只有在同一個詞能當某個參數的一個等值時才成為篩選。否則該參數保持缺少，也不另造區間查詢。

有效的關鍵字規則再加上去。參數值仍從語句取出，不從規則取出。模型啟用且合併後的狀態不是 `ready` 時，才把上表輸入送給側車，然後由宿主重算狀態。

## 關鍵字規則

一筆規則含 `phrase`、可省略的連線名稱、可省略的範本識別、參數欄名陣列、`source`（`human` 或 `ai`）、建立者，以及是否有效。

`phrase` 非空白，且連線、範本、參數欄名至少有一項。沒有參數值、資料列、連線字串、模型金鑰與密碼。

人為新增立即有效。操作者只能為自己看得到的範本加規則。只綁連線名稱時，已登入操作者即可新增。來自 AI 的規則只在操作者確認該次意圖後，按一次加入關鍵字才寫入。`phrase` 可由語句帶入，並在寫入前修改。參數只留欄名。模型自行完成的解析不寫入規則。建立者或管理員可把規則改為無效。比對時略過這位操作者現在看不到的範本。解析不讀常用項目。

## 確認清單

一次就緒的範本預覽由核心產生，例如：

```json
{
  "calls": [
    {
      "surface": "mcp",
      "name": "query",
      "connectionId": "inventory",
      "table": "sales",
      "filters": { "yearMonth": "2026-10" },
      "componentId": "c-table"
    },
    {
      "surface": "mcp",
      "name": "count",
      "connectionId": "inventory",
      "table": "sales",
      "filters": { "yearMonth": "2026-10" },
      "componentId": "c-count"
    }
  ]
}
```

每個資料表元件一筆列查詢。MCP 上每個計數元件一筆 `count`。確認請求帶回清單識別。執行前再核對清單、範本、角色與表面能力。實際送出的填數呼叫與清單相同，沒有額外的 `query`、`count` 或 Web API 資料 GET。

## 模型設定與探測

有效設定只有一份。欄位是 `enabled`、`placement`（`cloud` 或 `local`）、`endpoint`、`modelId`、`apiKey`、`probeTimeoutSeconds`、`parseTimeoutSeconds`。

`cloud` 的 `endpoint` 必須是 `https`，且有效金鑰非空白。`local` 的主機必須是 `localhost`、`127.0.0.1` 或 `::1`，金鑰可空白。

探測的標準輸入只有 `kind` 為 `probe`、`placement`、`endpoint`、`modelId`。草稿有帶金鑰就用草稿，否則用目前有效設定。解析不使用草稿。
