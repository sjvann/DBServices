# Good-spine 審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices 檢視與報表，feature，2026-10-12，updated 2026-10-20）
- 高度：feature（其下是史詩；父脊是 initiative `architecture-DBServices-2026-10-11`）
- 審查日：2026-10-20
- 鏡頭：good-spine 清單
- 判定：**revise**
- 未改脊

這份脊把六角形邊界、單一 `Workbench` 進程、`GET /connections`、一次已發布檢視呼叫，以及父脊的連線字串與稽核邊界寫成可執行規則，能力表也把 FR-1 至 FR-16 都掛上 AD 或 Deferred。還不能往下拆史詩：AD-15 要求被樣式 `viewId` 引用的檢視不能刪除或取消發布，執行這條所需的引用卻在另一個進程的 SQLite，而那份檔只有工作台能開。轉置機制、版面建議的輸入，以及目錄 HTTP 的線格式，也仍會讓兩個史詩各做一套。

## 清單對照

| 清單項 | 結果 |
| --- | --- |
| 擋下層真實分歧、沒有漏 | 未過。進程邊界、儲存欄位、工作階段、語句狀態、區塊種類與「不渲染連線清單」有擋住。被引用檢視的凍結由誰執行、目錄寫入的線格式、FR-9 的建議輸入、已登入但沒有該入口時的拒絕、兩種共享對象、多於一筆連線時的錯誤，都還能各做一套。 |
| 每條 AD 的 Rule 可執行，且真能阻止其 Prevents | 未過。AD-1 至 AD-7、AD-9 至 AD-14 的 Rule 對得上各自的 Prevents，而且 AD-1 的編譯邊界與專案參考一致。AD-15 的引用凍結沒有放得進去的執行者。AD-8 綁了 FR-9，輸入句卻只接受語句與樣式摘要，建議那一半無法照句執行。 |
| Deferred 不會讓兩個單位分叉 | 未過。規劃期通道、詞庫介面、側車、稽核天數、呼叫者身分證明與排除項都有「現在不得做什麼」。轉置的即時讀取或先落地沒有鎖住 `McpService` 側，兩個單位可以各做一種讀列。PDF 套件未定，與已經釘死的 QuestPDF 並存。 |
| 點名的技術是現行版 | 過。抽查 2026-10-20 的專案檔：`Workbench.Avalonia` 的 Avalonia 與 Avalonia.Desktop 12.1.4、`Workbench.Adapters` 的 ModelContextProtocol 2.2.0、`McpService` 的 ModelContextProtocol.AspNetCore 2.2.0、`Workbench.Storage.Sqlite` 的 Microsoft.Data.Sqlite 10.0.12、`DbServices.ModelSidecar` 的 Microsoft.Extensions.AI 10.10.0、Abstractions 10.10.1、OpenAI 10.10.1。目標框架為 `net10.0`。共用框架 10.0.12 不是 PackageReference，與同庫的 OpenApi、Mvc.Testing 10.0.12 同一帶。 |
| 追認棕地，而不是反過來改寫 | 部分過。`Workbench` 參考 `Workbench.Web`、`Workbench.Adapters`、`Workbench.Storage.Sqlite`；`Workbench.Web` 只參考 Core 與 Contracts；`Workbench.Avalonia` 只參考 Contracts；Adapters 與 Storage 只參考 Core；Core 只參考 Contracts。`McpService` 的 `GET /connections` 就是 JSON 陣列、只含 `Available` 的 `name` 與 `serverType`。`DbServices.Mcp` 仍是 `ToolCatalog` 與 `ConnectionSnapshot` 的家，工作台不參考它。AD-6 改指 ASP.NET Core `PasswordHasher`，與 `Workbench.Adapters` 的 `Pbkdf2PasswordHasher`（`pbkdf2$100000$…`）不是同一份驗證材料。 |
| 規格驅動時蓋住該規格的能力 | 部分過。能力表有 FR-1 至 FR-16。FR-1、FR-2、FR-6、FR-8、FR-11、FR-12、FR-14、FR-16 的分歧有規則可守。FR-4 以帶禁令的 Deferred 蓋住。FR-5 的引用凍結、FR-7 的失敗、FR-9 的建議、FR-3 的無入口拒絕、FR-13 的多連線錯誤，沒有被對應的 AD 定死。 |
| 父脊的 AD 不被削弱 | 過。沒有新 AD 放寬父 AD。見文末。 |
| 這個高度擁有的每個面向都已決定、延後或列為開放問題 | 未過。部署拓撲、不新增雲端供應商、TLS 外移、單一進程與記憶體壽命已決定。環境差異，以及健康檢查、指標、日誌出口、SQLite 備份，沒有決定、Deferred 或開放問題。 |

