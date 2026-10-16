---
id: SPEC-agent-data-plane
companions:
  - readonly-tools.md
  - glossary.md
  - later-surfaces.md
  - ../../planning-artifacts/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md
sources:
  - ../../../docs/planning/agent-data-plane/brief.md
  - ../../../docs/planning/agent-data-plane/prd.md
  - ../../../docs/planning/agent-data-plane/addendum.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# DBServices Agent 資料平面

## Why

整合方已經用 DBServices 接上 SQLite、SQL Server、MySQL、Oracle、PostgreSQL，但外部 Agent 仍沒有可發現的唯讀工具，容易改寫自由 SQL，或把連線字串帶進呼叫。要補的是這條可被外部 Agent 安全呼叫的資料平面。資料仍由現有 Session 與提供者執行。P0 只證明這組工具。對談、範本與敘事以後使用同一組工具，但不在這次的完成定義裡。

## Capabilities

- **CAP-1**
  - **intent:** 外部 Agent 能發現固定的唯讀工具，並在已支援的引擎上用相同名稱與參數查詢，而不必得知引擎種類。
  - **success:** 五個 `ServerType` 中，每一個有可連線測試庫者，都以相同工具名稱與參數鍵完成 `tables`、`fields`、`query`。客戶端不傳引擎種類。公開的 `IDbService` 與 `IDbServiceAsync` 簽名不變。既有 REST 與 GraphQL 路徑仍可呼叫，查詢形狀不變。

- **CAP-2**
  - **intent:** 呼叫者能對一個具名連線列出資料表與欄位、依整數 id 取一列、依等值條件查列、計數，並判斷列是否存在。
  - **success:** 回傳與同一條件下的既有 Session Call 一致。空列、計數 0、存在與否的布林都是成功，而不是未處理錯誤。各工具的參數、缺表與篩選規則見 `readonly-tools.md`。

- **CAP-3**
  - **intent:** 營運者只在伺服器組態登記具名連線；呼叫端用名稱選定資料來源，不能交出連線字串。
  - **success:** 工具輸入結構沒有連線字串欄位。參數帶有連線字串時得到 `invalid_arguments`，不依該字串建立連線，錯誤訊息不回顯該值。

- **CAP-4**
  - **intent:** 每一次呼叫使用恰好一個具名連線。
  - **success:** 已登記的名稱落在該連線。未登記的名稱得到 `unknown_connection`，且不改落到其他連線。省略名稱時，只在恰有一個組態完整的連線時採用它；零個或多個則得到 `connection_required`。

- **CAP-5**
  - **intent:** 某一個具名連線無法建立或無法開啟時，宿主與其他連線仍接受呼叫。
  - **success:** 該呼叫得到 `connection_failed`，程序不結束。其他連線的唯讀工具仍可呼叫。沒有任何組態完整的連線時，宿主仍能啟動並廣告工具。

- **CAP-6**
  - **intent:** 整合方能事後回答每一次工具呼叫是誰、對哪個具名連線、用了什麼條件、回了多少列或什麼錯誤。
  - **success:** 成功呼叫與被拒絕的呼叫都留下紀錄，含時間、呼叫者、連線名稱、工具名稱、參數，以及列數或錯誤碼。連線字串、密碼與結果列不在紀錄中，也不在宿主日誌中。紀錄寫不進去時，客戶端得到 `execution_failed`，且得不到本次的列、數字或布林。

- **CAP-7**
  - **intent:** 呼叫端不能經由此宿主執行新增、更新、刪除或自由 SQL。
  - **success:** 廣告清單沒有 `insert`、`update`、`delete`、`sql`。以這些名稱呼叫得到 `tool_disabled`，資料無變更。沒有確認後才寫入的入口。

## Constraints

- 本產品線不改寫公開的 `IDbService`／`IDbServiceAsync`，也不修改 Core、`FunctionRegistry` 或 Provider，不把 Core 做成會話伺服器。SQL、連線與方言留在現有 C# 路徑。
- REST、GraphQL 與 MCP 只經 `DbServiceFactory` 取得 `IDbService`。宿主不建立 Provider、不在工具裡分支引擎、不新增 SQL planner。
- P0 不部署 Python、不呼叫語言模型、不交付側車。日後側車的輸入與失敗邊界見 `later-surfaces.md`。
- 連線字串只存在伺服器組態與既有工廠。工具結構、錯誤訊息、稽核、宿主日誌與日後側車輸入都不攜帶它。結果列不寫進稽核或宿主日誌，也不外送到第三方。
- P0 的公開目錄是已發布模組視圖，不是六個表級 Session Call，也不是八條 REST 路由的包裝。REST 與 GraphQL 的路徑和查詢形狀維持不變。沒有已發布視圖時 Discovery 是空清單。
- P0 只提供 Streamable HTTP。日後若做 stdio，必須沿用同一套工具且不改工具參數。
- 沒有呼叫者身分時記為 `anonymous`，呼叫仍執行。P0 不加入驗證。信任邊界是營運者的網路，出貨時不依 host 名稱過濾。
- 等值篩選放在一個 `filters` 物件裡，不與 `table` 同層，避免欄位名與宿主參數碰撞。篩選值原樣送進 Session，不拼進 SQL。
- 關閉工具維持關閉，直到新的需求打開。P0 沒有設定開關可以把它們打開。
- P0 不新增健康檢查、指標，或稽核以外的第二個紀錄出口。不實作稽核保留天數；舊檔由營運者刪除。
- 錯誤是結構化物件，含穩定 `code` 與給人讀的中文 `message`。`message` 不含連線字串、密碼與堆疊中的秘密。碼的閉集、稽核欄位與結果編碼以架構骨架為準。
- 宿主不重算列，也不對 Session 的計數或布林做第二次彙總。

## Non-goals

- 對談查資料、結果匯出以外的報表版面、拖拉範本、對談套版。
- 結構的業務語言說明、結果敘事、跨來源簡報、需人確認的寫入提案。
- 像素級報表引擎、跨廠商 SQL planner、在產品內附帶或綁定語言模型。
- 改寫 WebService 的單一預設連線組態，或改變 REST、GraphQL 的路徑與查詢形狀。
- 修正 `docs/web/index.md` 的 Sqlite 敘述。
- stdio 宿主、完整授權模型，以及拒絕 `anonymous`。

## Success signal

驗收客戶端對一個可連線的具名連線完成 `tables` → `fields` → `query`，列集合與同一張表、同一組等值篩選下直接呼叫對應 Session Call 相同；將該腳本改對另一個有測試庫的 `ServerType` 執行時，工具名稱與參數鍵不變且成功。公開資料存取簽名的 diff 為空。對 `sql` 與 `insert` 的呼叫得到 `tool_disabled`，資料列數不變。抽樣的一筆成功與一筆拒絕，都能從稽核回答上述問題，且全文找不到該連線的連線字串。工具變多，或開放自由 SQL，不算達成。

## Open Questions

- Session 丟出例外（逾時、權限、方言錯誤），而不是回傳 `QueryResult.Kind` 為 `Error` 時，客戶端收到哪個 `code`，該次呼叫是否仍寫入稽核？已對應的領域失敗不得結束程序，訊息不得含連線字串；這個例外路徑還沒有採用的碼。
