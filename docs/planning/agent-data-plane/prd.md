---
title: DBServices Agent 資料平面
status: draft
created: 2026-10-11
updated: 2026-10-11
---

# PRD: DBServices Agent 資料平面

本 PRD 給產品、架構與後續故事拆解使用。定位與取捨見 [brief.md](brief.md)。技術邊界見 [addendum.md](addendum.md)。詞彙以第 3 節為準。功能需求只承諾 P0；P1 之後寫在第 10 節，沒有 FR 編號。推論標成 `[ASSUMPTION]`，並彙總在第 9 節。

## 1. Vision

DBServices 成為可被外部 Agent 安全呼叫的資料平面。整合方把已接上的業務庫，以具名連線交給 MCP 宿主；Agent 發現唯讀工具並查詢。資料仍由 `IQuerySession` 與現有提供者執行。

這個增量要證明的是工具面，不是報表產品。對談、範本與敘事都使用同一組工具，但它們不在 P0 的完成定義裡。現在做，是因為 Agent 已經會呼叫工具，而繼續讓每個專案直接組 SQL，會繞過已註冊的 Call，也無法稽核。

## 2. Target User

### 2.1 Jobs To Be Done

- 整合方要把既有 DBServices 連線交給外部 Agent，而不改公開的資料存取簽名。
- 整合方要 Agent 用固定工具查表、看欄位、依鍵值取列與計數，而不是生成自由 SQL。
- 整合方要換資料庫引擎時只改伺服器組態，不改 Agent 端的工具名稱。
- 整合方要事後回答某次查詢是誰、對哪個連線、用了什麼條件、回了多少列。

### 2.2 Non-Users (v1)

- 業務與分析人員。P0 沒有對談介面。
- 報表設計者。P0 沒有範本設計器。
- 需要 Agent 直接執行任意 SQL 或未確認寫入的呼叫端。

### 2.3 Key User Journeys

**UJ-1. 林可讓桌面 Agent 讀到庫存表。**

- **Persona + context：** 林可維護公司庫存系統的 DBServices 服務，庫在 SQL Server。她要把已上線的桌面 Agent 接到這座庫，又不想把連線字串放進 Agent 的提示。
- **Entry state：** MCP 宿主已啟動。伺服器組態裡有一個具名連線 `inventory`，`ServerType` 為 `SqlServer`。林可在 Agent 裡指到這個宿主。她已能操作 Agent，P0 不另做登入畫面。`[ASSUMPTION]` 呼叫者身分來自 MCP 傳輸層的客戶端識別；沒有識別時記為 `anonymous`，呼叫仍可執行。
- **Path：** Agent 列出工具，看到 `tables`、`fields`、`get`、`query`、`count`、`exists`。它對 `inventory` 呼叫 `tables`，再對 `products` 呼叫 `fields`，再以貨號鍵值呼叫 `query`。
- **Climax：** Agent 把列資料顯示給林可。她對得上在資料庫裡看到的同一批貨號。
- **Resolution：** 稽核留下這三次呼叫的連線名稱、工具、參數、列數與呼叫者。連線字串不在紀錄裡。
- **Edge case：** Agent 改呼叫 `insert`。宿主回傳 `tool_disabled`，資料表沒有新列。Agent 若在參數裡放連線字串，宿主拒絕且不建立連線。

**UJ-2. 林可把同一組工具接到 PostgreSQL。**

- **Persona + context：** 同一位林可，測試環境的庫存庫在 PostgreSQL。Agent 端的工具呼叫已經寫好。
- **Entry state：** 她只改宿主組態：另一個具名連線 `inventory_pg`，`ServerType` 為 `PostgreSQL`。
- **Path：** 她讓 Agent 對 `inventory_pg` 再走一次 `tables` → `fields` → `query`。
- **Climax：** 工具名稱與參數名稱不變，列資料來自 PostgreSQL。
- **Resolution：** 她不修改 Agent 的工具程式，也不修改 DBServices 的公開資料存取簽名。
- **Edge case：** `ServerType` 拼錯時，該連線不可用，宿主本身仍在。對該名稱的呼叫回傳可讀的連線錯誤，而不是未處理例外。