## Critical

### C1. 被引用檢視的凍結沒有合法的執行者

AD-15 的 Rule 寫：「仍被樣式 `viewId` 引用時，不能刪除、不能取消發布，也不能改名稱、輸出欄或參數。」同條又寫：「`McpService` 不依賴工作台進程就能列出已發布檢視。」「工作台不開啟這份目錄的檔案，也不參考 `DbServices.Mcp`。」AD-2 的 Rule 寫：「只有它開啟 `Workbench:StorePath` 指向的 SQLite。」AD-5 把樣式範本放在這份檔。

引用名單因此只在工作台進程。目錄的刪除、取消發布與改欄是 `McpService` 上、`/mcp` 之外的 HTTP。目錄史詩若要自己執行 AD-15，就要打開 `StorePath`（抵觸 AD-2），或反向呼叫工作台（脊沒有這條依賴，也抵觸「不依賴工作台進程」的那一句）。工作台史詩若在呼叫前自己拒絕，直接打 `McpService` 的人仍能刪除，而 Deferred 只說目錄寫入拒絕空白的 `X-DBServices-Caller`，沒有把引用檢查放在那裡。兩個史詩可以各選一種接法，而且沒有一種同時滿足 AD-2 與 AD-15。AD-15 的 Prevents（兩份檢視目錄、或在 `McpService` 再做登入）並沒有因此被這段引用句擋住；這段引用句本身不可執行。

**處置：** discuss

## High

### H1. 環境與營運沒有決定、延後或開放問題

結構種子寫了部署：一個工作台進程，與 `McpService`、`WebService` 分開；不新增雲端供應商；TLS 由部署環境終止。AD-2 鎖住恰好一個進程、聽取位址來自宿主組態、`AllowedHosts` 出貨值為 `*`、記憶體物件不分攤到第二個實例。慣例表寫「動態報表記憶體沒有閒置逾時」。這些是部署拓撲。

環境（本機、測試、正式是否只差組態）、健康檢查、指標、稽核以外的日誌出口、以及存有業務列的工作台 SQLite 如何備份，沒有出現在 AD、Deferred 或開放問題。父脊有一列營運慣例，並把健康檢查與指標寫進 Deferred。這份 feature 脊擁有新的 `Workbench` 進程，卻把這一半留白。兩個史詩可以各加一條健康路由或各寫一份日誌管線。

**處置：** autofix

### H2. 轉置 Deferred 讓 `McpService` 側可以各做一種讀列

Deferred 寫：「轉置是 `McpService` 依檢視定義即時讀取，還是先落地。定案前工作台不得用 Web API 的列組成動態報表。」這句只鎖工作台。FR-7 仍在 `binds` 裡，能力表寫「機制未定」。

檢視執行史詩可以在公開工具裡即時讀待轉資料庫，另一個史詩可以先落地再讓同一工具讀落地表。兩者都符合「動態報表的列只來自一次已發布檢視呼叫」（AD-3）。父 AD-9 把空的 `rows` 當成功，繼承表寫「工作台不重算列、不把空列改成錯誤」。轉置尚未完成時，一個史詩會交出空列成功報表，另一個會新增錯誤碼。新增的碼會擠進父 AD-7 已關閉的 `code` 清單；脊自己沒有加碼，所以還沒削弱父 AD，但 Deferred 沒有禁止這條分叉。

**處置：** discuss

### H3. AD-8 綁了 FR-9，輸入句卻使建議無法照句執行

AD-8 的 Binds 含 FR-9。Rule 寫：「輸入只有語句，以及此人可讀樣式的 `id`、`name`、`viewId` 與參數 `field`、`label`。」後面又寫：「建議不含業務列與參數值，欄與參數都屬於所引用的已發布檢視。未知欄名不能接受。忽略不改區塊。接受才把區塊加到末尾。」

FR-9 是設計師選定一筆已發布檢視之後提出區塊，不是對語句做比對。附錄的建議最小形狀是 `viewId` 加一個 `table`，其 `columns` 等於該檢視的輸出欄。AD-8 沒有這條全集，AD-14 只說「`table` 的欄是該檢視的輸出欄」，沒有要求建議必須含那一個全集表格。設計史詩可以依檢視欄位產生建議，交談史詩可以堅持「輸入只有語句」而拒絕沒有語句的建議。AD-8 的 Prevents（模型與關鍵字各回一份意圖、或從語句補猜連線）對交談成立，對 FR-9 的建議不成立。

