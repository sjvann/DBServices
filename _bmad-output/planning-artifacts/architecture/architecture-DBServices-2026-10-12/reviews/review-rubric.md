# Good-spine 審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices 報表工作台，2026-10-12）
- 高度：feature（其下是史詩與故事；父脊是 initiative `architecture-DBServices-2026-10-11`）
- 審查日：2026-10-12
- 鏡頭：good-spine 清單
- 判定：**revise**
- 未改骨幹

這份脊把六角形邊界、單一 SQLite、填數轉發、模型側車與可擴充關鍵字寫成可執行規則，FR-1 至 FR-22 都有歸屬。它還不能往下拆故事：連線名稱的讀法會把父脊鎖在 `McpService` 裡的連線字串帶進第二個進程，而且這個功能高度擁有的部署與營運包絡沒有著落。

## 清單對照

| 清單項 | 結果 |
| --- | --- |
| 擋下層真實分歧、沒有漏 | 未過。儲存欄位、確認清單、快照、模型探測與側車邊界有擋住。連線中繼資料的進程、Avalonia 與宿主之間的 HTTP、管理員與設計角色的變更權、欄位與取數軌跡的契約、關鍵字兩層合併、常用項目再次開啟，都還能讓兩個故事各做一套。 |
| 每條 AD 的 Rule 可執行，且真能阻止其 Prevents | 未過。AD-3、AD-5、AD-7、AD-9、AD-10、AD-11、AD-12 的規則對得上各自的 Prevents。AD-4 禁了第七個工具與選項類別上的連線字串，卻仍允許工作台進程載入 `Connections`。AD-1 禁止殼參考 `DbServices.Mcp`，結構種子卻把 Blazor 放進會參考該庫的 `Workbench` 專案。AD-6 擋住公開註冊與雙工作階段，沒有擋住已登入的非管理員建立操作者。 |
| Deferred 不會讓兩個單位分叉 | 過。PDF／CSV 套件被 AD-7 固定在宿主快照上，選定套件時不得再查資料。稽核保留天數沿用父脊，工作台不另寫第二份。排除項與 PRD 非目標一致。沒有 OpenAI 相容 HTTP 的本地執行庫，以及改回 Python，都要求先改 AD-11，不能在殼或 Core 另引一套 SDK。 |
| 點名的技術是現行版 | 過。Stack 每一列都有版本號，沒有缺號，本審查依指示未再查網。memlog 記 2026-10-12 的 NuGet 穩定版：Avalonia 與 Avalonia.Desktop 12.1.4（2026-10-09）、Microsoft.Extensions.AI 10.10.0（2026-09-09）、Microsoft.Extensions.AI.Abstractions 10.10.1 與 Microsoft.Extensions.AI.OpenAI 10.10.1（2026-09-25）、Microsoft.Data.Sqlite 10.0.12（2026-09-08）。與棕地重疊的釘選一致：全庫 `net10.0`、`WebService` 的 `Microsoft.AspNetCore.OpenApi` 10.0.12、`McpService` 的 `ModelContextProtocol.AspNetCore` 2.2.0、`DbServices.Provider.Sqlite` 的 `Microsoft.Data.Sqlite` 10.0.12。 |
| 追認棕地，而不是反過來改寫 | 部分過。`net10.0`、Nullable、ImplicitUsings、`McpService` 的 `POST /mcp`、不引入 EF、WebService 既有 GET 形狀都追認正確。AD-4 另立 `Surfaces:Web:ServerType`，與 WebService 組態 `ServerType:Name` 成為第二個引擎標籤。`Workbench` 參考 `DbServices.Mcp` 會在第二個進程重建 `ToolCatalog`，而現行目錄只在 `McpService` 啟動時由 `Connections` 綁定。 |
| 規格驅動時蓋住該規格的能力 | 部分過。能力對照表涵蓋 FR-1 至 FR-22。FR-1、FR-4、FR-6 至 FR-8、FR-10 至 FR-14、FR-16、FR-18 至 FR-20、FR-22 的分歧有規則可守。FR-3 的 Web API `ServerType`、FR-2／FR-15／FR-17 的角色變更、FR-5 的欄位形狀、FR-9 的取數軌跡、FR-21 的再次確認沒有被對應的 AD 定死。 |
| 父脊的 AD 不被削弱 | 未過。工作台不改 `IDbService`、不加第七個 MCP 工具、不在 `McpService` 加登入、不重算列、不寫第二份稽核、模型不進 Core 與 MCP 宿主，這些都守住父 AD。AD-4 的進程內讀取削弱父 AD-4：`Connections` 只由 `DbServices.Mcp` 在 MCP 宿主綁定，連線字串只出現在該程序的組態。 |
| 這個高度擁有的每個面向都已決定、延後或列為開放問題 | 未過。範式、專案切分、儲存、確認與快照、模型設定、錯誤與介面語文已決定；套件級 PDF、稽核天數與排除項已延後。部署與環境、基礎設施策略、營運這一整面只有結構種子末段的幾句，沒有決定、延後或開放問題。 |