## 3. Glossary

- **資料平面** — Agent 用來讀取（未來才寫入）業務資料的工具面。執行仍落在既有 Session 與提供者。
- **MCP 宿主** — P0 新增的 API 層程式。它向 MCP 客戶端廣告工具、接收呼叫、轉給 Session Call、寫稽核。它不是新的資料庫引擎。
- **具名連線** — 伺服器組態中的一筆連線。包含名稱、`ServerType`、連線字串。工具只接受名稱。
- **ServerType** — 交給 `DbServiceFactory` 的引擎名稱：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。
- **Session Call** — `IQuerySession` 的 Call 路徑上已註冊的函數。P0 使用其中的唯讀子集。
- **唯讀工具** — `tables`、`fields`、`get`、`query`、`count`、`exists`。
- **關閉工具** — `insert`、`update`、`delete`、`sql`。P0 不廣告、不執行。
- **工具參數** — Agent 呼叫工具時傳入的引數。不得包含連線字串。
- **等值篩選** — `query`、`count`、`exists` 上除了 `table` 以外的鍵值。語意與現有函數相同：每個鍵是欄位名，值是等值條件。不是 SQL 片段。
- **稽核紀錄** — 一次工具呼叫的事後紀錄。見 FR-14。
- **呼叫者** — MCP 傳輸層提供的客戶端識別。沒有時為 `anonymous`。
- **報表範本** — 後續能力。描述版面、元件與參數繫結的文件，不含查詢結果。P0 不實作。
- **Python 側車** — 後續能力。編譯後由 C# 呼叫，負責語句解析、版面建議與敘事。P0 不部署。

## 4. Features

### 4.1 引擎無關的宿主

**Description：** MCP 宿主與 REST、GraphQL 一樣，只經 `DbServiceFactory` 取得 `IDbService`。工具與路由不綁定任何一種資料庫。實現 UJ-1、UJ-2。

**Functional Requirements：**

#### FR-1: 只經工廠建立資料服務

MCP 宿主在建立任何資料存取時，使用 `DbServiceFactory.Create`（或其既有等價入口），由 `ServerType` 決定引擎。

**Consequences (testable)：**

- 組態中的五個 `ServerType` 名稱都能得到可用的 `IDbService`（連線字串為該引擎可接受的值，且資料庫可連線）。
- 未知的 `ServerType` 不建立服務，並產生可讀錯誤。
- 比對 P0 開發起點，公開的 `IDbService` 與 `IDbServiceAsync` 方法簽名沒有變更。
- 既有 REST 路由與 GraphQL 端點的路徑維持可呼叫。P0 不改它們的查詢形狀。

#### FR-2: 工具契約不隨引擎改變

同一組唯讀工具的名稱與參數，在五個 `ServerType` 上相同。

**Consequences (testable)：**

- UJ-2 的 `tables`、`fields`、`query` 不因從 `SqlServer` 換成 `PostgreSQL` 而改名或改參數鍵。
- 宿主不要求 Agent 傳入引擎種類。引擎只出現在伺服器組態。

### 4.2 唯讀工具

**Description：** 宿主把既有唯讀 Session Call 廣告成 MCP 工具。`query` 的多鍵等值篩選沿用函數註冊表現況；這比 REST 的單一 `Key`／`Value` 寬，仍不是自由 SQL。實現 UJ-1。

**Functional Requirements：**

#### FR-3: 預設工具清單

Agent 可列出宿主目前廣告的工具。預設清單恰好是六個唯讀工具。

**Consequences (testable)：**

- 清單為 `tables`、`fields`、`get`、`query`、`count`、`exists`。
- 清單不含 `insert`、`update`、`delete`、`sql`。
- 每個工具的說明表明用途，且不承諾接受 SQL 語句。

#### FR-4: tables

呼叫者可列出一個具名連線上的資料表名稱。

**Consequences (testable)：**

- 參數 `includeView` 可省略；省略時與現有函數相同，視為包含檢視。
- 回傳名稱清單。連線上沒有資料表時回傳空清單，而不是未處理例外。

#### FR-5: fields

呼叫者可取得指定資料表的欄位。

**Consequences (testable)：**