**處置：** autofix

### H4. 目錄 HTTP 的線格式沒有共同的家

AD-4 與 AD-15 都寫：「`Workbench` 與 `Workbench.Adapters` 不參考 `DbServices.Mcp`。」結構種子把 `Workbench.Contracts` 限成「樣式、意圖、動態報表」。AD-15 只說寫入、發布、取消發布與刪除是 `McpService` 上、`/mcp` 之外的 HTTP，拒絕空白的 `X-DBServices-Caller`。

Adapters 因此不能看見目錄型別，Contracts 也沒有被指定承載檢視文件。附錄已經有 `id`、`name`、`purpose`、`published`、`sources`、`links`、`parameters`、`columns`。脊沒有引用這些鍵，也沒有路徑與動詞。目錄史詩與模組設計史詩會各寫一份 JSON、各取一條路徑，還可能碰到必須維持現況的 `GET /connections`。這不是兩份檢視儲存（AD-15 的 Prevents 有擋住儲存），而是同一條 HTTP 的兩份契約。

**處置：** autofix

## Medium

### M1. 已登入但沒有該入口時，端口是否拒絕沒有寫進規則

AD-1 的 Prevents 含「兩個殼各寫一份入口判斷」，Rule 寫入口判斷只經過 `Workbench.Core`。AD-6 的 Rule 只約束未登入：「未登入的請求不回傳檢視、樣式、生成紀錄或業務列，也不呼叫 MCP。」FR-3 要的是直接指定另一站時拒絕，而且不建立檢視、不修改樣式、不產生動態報表。一個史詩只在頁面藏起入口，另一個在端口拒絕並把讀列次數留在 0。兩者都算「判斷經過 Core」或「頁面沒有自己判斷」。

**處置：** autofix

### M2. 兩種共享對象被收成一句「入口加共享範圍」

AD-5 的 Rule 寫：「私有紀錄只有擁有者可讀。管理員的產品介面不讀他人的私有生成紀錄。」慣例表寫：「入口加共享範圍。」FR-10 的共享樣式對報表設計師或終端使用者可讀；FR-14 的共享生成紀錄只對終端使用者可讀。附錄用 `visibility`，並把這兩個集合分開。樣式史詩與紀錄史詩可以讓 `shared` 對同一組入口開放，都宣稱符合 AD-5。私有樣式是否只有擁有者可讀，也只被「私有紀錄」四個字帶過，句子放在生成紀錄的定義後面。

**處置：** autofix

### M3. 零筆或多於一筆連線時，規則只要求不呼叫

AD-3 的 Rule 寫：「恰有一筆組態完整的具名連線時，適配器送出該 `connectionId`。零筆或多於一筆時不呼叫，讀列次數為 0。」FR-13 要求多於一筆時不產生動態報表，並顯示可讀錯誤。一個史詩顯示工作台自己的蛇形碼，另一個把這次留在 `incomplete` 或不顯示錯誤。讀列次數都是 0，AD-3 的 Prevents（用 Web API 組列，或人選擇通道）仍然成立，FR-13 的可觀察結果會分叉。

**處置：** autofix

### M4. AD-6 指定的 PasswordHasher 與現有驗證材料不是同一種

AD-6 的 Rule 寫：「以 ASP.NET Core 10.0.12 共用框架的 `PasswordHasher` 寫入恰好一個 `admin` 操作者。」「樣式」側的 AD-5 寫驗證材料在 `passwordVerifier`。棕地是 `Workbench.Core` 的 `IPasswordHasher` 端口，實作在 `Workbench.Adapters.Pbkdf2PasswordHasher`，格式為 `pbkdf2$100000$` 加鹽與雜湊，由 `Workbench` 宿主註冊。AD-1 又寫：「`Workbench.Core` 不參考 ASP.NET。」

登入史詩若把 Identity 的 `PasswordHasher` 放進 Core，就抵觸 AD-1；若放進宿主並換掉 PBKDF2，既有 `passwordVerifier` 會驗不過。沿用端口與 PBKDF2 才追認棕地，但那就不是 AD-6 點名的那個型別。Prevents（公開註冊、明文、cookie 與 bearer 各算一筆工作階段）兩種雜湊都擋得住；點名的型別本身讓兩個史詩分叉。

**處置：** discuss

### M5. PDF 套件寫成未定，專案檔已經引用 QuestPDF