棕地對過且脊沒有改寫的部分：方案內專案目標框架都是 `net10.0`；全庫沒有 Entity Framework 套件參考，工作台儲存指定 `Microsoft.Data.Sqlite`，與 `DbServices.Provider.Sqlite` 同一套件帶。`McpService/Program.cs` 以 `MapMcp("/mcp")` 掛載，工具只從 `ToolCatalog.Definitions` 的六個名稱註冊。`WebService` 已有 `GET /meta`、`GET /{resourceName}/fieldSet`、`GET /{resourceName}` 與 `?Key=&Value=`，也已有 `valueSet`、`/{id}`、POST、PUT、DELETE；AD-3 的允許清單與這組既有路由對得上，而且明確不發後四類。`ConnectionSnapshot` 已有 `ServerType`、`ConnectionString`、`Available`；`Available` 的現行語意是已知 `ServerType` 且連線字串非空白，不呼叫 `Create`。

## Critical

### C1. 連線中繼資料的讀法把父脊的連線字串帶進工作台進程

父 AD-4 規定 `Connections` 只由 `DbServices.Mcp` 綁定，值是 `ServerType` 與 `ConnectionString`；父脊的部署句把連線字串留在 MCP 那一個程序。現行 `McpService/Program.cs` 也只在該宿主把 `Connections` 組成 `ToolCatalog`。子脊 AD-4 要 `DbServices.Mcp` 新增一個不含連線字串屬性的讀取方法，結構種子又讓 `Workbench` 參考 `DbServices.Mcp`，而工作台是與 `McpService` 分開的進程。這個讀取方法只有在呼叫端持有同一份 `Connections` 時才讀得到快照，於是一個故事會把含連線字串的目錄放進工作台組態，另一個故事會在 `McpService` 加端點或把讀取收成第七個工具。AD-4 的 Prevents 只禁止新工具與「選項類別」上的連線字串屬性，擋不住組態裡的第二份目錄。這直接削弱繼承表裡的 parent AD-4。

**處置：** discuss

## High

### H1. 部署、環境與營運整面沉默，而且和記憶體快照衝突

結構種子只寫了：工作台是與 `McpService`、`WebService` 分開的進程、側車是子程序、SQLite 與 `model.json` 在工作台那台機器、不新增雲端供應商、TLS 由部署環境終止。沒有環境（本機、測試、正式）的差異規則，沒有健康檢查、指標、日誌出口或備份落在決定、Deferred 或開放問題。AD-7 把預覽快照放在宿主記憶體，鍵含操作者與快照識別；兩個執行個體時，確認打到甲、匯出打到乙，舊快照拒絕與「匯出等於畫面」會同時失敗。父脊有一列營運慣例；這份 feature 脊把該維度整段留白。

**處置：** autofix

### H2. AD-1 禁止殼碰 MCP，Blazor 卻住在參考 `DbServices.Mcp` 的專案裡

AD-1 要阻止頁面直接呼叫 MCP 或 Web API，並寫明兩個殼都不參考 `DbServices.Mcp`、`DBService`、Provider 或 `Microsoft.Extensions.AI`。Avalonia 是獨立專案，這條對它成立。Blazor 畫在 `Workbench` 進程內，而種子規定 `Workbench` 參考 `DbServices.Mcp`。Razor 頁與組合根在同一個編譯單元，頁面可以直接新 `ToolCatalog` 或呼叫 MCP 用戶端。依賴圖同時畫了 `host --> mcpLib`、`host --> store`、`host --> sidecar`，沒有一條編譯器看得到的端口邊界把頁面擋在配接器外面。

**處置：** autofix

