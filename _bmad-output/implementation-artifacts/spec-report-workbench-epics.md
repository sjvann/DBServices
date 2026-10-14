---
title: '報表工作台：登入、活資料、範本、語句與匯出'
type: 'feature'
created: '2026-10-13'
status: 'in-review'
baseline_revision: 'dd8b0906994406207fc8e47af46dffbd3df3a3aa'
baseline_commit: 'dd8b0906994406207fc8e47af46dffbd3df3a3aa'
review_loop_iteration: 2
followup_review_recommended: false
context: []
warnings:
  - multiple-goals
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** 操作者沒有一個與業務庫分開的報表工作台。未登入不得讀取連線、欄位、列、範本或常用項目，確認之前不得看到業務列，範本也不得把這次的列存進去。

**Approach:** 在現有方案加入一個 `Workbench` 進程與兩個殼。填數、結構、範本、角色、解析、軌跡與快照只經過 `Workbench.Core` 的端口；基礎設施以建構子注入，組裝只發生在 `Workbench`。MCP 只新增 `GET /connections`。既有資料平面不改。

## Boundaries & Constraints

**Always:**
- 新專案皆 `net10.0`，`Nullable` 與 `ImplicitUsings` 開啟：`Workbench`、`Workbench.Web`、`Workbench.Core`、`Workbench.Contracts`、`Workbench.Adapters`、`Workbench.Storage.Sqlite`、`Workbench.Avalonia`、`DbServices.ModelSidecar`、`Workbench.Tests`。
- 依賴方向：`Workbench` 參考 Web、Adapters、Storage，負責 DI、SQLite 路徑、聽取位址，沒有畫面。`Workbench.Web`（Blazor 與控制器）只參考 Core 與 Contracts。`Workbench.Avalonia` 只呼叫工作台 HTTP，JSON 用 Contracts，組態只有基底網址。Core 只參考 Contracts。Adapters 實作 Core 端口，可參考 `ModelContextProtocol` 2.2.0。Storage 實作儲存端口，使用 `Microsoft.Data.Sqlite` 10.0.12。側車只參考 Contracts 與 `Microsoft.Extensions.AI` 10.10.0、`Microsoft.Extensions.AI.Abstractions` 10.10.1、`Microsoft.Extensions.AI.OpenAI` 10.10.1。
- SOLID / DI：一個端口一個介面；Core 的用例類別只透過建構子取得端口，不 `new` 配接器、不讀組態、不使用服務定位器。控制器與 Blazor 頁面各依賴一個用例類別，不寫第二套判斷。`Workbench/Program.cs` 是唯一組裝處。
- 介面文字、空狀態、確認清單與工作台自己的錯誤用繁體中文。工作台錯誤是穩定英文蛇形碼加繁體中文訊息。MCP 錯誤碼與宿主訊息原樣顯示。
- 色票、字級、間距、圓角依 Epic UX-DR1–DR3。只有淺色。確認色只用在標籤為「查詢」「計數」「範本預覽」「確認」且即將執行的那一顆。等寬只用在工具名、錯誤碼、`ServerType`、參數鍵。
- 呼叫者標頭是 `X-DBServices-Caller`，值為操作者 `id`。未登入不呼叫 MCP，也不送 `anonymous`。
- 工作台 MCP 工具名稱只會是 `tables`、`fields`、`query`、`count`。Web 只送 `GET /meta`、`GET /{resourceName}/fieldSet`、零鍵資料表 GET、單鍵 `Key` 與 `Value`。
- `apiKey` 不進 SQLite、解析 JSON、工作台程序引數、日誌與測試輸出。命令列不覆寫 `apiKey`（Story 3.7 明文要求這一則不把金鑰寫進程序引數）。其餘模型欄位覆寫順序：內容根 `model.json`、Development 且有 `UserSecretsId` 的 user secrets、環境變數、命令列。
- 軌跡時間用 UTC。快照無閒置逾時，不寫入 SQLite。

