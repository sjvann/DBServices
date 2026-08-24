# 分層

| 層 | 專案 | 職責 |
|----|------|------|
| 門面 | `DBService` | `MainService`、`DbServiceBuilder`、DI 擴充 |
| 核心 | `DbServices.Core` | `DataBaseService`、Session、Catalog、Functions、事務、遷移、選項 |
| 提供者 | `DbServices.Provider.*` | `DbConnection` 工廠、方言 `SqlProviderBase` |
| 客戶端 | `DbServices.Cli`、`WebService` | 把輸入展開成 Session 請求或 REST |

```mermaid
flowchart TD
  clients[CLI / Web / SDK]
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
