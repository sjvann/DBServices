# 分層

| 層 | 專案 | 職責 |
|----|------|------|
| 門面 | `DBService` | `MainService`、`DbServiceBuilder`、DI 擴充 |
| 核心 | `DbServices.Core` | `DataBaseService`、Session、Catalog、Functions、事務、遷移、選項 |
| 提供者 | `DbServices.Provider.*` | `DbConnection` 工廠、方言 `SqlProviderBase` |
| 客戶端 | `DbServices.Cli`、`WebService`、`GraphQLService`、`McpService` | 把輸入展開成 Session 請求、REST、GraphQL 或 MCP 工具 |
| 報表工作台 | `Workbench` 與其 Core、Web、Adapters、Storage | 檢視發布、樣式、語句產生的動態報表、生成紀錄與 PDF。取列只經 MCP |
| 共用工廠 | `DbServices.Api.Shared` | `DbServiceFactory`：依 `ServerType` 建立 `IDbService` |
| Agent 工具 | `DbServices.Mcp` | 已發布檢視的 Discovery、調度與稽核。不建立 Provider，也不執行表級工具名稱 |

```mermaid
flowchart TD
  clients[CLI / REST / GraphQL / MCP / 報表工作台 / SDK]
  session[IQuerySession]
  fn[IFunctionRegistry]
  cat[ICatalogService]
  crud[IDbService]
  scope[IDbConnectionScope]
  prov[Provider]

  clients --> session
  session --> fn
  session --> cat
  session --> crud
  fn --> crud
  cat --> crud
  crud --> scope
  scope --> prov
```

反斜線指令只活在 CLI。EZIE 不內嵌在此儲存庫；連線用 `connectionId`。