- 必填 `table`。
- 每一欄至少包含名稱、型別、是否主鍵、是否不可為空。這些對應現有 `fields` 回傳的 `name`、`type`、`pk`、`notNull`。
- 資料表不存在時回傳結構化錯誤。

#### FR-6: get

呼叫者可依整數 Id 取一列。

**Consequences (testable)：**

- 必填 `table` 與整數 `id`。
- 沒有該列時回傳空結果，HTTP 或傳輸層不表現成未處理錯誤。
- 非整數主鍵不在 P0 擴充範圍。此行為跟隨現有 `get`。

#### FR-7: query

呼叫者可依等值篩選取列。

**Consequences (testable)：**

- 必填 `table`。其餘參數為等值篩選，可同時多個。
- 篩選值以參數傳遞，不把篩選值串進 SQL 文字。
- 沒有符合列時回傳空結果。

#### FR-8: count

呼叫者可取得符合等值篩選的列數。

**Consequences (testable)：**

- 必填 `table`。其餘參數為等值篩選，可省略。
- 回傳一個數值。無符合列時為 0。

#### FR-9: exists

呼叫者可判斷是否存在符合等值篩選的列。

**Consequences (testable)：**

- 必填 `table`。其餘參數為等值篩選，可省略。
- 回傳布林值。

### 4.3 具名連線

**Description：** 連線字串只活在伺服器。Agent 用名稱選定資料來源。實現 UJ-1 的秘密邊界與 UJ-2 的換庫。`[ASSUMPTION]` MCP 宿主使用自己的具名連線清單，不強制改寫 WebService 現有的單一 `ConnectionStrings:default` 形狀。

**Functional Requirements：**

#### FR-10: 組態中的具名連線

營運者在伺服器組態登記一筆以上具名連線。每筆包含名稱、`ServerType`、連線字串。

**Consequences (testable)：**

- 只有伺服器進程讀得到連線字串。工具的輸入結構沒有連線字串欄位。
- 至少一筆合法連線時，宿主可以啟動並廣告工具。

#### FR-11: 拒絕把連線字串當參數

工具參數不得用來提供連線字串。

**Consequences (testable)：**

- 參數含連線字串欄位，或呼叫端要求宿主改用參數內的連線字串時，宿主拒絕，且不依該字串建立連線。
- 錯誤訊息不回顯那條字串。

#### FR-12: 名稱解析

每次呼叫綁定到恰好一個具名連線。

**Consequences (testable)：**

- 參數給了已登記的名稱時，查詢落在該連線。
- 名稱未登記時拒絕，不改落到其他連線。
- `[ASSUMPTION]` 未帶名稱時：恰有一個具名連線則用該連線；零個或多個則回傳結構化錯誤，要求指定名稱。

#### FR-13: 單一連線失敗不拖垮宿主

某一個具名連線無法初始化或無法連線時，宿主維持可用。

**Consequences (testable)：**

- 其他具名連線的唯讀工具仍可呼叫。
- 失敗連線上的呼叫回傳連線錯誤，程序不結束。

### 4.4 稽核

**Description：** 每一次工具呼叫留下可事後閱讀的紀錄，供 UJ-1 的收尾使用。`[ASSUMPTION]` 紀錄寫入宿主本機可查的儲存；保留天數未定。

**Functional Requirements：**

#### FR-14: 呼叫紀錄

宿主對每一次工具呼叫寫入一筆稽核紀錄。

**Consequences (testable)：**

- 紀錄含時間、呼叫者、連線名稱、工具名稱、參數、結果列數或錯誤碼。
- 連線字串與密碼不出現在紀錄中。
- 被 FR-11、FR-15 拒絕的呼叫也有紀錄，結果為對應錯誤碼。
- 沒有傳輸層識別時，呼叫者記為 `anonymous`。

### 4.5 關閉寫入與自由 SQL

**Description：** P0 的廣告與執行路徑只有唯讀工具。實現 UJ-1 的拒絕寫入邊界。

**Functional Requirements：**

#### FR-15: 關閉工具不可執行

`insert`、`update`、`delete`、`sql` 在 P0 不執行。

**Consequences (testable)：**

