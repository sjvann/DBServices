---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-DBServices-2026-10-12/prd.md
  - _bmad-output/planning-artifacts/prds/prd-DBServices-2026-10-12/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-12/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/EXPERIENCE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/reconcile-prd.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/reconcile-architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/reconcile-addendum.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/reconcile-spec.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/mockups/key-live-preview.html
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/mockups/key-editor.html
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/mockups/key-preview.html
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/mockups/key-role-assign.html
  - _bmad-output/specs/spec-report-workbench/SPEC.md
  - _bmad-output/specs/spec-report-workbench/glossary.md
  - _bmad-output/specs/spec-report-workbench/workbench-shapes.md
  - _bmad-output/specs/spec-report-workbench/surfaces.md
  - _bmad-output/specs/spec-report-workbench/stack.md
  - _bmad-output/specs/spec-report-workbench/architecture-diagrams.md
---

# DBServices 報表工作台 - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for DBServices 報表工作台, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: 未登入不可進入工作台。未帶有效登入狀態的請求不回傳具名連線、欄位、業務列、範本或常用項目，工作台不以 `anonymous` 呼叫 MCP。沒有公開註冊端點。

FR-2: 管理員可建立操作者、建立範本群組，並指派或移除某個操作者在某個群組的設計角色或檢視角色。非管理員不能建立操作者，也不能變更角色指派。同一操作者可在不同群組具有不同角色；對同一群組至多一筆指派。設計角色在該群組同時滿足檢視角色的讀取能力。移除角色後，範本庫不再包含該群組。

FR-3: 只有管理者看得到工作台全貌，因此管理者不需另被指派規劃設計師，也看得到每個可用具名連線的名稱與 `ServerType`。其他帳號要有規劃設計師才看得到這份清單；沒有該工作角色時，左軌沒有活資料預覽，也不回傳連線清單。允許進入活資料預覽的人，連線範圍是全部已登記具名連線，不做資料表級授權。MCP 表面列出該宿主已登記且名稱可解析的連線。Web API 表面只列出一個連線，顯示名稱為 `default`，`ServerType` 來自工作台組態。清單、錯誤與後續請求都沒有連線字串欄位，操作者無法在工作台輸入連線字串。

FR-4: 操作者在 MCP 與 Web API 之間選擇這次預覽的取數表面。切換表面會換掉 FR-3 的連線清單。GraphQL 不是 v1 的取數表面。

FR-5: 操作者在 Web API 上列出資料表，再列出選定資料表的欄位。使用 `GET /meta` 與 `GET /{resourceName}/fieldSet`。不呼叫 `tables` 或 `fields`。這些是結構呼叫，不回傳業務列。沒有資料表時清單是空的。`fieldSet` 每一欄至少包含名稱、型別、是否主鍵、是否不可為空。

FR-6: 操作者在 Web API 上設定零個或一個等值篩選並確認後，可預覽符合條件的列。零個篩選讀取該表的列；恰好一個篩選使用 `Key` 與 `Value`；兩個以上篩選時拒絕確認。不呼叫 `query`。沒有符合列時預覽是空結果。工作台不另做第二次彙總。

FR-7: 工作台沒有表級計數動作。不呼叫 `count`，也不讀全表自己算筆數。報表上的筆數必須是該次模組視圖回傳列裡的一個輸出欄。

FR-8: 同一份已發布模組視圖改打另一個 `ServerType` 的具名連線時，視圖名稱與參數鍵不變。從 `inventory` 改到 `inventory_pg` 時亦然。工作台不要求操作者另選引擎；引擎顯示來自 FR-3。

FR-9: 每次結構呼叫與填數呼叫都在畫面上留下取數軌跡。對 MCP 的一次成功模組視圖呼叫，軌跡裡的呼叫者、連線名稱、視圖名稱、參數與列數能在該次 MCP 稽核紀錄中找到對應。表級 `query` 不是工作台的成功路徑。軌跡與稽核不包含業務列與連線字串。

FR-10: 活資料預覽沒有 SQL 輸入，也沒有新增、更新、刪除。工作台發出的 MCP 呼叫只會是已發布模組視圖。對 Web API 只發出 FR-5 與 FR-6 所述的 GET。操作者無法從工作台提交 `insert`、`update`、`delete`、`sql`，或 Web API 的 POST、PUT、DELETE。

FR-11: 未知資料表、連線失敗與空結果都變成操作者讀得懂的狀態。MCP 的 `unknown_table`、`connection_failed`、`invalid_arguments`、`execution_failed` 以原錯誤碼與不含秘密的訊息顯示。錯誤出現時，工作台仍可繼續選擇其他連線或資料表。空清單與零列使用空狀態，不顯示為未處理例外。

FR-12: 設計者可在畫布使用標題、文字、參數、資料表、計數五種元件。一份 MCP 範本綁定恰好一個已發布模組視圖。資料表元件的欄來自該視圖的輸出欄。計數元件綁該視圖的一個輸出欄，不另發 `count`。Web API 範本仍可綁一張表；含計數元件時不能在 Web API 上確認。

FR-13: 設計者可加入、拖曳重排與刪除元件，並設定繫結。畫布順序是由上到下的序列，拖曳只改變這個順序，不保存 x、y、寬、高。重新開啟範本時，順序與繫結保持為上次保存的結果。刪除元件後，保存結果不再包含該元件。

FR-14: 設計者選定已發布模組視圖或一張 Web API 資料表後，工作台提出元件建議；設計者可接受、修改或忽略。建議不含業務列與參數值。MCP 範本的欄名全集等於該視圖的輸出欄。Web API 範本等於該次 `fieldSet`。不來自 `fields`。類型只會是 FR-12 的五種。接受把建議的元件加到畫布末尾。忽略不改變畫布。修改後再接受，畫布採用修改後的版本。含有未知欄名的建議不能被接受。建議失敗時既有畫布保持不變。

FR-15: 管理員可建立與重新命名範本群組。對一個群組具有設計角色的操作者，可在該群組建立、重新命名、刪除範本，以及把範本移到另一個自己具有設計角色的群組。新範本在保存時必須選入一個群組，且操作者對該群組具有設計角色。範本恰好屬於一個群組。同一群組內名稱不重複。移到沒有設計角色的群組會被拒絕，範本仍在原群組。群組內仍有範本時，刪除該群組會被拒絕。檢視角色不能建立、修改、刪除或移動範本。

FR-16: 保存範本只寫入版面、繫結與參數定義。保存後的範本沒有資料列、連線字串、具名連線名稱與參數值。關閉後再打開，不需要連線仍可看到版面與繫結。要看到列，必須另做預覽確認。

FR-17: 只有管理者看得到全部範本群組與其範本。其他操作者只能列出自己具有設計角色或檢視角色的範本群組與其範本；沒有該群組角色時，範本庫與交談候選都不含該群組，以識別直接讀取或預覽時工作台拒絕，且填數呼叫次數是 0。建立、修改、刪除或移動範本仍要該群組的設計角色。管理者未被指派設計角色時可以看見該群組，但不能改其中的範本。

FR-18: 操作者提交語句後，工作台給出意圖：具名連線候選、範本候選、等值篩選。候選連線只來自己列出的具名連線。管理者的候選範本來自全部群組。其他操作者的候選範本只來自分自己有檢視角色或設計角色的範本。等值篩選的欄位必須是候選範本上的參數欄位，值是純量。解析結果不含連線字串與業務列。一句話裡的時間範圍只有在能對到某個參數欄位的一個等值時才成為篩選；不能對到時，該篩選不產生，工作台也不另造區間查詢。關鍵字規則即可通過此行為；不以側車已上線或綁定某個模型廠商作為完成條件。

FR-19: 意圖不是恰好一個連線、恰好一個範本、且該範本每個參數都有值時，工作台不執行填數呼叫。多個連線或多個範本時列出候選，讓操作者選到各剩一個。缺少參數值時指出缺的欄位或標籤。對不到任何連線或任何範本時說明對不到，候選為空。上述情況在補齊或選定之前，該次語句的填數呼叫次數是 0。改選候選本身不是確認。

