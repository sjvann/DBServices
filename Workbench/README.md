# 報表工作台

用已發布檢視產生動態報表。系統分析師定義並發布檢視，報表設計師排樣式，終端使用者用一句話取出這一次的列。

取列只呼叫 [McpService](../McpService/README.md) 的該檢視一次。連線字串不進工作台 SQLite，也不進生成紀錄。

## 啟動

先啟動 MCP 宿主，再啟動工作台。兩邊的 `Workbench:CatalogKey` 必須相同。

```bash
dotnet run --project McpService
dotnet run --project Workbench
```

- 網站：`http://localhost:5099`
- 檢視：`/views`
- 樣式：`/styles`
- 產生報表：`/talk`
- 生成紀錄：`/records`
- 環境（管理員）：`/environment`

零個操作者時，`Workbench:InitialAdmin` 會建立恰好一個管理員。範例密碼與目錄金鑰只供本機，對外開放前請更換。

桌面殼：

```bash
dotnet run --project Workbench.Avalonia
```

它只打工作台 HTTP，預設 `http://localhost:5099`。

完整說明見 [docs/workbench/index.md](../docs/workbench/index.md)。
