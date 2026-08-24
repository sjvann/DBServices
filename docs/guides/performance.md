# 效能

## 連線池

由 ADO.NET 驅動管理。用 `DbServiceOptions.MinPoolSize`／`MaxPoolSize`（建構器 `WithConnectionPool`）寫入連線字串。

| 情境 | Min | Max |
|------|-----|-----|
| 單機工具／測試 | 1–2 | 5–10 |
| 中型 API | 5–10 | 50–100 |
| 高併發 | 10–20 | 100+（同時看資料庫 `max_connections`） |

症狀：連線逾時、執行緒卡住 → 先查是否未 Dispose、是否長事務佔住連線。

## 結構快取

`EnableQueryCache` 快取的是**資料表結構**（`TableStructureCacheService`），不是查詢結果。適合 schema 穩定的服務；遷移後呼叫 catalog `Reload` 或重啟進程。

## 查詢

- 只要篩選、分頁、排序，用 `IAdvancedQueryService` 或參數化 `QuerySql`。
- `GetRecordByTableName` 會拉整表，大表請加 `Take`。
- 批次寫入請用迴圈 + 事務，或資料庫原生 bulk（本庫未封裝 SqlBulkCopy）。

## 重試

`RetryPolicyService` 只應對暫時性錯誤。寫入請保證冪等，或把重試關到事務外層自行控制。

## 日誌

生產環境預設關閉 `EnableDetailedLogging`，避免把 SQL 與參數打進 Log。