FR-20: 意圖就緒後，工作台先列出將執行的填數呼叫。操作者確認後才執行。MCP 上一次確認恰好呼叫該範本所綁的一個已發布模組視圖。Web API 上是零鍵或單鍵 GET；篩選不是零個或一個，或範本含計數元件時，整次拒絕。確認之後實際發出的呼叫與清單相同。任一呼叫失敗時，預覽只有該錯誤。

FR-21: 操作者可把一次就緒的叫法存成自己的常用項目，並在之後再次確認執行。常用項目含名稱、範本識別、取數表面、具名連線名稱、參數值與擁有者，不含資料列、連線字串與版面複本。常用清單只回傳擁有者自己的項目；管理員不能透過常用清單介面讀取他人的常用項目。再次開啟時讀取範本目前的版面與繫結，套上常用項目的連線與參數，並依 FR-20 重新確認後才查詢，不查詞庫，不讀舊列。範本已刪除，或操作者已失去該群組的檢視角色時，再次開啟被拒絕，填數呼叫次數是 0。參數欄位變更導致無法對齊時，工作台指出差異並停在未確認狀態。

FR-22: 操作者可對目前的預覽快照匯出 PDF、列印，並把其中的資料表元件匯出 CSV。PDF、列印與 CSV 中的數字和文字等於該預覽快照，包含空結果。匯出與列印不發出新的填數呼叫。離開該次預覽後，舊快照不能再匯出。列印版面以元件為單位換頁，不提供預印表單對位。PDF 預設 A4。同一份預覽快照連續匯出兩次，兩份 PDF 的業務數字相同。

### NonFunctional Requirements

NFR-1: 操作者看得到的介面文字、空狀態、確認清單與工作台自己產生的錯誤使用繁體中文。MCP 已回傳的訊息維持宿主原文。

NFR-2: 錯誤至少含穩定代碼與給人讀的訊息。工作台自己的錯誤是穩定英文蛇形碼加繁體中文訊息。訊息不含連線字串、密碼、金鑰與堆疊中的秘密。

NFR-3: 工作台登入密碼不以明文寫入 SQLite、日誌與軌跡。只以 `PasswordHasher` 的 `passwordVerifier` 存放。模型 `apiKey` 不進 SQLite、解析 JSON、程序引數、日誌與測試結果。讀取 API 只回傳金鑰是否已設定，不回傳金鑰值。

NFR-4: 解析器或建議步驟失敗時，不執行填數呼叫。已保存的範本、常用項目與關鍵字規則不變。設計器建議失敗時既有畫布保持不變，並顯示可讀錯誤。

NFR-5: 某個業務連線失敗不停止工作台，也不停止 MCP 宿主。其他連線仍可用。失敗連線上的呼叫回傳連線錯誤，程序不結束。

NFR-6: 同一份預覽快照連續匯出，業務數字相同。匯出與列印不得再查資料。

NFR-7: 連線字串只存在既有伺服器組態。工作台的請求、儲存、匯出檔、畫面、錯誤、取數軌跡與稽核不新增這個欄位。業務列只出現在確認後的預覽，以及由該預覽快照產生的 PDF、列印與 CSV。

NFR-8: 工作台儲存放在營運者自己的環境，不寫進業務庫，也不預設外送到第三方分析平台。應用不新增雲端供應商。TLS 由部署環境終止。

NFR-9: 三片都要通過才算完成：活資料預覽、編輯器與範本庫、交談套版與常用清單及匯出列印。前一片是下一片的閘門，不是可單獨交付的產品。

NFR-10: 軌跡對照使用 UTC。快照沒有閒置逾時。MCP 稽核保留天數仍未定，工作台不另定、也不實作保留政策。

NFR-11: Blazor 以 WCAG 2.2 AA 為無障礙底線（UX 標成假設）。Avalonia 使用同一閱讀順序、同一控制項名稱，並可全鍵盤操作。停用狀態不能只靠低對比顏色表示。

NFR-12: `AllowedHosts` 出貨值為 `*`。工作台不因為要列出連線而放寬 MCP 的工具清單或加入登入。

### Additional Requirements

**沒有外部 starter template。** 架構指定的是在現有方案加入新專案，不是套用第三方腳手架。這會影響 Epic 1 Story 1：先建立結構種子，再在其上實作行為。