- 它們不在 FR-3 的清單中。
- 若客戶端仍以這些名稱呼叫，回應為穩定錯誤碼 `tool_disabled`，資料無變更。
- 不提供「確認後寫入」的介面。那是第 10 節的寫入提案。

**Out of Scope：**

- 以設定開關打開關閉工具。
- 寫入前的人工確認流程。

## 5. Non-Goals (Explicit)

- 不把 Core、Provider 或公開的 `IDbService`／`IDbServiceAsync` 簽名改寫成另一種語言或另一套 API。
- 不自製跨廠商 SQL planner，也不把自由 `sql` 開放給 Agent。
- 不在產品內附帶或綁定單一語言模型。
- 不做像素級報表引擎。
- 不在 P0 交付對談介面、報表範本、Python 側車。
- 不把 REST 的八條路由當成 MCP 工具全集。REST 維持單鍵查詢與整數 Id。
- 不讓工具參數攜帶連線字串。

## 6. MVP Scope

### 6.1 In Scope

- FR-1 至 FR-15。
- 外部 MCP 客戶端能完成 UJ-1 與 UJ-2。
- 五個 `ServerType` 的工具契約一致。有可連線測試庫的引擎都要跑通 UJ-1 的三步（`tables`、`fields`、`query`）。

### 6.2 Out of Scope for MVP

- **P1 對談查資料與產表。** 理由：P0 要先固定工具契約。`[NOTE FOR PM]` 這是使用者原述的第二條產品線，P0 驗收後優先排入。
- **P2 拖拉範本設計。** 理由：範本繫結的是工具結果，工具未穩定前沒有繫結對象。
- **P3 對談套版。** 理由：依賴 P1 與 P2。
- **結構說明、結果敘事、跨來源簡報、寫入提案。** 理由：都消費既有工具或需要人確認寫入，不阻擋唯讀驗收。
- **Python 側車。** 理由：P0 沒有語句、版面或敘事要交給模型。
- **完整登入與授權。** 理由：P0 用傳輸層識別寫入稽核。是否拒絕 `anonymous` 見第 8 節。
- **修正 [docs/web/index.md](../../web/index.md) 的 Sqlite 敘述。** 理由：那是文件漂移，不改變 P0 行為。附錄記錄，文件另案更新。

## 7. Success Metrics

**Primary**

- **SM-1：** 驗收客戶端對一個可連線的具名連線完成 `tables` → `fields` → `query`，並得到與直接呼叫對應 Session Call 相同的列集合（同一張表、同一組等值篩選）。驗證 FR-3、FR-4、FR-5、FR-7。
- **SM-2：** 將 SM-1 的腳本改對另一個 `ServerType` 的具名連線執行，工具名稱與參數鍵不變且成功。五個工廠名稱中，每一個有測試庫者都通過。驗證 FR-1、FR-2。

**Secondary**

- **SM-3：** P0 合併範圍內，`IDbService` 與 `IDbServiceAsync` 的公開方法簽名 diff 為空。驗證 FR-1。
- **SM-4：** 預設工具清單等於六個唯讀工具；對 `sql` 與 `insert` 的呼叫得到 `tool_disabled`，資料列數不變。驗證 FR-3、FR-15。
- **SM-5：** 抽樣一筆成功查詢與一筆拒絕查詢，稽核含第 FR-14 所列欄位，且全文搜尋不到該連線的連線字串。驗證 FR-11、FR-14。

**Counter-metrics (do not optimize)**

- **SM-C1：** 不以工具總數、可執行的 SQL 種類，或寫入是否開放，作為完成度。這會把 SM-1 做成「開放 `sql` 讓查詢更容易成功」。
- **SM-C2：** 不以在宿主內複製五段引擎專用 SQL 來換取 SM-2。引擎差異留在既有 Provider。

## 8. Open Questions

1. 稽核紀錄的保留天數與存放媒體。P0 只要求本機可查。`[ASSUMPTION]` 見 FR-14。
2. 是否在 P0 拒絕 `anonymous`。目前假設放行並記入稽核。
3. 傳輸要先做 stdio、HTTP，或兩者同時。完成定義是外部 MCP 客戶端能走完 UJ-1，不在本 PRD 指定傳輸。
4. 具名連線清單與 WebService 單一 `ConnectionStrings:default` 要不要在實作時共用同一個組態區段。產品要求是 MCP 能表達多個名稱；不要求這一版改 REST 的組態。

