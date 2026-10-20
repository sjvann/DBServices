# 技術棧與組裝

目標框架 `net10.0`。版本對過 2026-10-20 的專案檔。這一輪不新增套件。共用框架的修補版不在專案檔裡釘死。

| 名稱 | 版本 |
| --- | --- |
| .NET | 10.0（`net10.0`） |
| Avalonia 與 Avalonia.Desktop | 12.1.4 |
| Microsoft.Data.Sqlite | 10.0.12 |
| Microsoft.Extensions.AI | 10.10.0 |
| Microsoft.Extensions.AI.Abstractions | 10.10.1 |
| Microsoft.Extensions.AI.OpenAI | 10.10.1 |
| ModelContextProtocol | 2.2.0 |
| QuestPDF | 2026.9.1 |

登入雜湊是專案內的 `Pbkdf2PasswordHasher`，實作 `Workbench.Core` 的 `IPasswordHasher`，由 `Workbench.Adapters` 呼叫。PDF 由 `Workbench.Adapters` 以 QuestPDF 產生。AI 套件只留在倉儲裡的 `DbServices.ModelSidecar`。三個入口不啟動它，也不部署 Python。

## 參考方向

`Workbench` 參考 `Workbench.Web`、`Workbench.Adapters` 與 `Workbench.Storage.Sqlite`。它負責組裝、依賴注入與聽取位址，沒有畫面。

`Workbench.Web` 的 Blazor 只參考 `Workbench.Core` 與 `Workbench.Contracts`。`Workbench.Adapters` 放置已發布檢視呼叫、目錄 HTTP 與連線筆數查詢，不參考 `DbServices.Mcp`。`Workbench.Storage.Sqlite` 只放操作者、入口指派、樣式範本與生成紀錄，路徑是 `Workbench:StorePath`。

`Workbench.Avalonia` 只呼叫工作台 HTTP。JSON 型別就是 `Workbench.Contracts`。

部署是一個工作台進程，與 `McpService`、`WebService` 分開。檢視目錄在 `McpService` 那一側。應用不新增雲端供應商。

專案樹與依賴圖見 `architecture-diagrams.md`。

## 組態鍵

| 鍵 | 用途 |
| --- | --- |
| `Workbench:StorePath` | 工作台 SQLite 檔 |
| `Workbench:InitialAdmin:DisplayName` | 零個操作者時的管理員顯示名稱 |
| `Workbench:InitialAdmin:Password` | 零個操作者時的管理員密碼。不寫入 SQLite 與日誌 |
| `Workbench:CatalogKey` | 目錄寫入的進程鍵。不是密碼，不寫入 SQLite |
| `Surfaces:Mcp:BaseUrl` | MCP 宿主基底網址。只在宿主與配接器 |
| `Surfaces:Web:BaseUrl` | 留給尚未綁定的轉置。不出現在畫面 |

沒有連線字串鍵。工作台不讀 WebService 的組態檔。

## 繼承的資料平面約束

| 父編號 | 在這裡的效力 |
| --- | --- |
| parent AD-1 | 不改公開簽名、Core、FunctionRegistry、Provider、既有 REST 與 GraphQL |
| parent AD-2 | Discovery 只列已發布檢視。Chat 只呼叫樣式所綁的那一個。不呼叫六個資料表工具與四個關閉工具 |
| parent AD-3 | 參數維持純量。不拼進 SQL，不送 `connectionString` |
| parent AD-4 | 連線字串只在 `McpService` 進程綁定。工作台不載入它 |
| parent AD-5 | 傳輸維持 `POST /mcp`，套件 2.2.0 |
| parent AD-6 | 呼叫者標頭維持 `X-DBServices-Caller`。工作台送操作者 `id`。不在 `McpService` 加登入 |
| parent AD-7、AD-9 | MCP 錯誤碼與成功形狀原樣。不重算列，不把空列改成錯誤 |
| parent AD-8 | 工作台不依賴 `fields` 的 `unknown_table`。宿主錯誤碼仍原樣顯示 |
| parent AD-10 | MCP 稽核仍是那一個 JSONL。工作台不寫第二份。保留天數仍未定 |
| parent AD-11 | 模型不進入 Core 與 MCP 宿主。三個入口不啟動側車 |
| parent AD-12 | 檢視定義不含連線字串與資料列。不在來源庫執行 `CREATE VIEW`。沒有連通路徑時不發布、不產生笛卡兒積 |

## 能力落點

| 能力 | 落點 |
| --- | --- |
| CAP-9 登入與入口 | `Workbench.Core`、SQLite、記憶體工作階段 |
| CAP-10 檢視定義與發布 | `DbServices.Mcp` 的目錄 HTTP。規劃期表結構的通道未定 |
| CAP-11 樣式 | `Workbench.Core`、SQLite |
| CAP-12 語句與動態報表 | Core 的比對，配接器的一次檢視呼叫，宿主記憶體 |
| CAP-13 生成紀錄與匯出 | SQLite 的紀錄，或記憶體中的未保存報表。PDF 在配接器 |

## 未啟用

`DbServices.ModelSidecar` 仍是 net10.0 獨立執行檔。三個入口的適配器不啟動它。既有 `model.json` 的載入順序維持不變：內容根的 `model.json`（不在 `wwwroot`）、Development 且有 `UserSecretsId` 時的 user secrets、環境變數、命令列。`apiKey` 不進程序引數。命令列能否覆寫 `apiKey` 仍未分開寫死；這一輪不代選。金鑰不進 SQLite、語句 JSON 與日誌。

關鍵字詞庫的新增介面不在這一輪。日後若寫入工作台 SQLite，一筆規則不得含參數值、資料列、連線名稱、連線字串或金鑰。

把檢視先落地成另一份資料表不在這一輪。現行是 `McpService` 依檢視定義即時讀取。
