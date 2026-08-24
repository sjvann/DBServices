# 架構

DBServices 是**薄的多資料庫存取層**：統一介面、方言 SQL 產生、連線範圍與 Session 分派。它不是新的資料庫引擎。

完整契約（前端協定、刻意不做的範圍）見 [架構契約](../ARCHITECTURE.md)。

```text
CLI / Web / C# SDK / EZIE plugin
        ↓
   IQuerySession   （Query / Call / Utility）
        ↓
 ICatalogService + IFunctionRegistry
        ↓
 IDbConnectionScope（每次操作取連線、用完歸還；事務期間持有）
        ↓
 Provider（連線工廠 + 方言 + 目錄 SQL）
```

## 相容性

公開 `IDbService`／`IDbServiceAsync` 簽名維持相容。新能力走 `IQuerySession`，不要再擴一長串 CRUD 多載。

## 深入

- [分層](layers.md)
- [連線範圍](connections.md)