- 新專案加入現有方案，目標框架 `net10.0`，Nullable 與 ImplicitUsings 開啟：`Workbench`、`Workbench.Web`、`Workbench.Core`、`Workbench.Contracts`、`Workbench.Adapters`、`Workbench.Storage.Sqlite`、`Workbench.Avalonia`、`DbServices.ModelSidecar`、`Workbench.Tests`。`Workbench` 參考 Web、Adapters 與 Storage，負責組裝、DI 與聽取位址，沒有畫面。部署恰好一個 `Workbench` 進程，與 `McpService`、`WebService` 分開。
- 六角形邊界：填數、結構呼叫、範本讀寫、角色判斷、解析、軌跡與快照只經過 `Workbench.Core` 的端口。Blazor 在進程內呼叫 Core，只參考 Core 與 Contracts。Avalonia 只呼叫工作台 HTTP，JSON 型別是 Contracts，控制器不另寫業務判斷。兩個殼與 `Workbench.Web` 都不參考 Adapters、`DbServices.Mcp`、`DBService`、Provider、`Microsoft.Extensions.AI`，也不持有 MCP 或 Web API 的基底網址。Core 不參考 ASP.NET、Avalonia 與模型套件。
- 單一宿主：只有 `Workbench` 開啟 `Workbench:StorePath` 指向的 SQLite，並在記憶體持有工作階段、待確認清單與預覽快照。Avalonia 不嵌入 Blazor，不開啟該檔。殼的組態只有工作台基底網址。不另開第二個工作台實例分攤這些記憶體物件。
- 填數轉發：MCP 用戶端在 Adapters，使用 `ModelContextProtocol` 2.2.0，只呼叫該範本所綁的已發布模組視圖，並帶 `X-DBServices-Caller`。不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`。Web API 只發送 `GET /meta`、`fieldSet` GET、零鍵資料表 GET，以及單鍵 `Key` 與 `Value`。計數、兩個以上篩選、POST、PUT、DELETE 不發送。結果列原樣進入預覽。
- 連線中繼資料：`McpService` 增加 `GET /connections`。它不是 MCP 工具，不掛在 `/mcp` 之下，也不進入 Discovery。回應每筆只有 `name` 與 `serverType`，且只含組態完整的連線。不回傳連線字串，也不回傳是否可用。`Workbench` 與 Adapters 不參考 `DbServices.Mcp`。Web API 顯示名稱固定為 `default`，`ServerType` 來自 `Surfaces:Web:ServerType`，基底網址來自 `Surfaces:Web:BaseUrl`。工作台不讀 WebService 的組態檔，選項類別不宣告連線字串，不修改 WebService 既有路由與 JSON。
- 工作台儲存使用 `Microsoft.Data.Sqlite` 10.0.12 的單一檔案。檔內只放操作者、群組、角色指派、範本、常用項目與關鍵字規則。不在業務庫建表。範本沒有連線名稱、參數值與資料列。常用項目有 `surface`（`mcp` 或 `web`）、連線名稱與純量參數值，沒有版面複本與資料列。
- 登入與第一個管理員：儲存裡零個操作者時，宿主讀 `Workbench:InitialAdmin:DisplayName` 與 `Workbench:InitialAdmin:Password`，以 ASP.NET Core 10.0.12 共用框架的 `PasswordHasher` 寫入恰好一個 `admin` 操作者。已有操作者之後這兩個鍵不再使用。缺密碼且零個操作者時，宿主不提供工作台功能。工作階段是宿主記憶體中的 `sessionId` 與操作者 `id`。Blazor cookie 與 Avalonia bearer 都只帶這個 `sessionId`。登出刪除該筆，兩邊同時失效。進程重啟後工作階段清空。
- 確認清單與預覽快照只在宿主記憶體。結構瀏覽不產生填數清單。MCP 上一次確認恰好一筆已發布模組視圖呼叫。Web API 上是零鍵或單鍵 GET。確認請求帶回清單識別。執行前再核對清單、範本、角色與表面能力；不一致則整次拒絕，不部分執行。離開該次預覽，或同一操作者的下一次確認，快照與舊清單刪除。快照與清單不寫入 SQLite。PDF 與 CSV 的具體套件尚未選定；選定時不得再查資料。
- 解析管線在 Core 做關鍵字比對，不啟動側車，也不讀常用項目。內建比對使用本次看得到的目錄：語句含連線 `name`、範本 `name`、參數 `field` 或 `label` 才入候選；對不到的不補猜。有效的關鍵字規則再加上去，參數值仍從語句取出。宿主重算 `ready`、`ambiguous`、`incomplete`，不以解析器自報狀態為確認依據。模型啟用且狀態不是 `ready` 時，才把解析輸入送給側車。候選若超出本次輸入，整份作廢，填數次數為 0。
- 模型設定只有一份。欄位是 `enabled`、`placement`（`cloud` 或 `local`）、`endpoint`、`modelId`、`apiKey`、`probeTimeoutSeconds`、`parseTimeoutSeconds`。內容根的 `model.json` 不在 `wwwroot`。覆寫順序是這個檔、Development 且有 `UserSecretsId` 時的 user secrets、環境變數、命令列。`cloud` 的 `endpoint` 必須是 `https` 且有效金鑰非空白。`local` 的主機必須是 `localhost`、`127.0.0.1` 或 `::1`，金鑰可空白。側車每次設定 `Endpoint`，不使用套件預設的 `https://api.openai.com/v1`。本地金鑰空白時，只在子程序內使用佔位憑證 `local`，不寫入設定檔。未決：覆寫順序包含命令列，同時規定 `apiKey` 不進入程序引數；命令列是否可以覆寫 `apiKey` 尚未分開寫死。
- 只有管理員可測試模型。測試提交草稿，不要求先儲存，也不改 `model.json`。檢查不通過回 `model_invalid_settings` 且不啟動側車。標準輸入只有 `kind` 為 `probe`、`placement`、`endpoint`、`modelId`。金鑰只放在子程序環境變數 `DBSERVICES_MODEL_API_KEY`。探測不含語句、範本、連線名稱、資料列與資料庫連線字串，也不把模型回覆內文帶回畫面。結果碼只允許 `model_ok`、`model_invalid_settings`、`model_unreachable`、`model_timeout`。`enabled` 為 false 時，交談仍只走關鍵字規則。預設探測逾時 10 秒、解析逾時 30 秒。
- 模型側車 `DbServices.ModelSidecar` 是 net10.0 的自包含執行檔，由 Adapters 啟動，標準輸入一筆 JSON、標準輸出一筆 JSON。它只參考 `Workbench.Contracts`、`Microsoft.Extensions.AI` 10.10.0、`Microsoft.Extensions.AI.Abstractions` 10.10.1、`Microsoft.Extensions.AI.OpenAI` 10.10.1。雲端與本地都用這個 OpenAI 相容客戶端。`IChatClient` 不進入 Blazor、Avalonia、Core 或 MCP 宿主。側車不組 SQL、不開資料庫連線、不寫檔，也不回傳要宿主直接插入的規則命令。此功能不部署 Python。逾時則結束子程序並回 `model_timeout`。
- 關鍵字規則存在工作台 SQLite，可新增與停用。一筆含 `phrase`、可省略的連線名稱、可省略的範本識別、參數欄名陣列、`source`（`human` 或 `ai`）、建立者與是否有效。沒有參數值、資料列、連線字串、模型金鑰與密碼。人為新增立即有效，且只能為自己看得到的範本加規則；只綁連線名稱時，已登入操作者即可新增。來自 AI 的規則只在操作者確認該次意圖後，再按一次加入關鍵字才寫入。模型自行完成的解析不寫入規則。建立者或管理員可把規則改為無效。比對時略過這位操作者現在看不到的範本。
- 元件契約：`type` 只許 `title`、`text`、`parameter`、`table`、`count`。`order` 從 0 連續。契約型別放在 `Workbench.Contracts`。
- 組態鍵：`Workbench:StorePath`、`Workbench:InitialAdmin:DisplayName`、`Workbench:InitialAdmin:Password`、`Surfaces:Mcp:BaseUrl`、`Surfaces:Web:BaseUrl`、`Surfaces:Web:ServerType`，以及 `model.json` 欄位。
- 版本：ASP.NET Core Blazor 10.0.12、Avalonia 與 Avalonia.Desktop 12.1.4、Microsoft.Data.Sqlite 10.0.12、ModelContextProtocol 2.2.0。
- 可見範圍以使用者確認為準：只有管理者看得到工作台全貌（活資料預覽與全部已登記具名連線、全部範本群組、交談套版、常用清單這個表面、預覽，以及帳號、角色指派、模型設定）。其他帳號依工作角色與群組角色。這取代架構慣例「任何已登入操作者都能做活資料預覽」。改範本仍要該群組的設計角色。常用項目的產品介面仍只回擁有者自己的項目。

**繼承的資料平面不變量（合併 2026-10-11，工作台必須遵守，不在本拆解重做資料平面）：**

- 工作台不修改 `IDbService`、`IDbServiceAsync`、`DbServices.Core`、`FunctionRegistry`、Provider，也不改既有 REST 與 GraphQL 的路徑和查詢形狀。工作台不建立 `IDbService`，不呼叫 `QuerySession`。
- Discovery 只列已發布模組視圖。工作台不把六個表級工具凍結成公開廣告，也不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`、`insert`、`update`、`delete`、`sql`。簽名、REST 與 GraphQL 路徑仍不改。
- `filters` 維持巢狀純量。工作台不把篩選拼進 SQL，不傳送 `connectionString`。
- `Connections` 仍只由 `DbServices.Mcp` 在 `McpService` 進程綁定。工作台進程不載入連線字串。
- MCP 傳輸維持 `McpService` 的 `POST /mcp`，套件 2.2.0。不在 `McpService` 加登入。呼叫者標頭維持 `X-DBServices-Caller`，工作台送出操作者 `id`。
- MCP 錯誤碼與成功形狀原樣顯示。工作台不重算列、不把空列改成錯誤。工作台不再依賴 `fields` 的 `unknown_table`。
- MCP 稽核仍是宿主那一個 JSONL。工作台不寫第二份 MCP 稽核。稽核寫入失敗而回 `execution_failed` 時，不顯示部分列。
- 模型不進入 Core 與 MCP 宿主。P0 的 `anonymous` 仍可呼叫 MCP；這條不沿用到工作台。工作台未登入的請求不呼叫 MCP。

### UX Design Requirements

UX-DR1: 實作淺色設計權杖，兩殼共用，沒有深色模式，沒有成功綠。色票為 `surface-base` `#F3F5F7`、`surface-raised` `#FFFFFF`、`surface-sunken` `#E7ECF1`、`ink-primary` `#1B2430`、`ink-secondary` `#4E5D6C`、`ink-disabled` `#8B98A5`、`border-hairline` `#D0D7DE`、`action` `#0F4C5C`、`action-foreground` `#FFFFFF`、`danger` `#8E1D2C`、`danger-foreground` `#FFFFFF`、`caution` `#7A4E00`。確認色只用於會發出填數呼叫的按鈕。危險色只用於刪除與拒絕。警示墨只用於意圖狀態 `ambiguous` 的文字，不做色塊徽章。

UX-DR2: 實作字級權杖，不引入網頁展示字體。`title` 20px/600、`body` 14px/400、`meta` 12px/400，字體為 Segoe UI 與 Microsoft JhengHei。`code` 13px，字體為 Cascadia Mono、Consolas、ui-monospace。等寬只用在工具名、錯誤碼、`ServerType` 與呼叫參數鍵。

UX-DR3: 實作間距 4 / 8 / 12 / 16 / 24 / 32。相關控制項用 8px，表面區塊之間用 24px，頁面邊距 24px，畫布上相鄰元件間距 8px。圓角 4px 用於按鈕、畫布區塊、預覽井與輸入框；6px 只用於對話框；全圓只用於角色徽章。深度靠紙面、抬升面、預覽井三層色調。對話框是唯一允許陰影的表面（模糊 16px、低透明度）。拖曳換序時被拖區塊只加髮線，不放大、不傾斜。

