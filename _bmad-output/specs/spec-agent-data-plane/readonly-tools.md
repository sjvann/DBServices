# 唯讀工具

Agent 的 Discovery 只列出已發布模組視圖。沒有已發布視圖時清單是空的，不回退成表級工具。名稱比對不區分大小寫。說明不承諾接受 SQL。

下列六個名稱是營運除錯路徑的行為，不是公開目錄。除錯路徑未開啟時，公開端點對它們回 `unknown_tool`。結果編碼、錯誤碼閉集與稽核欄位見架構骨架。

## 連線與篩選

- 選連線的參數名是 `connectionId`。已登記的名稱使用該連線。未登記則拒絕，不改落其他連線。
- 省略 `connectionId` 只在恰有一個組態完整的連線時採用它。零個或多個回 `connection_required`。組態完整的定義見 `glossary.md`。
- 空字串不是省略，視為 `unknown_connection`。
- 參數名 `connectionString` 或 `connectString`（不區分大小寫）拒絕，訊息不回顯該值，且不建立連線。
- `query`、`count`、`exists` 的等值條件只放在可省略的 `filters` 物件，可同時多個鍵。值原樣送進 Session，不拼進 SQL。
- `filters` 的鍵若與 `connectionId`、`table`、`id`、`includeView`、`connectionString`、`connectString` 相同（不區分大小寫），或兩個鍵只差大小寫，則拒絕且不呼叫 Session。
- `get`、`query`、`count`、`exists` 不先查表名。缺表不是 `unknown_table`，而是 Session 實際的列結果，或 `execution_failed`。

## tables

- 參數：`connectionId`；`includeView` 可省略，省略視為包含檢視。
- 成功：Session 回傳的資料表名稱清單。連線上沒有資料表時是空清單。

## fields

- 參數：`connectionId`；必填 `table`。
- 成功：該表的欄位。每一欄至少有名稱、型別、是否主鍵、是否不可為空，對應現有 `fields` 的 `name`、`type`、`pk`、`notNull`。
- 先以包含檢視的資料表清單核對 `table`（不區分大小寫）。不在清單中則 `unknown_table`，且不再取欄位。清單本身失敗時是 `execution_failed`，不是 `unknown_table`。

## get

- 參數：`connectionId`；必填 `table` 與整數 `id`。
- 成功：該列。沒有該列時是空結果。
- `id` 不是整數則拒絕。非整數主鍵不在 P0 擴充。

## query

- 參數：`connectionId`；必填 `table`；可省略 `filters`。
- 成功：符合等值條件的列。沒有符合列時是空結果。

## count

- 參數：`connectionId`；必填 `table`；可省略 `filters`。
- 成功：Session 的計數。沒有符合列時為 0。宿主不再數一次。

## exists

- 參數：`connectionId`；必填 `table`；可省略 `filters`。
- 成功：Session 的布林值。宿主不再判斷一次。

## 關閉名稱

`insert`、`update`、`delete`、`sql` 不在清單中。客戶端仍用這些名稱呼叫時得到 `tool_disabled`，資料無變更。其餘不在六個名稱裡的呼叫得到 `unknown_tool`。
