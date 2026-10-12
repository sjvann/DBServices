# API 參考

本區由 DocFX 從專案 XML 註解自動產生，涵蓋：

| 組件 | 說明 |
|------|------|
| `DBServices` | 工廠、建構器與依賴注入入口 |
| `DbServices.Core` | 核心契約、Session、函數、目錄、連線範圍 |
| `DbServices.Provider.*` | SQLite、SQL Server、MySQL、Oracle、PostgreSQL 提供者 |
| `DbServices.Api.Shared` | REST、GraphQL、MCP 共用的 `DbServiceFactory` |
| `DbServices.Mcp` | 唯讀工具目錄、調度與呼叫稽核 |

從左側目錄瀏覽命名空間與型別，或使用頂部搜尋。

> 公開相容表面為 `IDbService`／`IDbServiceAsync`；新能力請走 `IQuerySession`。