Deferred 寫：「PDF 與 CSV 的具體套件。產生位置已由 AD-7 固定；套件選定時不得再查資料。」Stack 寫：「這一輪不新增套件。」`Workbench.Adapters` 已參考 QuestPDF 2026.9.1，`SnapshotPdfWriter` 已使用它。套件史詩可以把 QuestPDF 當成已追認，也可以因為 Deferred 說未定而再選一套；後者又抵觸「不新增套件」。產生位置的鎖是有效的，套件身分不是。

**處置：** autofix

### M6. 列印的換頁句只綁在 PDF 上

AD-7 的 Rule 寫：「匯出與列印只接受這塊記憶體或一筆已開啟的生成紀錄，且不發出新的 MCP 呼叫。PDF 預設 A4，換頁以區塊為界。」資料來源被鎖住了。Blazor 仍可以對目前 DOM 呼叫瀏覽器列印，Avalonia 列印宿主產出的 PDF。兩邊都沒有新的 MCP 呼叫，換頁結果可以不同。FR-16 的列印也要依區塊換頁。

**處置：** autofix

## Low

### L1. 檢視名稱的唯一與大小寫沒有寫進 AD-15

父 AD-2 的 Discovery 名稱比對不區分大小寫。AD-15 禁止被引用時改名稱，沒有寫未引用時名稱在目錄內不可重複，也沒有寫大小寫折疊後仍算同一筆。兩個目錄史詩可以讓 `Sales` 與 `sales` 同時發布，Discovery 再把它們收成一個工具名。

**處置：** autofix

### L2. 「離開這次報表」沒有邊界

AD-7 的 Rule 寫：「離開這次報表，或同一操作者產生下一份，這塊記憶體刪除。」同一操作者的下一份是可測的。離開可以是路由切換、關閉頁面，或明確的結束動作。殼史詩與宿主史詩會在不同時機刪掉那塊以操作者 `id` 為鍵的記憶體，匯出就會一個還拿得到、一個已經沒有。

**處置：** discuss

### L3. 工作台自己的錯誤沒有共同的 JSON 鍵

慣例表寫：「工作台自己的錯誤是穩定英文蛇形碼加繁體中文訊息。MCP 錯誤沿用宿主的 `code` 與 `message`。」MCP 那一半有鍵。工作台這一半沒有。Avalonia 與 Blazor 都消費宿主 HTTP 時，一個史詩用 `code`／`message`，另一個用別的鍵，兩者都符合「蛇形碼加繁體中文訊息」。

**處置：** autofix

## 不構成發現

- 父 AD 沒有被新 AD 放寬。AD-4 維持 `GET /connections` 的現況，不把 `ConnectionSnapshot` 載入工作台，也不新增 MCP 工具，連線字串仍只在 `McpService` 進程。AD-15 拒絕空白呼叫者只作用在 `/mcp` 之外的目錄路由，並寫明「MCP 工具呼叫仍依父脊椎 AD-6」。AD-3 在零筆或多於一筆時不呼叫，是客戶端少打一通；宿主的 `connection_required` 沒有被改寫。已採納的規則沒有新增父 AD-7 以外的 MCP `code`。AD-9 至 AD-11 這一輪不啟動側車，沒有把 `IChatClient` 放進 Core 或 MCP 宿主。
- 專案圖與 csproj 一致，AD-1 的「`Workbench.Web` 只參考 Core 與 Contracts」可以由編譯器執行。`DbServices.ModelSidecar` 是 net10.0 獨立執行檔，不參考 `DbServices.Core`、Provider 或 `DbServices.Mcp`。
- `GET /connections` 的實作只輸出組態完整連線的 `name` 與 `serverType`，不呼叫 `Create`。脊描述的就是這條路由，不是第二份目錄。
- 舊的確認清單、`api/structure`、`api/plans` 與群組角色仍在 `Workbench.Web`。PRD 第 0 節已覆寫它們。AD-3 與 AD-7 禁止再用那些路徑組動態報表，這是行為替換，不是專案邊界被改寫。
- FR-4 的通道在 Deferred，並禁止用待轉資料庫的 `GET /meta` 充當，也禁止順手讀業務列。這一半不會讓兩個史詩現在各接一條規劃期通道。
- 詞庫介面、側車啟動、稽核保留天數、呼叫者身分證明，以及資料表級授權、SSO、公開註冊、GraphQL、自由 SQL，Deferred 都寫了這一輪不做的事。AD-12 把日後詞庫規則禁止含參數值、資料列、連線名稱、連線字串或金鑰。
