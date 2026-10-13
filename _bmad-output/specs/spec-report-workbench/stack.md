# 技術棧與組裝

版本是這次工作台的選定。目標框架 `net10.0`。Nullable 與 ImplicitUsings 開啟。

| 名稱 | 版本 |
| --- | --- |
| .NET | 10.0（`net10.0`） |
| ASP.NET Core Blazor | 10.0.12，共用框架 |
| Avalonia 與 Avalonia.Desktop | 12.1.4 |
| Microsoft.Data.Sqlite | 10.0.12 |
| Microsoft.Extensions.AI | 10.10.0 |
| Microsoft.Extensions.AI.Abstractions | 10.10.1 |
| Microsoft.Extensions.AI.OpenAI | 10.10.1 |
| ModelContextProtocol | 2.2.0 |
| ASP.NET Core Identity PasswordHasher | 10.0.12，共用框架 |

## 參考方向

`Workbench` 參考 `Workbench.Web`、`Workbench.Adapters` 與 `Workbench.Storage.Sqlite`。它負責組裝、依賴注入與聽取位址，沒有畫面。

`Workbench.Web` 的 Blazor 元件只參考 `Workbench.Core` 與 `Workbench.Contracts`。`Workbench.Adapters` 放置 MCP 用戶端、Web API 用戶端、`GET /connections` 與側車程序，不參考 `DbServices.Mcp`。`Workbench.Storage.Sqlite` 只放操作者、群組、角色指派、範本、常用項目與關鍵字規則，路徑是 `Workbench:StorePath`。

`Workbench.Avalonia` 只呼叫工作台 HTTP。JSON 型別就是 `Workbench.Contracts`。

`DbServices.ModelSidecar` 只參考 `Workbench.Contracts` 與上表的 AI 套件。雲端與本地都用這個 OpenAI 相容客戶端。發布為自包含執行檔。標準輸入一筆 JSON，標準輸出一筆 JSON。逾時則結束子程序並回 `model_timeout`。

部署是一個工作台進程，與 `McpService`、`WebService` 分開。`McpService` 只另加 `GET /connections`。側車是子程序。

專案樹見 `architecture-diagrams.md`。

## 組態鍵

| 鍵 | 用途 |
| --- | --- |
| `Workbench:StorePath` | 工作台 SQLite 檔 |
| `Workbench:InitialAdmin:DisplayName` | 零個操作者時的管理員顯示名稱 |
| `Workbench:InitialAdmin:Password` | 零個操作者時的管理員密碼。不寫入 SQLite、日誌與軌跡 |
| `Surfaces:Mcp:BaseUrl` | MCP 宿主基底網址。只在宿主與配接器 |
| `Surfaces:Web:BaseUrl` | Web API 基底網址。只在宿主與配接器 |
| `Surfaces:Web:ServerType` | Web API 表面顯示的引擎 |
| `model.json` | 內容根的模型設定，不在 `wwwroot`。欄位見 `workbench-shapes.md` |

殼的組態只有工作台基底網址。`AllowedHosts` 出貨值為 `*`。

## 能力所在

| 能力 | 所在 | 規則 |
| --- | --- | --- |
| CAP-1 身分與角色 | Core、SQLite、記憶體工作階段 | SPEC 約束中的帳密與工作階段 |
| CAP-2、CAP-3 活資料預覽 | Adapters 轉發，Core 產生確認清單 | `surfaces.md` |
| CAP-4 取數軌跡 | Core 的畫面軌跡 | 不進 SQLite，不寫第二份 MCP 稽核 |
| CAP-5 編輯器與範本庫 | Core、SQLite | `workbench-shapes.md` |
| CAP-6 交談與確認 | Core 的目錄比對、SQLite 詞庫、側車、記憶體清單 | `workbench-shapes.md` |
| CAP-7 常用與匯出 | SQLite 的常用項目、宿主記憶體的快照 | 匯出不得再查資料 |
| CAP-8 模型設定與測試 | `model.json`、側車 `probe` | 金鑰不進解析 JSON 與程序引數 |