## 9. Assumptions Index

- 第 2.3 節 UJ-1 — 呼叫者來自傳輸層識別；缺省為 `anonymous` 且仍可呼叫。
- 第 4.3 節 — MCP 具名連線組態獨立於 WebService 現有的單一預設連線，不強制同時改 REST 組態。
- FR-12 — 未指定連線名稱時，僅在恰有一個具名連線時自動選用。
- FR-14 — 稽核先寫入宿主本機可查的儲存，保留天數未定。
- 第 6.2 節與第 10 節 — 對談、範本、敘事、跨來源簡報、寫入提案、Python 側車都不在 P0 完成定義。
- 第 1 節 — P0 的 Agent 是外部客戶端。產品不附帶模型。

## 10. 後續能力

以下沒有 FR 編號。P0 驗收後才拆故事。順序沿用 brief 的地圖。

- **P1 對談查資料與產表。** 人在對談介面說一句查詢。解析結果是一個具名連線、一個唯讀工具、一組等值篩選。執行仍走 MCP 工具。此階段的報表是結果表與匯出。
- **P2 拖拉範本設計。** 人用元件與參數組成報表範本。範本保存版面與欄位繫結，不保存資料列。協助設計的步驟可依 `fields` 的結果建議元件與繫結。
- **P3 對談套版。** 一句話解析成具名連線、一份報表範本、一組參數。人確認後才填入查詢結果。
- **結構說明。** Agent 依 `tables` 與 `fields` 用業務語言描述這座庫，不另發 SQL。
- **結果敘事。** 列資料返回後，產生摘要與異常提示。數字只來自該次工具結果。
- **跨來源簡報。** 對多個具名連線各做唯讀呼叫，再寫成一份簡報。不新增跨廠商 SQL planner。
- **寫入提案。** Agent 草擬 `insert` 或 `update` 的參數。人確認後才執行。在此之前 FR-15 維持關閉。

P1 起若需要語言模型，使用 addendum 所述的 Python 側車。側車不執行 SQL，不讀取連線字串。

## 11. Cross-Cutting NFRs

- 工具的錯誤是結構化物件，至少含穩定的 `code` 與給人讀的 `message`。`message` 不含連線字串、密碼與堆疊中的秘密。
- P0 不在宿主內改寫查詢或做第二次彙總。列資料與計數來自既有 Session Call。
- 未知工具、關閉工具、未知連線、未知資料表，都走結構化錯誤，不讓未處理例外結束宿主進程。

## 12. Constraints and Guardrails

- **資料邊界：** 連線字串只存在伺服器組態與既有連線工廠。工具結構、錯誤、稽核不攜帶它。
- **執行邊界：** 唯讀工具的語意等於今天的 Session Call。P0 不新增查詢語言。
- **相容邊界：** 公開資料存取簽名與既有 REST、GraphQL 路徑保持可呼叫。
- **模型邊界：** P0 不呼叫語言模型，也不部署 Python 側車。

## 13. Data Governance

P0 的治理是最小集合：預設唯讀、連線秘密留在伺服器、每次呼叫可追溯到連線名稱與呼叫者。資料分類、保存年限、正式授權模型未決，見第 8 節。在那些決定之前，不把業務列資料寫進日誌的自由文字以外的第三方服務。`[ASSUMPTION]` 稽核存放在營運者自己的宿主上，不預設外送到別的分析平台。

## 14. API Contracts / Public Surface

- 新增的公開表面是 MCP 工具，名稱等於第 3 節的唯讀工具。
- 既有 REST 與 GraphQL 為相容表面。P0 不變更其路徑與查詢形狀。
- 既有 `IDbService`／`IDbServiceAsync` 為相容表面。簽名變更視為破壞 SM-3。
- 關閉工具若將來開放，必須以新的 FR 打開，而不是在 P0 實作中留下可呼叫的隱藏路徑。