UX-DR4: 實作 `confirm-button`。確認色底、白字、4px 圓角。標籤只限：查詢、計數、範本預覽、確認。一個表面上，即將執行的那一顆才用這個樣式。按下代表同意執行畫面上 `call-list` 已列明的填數呼叫，並進入預覽頁。`ready` 以外不可按。可否按下除了顏色，還有標籤與 `intent-status` 文字。

UX-DR5: 實作 `secondary-button`。白底、髮線、墨色字。用於接受建議、忽略建議、加入關鍵字、儲存範本、儲存常用項目、匯出 PDF、列印、匯出 CSV。接受建議不是確認，不能畫成 confirm-button。匯出三顆只在預覽頁，且不發出新的填數呼叫。

UX-DR6: 實作 `canvas-block`。白底髮線的一列。左側是順序把手與元件種類（標題、文字、參數、資料表、計數），右側是繫結摘要。沒有自由定位的選取框，沒有寬高把手。拖曳只改變由上到下的 `order`。每個區塊另有上移、下移，拖曳不是唯一改序手段。檢視角色只能看。刪除後保存結果不再含該元件。

UX-DR7: 實作 `preview-well` 為獨立預覽頁的本文。沒有快照時整頁用下凹底與次墨，文案是「確認之前，這裡沒有業務列。」不下載列來當佔位，匯出與列印不可按。確認成功後改為白底，由上到下放這一頁的報表，空結果也是結果，計數可以是 0。人留在這一頁時，上一筆成功的列留到下一次成功確認才換。離開這一頁後舊快照不能再匯出。這一版沒有註解、背景、圖片工具。頁首是匯出 PDF、列印、匯出 CSV。

UX-DR8: 實作 `call-list`。確認前列出將執行的填數呼叫，名稱用等寬字。MCP 上恰好一筆已發布模組視圖。Web API 上是零鍵或單鍵 GET。實際呼叫集合必須與清單相同。任一筆失敗時，預覽只有該錯誤。

UX-DR9: 實作 `error-block`。白底、危險色文字與 1px 危險色邊，不鋪滿危險色底。第一行是錯誤碼（等寬），第二行是給人讀的訊息。MCP 代碼原樣：`unknown_table`、`connection_failed`、`invalid_arguments`、`execution_failed`。不含連線字串、密碼、金鑰、堆疊秘密。出現後仍可改選其他連線或資料表。

UX-DR10: 實作 `trace-row`。每筆結構呼叫與填數呼叫一列，含工具名、連線名稱、結果或錯誤碼。不含業務列，不含連線字串。取數軌跡不是左軌目的地。寬度足夠時貼在主欄右側，不足時改到主欄下方。Blazor 以 1280px 為舒適工作寬，低於 1100px 時軌跡改到下方。

UX-DR11: 實作 `connection-row`。只顯示連線名稱與 `ServerType`。沒有連線字串欄位，也沒有密碼欄。切換取數表面會換掉整份清單。Web API 的顯示名固定為 `default`。

UX-DR12: 實作 `account-row` 與帳號表面。一列一個操作者，只顯示顯示名稱。建立與編輯的表單只有顯示名稱與登入秘密，不收集角色，不放角色徽章，不放工作種類。不能把操作者改成管理者。畫面上不能再建立第二個管理者。

UX-DR13: 實作 `role-badge`，只用在角色指派。藥丸形，次墨字，底用預覽井色。工作角色標籤是規劃設計師、範本編輯師、終端使用者。群組角色標籤是設計角色、檢視角色。不做成「管理者」徽章。管理者帳號本身不是一枚可勾選的工作徽章。

UX-DR14: 實作 `intent-status`。只顯示宿主重算後的狀態，三者都是文字不是色塊：`ready` 用主墨，`ambiguous` 用警示墨，`incomplete` 用次墨。多候選或缺參數時停在補齊，確認按鈕不可按，填數次數是 0。

UX-DR15: 實作左軌，寬 240px，組名用 meta 字級。管理者的左軌是全貌：活資料預覽、範本庫、交談套版、常用清單、預覽，以及帳號、角色指派、模型設定與測試同時出現，不因未被指派工作角色而隱藏。其他帳號有被指派的工作角色才出現對應的組：規劃設計師是活資料預覽；範本編輯師是範本庫；終端使用者是交談套版與常用清單；預覽在擁有任一工作角色時出現。編輯器從範本庫的一列進去，不佔軌上常駐位置。非管理者直接進入帳號、角色指派、模型設定與測試時拒絕，不建立操作者，也不變更角色。非管理者直接進入自己沒有的工作表面時同樣拒絕。帳號尚無工作角色、且不是管理者時，左軌沒有三類工作表面，說明「尚未指派工作角色。」帳號仍然存在。

UX-DR16: 工作角色與群組角色都只在角色指派裡指定，且可同時多個工作角色。帳號建立時沒有工作種類。群組角色在選定的範本群組上單選設計角色或檢視角色，或清空。同一帳號對同一群組至多一筆。設計角色在讀取時視同檢視角色。儲存角色不是確認，不發出填數呼叫。只有管理者看得到全貌。其他帳號依角色：規劃設計師才有活資料預覽，且連線範圍是全部已登記具名連線；範本編輯師或終端使用者才有範本庫，而且只列出自己有群組角色的範本，有設計角色才能改；終端使用者才有交談套版與常用清單。不做資料表級授權。常用清單的產品介面仍只回擁有者自己的項目，管理者的全貌不拿來讀取他人的常用項目。

UX-DR17: 預覽是獨立頁。確認成功後才進入，並顯示該次的列或計數。進入預覽頁時焦點移到該頁標題，並宣告頁名。頁內的列或錯誤更新用可被讀屏發現的區域，不把焦點從人正在讀的錯誤上扯走。匯出 PDF、列印、資料表匯出 CSV 只在該次預覽快照還在時可按。

UX-DR18: 資訊架構表面為：登入、活資料預覽、範本庫、編輯器、交談套版、常用清單、預覽、帳號、角色指派、模型設定與測試。對話框只疊一層。Esc 關閉最上層對話框，不確認。語句欄的 Enter 只送去解析，不確認填數。點按執行動作；會取業務列的動作只有 confirm-button。改選取數表面、具名連線、範本或意圖候選，本身不是確認。

UX-DR19: 空狀態與失敗文案使用來源用詞，至少包含：「確認之前，這裡沒有業務列。」「沒有符合的列。」「沒有資料表。」「此表面只接受單鍵。」「尚未指派工作角色。」錯誤碼保持原樣，人話另起一行。模型未啟用時只走關鍵字規則。模型測試的碼只會是 `model_ok`、`model_invalid_settings`、`model_unreachable`、`model_timeout`，訊息繁體中文，不帶回模型回覆內文。

UX-DR20: 狀態處理必須可測。沒有資料表時清單空並顯示「沒有資料表。」結構呼叫失敗顯示 error-block，不進入預覽頁。連線或查詢失敗時人留在預覽頁，宿主仍在，先前成功的列留到下一次成功確認才換，這次錯誤不把它們換成未處理例外。Web API 兩個以上篩選時拒絕並說明「此表面只接受單鍵」，不發送。範本含計數元件時整次確認拒絕。建議含未知欄不能接受。無權限範本不出現在清單與建議；以識別直接預覽時拒絕，填數次數 0。常用項目對不齊時指出差異，停在未確認。零個操作者且缺初始密碼時不提供工作台功能。來源沒有載入中畫面，也沒有未儲存變更的對話；儲存是明確的 secondary-button。

UX-DR21: 鍵盤與無障礙。焦點順序等於閱讀順序。上移、下移必須存在。confirm-button 的停用不能只靠顏色。停用墨在白底對比不足，不能當唯一狀態，停用原因要用 body 字級寫出來。Blazor 低於 1100px 不做手機版，允許橫向捲動，不把確認清單藏進只有懸停才看得到的地方。Avalonia 與 Blazor 同一資訊架構，最小視窗 1100×720，片 1 到片 3 的表面都在，包含獨立預覽頁。