**Never:**
- 不修改 `IDbService`、`IDbServiceAsync`、`DataBaseService`、`FunctionRegistry`、`IQuerySession`、Provider、既有 REST 與 GraphQL 路由與 JSON。不建立 `IDbService`，不呼叫 `QuerySession`、`QuerySql`、`ExecuteSql`。
- 不讓 Workbench、Adapters、Web、Avalonia、Core 參考 `DbServices.Mcp`。不把連線字串放進請求、儲存、匯出、畫面、錯誤、軌跡、稽核或選項類別。Web `fieldSet` / 資料信封的 `ConnectString` 在配接器邊界丟棄。
- 不加 MCP 工具，不把 `GET /connections` 掛在 `/mcp` 之下，不在 `McpService` 加登入。不寫第二份 MCP 稽核，不實作稽核保留天數。
- 不送 `insert`、`update`、`delete`、`sql`、`get`、`exists`，不送 Web 的 POST、PUT、DELETE。GraphQL 不是取數表面。不部署 Python。`IChatClient` 不進入 Blazor、Avalonia、Core 或 MCP 宿主。
- 不做深色模式、成功綠、圖表、自由座標、預印表單、儀表板牆、確認前的業務列、公開註冊、第二個管理者。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 種子管理者 | SQLite 零個操作者，且有 `Workbench:InitialAdmin:DisplayName` 與 `Password` | 恰好一個管理者；`passwordVerifier` 不是明文。再次啟動不再讀這兩個鍵 | 明文不出現在 SQLite、日誌、軌跡 |
| 無初始密碼 | 零個操作者且缺少密碼 | 登入與功能路由都回 `workbench_unavailable`／「工作台不提供功能。」不建立工作階段 | 不呼叫 MCP |
| 未登入 | 無有效 `sessionId`，要求連線、欄位、列、範本或常用項目 | 內容不回傳。cookie 與 bearer 都只帶 `sessionId` | 不呼叫 MCP，呼叫者不是 `anonymous` |
| 兩殼登出 | 瀏覽器與 Avalonia 持有同一 `sessionId`，任一殼登出或進程重啟 | 該筆刪除；兩邊同時失效。重啟後工作階段清空 | 舊 `sessionId` 不能再讀 |
| 帳號與角色 | 管理者建立只有名稱與秘密的帳號；角色指派可同時勾選三個工作角色，同一群組至多一筆設計或檢視 | 帳號列只有名稱。不能建立第二個管理者。設計角色在讀取時視同檢視角色。儲存角色不發出填數呼叫 | 非管理者進入帳號或角色指派被拒絕，資料不變 |
| 左軌 | 非管理者的工作角色組合 | 規劃設計師才有活資料預覽；範本編輯師或終端使用者才有範本庫；終端使用者才有交談套版與常用清單；擁有任一工作角色才有預覽。管理者不受指派限制，左軌同時有這三類工作表面，以及帳號、角色指派、模型設定 | 沒有任何工作角色時沒有三類工作表面，文案「尚未指派工作角色。」直接進入沒有的表面被拒絕 |
| 連線清單 | 管理者或規劃設計師；MCP 表面 | `GET /connections` 的每一筆只有 `name` 與 `serverType`，且只含組態完整的連線 | 不回傳連線字串與是否可用。Web 表面改為恰好一筆 `default`，`ServerType` 來自 `Surfaces:Web:ServerType` |
| 結構瀏覽 | 已登入且有權限，省略 `includeView` | 只呼叫 `tables` 與 `fields`，或 Web 的 `GET /meta` 與 `fieldSet`。欄位至少有名稱、型別、是否主鍵、是否不可為空 | 不產生填數清單、不回傳業務列。空表清單顯示「沒有資料表。」 |
| 結構錯誤 | MCP 回 `unknown_table`、`connection_failed` 或 `invalid_arguments` | 錯誤區塊第一行原錯誤碼，第二行不含秘密的訊息。人可改選其他連線或表 | 軌跡含操作者 `id`、連線名稱、工具與錯誤碼，不寫入工作台 SQLite |
| 確認前 | 已選連線、表與篩選，尚未按查詢 | 預覽頁文案「確認之前，這裡沒有業務列。」 | 不發送填數呼叫 |
| MCP 查詢 | 按下「查詢」 | 清單恰好一筆 `query`，篩選以參數傳遞。列等於該次工具結果。零列顯示「沒有符合的列。」 | 失敗時人留在預覽頁；先前成功列留到下一次成功確認才換。`execution_failed` 不顯示部分列 |
| Web 查詢 | 零個或一個篩選 | 零個送 `GET /{resourceName}`；一個送 `Key` 與 `Value` | 兩個以上拒絕，文案「此表面只接受單鍵」，該請求不發送 |
| MCP 計數 | 按下「計數」 | 清單恰好一筆 `count`，顯示工具回傳的數字；無列顯示 0 | Web 表面沒有計數動作，不以讀全表自己計算 |
| 換引擎 | 同一表與篩選，連線從 `inventory`（SqlServer）改到 `inventory_pg`（PostgreSQL） | `tables`、`fields`、`query`、`count` 的名稱與參數鍵不變 | 畫面無連線字串，也不要求另選引擎 |
| 稽核對照 | 一次成功的 MCP `query` 或 `count` | 軌跡含操作者 `id`、連線名稱、工具、等值篩選、列數或計數值。宿主 JSONL 找得到相同呼叫者、連線、工具、篩選與列數 | 稽核寫入失敗而回 `execution_failed` 時只顯示該錯誤。工作台不寫第二份稽核 |
| 連線失敗 | 某一筆具名連線失敗 | 工作台與 MCP 宿主都仍在，其他連線仍可選 | 該筆是連線錯誤，程序不結束 |
| 保存範本 | 範本編輯師對目標群組有設計角色 | 範本恰屬該群組，同群組名稱不重複。保存只有識別、群組、名稱與元件 | 沒有設計角色則拒絕且不建立。無關連線時重新打開仍看得到版面，沒有業務列 |
| 畫布 | 五種元件，拖曳或上移／下移 | `type` 只會是 `title`、`text`、`parameter`、`table`、`count`。`order` 從 0 連續，不保存 x、y、寬、高 | 檢視角色只能看。刪除後重新開啟不再含該元件 |
| 建議 | 選定資料表後產生建議 | 產生前只有結構呼叫。至少一個資料表元件，欄名全集等於該次 `fields` | 未知欄名不能接受。忽略或失敗不改既有畫布，失敗有可讀錯誤，填數次數是 0 |
| 範本可見性 | 沒有該群組角色的非管理者 | 清單不含該群組；以識別直接讀取被拒絕。重新命名群組不改變識別，其中範本仍屬於它 | 填數次數是 0。管理者看得到但不能在沒有設計角色時修改。群組內仍有範本時刪除群組被拒絕。移到沒有設計角色的群組被拒絕且留在原群組。檢視角色不能建立、修改、刪除或移動 |
| 範本預覽 | 每個參數都有純量值，尚未確認 | 每個資料表元件一筆列查詢；MCP 上每個計數元件一筆 `count`；篩選是全部參數。確認鈕為「範本預覽」 | 缺欄、Web 篩選不是零或一、或 Web 含計數：整次拒絕，填數次數 0。任一筆失敗只顯示該錯誤 |
| 語句 | 終端使用者或管理者送出語句；Enter | 只解析不確認。候選只來自己列出的連線與有權限範本。參數值是其後第一個空白分隔詞；全數字可作數字 | 不是恰好一個連線、一個範本、且每個參數都有值時，確認不可按，填數次數 0。狀態文字為 `ready`、`ambiguous` 或 `incomplete`。解析失敗不改範本、常用項目與規則 |
| 確認核對 | 狀態為 `ready`，按下「確認」 | 執行前再核對清單識別、範本、角色與表面能力。一致時實際呼叫集合與清單相同 | 不一致則整次拒絕，不部分執行 |
| 常用項目 | 儲存一次就緒叫法 | 含名稱、範本識別、表面、連線名稱、參數值、擁有者。清單只回擁有者自己的 | 再開讀範本目前版面，不查詞庫、不讀舊列。範本已刪、失去檢視角色或參數對不齊時指出差異，填數次數 0 |
| 匯出 | 預覽頁仍有這次快照 | PDF、列印、CSV 的數字與文字等於快照，含空結果。PDF 為 A4，換頁以元件為界 | 不發出新的填數呼叫。離開預覽或再次確認後舊快照匯出被拒絕 |
| 關鍵字 | 人為新增，或確認後再按「加入關鍵字」 | 人為規則立刻有效，來源 `human`。AI 規則只有確認後的第二次按鈕才寫入，來源 `ai`，只留欄名 | 無效規則與現在看不到的範本在比對時略過。規則不含參數值、列、連線字串、金鑰 |
| 模型測試 | 管理者送出未儲存草稿 | 不改 `model.json`。標準輸入只有 `kind=probe`、`placement`、`endpoint`、`modelId`。金鑰只在子程序環境變數 `DBSERVICES_MODEL_API_KEY` | 讀取設定只回金鑰是否已設定。非管理者拒絕。檢查失敗回 `model_invalid_settings` 且不啟動側車。結果碼只會是 `model_ok`、`model_invalid_settings`、`model_unreachable`、`model_timeout` |
| 模型解析 | 已啟用且關鍵字狀態不是 `ready` | 側車標準輸入 `kind=parse`，只含語句、看得到的連線名稱與 `ServerType`、有權限範本摘要、允許的表面。宿主重算狀態 | 候選超出這次輸入則整份作廢，填數次數 0。已 `ready`、未啟用或失敗時不把側車狀態當成可確認。失敗不改範本、常用項目與規則 |