### H3. Web API 的 `ServerType` 改由工作台組態提供，FR-3 與棕地各有一份

PRD FR-3 與附錄寫明 Web API 那一筆的顯示名稱是 `default`，`ServerType` 來自 WebService 組態。棕地就是 `WebService/appsettings.json` 的 `ServerType:Name` 配 `ConnectionStrings:default`，`Program.cs` 用這兩個值啟動。AD-4 改為工作台的 `Surfaces:Web:ServerType`，並規定不修改 WebService 的路由與 JSON。兩個故事可以讓畫面上的引擎標籤與實際回答 `GET /meta` 的進程不一致，UJ-1 在 Web API 表面的「換引擎不換契約」無法被核對。memlog 記使用者接受了這條；它仍然和規格後果以及棕地的唯一 `ServerType` 來源衝突，不能當成已追認。

**處置：** discuss

### H4. Avalonia 只被允許呼叫工作台 HTTP，這條 HTTP 沒有契約

AD-1 與 AD-2 規定 Avalonia 不嵌入 Blazor、不開 SQLite、只呼叫 `Workbench` 的 HTTP，再轉進同一組端口。登入如何換成 bearer、登出如何使 AD-6 的同一筆工作階段失效、確認識別與快照識別走哪一條路徑、錯誤碼如何包進 HTTP，都沒有名字。桌面殼故事與宿主故事可以各自定義一組資源路徑與權杖，兩邊都自稱符合「HTTP 轉進端口」。

**處置：** discuss

## Medium

### M1. 管理員、設計角色與檢視角色的變更權沒有規則

AD-6 阻止公開註冊、明文密碼與兩套殼各做工作階段，FR-1 的未登入拒絕也寫進去了。FR-2 的「非管理員不能建立操作者或改角色」、FR-15 的群組重新命名與非空群組不可刪、設計角色才能改範本、FR-17 的直接指定無權範本必須零次填數，都沒有進 AD。AD-5 只規定資料形狀（一個人對一個群組至多一筆 `design` 或 `view`）。一個故事可以讓任何已登入操作者呼叫建立操作者的端口，另一個故事可以只在 Blazor 頁面藏起按鈕。

**處置：** autofix

### M2. 欄位中繼資料與取數軌跡沒有雙方殼共用的契約

FR-5 要求 `fields` 每一欄至少有名稱、型別、是否主鍵、是否不可為空；MCP 成功形狀依父 AD-9 是 `columns`／`rows`，Web API 的 `fieldSet` 是另一條既有 JSON。AD-3 要求結果原樣進入預覽、不做第二次彙總，沒有給 `Workbench.Contracts` 一個欄位埠。FR-9 的取數軌跡要能對上 MCP 稽核的呼叫者、連線、工具、篩選與列數，慣例表只有錯誤碼與 UTC，沒有軌跡欄位。Blazor 與 Avalonia，以及 MCP 與 Web 兩個配接器，可以各投影一套欄位與各顯示一套軌跡。

**處置：** autofix

### M3. 目錄比對與 AD-12 規則合併成一個意圖，衝突時沒有贏家

AD-8 的 Prevents 是不讓殼在模型意圖與關鍵字意圖之間挑，這一半做得到：未 `ready` 才呼叫側車，宿主重算狀態。比對來源卻有兩層，目錄名稱與仍有效的規則，規則只寫「合併成一個意圖」。一句話同時對上連線 A 與一筆綁連線 B 的 `phrase` 時，一個故事取聯集變成 `ambiguous`，另一個故事讓規則覆蓋目錄。附錄的「參數標籤後第一個詞當值、不補猜」也沒有被收成規則，匹配故事還可以各寫一組分詞。

**處置：** discuss

### M4. FR-21 再次叫用沒有綁回確認清單

能力表把 FR-21 只交給 AD-5。AD-5 定了常用項目的欄位：有 `surface`、連線名稱與參數值，沒有版面複本與資料列。AD-7 的 Binds 是 FR-19、FR-20、FR-22，不含 FR-21。再次開啟時應讀範本目前的版面、對齊參數、停在確認之後才填數；一個故事可以依儲存的參數直接送出 `query`，另一個故事可以走 AD-7 的清單識別。失去檢視角色或參數對不齊時的零次填數也只寫在 PRD，不在規則裡。

**處置：** autofix