UX-DR22: 來自 AI 的關鍵字規則，要等這次意圖已確認，再按一次「加入關鍵字」才寫入。開啟常用項目不查詞庫。加入關鍵字用 secondary-button。

UX-DR23: 四條具名旅程必須可走完，且失敗分支可測。UJ-1 林可：有規劃設計師，在 `inventory`（SqlServer）與 `inventory_pg`（PostgreSQL）之間換連線，工具名稱與參數鍵不變，畫面上沒有連線字串；連線失敗時先前成功列留在預覽頁。UJ-2 周寧：有範本編輯師與規劃設計師，以及目標群組的設計角色，接受 `fields` 建議、重排、加上標題文字參數與計數，重新打開時沒有業務列；未知欄不能接受。UJ-3 陳安：只有終端使用者與檢視角色，沒有規劃設計師所以沒有活資料預覽，用語句叫出月報，確認前填數次數是 0，確認後才進入預覽並匯出列印；常用清單再開用範本目前版面。UJ-4：陳安看不到薪資群組範本，也看不到周寧的常用項目；直接用識別預覽被拒絕。另加管理者旅程：管理者自己的左軌是全貌。建立只有名稱與秘密的帳號，在角色指派勾選工作角色與群組角色，該操作者重新登入後左軌只出現被勾選的工作段；非管理者打開角色指派被拒絕。

UX-DR24: 視覺對照四張畫面參考：`mockups/key-live-preview.html`、`mockups/key-editor.html`、`mockups/key-preview.html`、`mockups/key-role-assign.html`。不接回已拒絕的做法：自由座標、預印表單、分欄帶、子報表、圖表、結果敘事、確認前顯示業務列、連線字串欄位、儀表板卡片牆、套印頁面畫板。

### FR Coverage Map

FR-1: Epic 1 - 未登入不可進入
FR-2: Epic 1 - 管理員建立操作者、群組與角色
FR-3: Epic 1 - 管理者與規劃設計師看到具名連線
FR-4: Epic 1 - 選擇 MCP 或 Web API
FR-5: Epic 1 - 瀏覽資料表與欄位
FR-6: Epic 1 - 確認後以等值篩選預覽列
FR-7: Epic 1 - MCP 上確認一個已發布模組視圖，不提供表級計數
FR-8: Epic 1 - 換引擎不換工具契約
FR-9: Epic 1 - 取數軌跡對得上稽核
FR-10: Epic 1 - 預覽不提供寫入與自由 SQL
FR-11: Epic 1 - 可讀的失敗與空狀態
FR-12: Epic 2 - 五種元件
FR-13: Epic 2 - 由上到下編排畫布
FR-14: Epic 2 - 依欄位提出繫結建議
FR-15: Epic 2 - 在群組中建立與移動範本
FR-16: Epic 2 - 範本不含資料與連線
FR-17: Epic 2 - 管理者看全部群組，其他人只看自己有角色的群組
FR-18: Epic 3 - 語句解析成意圖
FR-19: Epic 3 - 未就緒時不填數
FR-20: Epic 3 - 確認後只執行列明的填數呼叫
FR-21: Epic 3 - 保存與再次叫用自己的常用項目
FR-22: Epic 3 - 用預覽快照匯出與列印

## Epic List

### Epic 1: 登入後核對活資料
管理者與規劃設計師登入後，能選定 MCP 或 Web API、看到具名連線，瀏覽資料表與欄位，確認後才看到列或計數，並留下對得上稽核的取數軌跡。管理者看得到這片的全貌；其他帳號沒有規劃設計師就沒有這個表面。管理員能建立帳號、建立群組、指派工作角色與群組角色。
**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-11

### Epic 2: 排出不含資料列的範本
範本編輯師用標題、文字、參數、資料表、計數排出由上到下的範本，接受或忽略依欄位產生的建議，並把範本放進自己有設計角色的群組。重新打開時只有版面與繫結。管理者看得到全部群組；其他人只看到自己有角色的群組。沒有設計角色就不能改。
**FRs covered:** FR-12, FR-13, FR-14, FR-15, FR-16, FR-17

### Epic 3: 用語句套版並帶走這次的報表
終端使用者用語句得到連線、範本與等值參數，確認後才看到填好的報表，存成自己的常用項目，並用這次預覽快照匯出 PDF、列印或匯出 CSV。關鍵字規則即可完成這次叫法；模型設定是同一條路徑上的加強。
**FRs covered:** FR-18, FR-19, FR-20, FR-21, FR-22

## Epic 1: 登入後核對活資料

管理者與規劃設計師登入後，能選定 MCP 或 Web API、看到具名連線，瀏覽資料表與欄位，確認後才看到列或計數，並留下對得上稽核的取數軌跡。管理者看得到這片的全貌；其他帳號沒有規劃設計師就沒有這個表面。管理員能建立帳號、建立群組、指派工作角色與群組角色。

### Story 1.1: 第一個管理者從瀏覽器登入

As a 管理者,
I want 安裝完成後從瀏覽器登入，未登入的人進不了工作台,
So that 之後每一次讀取都能對上一個操作者。

**Acceptance Criteria:**

**Given** 現有方案加入 `Workbench`、`Workbench.Web`、`Workbench.Core`、`Workbench.Contracts`、`Workbench.Storage.Sqlite` 與 `Workbench.Tests`，目標框架 `net10.0`
**When** 組態尚未有任何操作者，且提供了 `Workbench:InitialAdmin:DisplayName` 與 `Workbench:InitialAdmin:Password`
**Then** 宿主用 `PasswordHasher` 寫入恰好一個管理者，SQLite 只有操作者資料，`passwordVerifier` 不是明文
**And** 明文密碼不出現在 SQLite、日誌與軌跡；已有操作者之後，這兩個初始鍵不再使用

**Given** 儲存裡零個操作者且缺少初始密碼
**When** 有人開啟工作台
**Then** 工作台不提供功能

**Given** 沒有註冊端點
**When** 未持有有效工作階段的人要求連線、欄位、列、範本或常用項目
**Then** 這些內容都不回傳，工作台也不呼叫 MCP，不以 `anonymous` 當作呼叫者

**Given** 管理者在瀏覽器登入
**When** 宿主建立工作階段
**Then** 記憶體中是一筆 `sessionId` 與操作者 `id`，cookie 只帶 `sessionId`；登出刪除該筆；進程重啟後工作階段清空
**And** 登入畫面使用淺色權杖與繁體中文，`Core` 不參考 ASP.NET

**Given** 瀏覽器與桌面殼繪製工作台表面
**When** 使用共用視覺權杖
**Then** 色票、字級、間距與圓角依 `DESIGN.md`；只有淺色，沒有成功綠；確認色只用於會發出填數呼叫的按鈕；等寬只用在工具名、錯誤碼、`ServerType` 與參數鍵
**And** 深度靠紙面、抬升面與預覽井；對話框是唯一允許陰影的表面；焦點順序等於閱讀順序；停用狀態不能只靠低對比顏色表示

### Story 1.2: 桌面殼與瀏覽器一起登出

As a 操作者,
I want 在桌面殼登入，並與瀏覽器共用同一次登出,
So that 兩殼不會各留一份工作階段。

**Acceptance Criteria:**

**Given** 新增 `Workbench.Avalonia`，它只呼叫工作台 HTTP，JSON 型別是 `Workbench.Contracts`，殼的組態只有工作台基底網址
**When** 操作者在桌面殼登入
**Then** bearer 只帶與瀏覽器相同的那一筆 `sessionId`，Avalonia 不嵌入 Blazor，也不開啟 SQLite

**Given** 同一個操作者在瀏覽器與桌面殼都已登入
**When** 任一殼登出
**Then** 宿主刪除該筆工作階段，兩邊同時失效

**Given** 部署恰好一個 `Workbench` 進程
**When** 桌面殼開啟
**Then** 最小視窗是 1100×720，`AllowedHosts` 出貨值為 `*`

### Story 1.3: 管理員決定誰看得到哪個表面

As a 管理者,
I want 建立只有名稱與密碼的帳號，並指派或移除工作角色與群組角色,
So that 其他人只看見自己角色該看的表面。

**Acceptance Criteria:**

