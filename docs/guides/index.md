# 指南

依主題深入使用 DBServices。建議先完成 [快速開始](../getting-started/quickstart.md)。

| 指南 | 何時閱讀 |
|------|----------|
| [資料庫提供者](providers.md) | 選擇 SQLite／SQL Server／MySQL／Oracle／PostgreSQL |
| [依賴注入](dependency-injection.md) | ASP.NET Core 或 Generic Host |
| [建構器](builder.md) | 程式化組態、測試時替換相依 |
| [CRUD](crud.md) | 新增、更新、刪除與依鍵讀取 |
| [查詢](query.md) | 條件、分頁、排序、`IAdvancedQueryService` |
| [Session](session.md) | Query／Call／Utility 三路分派 |
| [函數](functions.md) | 內建 `tables`、`get`、`insert`…與自訂註冊。MCP 只露出其中六個唯讀名稱 |
| [事務](transactions.md) | `TransactionService` 與持有連線 |
| [遷移](migration.md) | `MigrationBase` 與版本表 |
| [多資料庫](multi-database.md) | `IMultiDatabaseService` 與 keyed DI |
| [效能](performance.md) | 連線池、快取、批次 |
| [安全](security.md) | 參數化、驗證、連線字串 |
| [PostgreSQL JSON](postgresql-json.md) | JSON／JSONB 欄位 |
| [新增提供者](add-provider.md) | 擴充新的資料庫引擎 |
