---
id: SPEC-report-workbench
companions:
  - glossary.md
  - workbench-shapes.md
  - surfaces.md
  - stack.md
  - architecture-diagrams.md
sources:
  - ../../planning-artifacts/architecture/architecture-DBServices-2026-10-12/ARCHITECTURE-SPINE.md
  - ../../planning-artifacts/prds/prd-DBServices-2026-10-12/prd.md
  - ../../planning-artifacts/prds/prd-DBServices-2026-10-12/addendum.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# 報表工作台

## Why

業務人員已能用同一組唯讀工具讀取多種資料庫，但沒有自己的表面：把報表做成可重複使用的範本，用語句叫出來，確認後才填上當下的資料，再匯出或列印。報表工作台是這個表面，也是 MCP 與 Web API 之外的新同層客戶端。畫布與模型增加的是意圖、版面與繫結。連線字串留在伺服器。人確認之前不去取業務列。活資料預覽、編輯器與範本庫、交談套版與常用及匯出列印，三片都通過才算完成。

## Capabilities

- **CAP-1**
  - **intent:** 已登入的操作者才能使用工作台；管理員能建立操作者、建立與重新命名群組，並指派或移除某人在某群組的設計或檢視角色。
  - **success:** 沒有註冊端點。未帶有效工作階段的請求不回傳連線、欄位、列、範本或常用項目，也不呼叫 MCP。非管理員不能建立操作者、不能建立群組，也不能變更角色。管理員能重新命名群組。同一人可在不同群組有不同角色，對同一群組至多一筆。設計角色在該群組同時具備檢視能力。移除角色後，清單不再含該群組。儲存裡零個操作者時恰好建立一個管理員；已有操作者後不再讀初始設定。缺初始密碼且零個操作者時，不提供工作台功能。

- **CAP-2**
  - **intent:** 已登入操作者能在 MCP 與 Web API 之間選擇取數表面，看到可用具名連線的名稱與 ServerType，並瀏覽資料表與欄位。
  - **success:** 切換表面會換掉連線清單。清單、錯誤與後續請求都沒有連線字串欄位，操作者不能輸入連線字串。結構瀏覽不產生填數清單，也不回傳業務列。沒有資料表時清單是空的，畫面是空狀態。換到另一個 ServerType 的具名連線時，工具名稱與參數鍵不變，操作者不另選引擎。某連線失敗時，工作台與 MCP 宿主都仍在，其他連線仍可用，先前成功列不被這次錯誤覆蓋成未處理例外。GraphQL 不是取數表面。表面差異見 `surfaces.md`。

- **CAP-3**
  - **intent:** 操作者確認後能以等值篩選預覽列；在 MCP 上還能預覽同一條件的計數。預覽不提供寫入或自由 SQL。
  - **success:** 活資料預覽的確認清單恰好一筆 `query` 或一筆 `count`。無符合列時預覽是空結果，計數顯示 0，都不是未處理錯誤。預覽列與計數等於該次工具或 GET 的結果，不做第二次彙總。操作者無法提交 `insert`、`update`、`delete`、`sql`，或 Web API 的 POST、PUT、DELETE。MCP 的 `unknown_table`、`connection_failed`、`invalid_arguments`、`execution_failed` 以原碼與不含秘密的訊息顯示。錯誤後仍可改選其他連線或資料表。允許的請求見 `surfaces.md`。

- **CAP-4**
  - **intent:** 每一次結構呼叫與填數呼叫都在畫面上留下取數軌跡，使 MCP 的成功查詢能對上該次稽核。
  - **success:** 軌跡含操作者 id、具名連線名稱、工具或請求種類、等值篩選，以及列數、計數值或錯誤碼。不含業務列與連線字串。MCP 成功的 `query`，其呼叫者、連線名稱、工具、篩選與列數能在該次 MCP 稽核找到，且稽核呼叫者等於操作者 id。稽核寫入失敗而回 `execution_failed` 時，顯示該錯誤，不顯示部分列。Web API 沒有那份稽核檔，軌跡仍用同一組欄位，不因此修改 WebService。軌跡不寫入工作台 SQLite。

- **CAP-5**
  - **intent:** 具設計角色者能用五種元件排出由上到下的範本、接受或忽略依欄位產生的繫結建議，並把範本放進自己具有設計角色的群組。
  - **success:** 設計者可加入、重排與刪除元件。拖曳只改變由上到下的順序。刪除後，保存結果不再包含該元件。建議只做結構呼叫，不含業務列與參數值；至少一個資料表元件的欄名全集等於該次 `fields`。未知欄名不能接受。接受把元件加到畫布末尾；修改後再接受，畫布採用修改後的版本；忽略或建議失敗不改畫布。重新開啟時順序與繫結與上次保存相同，不需要連線也能看版面。要看到列必須另做預覽確認。同一群組內名稱唯一。移入自己沒有設計角色的群組會被拒絕，範本留在原處。群組內仍有範本時不能刪該群組。檢視角色不能建立、修改、刪除或移動範本。清單只含自己有角色的群組。以識別讀取或預覽無權限範本時拒絕，填數次數為 0。欄位見 `workbench-shapes.md`。