**Given** 管理者已登入
**When** 建立操作者
**Then** 表單只有顯示名稱與登入秘密，帳號列只顯示名稱，沒有角色徽章，也不能把該帳號改成第二個管理者

**Given** 管理者在角色指派勾選工作角色，並在一個群組上指定設計角色或檢視角色
**When** 儲存
**Then** 一個帳號可以同時有規劃設計師、範本編輯師、終端使用者；同一帳號對同一群組至多一筆，設計角色在讀取時視同檢視角色
**And** 這一步不發出填數呼叫；角色徽章只出現在角色指派

**Given** 該帳號已被指派工作角色，或在某個群組上有設計角色或檢視角色
**When** 管理者移除一個工作角色，或清掉該群組的角色指派
**Then** 該工作角色或該筆群組指派不再存在；工作角色被全部移除、且帳號不是管理者時，左軌沒有三類工作表面，並說明「尚未指派工作角色。」
**And** 非管理者不能移除角色；移除本身不發出填數呼叫

**Given** 一個非管理者直接打開帳號或角色指派
**When** 工作台處理該請求
**Then** 請求被拒絕，不建立操作者，也不變更角色

**Given** 一個沒有工作角色、也不是管理者的帳號登入
**When** 左軌顯示
**Then** 沒有三類工作表面，並說明「尚未指派工作角色。」帳號仍然存在

### Story 1.4: 看到具名連線並切換取數表面

As a 規劃設計師,
I want 看到連線名稱與 `ServerType`，並在 MCP 與 Web API 之間切換,
So that 我能核對換了引擎，畫面上仍然沒有連線字串。

**Acceptance Criteria:**

**Given** `McpService` 提供 `GET /connections`，它不是 MCP 工具、不在 `/mcp` 之下、也不進入 Discovery
**When** 管理者或規劃設計師開啟活資料預覽
**Then** MCP 清單每筆只有 `name` 與 `serverType`，且只含組態完整的連線；不回傳連線字串，也不回傳是否可用
**And** 工作台與配接器不參考 `DbServices.Mcp`，也不載入連線字串

**Given** 操作者把取數表面改成 Web API
**When** 連線清單重新載入
**Then** 清單換成恰好一筆，顯示名稱是 `default`，`ServerType` 來自 `Surfaces:Web:ServerType`
**And** GraphQL 不在可選表面裡

**Given** 帳號只有範本編輯師或終端使用者
**When** 這個人直接進入活資料預覽
**Then** 左軌沒有這個表面，請求被拒絕，連線清單不回傳
**And** 管理者不需另被指派規劃設計師也看得到全部已登記連線

**Given** 某一筆具名連線失敗
**When** 操作者仍在看清單
**Then** 工作台與 MCP 宿主都仍在，其他連線仍可選

### Story 1.5: 確認前先看資料表與欄位

As a 規劃設計師,
I want 對選定的連線列出資料表，再列出欄位,
So that 我在取回業務列之前先確認結構。

**Acceptance Criteria:**

**Given** 已選定一個 MCP 具名連線
**When** 操作者瀏覽資料表與某個資料表的欄位
**Then** 工作台只發送 `GET /meta` 與 `GET /{resourceName}/fieldSet`，不呼叫 `tables` 或 `fields`
**And** 這些呼叫不產生填數清單，也不回傳業務列；每一欄至少有名稱、型別、是否主鍵、是否不可為空
**And** 成功的 Web API 結構呼叫各在畫面上留一筆軌跡，含操作者 `id`、連線名稱、請求種類以及列數；不含業務列與連線字串，也不寫入工作台 SQLite

**Given** 同一組瀏覽先對 `inventory`（`SqlServer`），再只把連線改成 `inventory_pg`（`PostgreSQL`）
**When** 再次列出資料表與欄位
**Then** 仍只走 Web API 的 `GET /meta` 與 `fieldSet`，不改呼叫表級 MCP 工具

**Given** 取數表面是 Web API
**When** 操作者做同樣的瀏覽
**Then** 工作台只發送 `GET /meta` 與 `GET /{resourceName}/fieldSet`

**Given** 連線上沒有資料表
**When** 清單返回
**Then** 清單是空的，畫面寫「沒有資料表。」

**Given** MCP 回傳 `unknown_table`、`connection_failed` 或 `invalid_arguments`
**When** 畫面顯示錯誤
**Then** 錯誤區塊第一行是宿主原錯誤碼，第二行是不含秘密的訊息，操作者仍可改選其他連線或資料表
**And** 這次結構呼叫在畫面上留一筆軌跡，含操作者 `id`、連線名稱、工具或請求種類與錯誤碼，不含業務列與連線字串，也不寫入工作台 SQLite；Blazor 寬度低於 1100px 時軌跡改到主欄下方，否則在主欄右側

### Story 1.6: 確認後才看到列

As a 規劃設計師,
I want 設定等值篩選並按下查詢之後才看到列,
So that 確認之前預覽頁沒有業務列。

**Acceptance Criteria:**

**Given** 操作者已選好連線、資料表與零個以上等值篩選，尚未按查詢
**When** 預覽頁開啟
**Then** 頁面寫「確認之前，這裡沒有業務列。」呼叫清單尚未執行

**Given** 取數表面是 MCP
**When** 操作者按下標成「查詢」的確認鈕
**Then** 清單是零鍵或單鍵 GET，不呼叫 `query`；確認後進入預覽頁，列等於該次 GET 結果
**And** 沒有符合的列時，畫面寫「沒有符合的列。」這是空結果，不是未處理錯誤

**Given** 取數表面是 Web API，篩選是零個或恰好一個
**When** 操作者確認
**Then** 零個篩選發送 `GET /{resourceName}`，一個篩選發送 `Key` 與 `Value`
**And** 兩個以上篩選時拒絕確認，說明「此表面只接受單鍵」，並且不發送該請求

**Given** 操作者把連線從 `inventory`（`SqlServer`）改到 `inventory_pg`（`PostgreSQL`），表與篩選不變
**When** 再次確認
**Then** 仍是 Web API 的零鍵或單鍵 GET，不改成 `query`

**Given** 預覽頁已有一筆成功的列，下一次查詢失敗
**When** 錯誤出現
**Then** 人留在預覽頁，先前成功的列留到下一次成功確認才換；`execution_failed` 不顯示部分列
**And** 畫面上沒有新增、更新、刪除或 SQL 輸入，也不發送 POST、PUT、DELETE

### Story 1.7: 在 MCP 上確認後看到計數

As a 規劃設計師,
I want 對同一張表與同一組等值篩選按下計數後看到筆數,
So that 工作台不會把全表讀回來自己數。

**Acceptance Criteria:**

**Given** 取數表面是 MCP，操作者已選好資料表與等值篩選
**When** 按下標成「計數」的確認鈕
**Then** 清單恰好一筆該已發布模組視圖，視圖名稱與參數鍵在換連線後不變；沒有單獨的計數按鈕，工作台不讀全表自己數
**And** 沒有符合的列時顯示 0，不是未處理錯誤

**Given** 同一組計數先對 `inventory`（`SqlServer`），再只把連線改成 `inventory_pg`（`PostgreSQL`），表與篩選不變
**When** 再次按下計數
**Then** 工具名稱仍是 `count`，參數鍵仍是連線、資料表與等值篩選

**Given** 取數表面是 Web API
**When** 操作者查看可執行動作
**Then** 沒有計數動作，工作台不以讀完全表的方式自己計算筆數

### Story 1.8: 這次查詢對得上 MCP 稽核

As a 規劃設計師,
I want 每一次填數呼叫都留在畫面上，而且成功的 MCP 查詢對得上宿主稽核,
So that 我能證明畫面上的列就是該次唯讀呼叫。

**Acceptance Criteria:**

**Given** 一次成功的 MCP 模組視圖呼叫
**When** 軌跡寫到畫面上
**Then** 含操作者 `id`、具名連線名稱、工具、等值篩選，以及列數或計數值；不含業務列與連線字串
**And** 該次 MCP 稽核找得到相同的呼叫者、連線名稱、工具、篩選與列數，稽核呼叫者等於操作者 `id`