</intent-contract>

## Code Map

- `E:\sjvann\DBServices\DBServices.sln` -- 加入九個新專案。不要改既有專案的參考。
- `E:\sjvann\DBServices\Directory.Build.props` L7–16 -- 把 `Workbench.Storage.Sqlite` 加入 SQLitePCLRaw 3.53.3 的條件，與現有 Sqlite 釘選相同。
- `E:\sjvann\DBServices\McpService\Program.cs` L10–17、L83–84 -- 連線已綁成 `ToolCatalog` 單例；在 `MapMcp("/mcp")` 旁加 `GET /connections`，不要放進工具迴圈。
- `E:\sjvann\DBServices\DbServices.Mcp\ToolCatalog.cs` L17–26、L40、L42–49、L65–78 -- 六個工具定義與 `Connections`。`Available` 為已知引擎且連線字串非空白。新端點只投影名稱與 `ServerType`。Workbench 不參考這個專案，只呼叫 HTTP。
- `E:\sjvann\DBServices\McpService\Program.cs` L153–215 -- 稽核路徑 `Audit:Path` 或 `audit/mcp.jsonl`；`X-DBServices-Caller` 空白才會變成 `anonymous`。工作台有工作階段才呼叫，並帶操作者 `id`。
- `E:\sjvann\DBServices\DbServices.Mcp\CallAudit.cs` L46–86 -- JSONL 欄位 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。對照時用這些欄位，不解析 `value` 裡的業務列到畫面上。
- `E:\sjvann\DBServices\WebService\DbWebService.cs` L41–52 -- 既有 GET／POST／PUT／DELETE。工作台只使用 GET `/meta`、`/fieldSet`、空白路徑的 `Key`／`Value`。不要改這個檔。
- `E:\sjvann\DBServices\WebService\Models\QueryModel.cs` -- 只有 `Key` 與 `Value`。多的查詢參數會被忽略，所以兩個以上篩選必須在工作台拒絕、不發送。
- `E:\sjvann\DBServices\DbServices.Core\Models\TableBaseModel.cs` L165 -- 信封含 `ConnectString`。配接器讀 `FieldDefine` 與 `RecordData` 後丟棄此鍵。
- `E:\sjvann\DBServices\McpService.Tests\ToolListTests.cs` -- 既有六工具測試。新連線端點測試放在同一專案，既有斷言保持通過。
- `E:\sjvann\DBServices\_bmad-output\planning-artifacts\ux-designs\ux-DBServices-2026-10-13\mockups\` -- `key-live-preview.html`、`key-editor.html`、`key-preview.html`、`key-role-assign.html` 是資訊架構對照，不是要複製的實作。

## Tasks & Acceptance

**Execution:**
- `E:\sjvann\DBServices\Directory.Build.props` -- 將 `Workbench.Storage.Sqlite` 納入既有 SQLite 原生函式庫釘選 -- 避免新儲存專案帶入已釘選掉的舊原生套件。
- `E:\sjvann\DBServices\Workbench.Contracts\Workbench.Contracts.csproj` -- 建立 `net10.0` 類別庫，放入元件、登入、連線、軌跡、錯誤、範本、意圖、常用項目、匯出與模型的 JSON 型別，以及 `DesignTokens` 常數 -- 兩殼與側車共用契約，不含行為。
- `E:\sjvann\DBServices\Workbench.Core\Workbench.Core.csproj` -- 建立只參考 Contracts 的類別庫。`Ports\` 每個基礎設施一個介面（密碼雜湊、操作者與群組與指派、範本、常用項目、關鍵字、工作階段、連線目錄、資料表面、MCP 稽核讀取、預覽記憶、模型側車、UTC 時鐘）。用例類別以建構子取得這些介面：種子管理者、工作階段、帳號、角色與左軌、活資料預覽、範本庫與畫布、建議、語句解析、填數清單、常用項目、快照匯出、關鍵字、模型設定 -- Core 不參考 ASP.NET、Avalonia、Sqlite、MCP、AI。
- `E:\sjvann\DBServices\Workbench.Storage.Sqlite\Workbench.Storage.Sqlite.csproj` -- 實作操作者、群組、角色、範本、常用項目、關鍵字端口。檔內只有這些資料；範本不存連線名稱、參數值與列；常用項目存表面、連線名稱與純量參數值，不存版面複本與列。密碼只存 `passwordVerifier` -- 業務庫不建表。
- `E:\sjvann\DBServices\Workbench.Adapters\McpSurface.cs` -- 用 `ModelContextProtocol` 2.2.0 呼叫 `tables`、`fields`、`query`、`count`，省略 `includeView` 時不送該引數，篩選以巢狀純量參數傳送，並加 `X-DBServices-Caller` -- 不呼叫 `get`、`exists` 與寫入工具。
- `E:\sjvann\DBServices\Workbench.Adapters\McpConnectionCatalog.cs` -- `GET {Surfaces:Mcp:BaseUrl}/connections`，只保留 `name` 與 `serverType` -- 不參考 `DbServices.Mcp`。
- `E:\sjvann\DBServices\Workbench.Adapters\McpAuditReader.cs` -- 由宿主注入稽核檔路徑，讀取那一份 JSONL。成功的 MCP `query`／`count` 必須用讀到的列核對呼叫者、連線、工具、篩選與列數，並把是否相符留在軌跡上 -- 不另寫稽核檔，也不在配接器裡讀組態系統。找不到列時不改寫 MCP 已成功的業務列。
- `E:\sjvann\DBServices\Workbench.Adapters\WebSurface.cs` -- 只發允許的 GET；`fieldSet` 與資料信封丟棄 `ConnectString`；Web 連線目錄回傳一筆 `default` -- 不讀 WebService 的組態檔，不發送計數與多鍵。
- `E:\sjvann\DBServices\Workbench.Adapters\SidecarLauncher.cs` -- 啟動 `DbServices.ModelSidecar`，金鑰只放子程序環境變數，逾時結束程序。探測與解析的標準輸入遵守矩陣 -- `cloud` 要 https 且金鑰非空白；`local` 主機只能是 `localhost`、`127.0.0.1`、`::1`，空白金鑰時子程序內使用佔位憑證 `local` 且不寫回設定。
- `E:\sjvann\DBServices\Workbench.Adapters\SnapshotPdfWriter.cs` -- 用與 `net10.0` 相容的 QuestPDF 將記憶體中的快照畫成 A4 PDF -- 渲染時不呼叫資料表面。CSV 由 Core 依快照欄位直接寫出。
- `E:\sjvann\DBServices\McpService\Program.cs` -- 在 L84 之前對應 `GET /connections`：只列出 `ToolCatalog.Connections` 中 `Available` 的名稱與 `ServerType` -- 不是 MCP 工具，不回傳連線字串與 `Available`。
- `E:\sjvann\DBServices\McpService.Tests\ConnectionListTests.cs` -- 斷言完整連線才出現、JSON 只有 `name` 與 `serverType`、工具清單仍恰好六個 -- 保護既有唯讀廣告。
- `E:\sjvann\DBServices\DbServices.ModelSidecar\Program.cs` -- 讀一筆 JSON、寫一筆 JSON。`probe` 與 `parse` 使用 OpenAI 相容客戶端並每次設定 `Endpoint`，不使用套件預設的 `https://api.openai.com/v1`。不組 SQL、不開資料庫、不寫檔、不回傳可直接插入的規則命令 -- 模型留在側車。
- `E:\sjvann\DBServices\Workbench\Program.cs` -- 唯一組裝處：註冊端口實作、記憶體工作階段與預覽快照、種子管理者、Blazor 互動伺服器與薄控制器。`AllowedHosts` 為 `*`。`model.json` 放內容根且不在 `wwwroot`。命令列組態不綁 `apiKey`。零個操作者且無初始密碼時，功能路由回 `workbench_unavailable` -- 單一進程持有 SQLite 與記憶體狀態。
- `E:\sjvann\DBServices\Workbench.Web\Controllers\` -- 每個控制器注入一個 Core 用例，投影 Contracts。HTTP 表面固定為 `/api/sessions`、`/api/me`、`/api/operators`、`/api/groups`、`/api/assignments`、`/api/connections`、`/api/structure`、`/api/plans`、`/api/templates`、`/api/suggestions`、`/api/intent`、`/api/favorites`、`/api/preview`（含 `pdf`、`csv`、`print`）、`/api/keyword-rules`、`/api/model`（含 `probe`） -- Avalonia 與測試走這層，控制器不做業務判斷。Blazor 不經這些路由，直接呼叫同一批用例。
- `E:\sjvann\DBServices\Workbench.Web\Components\Routes.razor` -- 路由使用 `InteractiveServer`，讓按鈕在瀏覽器裡執行。工作階段在第一次請求從 cookie 讀入元件狀態，之後的點擊不再依賴已清空的 `HttpContext` -- 靜態預渲染不能當成已接上用例。
- `E:\sjvann\DBServices\Workbench.Web\Components\` -- 每個寫入或填數按鈕在點擊時呼叫對應用例：活資料預覽要列出連線並讓人改選，載入資料表與欄位，確認後把列畫成表格，不能永遠用第一筆連線，也不能把參數鍵拼成未編碼的 HTML。角色指派的儲存要寫入指派。編輯器要能加入五種元件、上移下移、刪除並儲存。交談套版在 `ambiguous` 時列出候選，確認成功後進入預覽。常用清單要能開啟。確認後要有「加入關鍵字」。模型頁要能儲存與探測。預覽頁要畫出資料表儲存格，並列印、PDF、CSV。兩殼都要能登出 -- 編輯器不佔左軌常駐位置。低於 1100px 時軌跡改到主欄下方。
- `E:\sjvann\DBServices\Workbench.Web\wwwroot\tokens.css` -- 從 `DesignTokens` 實作淺色權杖與兩種按鈕 -- 停用原因用 body 字級寫出，不只靠顏色。
- `E:\sjvann\DBServices\Workbench.Avalonia\Program.cs` -- Avalonia 12.1.4 與 Avalonia.Desktop。最小視窗 1100×720。預設基底網址與 `Workbench` 的 launchSettings 相同。查詢、計數、確認、儲存角色、開啟常用、匯出、加入關鍵字與登出都呼叫對應的 `/api`，查詢與計數在已選資料表時可按，不能把主區清空或把確認鈕寫死停用。登入後 bearer 只帶 `sessionId`。不嵌入 Blazor，不開啟 SQLite -- 殼的組態只有工作台基底網址。
- `E:\sjvann\DBServices\Workbench.Tests\Workbench.Tests.csproj` -- MSTest 加 `Microsoft.AspNetCore.Mvc.Testing`。以 `WebApplicationFactory` 驅動 HTTP，並以 Blazor 測試主機檢查左軌、確認鈕與預覽文案。矩陣每一列至少一個測試。稽核列用真實 `McpService` 的 JSONL 對照。假資料表面記錄工具名稱與參數鍵，證明未發送被禁止的呼叫 -- 測試輸出不含金鑰與連線字串。

**Acceptance Criteria:**
- Given 規劃設計師林可已登入，when 她在活資料預覽於 `inventory` 與 `inventory_pg` 之間換連線並確認查詢，then 瀏覽器與 Avalonia 的工具名稱和參數鍵不變、畫面沒有連線字串；連線失敗時她留在預覽頁，先前成功的列留到下一次成功確認才換。
- Given 範本編輯師周寧具有規劃設計師與目標群組的設計角色，when 她接受 `fields` 建議、重排、加上標題文字參數與計數後重新打開，then 畫面上沒有業務列；含未知欄名的建議不能接受。
- Given 終端使用者陳安沒有規劃設計師、只有檢視角色，when 她用語句叫出看得到的月報，then 左軌沒有活資料預覽；確認前填數次數是 0；確認後才進入預覽並可匯出與列印；常用清單再開用的是範本目前版面。
- Given 陳安沒有薪資群組角色，when 她打開範本庫或用該範本識別要求預覽，then 清單沒有該範本，直接預覽被拒絕且填數次數是 0；她也看不到周寧的常用項目。
- Given 管理者已登入，when 他建立只有名稱與秘密的帳號並在角色指派勾選工作角色與群組角色，then 他的左軌是全貌；該操作者重新登入後只出現被勾選的工作段；非管理者打開角色指派被拒絕。
- Given 兩殼繪製同一表面，when 使用共用權杖，then 只有淺色、沒有成功綠；焦點順序等於閱讀順序；上移與下移都存在；確認鈕停用時除了顏色還有 body 字級的原因；Esc 只關閉最上層對話框且不確認。

## Spec Change Log

### 2026-10-13 — bad_spec loopback 1
- 觸發：Blazor 沒有 `InteractiveServer`，活資料預覽、角色指派、交談確認、常用開啟與 Avalonia 的查詢計數都沒有在點擊時呼叫用例；測試只檢查靜態 HTML 與 `/api`。
- 修正：Tasks 與 Design Notes 寫明互動轉譯、工作階段在線路中的讀法、每個按鈕要呼叫的用例、Avalonia 必須打同一組 `/api`、稽核讀取結果要留下、空欄名不得全命中、列印要編碼、PDF 可跨頁、側車要讀 stderr。
- 避免：再交一個只有預渲染字串、按鈕按了沒有作用的殼。
- KEEP：Core 端口與 `Workbench/Program.cs` 的建構子注入；`/api` 控制器與矩陣測試；`GET /connections` 只回名稱與 `serverType`；SQLite 不存連線字串、參數值與列；命令列剔除 `apiKey`；Web 信封丟棄 `ConnectString`；真實 MCP JSONL 對照測試；側車只參考 Contracts 與指定的 AI 套件；`DesignTokens` 淺色常數。
- 原始碼必須是普通 C#。禁止 `goto IL_`、`if (1 == 0)`，以及 `_003C_003Ez__ReadOnlySingleElementList`。單元素序列用陣列。互動線路裡，工作階段只在 `HttpContext` 有值時寫入，已有值不得被覆寫成 null。

### 2026-10-13 — bad_spec loopback 2
- 觸發：`FillExecutor`、`Canonical` 與 `LivePreviewService` 含反編譯形狀（`goto IL_0654`、`if (1 == 0)`、`_003C_003Ez__ReadOnlySingleElementList`）。互動布局在 `HttpContext` 為 null 時把工作階段寫成 null。
- 修正：Design Notes 禁止反編譯形狀，並寫明工作階段不得被事後覆寫成 null。
- 避免：再交一份測試能過、但執行期會 `InvalidCastException` 或點擊後變成未登入的程式。
- KEEP：上一則 KEEP 全部保留。Blazor 必須 `InteractiveServer`。每個按鈕在點擊時呼叫用例。Avalonia 呼叫同一組 `/api`。空欄名不得全命中。列印要編碼。PDF 可跨頁。

## Review Triage Log

### 2026-10-13 — Review pass
- verdicts: 55 findings — high 10, medium 23, low 2, false 0, maybe-false 20
- findings:
  - `[high]` `[bad_spec]` Blazor 沒有互動轉譯，按鈕在瀏覽器不執行 — `Routes.razor` 沒有 rendermode；本輪已把互動轉譯寫進 Tasks。
  - `[high]` `[bad_spec]` 活資料預覽不載入資料表、永遠用第一筆連線、不畫列 — `LivePreview.razor` 的 `_loadedTables` 沒有被設成 true，`Run` 使用 `First()`。
  - `[high]` `[bad_spec]` Avalonia 查詢與計數沒有送出，多個表面把主區清空 — 桌面殼沒有接上對應的 `/api`。
  - `[high]` `[bad_spec]` 角色指派、編輯器、模型儲存、加入關鍵字與候選清單沒有接到用例 — `Assignments.razor` 的儲存只打開對話框。
  - `[medium]` `[patch]` 列印把儲存格用 CSV 跳脫塞進 HTML — 本輪因 bad_spec 不直接修；Design Notes 已要求編碼。
  - `[medium]` `[bad_spec]` 成功填數讀了稽核卻丟棄結果 — `DataServices.cs` 的 `Remember` 以 `_ = audit.ReadAll()` 丟掉回傳值。
  - `[medium]` `[bad_spec]` 側車不讀 stderr，且解析失敗仍可能回 `model_ok` — Design Notes 已要求讀 stderr，且沒有 JSON 物件時不得交出候選。
  - `[medium]` `[bad_spec]` 模型儲存缺少 cloud／local 檢查，側車路徑預設是空的 — 重新衍生時一併接上既有驗證與路徑。
  - `[medium]` `[bad_spec]` 空白說法與空欄名會命中全部範本 — `IntentServices.cs` 對空的 `ColumnNames` 使用 `All`，空集合為真。
  - `[medium]` `[bad_spec]` 語句確認沒有核對欄位是否仍在，PDF 單頁放不下會失敗，連線目錄失敗被顯示成沒有連線 — 重新衍生時依矩陣的錯誤與換頁規則。
  - `[maybe-false]` `[defer]` 密碼驗證字串壞掉時登入丟出例外 — 若驗證器對非法 verifier 沒有接住 `FormatException` 即成立；本輪未逐行執行該路徑。
  - `[maybe-false]` `[defer]` 稽核 JSONL 壞行會讓成功填數在存快照前丟出 — 需看 `ReadAll` 是否接住 `JsonException`。
  - `[maybe-false]` `[defer]` Web 主機回非 JSON 時瀏覽丟出而不是連線錯誤 — 需看 `WebSurface` 是否接住 `HttpRequestException` 與 `JsonException`。
  - `[maybe-false]` `[defer]` 連線目錄請求失敗時丟出而不是空清單或錯誤 — 需看 `McpConnectionCatalog` 的例外路徑。
  - `[maybe-false]` `[defer]` Web 資料表名稱空白時打到網站根路徑 — 需看空表名是否在送出前被拒絕。
  - `[maybe-false]` `[defer]` 側車標準輸入或輸出丟出時探測沒有變成 `model_unreachable` — 需看 `SidecarLauncher` 的 catch。
  - `[maybe-false]` `[defer]` 側車收到未知 kind 仍呼叫模型 — 需看 `Program.cs` 對 kind 的分支。
  - `[maybe-false]` `[defer]` 高於一頁的區塊讓 PDF 丟出 — 需看 `ShowEntire` 是否仍在；Design Notes 已禁止這個失敗方式。
  - `[maybe-false]` `[defer]` 列數少於欄數時 PDF 丟出 — 需看儲存格建立是否假設等長。
  - `[medium]` `[patch]` 列印文字含逗號或標記時版面壞掉 — 與列印編碼同一根因，留待重新衍生。
  - `[maybe-false]` `[defer]` 顯示名稱重複時建立帳號丟出唯一約束 — 需看建立前是否比對名稱。
  - `[maybe-false]` `[defer]` 群組名稱空白或重複時丟出或寫入空名 — 需看重新命名是否拒絕空白。
  - `[maybe-false]` `[defer]` 刪除群組後指派列仍在 — 需看刪除是否同一交易清掉指派。
  - `[maybe-false]` `[defer]` 範本名稱空白或補空白後重複會碰到 UNIQUE — 需看儲存前是否 Trim 並拒絕空白。
  - `[maybe-false]` `[defer]` 常用項目名稱空白會寫入或丟出 — 需看 `Save` 是否拒絕空白名稱。
  - `[maybe-false]` `[defer]` 確認詞空白仍寫入 AI 規則 — 需看 `AddFromConfirm` 是否拒絕空白說法；Design Notes 已禁止。
  - `[medium]` `[bad_spec]` 空欄名的 AI 規則命中每個可見範本 — 與 `ColumnNames.All` 同一行，已在 Design Notes 禁止。
  - `[maybe-false]` `[defer]` 參數欄位是 null 時組呼叫清單丟出 — 需看 `ColumnsMatch` 是否先拒絕空欄位。
  - `[maybe-false]` `[defer]` 錯誤訊息裡的 `Server=` 沒有被裁掉 — 需看 `Scrub` 是否只裁連線字串標記的一部分。
  - `[maybe-false]` `[defer]` 指派列順序不同就讓確認變成 `plan_mismatch` — 需看雜湊前是否排序。
  - `[maybe-false]` `[defer]` 模型檔寫入失敗時記憶體與磁碟不一致 — 需看是否先寫檔再換記憶體快照。
  - `[maybe-false]` `[defer]` 表面基底網址不是絕對 URI 時宿主啟動丟出 — 需看 `Uri` 建構是否有 `TryCreate`。
  - `[high]` `[bad_spec]` 瀏覽器與 Avalonia 都不能改選連線後確認 — 活資料預覽固定第一筆，桌面查詢鈕沒有送出。
  - `[high]` `[bad_spec]` 交談確認沒有進入預覽，也因此不能匯出 — `Intent` 頁沒有把確認結果打開成預覽。
  - `[high]` `[bad_spec]` 常用清單頁不能再開目前版面 — 頁面只列出名稱。
  - `[high]` `[bad_spec]` 角色指派畫面沒有把勾選寫進去 — `AskAssign` 只呼叫 `Show`。
  - `[medium]` `[bad_spec]` 稽核對照沒有用讀到的列 — 與 `Remember` 丟棄回傳值同一處。
  - `[medium]` `[patch]` PDF 測試只檢查檔頭，空白 PDF 也會過 — 預先驗證的斷言缺口；重新衍生時測試要看到欄名與列值。
  - `[medium]` `[patch]` 建議測試沒有斷言欄名等於該次 fields — 預先驗證；重新衍生時要斷言並接受那組欄位。
  - `[medium]` `[patch]` 模型回傳目錄內候選時沒有測試會變成 ready — 預先驗證；補上集合內的解析斷言。
  - `[medium]` `[patch]` 目錄外名稱夾帶目錄內連線時，作廢沒有被釘住 — 預先驗證；混合名單不得變成 ready。
  - `[medium]` `[patch]` 確認後的 AI 詞沒有再拿去解析 — 預先驗證；建立後要用該詞再解析一次。
  - `[medium]` `[patch]` 工作台沒有對真實 `/connections` 做解析斷言 — 預先驗證；沿用真實 McpHost 斷言名稱與 serverType。
  - `[medium]` `[patch]` 真實 MCP 查詢的列值沒有進工作台斷言 — 預先驗證；回應列要是 `one`。
  - `[medium]` `[patch]` Web 的 meta 與 fieldSet 解析沒有被呼叫 — 預先驗證；要斷言欄名、主鍵與不可為 null，且不含 ConnectString。
  - `[medium]` `[patch]` 常用項目對不齊沒有斷言 `alignment_failed` — 預先驗證；拿掉參數後再開啟。
  - `[medium]` `[patch]` 多呼叫計畫的第二次失敗沒有被釘住 — 預先驗證；失敗時不得把這次的列存成新快照。
  - `[medium]` `[patch]` 空白金鑰的儲存是否保留舊值沒有測試 — 預先驗證；PUT 空白金鑰後 `apiKeySet` 仍為 true。
  - `[medium]` `[patch]` `MapParse` 沒有被固定文字執行過 — 預先驗證；用純函數斷言，不呼叫真實模型。
  - `[low]` `[patch]` 原生 SQLite 3.53.3 沒有被測試觀察 — 釘選已在 `Directory.Build.props`；重新衍生時保留這條釘選。
  - `[low]` `[defer]` Escape 只測了 `DialogStack`，沒有送進編輯器或 Avalonia 視窗 — 單元行為已測；不為這一輪另起 UI 測試框架。
  - `[medium]` `[bad_spec]` 空欄名讓 `All` 為真 — 與上面的空欄名規則同一根因。
  - `[medium]` `[bad_spec]` 測試停在 HTTP 與靜態字串，沒有走操作者點擊的表面 — 規格的測試任務允許這樣過關，Tasks 已改成點擊必須呼叫用例。
  - `[high]` `[bad_spec]` Avalonia 沒有把查詢與計數送出 — 與桌面殼任務同一缺口。
  - `[high]` `[bad_spec]` Avalonia 的常用、帳號與模型把主區清空 — 這些表面必須呼叫對應的 `/api`。

### 2026-10-13 — Review pass 2
- verdicts: 55 findings — high 4, medium 28, low 8, false 0, maybe-false 15
- findings:
  - `[high]` `[bad_spec]` MCP 欄位用 `ToString()` 連引號讀進名稱，找不到欄時變成 `connection_failed` — `McpSurface` 的 `ReadFields` 與 `Index`。本輪改規格禁止壞掉的解析，並要求重新衍生。
  - `[medium]` `[bad_spec]` `ReadCount` 把整段 JSON 文字解析成 decimal，物件會變成 0 — 與計數結果無法分辨。
  - `[medium]` `[bad_spec]` 字串參數與稽核篩選因原始 JSON 文字對不上，`AuditMatched` 會是 false — `FiltersMatch` 與 `MapParse`。
  - `[medium]` `[defer]` `GET /connections` 沒有驗證，失敗時回空清單 — 規格明文說它不是登入端點；空清單與失敗混在一起留待後續。
  - `[low]` `[reject]` 工作階段沒有閒置到期 — 規格寫明快照沒有閒置逾時，工作階段在重啟時清空，這是預期行為。
  - `[medium]` `[patch]` 控制器忽略 antiforgery，重複名稱變成未處理的 SQLite 例外 — 留待重新衍生時先拒絕空白與重複名稱。
  - `[medium]` `[bad_spec]` 刪除群組不刪指派，管理者沒有設計角色就不能改範本 — 後者符合規格「管理者沒有設計角色時可以看見但不能改」；刪指派是缺口。
  - `[medium]` `[bad_spec]` 語句確認前不做欄位核對，建議把所有表的欄名併成一個集合 — 重新衍生時建議只核對該次資料表。
  - `[medium]` `[patch]` 模型儲存把金鑰明文寫進 `model.json`，且 `Merge` 沒被 `Load` 使用 — `model.json` 是規格允許的金鑰位置；`Merge` 未使用是死碼。
  - `[medium]` `[bad_spec]` 側車逾時後 stdout 讀取留在背景，逾時碼可能變成 `model_unreachable` — 重新衍生時逾時必須回 `model_timeout` 並結束子程序。
  - `[high]` `[bad_spec]` Core 含反編譯產物：`goto IL_0654`、`if (1 == 0)`、`_003C_003Ez__ReadOnlySingleElementList` — 已在 `FillExecutor.cs`、`Canonical.cs`、`LivePreviewService.cs`、`ReadOnlySingleElementList.cs` 看到。這是本輪重做的根因。
  - `[medium]` `[defer]` 軌跡只增不減，PBKDF2 迭代次數沒有上限 — 規格沒有要求軌跡淘汰或迭代上限。
  - `[low]` `[patch]` CSV 沒有處理 CR 與公式開頭 — 直接跳脫即可，本輪因 bad_spec 不單獨修。
  - `[low]` `[reject]` 關鍵字區分大小寫、數字強制轉換不含負數 — 規格寫的是其後第一個詞、全數字才作數字，這是選定的讀法。
  - `[high]` `[bad_spec]` 互動電路初始化時 `HttpContext` 為 null，工作階段被寫成 null — `WorkbenchLayout.cs` 的 `SessionId` 在沒有請求時回傳 null，`OnInitializedAsync` 仍寫入。
  - `[maybe-false]` `[defer]` Web 本文不是 JSON 陣列時結構查詢拋出 — 需看 `WebSurface` 是否接住 `JsonException`。
  - `[maybe-false]` `[defer]` `FieldDefine` 不是陣列時拋出 — 需看讀取前是否檢查 `ValueKind`。
  - `[maybe-false]` `[defer]` 連線目錄的成功回應不是陣列時拋出 — 需看 `McpConnectionCatalog`。
  - `[maybe-false]` `[defer]` 稽核欄位型別不符時成功查詢被中斷 — 需看 `JsonlAuditReader` 是否逐行接住例外。
  - `[maybe-false]` `[defer]` 秘密是 null 或迭代小於 1 時登入拋出 — 需看 `Verify` 的 catch 是否含 `ArgumentOutOfRangeException`。
  - `[maybe-false]` `[defer]` stderr 不關閉時側車一直等待 — 需看逾時是否同時涵蓋 stderr。
  - `[low]` `[reject]` 表面名稱不是小寫 `web` 時走 MCP — 契約的表面值就是 `mcp` 與 `web`。
  - `[maybe-false]` `[defer]` 顯示名稱只在 Trim 前查重，尾端空白會撞上唯一約束 — 需看建立路徑是否先 Trim。
  - `[maybe-false]` `[defer]` 範本名稱 null 或未 Trim 就查重 — 需看 `TemplateLibraryService`。
  - `[maybe-false]` `[defer]` 群組名稱重複拋出 `SqliteException` — 需看是否接住唯一約束。
  - `[maybe-false]` `[defer]` 重新命名空白群組會寫入空名 — 需看是否拒絕空白。
  - `[low]` `[reject]` 兩個進程同時種子管理者會讓第二個崩潰 — 規格要求恰好一個 `Workbench` 進程。
  - `[medium]` `[patch]` 沒有終端角色的人仍可能寫入 AI 規則或開啟常用項目 — 重新衍生時這兩個動作都要檢查終端角色或管理者。
  - `[maybe-false]` `[defer]` 常用名稱是 null 時拋出 — 需看儲存前是否拒絕空白。
  - `[medium]` `[patch]` 同一份清單可以再次確認並再填一次 — 規格說下一次確認會刪舊快照；再次確認本身是一次新的同意，除非清單已被用過。留待重新衍生時用過的清單識別不得再執行。
  - `[medium]` `[bad_spec]` 沒有篩選或稽核多了鍵仍標成相符 — `FillExecutor` 的比對不是全等。
  - `[low]` `[patch]` CSV 只含 CR 的儲存格會拆列 — 與 CSV 跳脫同一處。
  - `[maybe-false]` `[defer]` 壞掉的元件 JSON 讓整份清單失敗 — 需看儲存讀取是否逐筆接住 `JsonException`。
  - `[maybe-false]` `[defer]` 語句是 null 時解析拋出 — 需看 `Split` 前是否把 null 當空字串。
  - `[medium]` `[bad_spec]` 編輯器每個區塊共用同一組渲染序號，上移下移可能打到錯誤區塊 — `Editor.cs` 的序列號沒有乘上區塊索引。
  - `[medium]` `[patch]` 群組角色沒有清除選項 — 規格要求可以清掉指派。重新衍生時要有清空。
  - `[low]` `[patch]` Avalonia 登入失敗沒有訊息 — 直接顯示錯誤本文即可。
  - `[high]` `[bad_spec]` Avalonia 儲存角色時群組清單是空的，會清掉既有指派 — `MainWindow.cs` 送出的 `Groups` 是空集合。
  - `[maybe-false]` `[defer]` Avalonia 的 appsettings 不是合法 JSON 時視窗無法開啟 — 需看 `ReadBaseUrl` 是否接住 `JsonException`。
  - `[high]` `[bad_spec]` carried 瀏覽器點擊變成未登入 — 與 `SessionId()` 回傳 null 同一處，上一輪要求的「不得覆寫成 null」仍未做到。
  - `[medium]` `[bad_spec]` Avalonia 沒有「加入關鍵字」，也不呼叫 `/api/keyword-rules` — 本輪 Tasks 已要求這個按鈕。
  - `[medium]` `[bad_spec]` carried 稽核篩選不是全等比對 — 與 `FiltersMatch` 同一處。
  - `[medium]` `[bad_spec]` 刪除會刪整份範本，畫布區塊沒有單獨移除 — Tasks 要求刪除元件後保存結果不再含該元件。
  - `[low]` `[reject]` 端口介面不在 `Ports\` 目錄 — 介面仍在 Core，組裝仍只依賴抽象，目錄名稱不改變行為。
  - `[low]` `[reject]` 稽核讀取檔名不是 `McpAuditReader.cs` — 行為在 `JsonlAuditReader`，規格要的是讀取與核對，不是檔名。
  - `[medium]` `[patch]` carried 模型解析測試沒有釘住重算與作廢 — 與第一輪同一斷言缺口，程式仍是只斷言不是 ready；本輪不再改這條測試。
  - `[medium]` `[patch]` carried 真實 MCP 查詢列沒有進工作台斷言 — 與第一輪同一缺口，不再改。
  - `[medium]` `[patch]` carried 工作台沒有執行 `McpConnectionCatalog` — 與第一輪同一缺口，不再改。
  - `[medium]` `[patch]` carried PDF 只檢查檔頭 — 與第一輪同一缺口，不再改。
  - `[medium]` `[patch]` carried 確認後的 AI 詞沒有再被解析 — 與第一輪同一缺口，不再改。
  - `[medium]` `[bad_spec]` carried 測試仍停在 HTTP，沒有走兩個殼的點擊 — 第一輪已改 Tasks，第二輪程式仍用 HTTP 測試證明；不再為了補瀏覽器測試而重寫規格。
  - `[low]` `[reject]` 密碼雜湊不是 ASP.NET `PasswordHasher` — 意圖契約要求的是驗證字串不是明文，Core 不得參考 ASP.NET；目前的端口實作滿足這兩條。
  - `[low]` `[reject]` 活資料預覽首屏同時有查詢與計數 — 兩顆都是確認鈕，停用時有原因，規格允許多個動作裡只有即將執行的那顆使用確認色。

## Design Notes

組裝只在宿主。用例依賴端口，殼依賴用例：

```csharp
builder.Services.AddSingleton<IOperatorDirectory, SqliteOperatorDirectory>();
builder.Services.AddSingleton<IDataSurface, RoutingDataSurface>();
builder.Services.AddSingleton<LivePreviewService>();
builder.Services.AddSingleton<IPreviewMemory, MemoryPreviewStore>();
```

`RoutingDataSurface` 依這次的 `mcp` 或 `web` 轉給對應配接器，本身不含 SQL。Blazor 頁面與控制器都呼叫 `LivePreviewService`，因此兩殼不會各寫一套確認規則。

元件契約：`type` 為 `title`、`text`、`parameter`、`table`、`count`；`order` 從 0 連續。參數只有 `field` 與 `label`。資料表有一個表名與至少一個欄名。計數只有表名。標題與文字只有字串。

工作台自己的拒絕碼使用蛇形英文，訊息繁體中文，至少包含：`workbench_unavailable`、`unauthenticated`、`forbidden`、`single_key_only`、`suggestion_rejected`、`intent_not_ready`、`plan_mismatch`、`snapshot_gone`、`alignment_failed`，以及模型四個結果碼。MCP 的 `unknown_table`、`connection_failed`、`invalid_arguments`、`execution_failed` 不改名。

語句的 `ready` 只由宿主重算：恰好一個連線、恰好一個範本、每個參數都有純量值。側車自報的狀態不能讓確認鈕變成可按。欄名陣列是空的 AI 規則不得命中任何範本。空白說法不得寫入。列印 HTML 要編碼文字，不能沿用 CSV 跳脫。PDF 超過一頁時在元件邊界換頁，不能因為單頁放不下而丟出例外。`MapParse` 在沒有 JSON 物件時不得回報已解析出候選。側車要讀取 stderr，避免緩衝寫滿後停住。

## Verification

**Commands:**
- `dotnet test E:\sjvann\DBServices\Workbench.Tests\Workbench.Tests.csproj` -- expected: 矩陣每一列與六條驗收都有對應測試且通過，輸出不含金鑰與連線字串
- `dotnet test E:\sjvann\DBServices\McpService.Tests\McpService.Tests.csproj` -- expected: 新的 `/connections` 測試與既有六工具測試通過
