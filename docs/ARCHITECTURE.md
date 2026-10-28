# DBServices 架構契約

本文件定義前端協定、分層與刻意不做的範圍。公開 `IDbService`／`IDbServiceAsync` 簽名維持相容；新能力走 `IQuerySession`。

## 分層

```
CLI / REST / GraphQL / MCP / 報表工作台 / C# SDK / EZIE plugin
        ↓
   IQuerySession   （Query / Call / Utility）
        ↓
 ICatalogService + IFunctionRegistry
        ↓
 IDbConnectionScope（每次操作取連線、用完歸還；事務期間持有）
        ↓
 Provider（連線工廠 + 方言 + 目錄 SQL）
```

報表工作台不持有來源庫連線。它把已發布檢視寫進 `McpService` 的目錄，取列時只呼叫該檢視一次。說明見 [MCP 資料平面](mcp/index.md) 與 [動態報表工作台](workbench/index.md)。

## 三路分派

| Opcode | 用途 | 例子 |
|--------|------|------|
| Query | 交給底層引擎的 SQL | `SELECT ...`、`INSERT ...` |
| Call | 已註冊函數 | `get`、`tables`、`insert` |
| Utility | 工作階段管理 | `ping`、`reload` |

反斜線指令（`\dt`、`\d`）只存在於 `dbsql` 客戶端，展開成 Call／Catalog 請求後再送 Session。

## 連線

- Provider 只提供 `Func<DbConnection>`，不長活持有連線。
- 一般操作：建立 → 開啟 → 執行 → Dispose（歸還 pool）。
- 事務：`AcquireHeldConnection` 持有同一連線，Dapper 呼叫帶 ambient `IDbTransaction`。

## 非目標

- 不自製跨廠商 SQL 方言或 planner。
- 不實作 MVCC／WAL／自研儲存引擎。
- 不複製 libpq 二進位協定。
- 不在 DBServices 內嵌 `Ezie.Sdk`。

EZIE 元件庫走獨立 plugin pack（`ezie.dbservices.*`），見 AI_EZIE `docs/ezie/dbservices-pack.md`。