**Given** MCP 因稽核寫不進去而回傳 `execution_failed`
**When** 工作台顯示結果
**Then** 畫面是該錯誤，不顯示部分列，工作台不寫第二份 MCP 稽核，軌跡也不寫入工作台 SQLite

**Given** 同一次填數走 Web API
**When** 軌跡顯示
**Then** 仍使用同一組欄位，並且不因此修改 WebService

## Epic 2: 排出不含資料列的範本

範本編輯師用標題、文字、參數、資料表、計數排出由上到下的範本，接受或忽略依欄位產生的建議，並把範本放進自己有設計角色的群組。重新打開時只有版面與繫結。管理者看得到全部群組；其他人只看到自己有角色的群組。沒有設計角色就不能改。

### Story 2.1: 建立一份不含資料列的範本

As a 範本編輯師,
I want 在自己有設計角色的群組建立一份範本,
So that 版面可以重複使用，而且裡面沒有這次的資料列。

**Acceptance Criteria:**

**Given** 帳號有範本編輯師，且對目標群組有設計角色
**When** 命名並保存新範本
**Then** 範本恰好屬於該群組，同一群組內名稱不重複
**And** 保存結果只有識別、群組、名稱與元件；沒有具名連線、連線字串、參數值或資料列

**Given** 操作者對目標群組沒有設計角色
**When** 把新範本保存到該群組
**Then** 被拒絕，不建立範本

**Given** 範本已保存，當時沒有可用連線
**When** 關閉後再打開
**Then** 仍看得到版面與繫結，畫面上沒有業務列

**Given** 帳號沒有範本編輯師、沒有終端使用者，也不是管理者
**When** 直接進入範本庫
**Then** 左軌沒有這個表面，範本清單不回傳

### Story 2.2: 用五種元件由上到下編排

As a 範本編輯師,
I want 用標題、文字、參數、資料表、計數排版，並能重排與刪除,
So that 報表順序就是閱讀順序。

**Acceptance Criteria:**

**Given** 設計者在編輯器加入元件
**When** 保存
**Then** `type` 只會是 `title`、`text`、`parameter`、`table`、`count`，沒有圖表
**And** 標題與文字保存字串；參數恰好一個欄位與一個標籤，不存值；MCP 範本的資料表欄來自所綁模組視圖的輸出欄；計數綁該視圖的一個輸出欄，不另發 `count`；Web API 範本的資料表仍綁一張表

**Given** 畫布上有多個元件
**When** 拖曳，或按上移、下移
**Then** `order` 從 0 連續，只改變由上到下的順序，不保存 x、y、寬、高
**And** 拖曳不是唯一的改序手段；畫布區塊沒有自由定位的選取框

**Given** 設計者刪除一個元件並保存
**When** 重新開啟
**Then** 保存結果不再包含該元件，其餘順序與繫結與上次保存相同

**Given** 帳號對該群組只有檢視角色
**When** 打開這份範本
**Then** 看得到元件，不能加入、重排、刪除或改繫結

### Story 2.3: 接受或忽略依欄位產生的建議

As a 範本編輯師,
I want 選定資料表後接受、修改或忽略建議,
So that 繫結來自剛看過的欄位，而不是一份含有資料列的草稿。

**Acceptance Criteria:**

**Given** 設計者選定資料表
**When** 工作台產生建議
**Then** 產生前不回傳業務列與參數值；MCP 範本的欄名全集等於該視圖的輸出欄，Web API 範本等於該次 `fieldSet`
**And** 建議中的欄名都存在於該次視圖輸出欄或 `fieldSet`，類型只會是五種元件，且不來自 `fields`

**Given** 建議的欄名都已知
**When** 設計者接受
**Then** 元件加到畫布末尾；修改後再接受時，畫布採用修改後的版本

**Given** 設計者忽略建議，或建議失敗，或建議含有未知欄名
**When** 畫布重新讀取
**Then** 未知欄名不能接受；忽略與失敗都不改變既有畫布；失敗時顯示可讀錯誤，填數呼叫次數是 0

### Story 2.4: 沒有角色的人看不到這份範本

As a 管理者,
I want 重新命名群組、重新命名或刪除範本，並在移除角色後讓對方看不到該群組,
So that 另一組範本不會出現在無權限的清單裡。

**Acceptance Criteria:**

**Given** 管理者重新命名群組
**When** 名稱保存
**Then** 群組識別不變，其中的範本仍屬於它

**Given** 設計者對該群組有設計角色，群組內已有一份範本
**When** 重新命名範本，或刪除這份範本
**Then** 新名稱在同一群組內不重複，範本仍屬於原群組；刪除後該群組的清單不再包含這份範本

**Given** 操作者原本對某群組有設計角色或檢視角色，管理者清掉這筆指派
**When** 該操作者再列出範本庫，或用原範本識別直接讀取
**Then** 清單不再包含該群組；直接讀取被拒絕，填數呼叫次數是 0

**Given** 設計者把範本移到另一個自己有設計角色的群組
**When** 移動完成
**Then** 範本只屬於新群組
**And** 移到沒有設計角色的群組會被拒絕，範本留在原群組

**Given** 群組內仍有範本
**When** 有人刪除該群組
**Then** 刪除被拒絕

**Given** 檢視角色嘗試建立、修改、刪除或移動範本
**When** 工作台處理該動作
**Then** 被拒絕

**Given** 一個非管理者沒有薪資群組的設計或檢視角色
**When** 他打開範本庫，或用該範本識別直接讀取
**Then** 清單裡沒有該範本；直接讀取被拒絕，填數呼叫次數是 0
**And** 管理者看得到該群組；管理者沒有設計角色時可以看見，但不能改其中的範本

## Epic 3: 用語句套版並帶走這次的報表

終端使用者用語句得到連線、範本與等值參數，確認後才看到填好的報表，存成自己的常用項目，並用這次預覽快照匯出 PDF、列印或匯出 CSV。關鍵字規則即可完成這次叫法；模型設定是同一條路徑上的加強。

### Story 3.1: 從範本確認後才填上這次的列

As a 終端使用者,
I want 對一份看得到的範本按下範本預覽，確認後才看到填好的報表,
So that 範本裡的參數值只活在這一次，不會寫回範本。

**Acceptance Criteria:**

**Given** 範本有資料表元件、計數元件與參數，操作者已補上每個參數的純量值，尚未確認
**When** 工作台列出呼叫清單
**Then** MCP 上恰好一筆該範本所綁的模組視圖，參數屬於該視圖已宣告的篩選；Web API 上是零鍵或單鍵 GET
**And** 確認鈕標成「範本預覽」；確認前預覽頁沒有業務列

**Given** 清單與範本、角色、表面能力仍然一致
**When** 操作者確認
**Then** 實際發出的填數呼叫與清單相同，沒有額外的模組視圖呼叫或 Web API 資料 GET；確認後進入預覽頁，焦點在該頁標題

**Given** 任一資料表缺少某個參數欄位，或 Web API 的篩選不是零個或一個，或 Web API 的範本含計數元件
**When** 操作者確認
**Then** 整次拒絕，填數呼叫次數是 0；含計數元件時不以讀取全表來產生計數

**Given** 清單中任一筆呼叫失敗
**When** 預覽顯示
**Then** 只有該錯誤，不用其他呼叫的舊列湊成一份成功報表

### Story 3.2: 語句未就緒時不去取列

As a 終端使用者,
I want 輸入語句後先看到連線、範本與參數，缺了就補齊,
So that 工作台在我確認之前不會去取業務列。

**Acceptance Criteria:**

**Given** 帳號有終端使用者，或帳號是管理者
**When** 打開交談套版並送出語句
**Then** 左軌有這個表面；沒有終端使用者、也不是管理者的帳號直接進入時被拒絕
**And** Enter 只送去解析，不確認填數；改選候選不是確認

**Given** 語句裡出現看得到的連線名稱、範本名稱，或參數的欄位名稱或標籤
**When** 關鍵字比對完成
**Then** 對到的連線與範本進入候選；參數值是其後第一個不以空白拆開的詞，全由數字組成時可作數字；對不到的不補猜
**And** 比對在 `Workbench.Core` 完成，不啟動側車，也不讀常用項目；管理者的候選範本來自全部群組，其他人只來自己有角色的範本；時間範圍只有在同一個詞能當某個參數的一個等值時才成為篩選，否則不另造區間查詢