- **CAP-6**
  - **intent:** 操作者用語句從自己看得到的連線與範本得到意圖；就緒後先看到將執行的填數呼叫，確認後才執行，且實際呼叫與所列相同。
  - **success:** 未就緒、候選超出本次輸入、任一資料表缺少參數欄、或表面能力不符時，整次拒絕，不部分執行，填數次數為 0。改選候選不是確認。執行前再核對清單、範本、角色與表面能力。任一筆失敗時預覽只有該錯誤，不用其他呼叫的舊列湊成成功報表。解析失敗不改範本、常用項目與關鍵字規則。關鍵字比對不啟動側車、不讀常用項目；模型只在啟用且關鍵字狀態不是 ready 時才呼叫。宿主重算狀態。狀態、輸入與清單見 `workbench-shapes.md`。

- **CAP-7**
  - **intent:** 操作者能把一次就緒叫法存成自己的常用項目，之後以範本目前的版面重新確認再查；並能把該次預覽快照匯出 PDF、列印，以及把資料表元件匯出 CSV。
  - **success:** 常用清單只回擁有者自己的項目。管理員的維運備份可以接觸檔案，產品介面不讀他人的常用項目。再次開啟讀範本目前的版面，套上該筆連線與參數，重新產生清單，確認後才查，不查詞庫，不讀舊列。範本已刪、失去檢視角色，或參數欄對不齊時，指出差異，停在未確認，填數次數為 0。PDF、列印與 CSV 的數字和文字等於該次快照，包含空結果，且不發出新的填數呼叫。離開該次預覽，或同一操作者的下一次確認，舊快照與舊清單刪除，舊識別不能再匯出。同一份快照連續匯出，業務數字相同。PDF 預設 A4，換頁以元件為界，不對位預印表單。

- **CAP-8**
  - **intent:** 管理員能設定一份雲端或本地的模型，並在拿它解析之前測試連線；模型未啟用時，交談仍只走關鍵字規則。
  - **success:** 讀取 API 回傳金鑰是否已設定，不回傳金鑰值。只有管理員可測試。測試提交草稿，不要求先儲存，也不改 `model.json`。檢查不通過回 `model_invalid_settings` 且不啟動側車。結果碼只允許 `model_ok`、`model_invalid_settings`、`model_unreachable`、`model_timeout`，訊息為繁體中文，不帶回模型回覆內文。探測不含語句、範本、連線名稱、資料列與資料庫連線字串。解析不用草稿，只用有效設定。測試失敗不修改範本、常用項目與關鍵字規則，填數次數為 0。`enabled` 為 false 時不呼叫模型。預設探測逾時 10 秒、解析逾時 30 秒。設定欄位見 `workbench-shapes.md`。

## Constraints