### M5. 「是否可用」沒有綁到現行的組態判斷，列出連線就可以提前 `Create`

`ConnectionSnapshot.Available` 的現行語意是：`ServerType` 屬於五個已知名稱且連線字串非空白。父 AD-4 規定啟動只取快照，`Create` 發生在該鍵第一次被選中，失敗不快取。AD-4 要求讀取方法回傳「是否可用」，卻沒有引用這個旗標，也沒有禁止列出清單時開連線。一個故事會在列清單時呼叫 `Create` 探活，改變父脊的建立時機與快取；另一個故事只回傳組態完整與否。

**處置：** autofix

## Low

### L1. `tables` 省略 `includeView` 時的預設沒有寫進工作台規則

父 AD-3 規定省略 `includeView` 時為布林 true，FR-5 要求與 MCP 宿主相同。繼承表把 parent AD-3 收成篩選與連線字串兩句，AD-3 的工具清單沒有再提這個省略。一個 MCP 用戶端故事可以每次送 `false`，活資料預覽的資料表清單就會與宿主預設分叉。

**處置：** autofix

### L2. 登入工作階段的壽命，以及初始管理員密碼留在組態的方式，沒有著落

AD-6 規定 cookie 與 bearer 指向宿主上的同一筆工作階段，登出使其失效，也規定零個操作者時才讀 `Workbench:InitialAdmin:Password`。工作階段閒置多久、重啟後是否仍有效，沒有數字；快照的 30 分鐘不能拿來代替登入壽命。初始密碼寫入 `passwordVerifier` 之後，組態裡的明文鍵還能留在 appsettings、被日誌印出，或被當成第二次啟動的輸入。AD-9 替模型金鑰寫了覆寫順序與禁止出現的位置，初始密碼沒有同等句子。

**處置：** discuss

### L3. 側車自包含發布沒有執行環境識別

AD-11 規定 `DbServices.ModelSidecar` 發布為自包含執行檔，由 `Workbench` 啟動。沒有 RID，也沒有「與工作台宿主相同執行環境」的句子。一個發布故事可以產出 `win-x64`，另一個可以產出不帶執行環境的框架相依檔，宿主在目標機器上啟動不了子程序。

**處置：** autofix

### L4. 列印可以由殼對 DOM 呼叫列印，而不使用宿主產出的快照檔

AD-7 規定 PDF、CSV 與列印由宿主從快照產生，殼不另查資料，換頁以元件為界，PDF 預設 A4。數字來源被鎖住了。Blazor 仍可以對目前 DOM 呼叫瀏覽器列印，Avalonia 則列印宿主的 PDF，兩邊的換頁結果不同，而且都沒有新的填數呼叫，測 FR-22 的「沒有新填數」會放行。

**處置：** autofix

## 不構成發現

- 點名套件都有版本。AI 套件 10.10.0 與 Abstractions／OpenAI 10.10.1 的日期差已記在 memlog，不是漏釘。與棕地重疊的 `net10.0`、OpenApi 10.0.12、MCP 2.2.0、Sqlite 10.0.12 與專案檔一致。
- 工作台儲存使用 `Microsoft.Data.Sqlite`，沒有把 EF 引進現有的 Dapper／Provider 路徑，也沒有改 `IDbService` 簽名。
- 父 AD-2 的六個廣告工具維持關閉寫入與 `sql`。`McpService` 只依 `ToolCatalog.Definitions` 註冊工具，新的讀取方法不會自動變成第七個工具；真正的分叉在 C1 的進程與組態，不在反射註冊。
- 父 AD-6 的 `anonymous` 仍可呼叫 MCP 宿主。工作台改為送操作者 `id`、不在 `McpService` 加登入，這是收緊客戶端，沒有削弱宿主。
- 父 AD-11 的模型邊界被收成 C# 側車：`IChatClient` 只在 `DbServices.ModelSidecar`，金鑰不進引數與解析 JSON，失敗不改範本、不填數。Deferred 要求改回 Python 時先改父附錄與 AD-11，現在的故事不能各選一套側車。
- GraphQL 不在填數路徑，與 PRD 及「不改 GraphQL 路徑」一致。
- Deferred 的 PDF／CSV 套件、稽核保留天數、資料表級授權、SSO、公開註冊、自由 SQL、區間與跨表，都已把本版行為鎖住，兩個故事不能靠這些項目分叉。
