# Epic 1 Context: 方晴把來源註記成可確認的 DBML

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

方晴把 Excel 字典或二代平台結構收成同一份草稿，補上整包說明、表註、欄註與關聯，匯出並確認成 DBML。管理員用既有入口指派交出資料治理；沒有入口的人進不了註記站。註記站不讀業務列，確認過的實體名對得上二代平台，來源庫不被改寫。確認後的包已保存並成為唯一啟用包。模組設計工具目錄與 MCP 引用面還沒改。

## Stories

- Story 1.1: 指派資料治理入口並打開註記站
- Story 1.2: 匯入 Excel 產生草稿
- Story 1.3: 從二代平台讀出結構草稿
- Story 1.4: 編輯四層註記並保存重開
- Story 1.5: 匯出 DBML 並確認成唯一啟用包
- Story 1.6: 再讀來源、看差異、放棄草稿

## Requirements & Constraints

兩種來源進同一套可含多張表的註記包。四層是整包功能說明、表註、欄註、關聯。欄註與關聯理由可空白。個資去識別化只存文字，不執行遮罩。唯一索引勾記只保留語意，人在說明裡寫它不是真正的唯一鍵，畫面上不改勾記。重開後四層仍在，空白欄註仍空白。

Excel 只接受 `Requirements/Samples.xlsx` 的 H/S 同表契約。對不上就拒絕，既有草稿不變，也不要求連線。「00.資料總表」與第二種版型不做。匯出的表名、欄名、型別、索引與 note／Note 須對上 `Requirements/LH_TXN_MLST.sample.dbml`。匯出不要求已對上活庫或已確認。

確認要求說明非空白，且至少一張表、一個欄；每個實體表名與欄名都在二代平台。其後若已保存檢視的來源表、輸出欄或關聯端點會落到新包外，拒絕並列出檢視名稱；舊包與檢視不取消發布、不改寫。通過後這一份是唯一啟用包，舊包退出啟用但保留。未確認草稿不進設計目錄。

再讀只改目前草稿。比對鍵是實體表名加實體欄名，去首尾空白後區分大小寫。同鍵人寫註記保留；草稿沒有、上一份已確認包有的同鍵註記抄進來。改名是刪除再新增，註記不搬家。另列可見新增、刪除、型別、可否空白、主鍵、外鍵、唯一索引勾記。未再確認前設計目錄仍是舊包。放棄草稿不改已確認包。

失敗時已確認包與已保存檢視不變。介面與註記站錯誤用繁體中文，含穩定代碼，不含連線字串、密碼與堆疊秘密。業務列讀取次數為 0。不要做 ETL、遮罩、自由 SQL、選連線、深色模式、Avalonia 資料治理入口、合併左軌，或用 AI 由註解建立檢視。

## Technical Decisions

`Workbench.Core` 擁有入口判斷與 Excel 形狀，不參考 `DbServices.Mcp`、ASP.NET 或 Provider。`Workbench.Web` 只參考 Core 與 Contracts。`Workbench.Adapters` 只經 HTTP 呼叫 `McpService`。包檔、合併、DBML、確認對帳與啟用包讀取在 `DbServices.Mcp`。不新增套件、不升版。註記包不是工作台 SQLite 表。不修改 `IDbService`、Core、Provider、既有 REST 與 GraphQL。資料平面工具錯誤清單不改寫，也不拿來表示匯入或確認。

檔在 `McpService` 內容根，鍵 `Catalog:AnnotationsPath`，預設 `catalog/annotations.json`，只由一個目錄型別寫入。鍵為 `id`、`title`、`purpose`、`status`（`draft`｜`confirmed`）、`active`、`tables`、`links`、`dbml`。工作中草稿一份，`active` 為 false。已確認且啟用同時一份。從啟用包改內容是另寫草稿。放棄只刪草稿。被取代者仍 `confirmed`、`active` 改 false。無 `connectionString`、無 `rows`。欄上留再讀線索與個資文字。DBML 只由目錄產生，確認時重算。檔名是 `title` 加 `.dbml`；`title` 不得含 `\ / : * ? " < > |`。不讀確認前另存檔，不另做備份，不寫第二份稽核。

Excel：表名取 S 側檔名，同值一表；該欄空白併入上一表。欄名取 S 側；S 空白則用 H 側，且 note 不加 H 欄名。型別取 H 側：`CHAR(n)`→`char(n)`，`VARCHAR(n)`→`varchar(n)`，`DATE`→`date`，`STRING`→`text`，其餘小寫。允許空白不是 `Y` 即 `not null`。S 側類型是畫面格式。欄 note 把描述與備註拆段去重，以全形分號連接；欄名不同才加 H 欄名；畫面格式用儲存格結果；僅 H 型別含括號長度且與 S 長度不同才加 S 長度，`DATE`、`STRING` 不加；個資有值才加。勾 `V` 者依 S 序號成索引，名 `ix_` + 表名全小寫 + `_append`，說明固定「S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append」。表 Note 以來源檔起句，再接欄位列後第一個有說明的類型欄；中英間補空白，半形逗號改全形，缺句號補上。忽略計數公式與跨表超連結。整包說明是人工欄。