- 工作台是獨立於 MCP 與 Web API 的同層進程，不建立 `IDbService`、不呼叫 `QuerySession`，也不修改 `IDbService`、`IDbServiceAsync`、`DbServices.Core`、`FunctionRegistry`、Provider，以及既有 REST 與 GraphQL 的路徑和查詢形狀。
- 填數、結構呼叫、範本讀寫、角色判斷、解析、軌跡與快照只經過 `Workbench.Core` 的端口；Blazor 在進程內呼叫，Avalonia 只經工作台 HTTP 轉入同一組端口，控制器不另寫業務判斷。
- 兩個殼與 `Workbench.Web` 都不參考配接器、`DbServices.Mcp`、`DBService`、Provider 或 `Microsoft.Extensions.AI`，也不持有 MCP 或 Web API 的基底網址；Core 不參考 ASP.NET、Avalonia 與模型套件。
- 部署恰好一個 `Workbench` 進程：只有它開啟工作台 SQLite，並在記憶體持有工作階段、確認清單與預覽快照；Avalonia 不嵌入 Blazor、不開啟該檔；不另開實例分攤這些物件。
- 殼的組態只有工作台基底網址；聽取位址來自宿主組態；工作台不讀 WebService 的組態檔，選項類別不宣告連線字串。
- Discovery 只列已發布模組視圖。工作台只呼叫該範本所綁的已發布模組視圖，傳輸維持 `POST /mcp` 與 ModelContextProtocol 2.2.0，並以操作者 id 填 `X-DBServices-Caller`；不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`、`insert`、`update`、`delete`、`sql`，也不在 `McpService` 加登入。
- `filters` 維持巢狀純量，不拼進 SQL，請求不帶 `connectionString`。
- 連線字串只留在 `McpService` 的 `Connections` 與 WebService 的 `ConnectionStrings`；名稱與 ServerType 只經 `GET /connections` 取得，該路由不是 MCP 工具、不在 `/mcp` 之下、不進廣告清單，且不回傳連線字串或是否可用。
- 工作台登入密碼只以 `PasswordHasher` 的 `passwordVerifier` 存放；模型 `apiKey` 只在模型組態；三者不進同一個可讀欄位；明文密碼不寫入 SQLite、日誌與軌跡；工作台不在業務庫建表。
- 工作階段是宿主記憶體中的一筆 `sessionId` 與操作者 id；cookie 與 bearer 都只帶 `sessionId`；登出刪除該筆使兩殼同時失效；重啟後清空。
- 模型不進入 Core 與 MCP 宿主；`IChatClient` 只在由 C# 啟動的 `DbServices.ModelSidecar`；側車不組 SQL、不開資料庫連線、不寫檔、不回傳可直接插入的規則命令；此功能不部署 Python。
- 關鍵字規則存在工作台 SQLite，只把說法對到連線、範本與參數欄名，不存參數值；AI 規則只在操作者確認該次意圖並明示加入後才寫入；比對略過操作者現在看不到的範本。
- MCP 錯誤碼與成功形狀原樣顯示；不重算列、不把空列改成錯誤；`unknown_table` 只沿用 `fields`；`query` 與 `count` 不另造缺表碼；不寫第二份 MCP 稽核，也不實作保留天數。
- 群組角色只約束範本；已登入操作者可對全部已登記具名連線做活資料預覽；不做資料表級或欄位級授權。
- 操作者可見的工作台文字、空狀態、確認清單與工作台自己的錯誤使用繁體中文；工作台錯誤是穩定英文蛇形碼加繁體中文訊息；MCP 訊息維持宿主原文；訊息不含連線字串、金鑰、密碼與堆疊秘密。軌跡對照使用 UTC。快照沒有閒置逾時。
- 三片都要通過才算完成，前一片是下一片的閘門，不是可單獨交付的產品。
- 工作台儲存在營運者自己的環境，不預設外送到第三方。某個業務連線失敗不停止宿主。`AllowedHosts` 出貨值為 `*`。TLS 由部署環境終止。應用不新增雲端供應商。
- 模型組態只有一份。覆寫順序是內容根的 `model.json`（不在 `wwwroot`）、Development 且有 `UserSecretsId` 時的 user secrets、環境變數、命令列。`apiKey` 不進 SQLite、解析 JSON、程序引數、日誌與測試結果。側車每次設定 `Endpoint`，不使用套件預設的 `https://api.openai.com/v1`。本地金鑰空白時，只在子程序內使用佔位憑證 `local`，不寫入設定檔。金鑰只放在子程序環境變數 `DBSERVICES_MODEL_API_KEY`。

## Non-goals

- 第二套查詢引擎、自由 SQL、區間條件、跨表 join，或跨廠商 SQL planner。
- 在瀏覽器、範本、常用項目、錯誤、軌跡或稽核放置連線字串或業務列。
- 像素級套印：預印表單、分欄帶、子報表、絕對座標。圖表、結果敘事、跨來源簡報、寫入提案。
- 把 GraphQL 或 CLI 當作 v1 的填數路徑。
- 以綁定單一模型廠商，或側車已上線，作為語句解析的完成條件。關鍵字規則達到 CAP-6 即算該行為完成。
- 修改資料平面 P0 的工具契約或公開資料存取簽名。
- 企業目錄、SSO、公開註冊，以及資料表級或欄位級授權。
- 沒有 OpenAI 相容 HTTP 的本地執行庫。要支援時先改側車決策，不在殼或 Core 另引一套 SDK。
- 把側車改回編譯 Python。若要改回，先改資料平面附錄與本規格的側車決策。

## Success signal

一份已保存的範本先對一個可連線的具名連線完成確認預覽，再只改連到另一個 ServerType 的具名連線，以相同參數再確認。兩次的 `query` 名稱與參數鍵不變，而且都得到該次工具回傳的列。抽樣的範本、常用項目、錯誤、取數軌跡與對應 MCP 稽核，全文都找不到連線字串與該次業務列。一句尚有多個候選或缺參數的語句，確認前的填數次數是 0；選定並確認後，實際呼叫集合等於畫面上列明的集合。開放 `sql`、掃全表算筆數、圖表、絕對座標，或綁定某個模型廠商，都不算達成。

## Open Questions

- MCP 稽核保留天數仍未定。工作台不另定，也不實作保留政策。
- PDF 與 CSV 的套件尚未選定。產生位置固定在宿主的該次快照上；選定時不得再查資料。
- 模型組態的覆寫順序包含命令列，同時規定 `apiKey` 不進入程序引數。命令列是否可以覆寫 `apiKey`，架構脊沒有分開寫死。
