# McpService

給外部 Agent 與報表工作台用的 MCP 宿主。Discovery 只列出已發布檢視，工具名稱就是檢視名稱。

## 啟動

```bash
dotnet run --project McpService
```

- 工具端點：`POST http://localhost:5291/mcp`
- 檢視目錄：`catalog/views.json`（`Catalog:Path`）
- 連線目錄：`appsettings.json` 的 `Connections`，以及工作台寫入的 `catalog/connections.json`

目錄 HTTP（`/views`、`/connections`、`/schema/*`）要求 `X-DBServices-Caller`，且 `X-DBServices-Workbench` 等於 `Workbench:CatalogKey`。這個金鑰要與 [Workbench](../Workbench/README.md) 相同。

連線字串只留在伺服器。工具參數是 `connectionId` 加上該檢視宣告的純量參數。

完整說明見 [docs/mcp/index.md](../docs/mcp/index.md)。