讀結構只走 `SchemaGraph.Read`（表、欄、型別、主鍵、外鍵，註解留空）。不呼叫 `Preview` 或 `GET /schema/graph`，路由忽略 `connectionId`。只在組態完整連線恰有一筆、且名稱等於 `Annotation:PlatformConnection` 時讀或對帳；否則不呼叫，讀列次數 0。空表清單仍可成草稿。`Connections` 仍只有 `ServerType` 與 `ConnectionString`（`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL` 之一，字串非空白，名稱不分大小寫）。工作台進程不載入連線字串。

關聯是 `from`、`fromColumn`、`to`、`toColumn`、`origin`、`note`。`origin` 只許 `foreign-key` 或 `declared`。兩端對調仍算同一段。寫進檢視才對成 `foreign-key`／`accepted`。不改檢視文件欄位。一端不在本包就拒絕。

目錄 HTTP 在 `/mcp` 外，寫入要 `X-DBServices-Caller` 與 `X-DBServices-Workbench`：`PUT /annotations/draft`、`DELETE /annotations/draft`、`POST /annotations/read-structure`、`POST /annotations/confirm`、`GET /annotations/draft/dbml`。落點判斷在 `DbServices.Mcp`。`SchemaGraph.Read` 失敗沿用 `connection_failed`、`execution_failed`。寫入失敗是 `execution_failed`「沒有保存。」非管理員沿用 `forbidden`「沒有權限。」錯誤鍵 `code`、`message`。新代碼只許：

- `import_rejected`：無法匯入。這份活頁簿對不上字典契約。既有草稿沒有改。
- `draft_exists`：再讀以外的匯入或讀結構。已有工作中的草稿，沒有改。
- `no_connection`：沒有讀取結構。
- `connection_unselectable`：註記站不能選擇連線，沒有讀取結構。不接連線名稱。
- `not_platform_source`：不能用{連線名稱}讀結構或確認。
- `confirm_incomplete`：無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。
- `confirm_unmatched`：無法確認。這些表或欄對不上來源：{名稱}。
- `confirm_blocked`：無法確認。這些檢視會落到新包之外：{檢視名稱}。
- `link_outside`：關聯的一端不在這份註記包裡。

名稱以頓號接在冒號後。連線名稱可出現，連線字串不可。

入口代碼 `governance`，名稱「資料治理」，路徑 `/governance`，加入 `EntryNames`。僅此入口時登入直達。站點在 `WorkbenchLayout`。Avalonia 不加。

## UX & Interaction Patterns

同一頁：package-bar、purpose-field、table-list、table-note、column-editor、link-editor、diff-list、dbml-block、progress-line、result-line、status-message。左軌「現在的工作」只有「註記」。指派頁多一個「資料治理」核取方塊；失敗時勾選回到保存前，不新增管理員。兩個以上入口才經換工作進入。

冷開啟不顯示空差異列。說明可先空白著存，空框不改危險色。表註可空，不擋保存或確認。欄註空白顯示「尚未註記」，仍可確認。啟用包可再讀、可匯出；修改進入新草稿。被取代包唯讀可匯出。放棄後若有已確認包就唯讀打開，否則回冷開啟。

確認與放棄各先一層對話框；將被取代時寫出舊包名。放棄：「放棄這份草稿？已確認的包維持原樣。」Esc 關閉且不執行。讀取或匯入中停用匯入、讀結構與確認。進行中只寫「正在讀取結構。」或「正在匯入。」成功只寫「已確認。設計目錄改看這一包。」有舊包則接「取代了{舊包名稱}。」錯誤是訊息在上、代碼在下。匯出先顯示唯讀 DBML 並下載。差異列留到草稿關閉，文字要有「新增」「刪除」或「已改變」。

只沿用 `tokens.css`，不新增色票。表名、欄名與 DBML 用程式字。控制項至少 36px，焦點 `:focus-visible`，減少動態時取消過渡，無只靠懸停的操作。Tab：左軌、工具列、整包說明、表清單、表註、欄註、關聯、差異、DBML。錯誤 `role="alert"`，成功 `role="status"`。寬於 1099px 左表右編、左軌 240px；其下單欄；720px 以下表單一欄、左軌 200px。

## Cross-Story Dependencies

Story 1.1 是進入邊界。已有草稿時 Story 1.2 與 1.3 拒絕且不改檔；再讀只由 Story 1.6 接手。Story 1.5 切換啟用旗標並檢查檢視落點，不改寫檢視。放棄不得碰到已確認包。

設計清單與 `source_annotation` 的 Discovery 屬於下一則史詩。這一則只寫入唯一啟用包，並擋下會把已保存檢視擠出包外的確認。沒有啟用包時不要回退成即時結構或表級工具。