**Given** 宿主重算後的狀態不是恰好一個連線、恰好一個範本、且每個參數都有純量值
**When** 畫面顯示意圖
**Then** 多個候選時列出候選，缺少參數時指出缺的欄位或標籤，對不到時說明對不到且候選為空
**And** 狀態以文字顯示 `ready`、`ambiguous` 或 `incomplete`；確認鈕不可按，該次填數次數是 0；解析失敗不改範本、常用項目與關鍵字規則

**Given** 畫面上有一層對話框
**When** 操作者按 Esc
**Then** 關閉最上層對話框，不確認填數；對話框只疊一層

### Story 3.3: 確認後只執行畫面上的填數呼叫

As a 終端使用者,
I want 意圖就緒後先看到將執行的呼叫，再按下確認,
So that 實際取回的列就是我剛同意的那一組呼叫。

**Acceptance Criteria:**

**Given** 宿主重算後的狀態是 `ready`
**When** 畫面列出呼叫清單
**Then** 清單由核心依該範本產生，規則與 Story 3.1 相同；確認鈕標成「確認」，而且可以按

**Given** 操作者按下確認
**When** 宿主執行前再核對清單識別、範本、角色與表面能力
**Then** 一致時實際呼叫集合與清單相同；不一致時整次拒絕，不部分執行，填數次數是 0

**Given** 確認成功
**When** 進入預覽頁
**Then** 頁上的表與計數來自該次唯讀呼叫；確認之前這一頁沒有業務列

### Story 3.4: 把這次叫法存成自己的常用項目

As a 終端使用者,
I want 把一次就緒的叫法存下來，之後用範本現在的版面再確認一次,
So that 明天不必重打語句，也不會看到昨天的舊列。

**Acceptance Criteria:**

**Given** 一次意圖已經就緒
**When** 操作者儲存常用項目
**Then** 項目含名稱、範本識別、取數表面、具名連線名稱、參數值與擁有者；不含資料列、連線字串與版面複本

**Given** 兩位操作者各自有常用項目
**When** 其中一人打開常用清單
**Then** 只回傳自己的項目；管理者看得到這個表面，但不能從產品介面讀到他人的常用項目
**And** 沒有終端使用者、也不是管理者的帳號沒有這個表面

**Given** 常用項目仍對得上範本與檢視角色
**When** 再次開啟
**Then** 工作台讀範本目前的版面與繫結，套上該筆連線與參數，重新產生清單；不查詞庫，不讀舊列；確認後才查詢

**Given** 範本已刪、操作者已失去檢視角色，或參數欄對不齊
**When** 再次開啟
**Then** 指出差異，停在未確認，填數呼叫次數是 0

### Story 3.5: 用這次預覽快照匯出與列印

As a 終端使用者,
I want 把眼前這份預覽匯出 PDF、列印，或把資料表匯出 CSV,
So that 帶走的數字就是剛剛確認的那一次，而不是重新查來的另一批。

**Acceptance Criteria:**

**Given** 預覽頁有這次快照
**When** 操作者匯出 PDF、列印，或把資料表元件匯出 CSV
**Then** 數字和文字等於該快照，包含空結果；過程不發出新的填數呼叫
**And** 三顆按鈕是次要按鈕，只在這一頁、且快照還在時可按；PDF 預設 A4，換頁以元件為界，不對位預印表單

**Given** 同一份快照連續匯出兩次
**When** 比較兩份 PDF 的業務數字
**Then** 數字相同

**Given** 操作者離開該次預覽，或同一操作者又確認了一次
**When** 用舊快照識別要求匯出
**Then** 舊快照與舊清單已刪除，匯出被拒絕；快照沒有閒置逾時，也不寫入 SQLite

### Story 3.6: 把一種說法存成可停用的關鍵字規則

As a 終端使用者,
I want 把對得到的說法存成規則，並在確認後才把模型的說法加進去,
So that 下次不用重教，也不會把這次的參數值寫進共用詞庫。

**Acceptance Criteria:**

**Given** 操作者新增一筆人為規則
**When** 規則寫入工作台 SQLite
**Then** 含說法、可省略的連線名稱、可省略的範本識別、參數欄名、來源 `human`、建立者與有效標記；沒有參數值、資料列、連線字串或金鑰
**And** 說法非空白，且連線、範本、參數欄名至少有一項；立刻有效；只能為自己看得到的範本加規則，只綁連線名稱時已登入即可新增

**Given** 一次意圖已確認
**When** 操作者再按一次「加入關鍵字」
**Then** 才寫入來源為 `ai` 的規則；說法可在寫入前修改，參數只留欄名
**And** 模型自行完成的解析不寫入規則；尚未確認時這個按鈕不寫入

**Given** 建立者或管理者把規則改為無效，或操作者現在看不到該範本
**When** 下一句話做關鍵字比對
**Then** 無效規則與現在看不到的範本都被略過

### Story 3.7: 管理員測試一份模型設定

As a 管理者,
I want 設定一份雲端或本地模型，並在拿它解析之前測試連線,
So that 金鑰不會出現在畫面上，測試也不會改到已經在用的設定。

**Acceptance Criteria:**

**Given** 只有管理者看得到模型設定與測試
**When** 非管理者直接進入
**Then** 請求被拒絕，不讀取也不修改模型設定

**Given** 有效設定只有一份，欄位是 `enabled`、`placement`、`endpoint`、`modelId`、`apiKey` 與兩個逾時
**When** 讀取設定
**Then** 回傳金鑰是否已設定，不回傳金鑰值；`model.json` 在內容根、不在 `wwwroot`
**And** 金鑰不進 SQLite、解析 JSON、工作台程序引數、日誌與測試結果；命令列能否覆寫 `apiKey` 仍是未決，這一則不把金鑰寫進程序引數

**Given** 管理者送出一份尚未儲存的草稿來測試
**When** 檢查通過並啟動側車
**Then** 不改 `model.json`；標準輸入只有 `kind` 為 `probe`、`placement`、`endpoint`、`modelId`；金鑰只放在子程序環境變數 `DBSERVICES_MODEL_API_KEY`
**And** 結果碼只會是 `model_ok`、`model_invalid_settings`、`model_unreachable`、`model_timeout`，訊息為繁體中文，不帶回模型回覆內文；探測不含語句、範本、連線名稱、資料列與資料庫連線字串

**Given** 檢查不通過，或 `enabled` 為 false
**When** 交談套版解析一句話
**Then** 檢查不通過時回 `model_invalid_settings` 且不啟動側車；未啟用時仍只走關鍵字規則；測試失敗不改範本、常用項目與關鍵字規則，填數次數是 0

### Story 3.8: 關鍵字還沒就緒時才請模型解析

As a 終端使用者,
I want 關鍵字對不出就緒意圖時，改由已啟用的模型補這一次解析,
So that 模型只補意圖，不另開一條查詢路徑。

**Acceptance Criteria:**

**Given** 模型已啟用，而且關鍵字比對後的狀態不是 `ready`
**When** 宿主呼叫側車
**Then** `DbServices.ModelSidecar` 由配接器啟動，標準輸入一筆 `kind` 為 `parse` 的 JSON，標準輸出一筆 JSON；輸入只有語句、看得到的連線名稱與 `ServerType`、有權限範本摘要，以及允許的取數表面
**And** 側車不組 SQL、不開資料庫連線、不寫檔，也不回傳可直接插入的規則命令；此功能不部署 Python

**Given** 側車回傳候選
**When** 宿主重算狀態
**Then** 不以側車自報的狀態當作可以確認的依據；候選超出這次輸入時整份作廢，填數次數是 0
**And** 解析使用已儲存的有效設定，不用測試草稿；預設解析逾時 30 秒，逾時則結束子程序並回 `model_timeout`

**Given** 關鍵字狀態已經是 `ready`，或模型未啟用，或側車失敗
**When** 這次解析結束
**Then** 已就緒時不呼叫側車；失敗時交談得到可讀錯誤，範本、常用項目與關鍵字規則不變，填數次數是 0

