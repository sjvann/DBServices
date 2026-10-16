---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - docs/planning/agent-data-plane/prd.md
  - docs/planning/agent-data-plane/brief.md
  - docs/planning/agent-data-plane/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md
---

# DBServices Agent 資料平面 - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for DBServices Agent 資料平面, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR-1: MCP 宿主建立資料存取時只使用 `DbServiceFactory.Create`（或其既有等價入口），由 `ServerType` 決定引擎。五個名稱在連線字串可接受且資料庫可連線時都能得到 `IDbService`。未知 `ServerType` 不建立服務並產生可讀錯誤。對照 P0 起點，公開的 `IDbService` 與 `IDbServiceAsync` 方法簽名沒有變更。既有 REST 路由與 GraphQL 端點路徑維持可呼叫，查詢形狀不改。

FR-2: 同一組唯讀工具的名稱與參數，在五個 `ServerType` 上相同。宿主不要求 Agent 傳入引擎種類。引擎只出現在伺服器組態。

FR-3: Agent 的 Discovery 不列出表級唯讀工具。公開清單只含已發布模組視圖。沒有已發布視圖時，Discovery 是空清單，不回退成六個表級工具。清單不含 `tables`、`fields`、`get`、`query`、`count`、`exists`，也不含 `insert`、`update`、`delete`、`sql`。每個已發布視圖的說明表明用途，且不承諾接受 SQL 語句。

FR-4 至 FR-9，以及 Story 1.2、1.3、1.4、1.5、1.7，描述營運除錯路徑上的表級行為，不是 Agent 的 Discovery。除錯路徑的開關形式留給架構。除錯路徑未開啟時，這些名稱在公開端點上的結果遵守 Story 1.1：回應碼為 `unknown_tool`。外部 Agent 不得用它們代替尚未發布的模組視圖。

FR-4: 呼叫者可列出一個具名連線上的資料表名稱。參數 `includeView` 可省略；省略時與現有函數相同，視為包含檢視。回傳名稱清單。連線上沒有資料表時回傳空清單，而不是未處理例外。

FR-5: 呼叫者可取得指定資料表的欄位。必填 `table`。每一欄至少包含名稱、型別、是否主鍵、是否不可為空，對應現有 `fields` 的 `name`、`type`、`pk`、`notNull`。資料表不存在時回傳結構化錯誤。

FR-6: 呼叫者可依整數 Id 取一列。必填 `table` 與整數 `id`。沒有該列時回傳空結果，傳輸層不表現成未處理錯誤。非整數主鍵不在 P0 擴充範圍。

FR-7: 呼叫者可依等值篩選取列。必填 `table`。其餘等值篩選可同時多個。篩選值以參數傳遞，不把篩選值串進 SQL 文字。沒有符合列時回傳空結果。

FR-8: 呼叫者可取得符合等值篩選的列數。必填 `table`。其餘等值篩選可省略。回傳一個數值。無符合列時為 0。

FR-9: 呼叫者可判斷是否存在符合等值篩選的列。必填 `table`。其餘等值篩選可省略。回傳布林值。

FR-10: 營運者在伺服器組態登記一筆以上具名連線。每筆包含名稱、`ServerType`、連線字串。只有伺服器進程讀得到連線字串。工具的輸入結構沒有連線字串欄位。至少一筆合法連線時，宿主可以啟動並廣告工具。

FR-11: 工具參數不得用來提供連線字串。參數含連線字串欄位，或呼叫端要求宿主改用參數內的連線字串時，宿主拒絕，且不依該字串建立連線。錯誤訊息不回顯那條字串。

FR-12: 每次呼叫綁定到恰好一個具名連線。已登記的名稱查詢落在該連線。名稱未登記時拒絕，不改落到其他連線。未帶名稱時：恰有一個具名連線則用該連線；零個或多個則回傳結構化錯誤，要求指定名稱。

FR-13: 某一個具名連線無法初始化或無法連線時，宿主維持可用。其他具名連線的唯讀工具仍可呼叫。失敗連線上的呼叫回傳連線錯誤，程序不結束。

FR-14: 宿主對每一次工具呼叫寫入一筆稽核紀錄。紀錄含時間、呼叫者、連線名稱、工具名稱、參數、結果列數或錯誤碼。連線字串與密碼不出現在紀錄中。被 FR-11、FR-15 拒絕的呼叫也有紀錄。沒有傳輸層識別時，呼叫者記為 `anonymous`。

FR-15: `insert`、`update`、`delete`、`sql` 在 P0 不執行。它們不在 FR-3 的清單中。若客戶端仍以這些名稱呼叫，回應為穩定錯誤碼 `tool_disabled`，資料無變更。不提供確認後寫入的介面。

### NonFunctional Requirements

NFR-1: 工具的錯誤是結構化物件，至少含穩定的 `code` 與給人讀的 `message`。`message` 不含連線字串、密碼與堆疊中的秘密。

NFR-2: 未知工具、關閉工具、未知連線、未知資料表，都走結構化錯誤，不讓未處理例外結束宿主進程。

NFR-3: 連線字串只存在伺服器組態與既有連線工廠。工具結構、錯誤、稽核不攜帶它。

NFR-4: 唯讀工具的語意等於今天的 Session Call。P0 不新增查詢語言，不在宿主內重算列或做第二次彙總。`fields` 為了缺表判斷而先呼叫 `tables` 是架構允許的唯一額外呼叫，不改變列資料本身。

NFR-5: 公開資料存取簽名與既有 REST、GraphQL 路徑保持可呼叫。

NFR-6: P0 不呼叫語言模型，也不部署 Python 側車。

NFR-7: 預設唯讀。每次呼叫可追溯到連線名稱與呼叫者。稽核放在營運者自己的宿主上，不把業務列資料外送到別的分析平台。P0 不做完整登入；沒有呼叫者身分時記為 `anonymous` 且仍可呼叫。

### Additional Requirements

- 棕地。沒有入門範本。P0 新增兩個專案：ASP.NET Core 宿主 `McpService`，以及不含 ASP.NET 型別的 `DbServices.Mcp`。第一個實作故事是這個宿主骨架，不是套用外部 starter。
- 目標框架 `net10.0`。套件 `ModelContextProtocol.AspNetCore` 2.2.0 與 `ModelContextProtocol` 2.2.0。
- P0 傳輸只有 Streamable HTTP。`MapMcp` 路徑 `/mcp`。`SessionMode` 為 `Stateless`。舊版 SSE 關閉。不加入 OpenAPI、Scalar 或 CORS。
- 聽取位址來自宿主組態，不在程式裡寫死。`AllowedHosts` 出貨值為 `*`。
- 新專案不參考 `DbServices.Provider.*`。資料服務只來自 `DbServiceFactory.Create`。執行只走既有 `QuerySession.Execute`，opcode 為 `Call`。
- 六個工具進入同一個調度器。工具參數使用 `connectionId`。`query`、`count`、`exists` 的等值條件放在 `filters` 物件，呼叫 Session 前撲平，並去掉 `connectionId`。
- 組態是宿主自己的 `Connections` 字典，鍵為 `connectionId`，值只有 `ServerType` 與 `ConnectionString`。不讀 WebService 的 `ConnectionStrings:default`。
- `IDbService` 只在建立成功後快取。失敗不快取，程序不結束。
- 呼叫者只讀標頭 `X-DBServices-Caller`。
- 錯誤碼只允許 `tool_disabled`、`unknown_tool`、`unknown_connection`、`connection_required`、`connection_failed`、`invalid_arguments`、`unknown_table`、`execution_failed`。訊息為中文。Session 回傳 `QueryResult.Kind` 為 `Error` 時，回應碼是 `execution_failed`，不是空列成功，也不是 `unknown_table`。稽核那一行寫不進去時同樣是 `execution_failed`，且不回傳本次的列、數字或布林。這些失敗不讓程序結束。
- 參數名 `connectionString` 或 `connectString`（不區分大小寫）視為 `invalid_arguments`，不建立連線，訊息不回顯該值。
- `fields` 先以 `includeView` true 呼叫 `tables`。名稱以不區分大小寫的序數比較，不在清單中回 `unknown_table` 且不再呼叫 `fields`。這次內部呼叫不另寫稽核。`get`、`query`、`count`、`exists` 不先查表名。
- 成功結果：`tables`、`fields`、`get`、`query` 回傳 `columns` 與 `rows`；空列是成功。`count` 回傳一個 JSON 數字。`exists` 回傳一個 JSON 布林。
- 六個工具的 MCP 註記都設唯讀，不設破壞性。
- 稽核是單一 JSONL 檔。預設 `audit/mcp.jsonl`，可用 `Audit:Path` 覆寫。鍵固定為 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。時間為 UTC ISO-8601。不寫列資料與連線字串。並行追加不得把一行寫斷。保留天數不在 P0。
- P0 不實作 stdio 宿主、授權模型、側車、報表範本、確認後寫入、TLS 憑證。這些留在架構的 Deferred，不拆成 P0 故事。

### UX Design Requirements

無。P0 沒有使用者介面，也沒有 UX 設計契約。P1 對談介面開始才做 UX 設計。

### FR Coverage Map

FR-1: Epic 1 - Story 1.1、Story 1.2、Story 1.7
FR-2: Epic 1 - Story 1.7
FR-3: Epic 1 - Story 1.1
FR-4: Epic 1 - Story 1.2
FR-5: Epic 1 - Story 1.3
FR-6: Epic 1 - Story 1.4
FR-7: Epic 1 - Story 1.5
FR-8: Epic 1 - Story 1.5
FR-9: Epic 1 - Story 1.5
FR-10: Epic 1 - Story 1.1、Story 1.2
FR-11: Epic 1 - Story 1.2、Story 1.5
FR-12: Epic 1 - Story 1.2
FR-13: Epic 1 - Story 1.2、Story 1.7
FR-14: Epic 1 - Story 1.6
FR-15: Epic 1 - Story 1.1
FR-16: Epic 2 - Story 2.1
FR-17: Epic 2 - Story 2.1
FR-18: Epic 2 - Story 2.3
FR-19: Epic 2 - Story 2.2
FR-20: Epic 2 - Story 2.4
FR-21: Epic 2 - Story 2.5

## Epic List

### Epic 1: 外部 Agent 安全讀取具名業務庫

整合方啟動宿主並登記具名連線之後，外部 Agent 發現的是已發布模組視圖，並完成該視圖的唯讀呼叫。寫入與自由 SQL 被拒絕。表級 Call 不是這則史詩的公開成功路徑。事後能回答是誰、對哪個連線、用了什麼條件、回了多少列。換引擎只改伺服器組態。

**FRs covered:** FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-11, FR-12, FR-13, FR-14, FR-15

FR-16 至 FR-21 不在這則史詩，見 Epic 2。

### Epic 2: 人手寫的模組視圖成為 Agent 的公開目錄

營運者放入一條已寫好的模組視圖，人確認解說後才發布。外部 Agent 的 Discovery 只有這份視圖與它的解說，帶上視圖自己的參數取得列。執行仍是唯讀，不在來源庫建立檢視表。沒有拖拉設計器。

**FRs covered:** FR-16, FR-17, FR-18, FR-19, FR-20, FR-21

## Epic 1: 外部 Agent 安全讀取具名業務庫

整合方啟動宿主並登記具名連線之後，外部 Agent 發現的是已發布模組視圖，並完成該視圖的唯讀呼叫。寫入與自由 SQL 被拒絕。表級 Call 不是這則史詩的公開成功路徑。事後能回答是誰、對哪個連線、用了什麼條件、回了多少列。換引擎只改伺服器組態。

### Story 1.1: 啟動宿主，Discovery 只列出已發布模組視圖

As a 整合方,
I want 外部 Agent 列出工具時只看見已發布模組視圖，寫入與自由 SQL 被拒絕,
So that Agent 在還沒查資料之前就不會掃表，也不會走到一條無法稽核的寫入路徑。

**Acceptance Criteria:**

**Given** 方案裡新增 `McpService`（`net10.0` 的 ASP.NET Core 宿主）與 `DbServices.Mcp`（不參考 ASP.NET 與任何 `DbServices.Provider.*`）
**When** 宿主以 `ModelContextProtocol.AspNetCore` 2.2.0 啟動，`MapMcp` 在 `/mcp`，`SessionMode` 為 `Stateless`，舊版 SSE 關閉
**Then** 不加入 OpenAPI、Scalar 或 CORS，聽取位址來自組態，`AllowedHosts` 為 `*`
**And** 公開的 `IDbService` 與 `IDbServiceAsync` 簽名沒有變更，既有 REST 與 GraphQL 路徑仍可呼叫

**Given** 組態的 `Connections` 至少有一筆含已知 `ServerType` 與連線字串
**When** 外部 MCP 客戶端列出工具
**Then** 清單的名稱集合等於已發布模組視圖的名稱集合，不多不少
**And** 清單不含 `tables`、`fields`、`get`、`query`、`count`、`exists`，也不含 `insert`、`update`、`delete`、`sql`
**And** 沒有已發布視圖時清單是空的，不回退成六個表級工具
**And** 每一筆說明表明用途、不承諾接受 SQL，且已發布視圖都標成唯讀、不標成破壞性

**Given** 宿主已啟動
**When** 客戶端呼叫 `insert`、`update`、`delete` 或 `sql`
**Then** 回應碼為 `tool_disabled`，中文 `message` 不含連線字串、密碼或堆疊秘密，資料沒有變更，程序不結束
**And** 呼叫未出現在 Discovery、且不是上述四個關閉名稱的工具時，回應碼為 `unknown_tool`，程序不結束

**Given** 某一筆連線的 `ServerType` 不在五個已知名稱裡
**When** 宿主啟動
**Then** 該筆標成不可用，程序仍在，其餘合法連線不影響 Discovery

### Story 1.2: 用 connectionId 列出資料表

As a 營運除錯呼叫者,
I want 用連線名稱列出該庫的資料表,
So that 除錯時不用連線字串、也不用知道引擎種類，就能看見有哪些表。

這些工具不在 Discovery，除錯清單與 Discovery 分開。除錯路徑未開啟時，公開端點對 `tables` 回 `unknown_tool`。

**Acceptance Criteria:**

**Given** 宿主組態有 `Connections` 字典，鍵是 `connectionId`，值只有 `ServerType` 與 `ConnectionString`，而且不讀 WebService 的 `ConnectionStrings:default`
**When** 外部 MCP 客戶端查看 `tables` 的輸入結構
**Then** 參數是 `connectionId` 與可省略的 `includeView`，沒有連線字串欄位

**Given** 恰有一個可連線的具名連線，庫裡有資料表
**When** Agent 以該 `connectionId` 呼叫 `tables`，且省略 `includeView`
**Then** 宿主經 `DbServiceFactory.Create` 取得服務，以既有 `QuerySession.Execute`、opcode `Call`、函式名稱 `tables` 執行，並在呼叫前去掉 `connectionId`
**And** 回傳該次 `QueryResult` 的 `columns` 與 `rows`，名稱包含檢視，且與直接呼叫 Session 的 `tables` 相同

**Given** 同一個可連線的具名連線，庫裡沒有資料表
**When** Agent 呼叫 `tables`
**Then** 回傳空的列清單，程序不結束

**Given** 可連線的具名連線，Session 的 `tables` 回傳 `QueryResult.Kind` 為 `Error`
**When** Agent 呼叫 `tables`
**Then** 回應碼為 `execution_failed`，不是空的列清單，程序不結束

**Given** 可連線的具名連線
**When** Agent 以 `includeView` 為 false 呼叫 `tables`
**Then** 結果與現有函數在 `includeView` 為 false 時相同

**Given** 恰有一個可用連線
**When** Agent 呼叫 `tables` 且未帶 `connectionId`
**Then** 查詢落在那一個連線

**Given** 可用連線是零個或多個
**When** Agent 呼叫 `tables` 且未帶 `connectionId`
**Then** 回應碼為 `connection_required`，不改落到其他連線

**Given** 已登記連線 `inventory`
**When** Agent 以未登記的 `connectionId` 呼叫 `tables`
**Then** 回應碼為 `unknown_connection`，查詢不落在 `inventory`

**Given** Agent 在參數裡放入名為 `connectionString` 或 `connectString` 的欄位
**When** 它呼叫 `tables`
**Then** 回應碼為 `invalid_arguments`，中文 `message` 不含該值，宿主不依該值建立連線

**Given** `inventory` 無法連線，`catalog` 可以連線
**When** Agent 對 `inventory` 呼叫 `tables`，再對 `catalog` 呼叫 `tables`
**Then** 前者回應碼為 `connection_failed`，後者回傳資料表名稱，程序不結束
**And** 失敗的建立不會被快取，之後 `inventory` 恢復可連線時，再次呼叫可以成功

### Story 1.3: 看欄位，不存在的表得到錯誤

As a 營運除錯呼叫者,
I want 取得指定資料表的欄位，表不存在時得到結構化錯誤,
So that 除錯時能依名稱、型別與主鍵組查詢，而不會把缺表看成一張沒有欄位的表。

這些工具不在 Discovery，除錯清單與 Discovery 分開。除錯路徑未開啟時，公開端點對 `fields` 回 `unknown_tool`。

**Acceptance Criteria:**

**Given** 一個可連線的具名連線，且該庫有資料表 `products`
**When** Agent 以該 `connectionId` 與 `table` 為 `products` 呼叫 `fields`
**Then** 宿主先對同一連線呼叫 Session 的 `tables`，`includeView` 為 true
**And** `products` 與回傳名稱以不區分大小寫的序數比較命中後，才呼叫 Session 的 `fields`
**And** 回傳該次 `QueryResult` 的 `columns` 與 `rows`，每一欄至少有 `name`、`type`、`pk`、`notNull`，且與直接呼叫 Session 的 `fields` 相同

**Given** 同一個可連線的具名連線
**When** Agent 以不在 `tables` 清單中的 `table` 呼叫 `fields`
**Then** 回應碼為 `unknown_table`，中文 `message` 不含連線字串、密碼或堆疊秘密
**And** 宿主不再呼叫 `fields`，程序不結束
**And** `IFunctionRegistry` 與公開的 `IDbService` 簽名沒有為了這個錯誤而改寫

**Given** 同一個可連線的具名連線
**When** `fields` 先呼叫的 `tables` 回傳 `QueryResult.Kind` 為 `Error`
**Then** 回應碼為 `execution_failed`，不是 `unknown_table`，且不再呼叫 `fields`，程序不結束

**Given** Agent 呼叫 `fields` 且未帶 `table`
**When** 宿主處理這次呼叫
**Then** 回應碼為 `invalid_arguments`，程序不結束

**Given** Story 1.2 的連線規則
**When** Agent 呼叫 `fields`
**Then** `connectionId` 的省略、未知名稱，以及 `connectionString` 參數，與 `tables` 使用同一套調度結果

### Story 1.4: 依整數 Id 取一列

As a 營運除錯呼叫者,
I want 用整數 Id 從指定資料表取回一列,
So that 除錯時能對到資料庫裡的同一筆資料，找不到時得到空結果而不是未處理錯誤。

這些工具不在 Discovery，除錯清單與 Discovery 分開。除錯路徑未開啟時，公開端點對 `get` 回 `unknown_tool`。

**Acceptance Criteria:**

**Given** 一個可連線的具名連線，資料表 `products` 有整數 Id 的一列
**When** Agent 以該 `connectionId`、`table` 為 `products`、整數 `id` 呼叫 `get`
**Then** 宿主以既有 `QuerySession.Execute`、opcode `Call`、函式名稱 `get` 執行，並在呼叫前去掉 `connectionId`
**And** 回傳該次 `QueryResult` 的 `columns` 與 `rows`，且與直接呼叫 Session 的 `get` 相同
**And** 這次呼叫不先執行 `tables`

**Given** 同一個可連線的具名連線，該 Id 沒有列
**When** Agent 呼叫 `get`
**Then** 回傳空的列清單，這是成功結果，程序不結束

**Given** 可連線的具名連線，Session 的 `get` 回傳 `QueryResult.Kind` 為 `Error`
**When** Agent 呼叫 `get`
**Then** 回應碼為 `execution_failed`，不是空的列清單，程序不結束

**Given** Agent 呼叫 `get`，`id` 不是整數，或未帶 `table`、未帶 `id`
**When** 宿主處理這次呼叫
**Then** 回應碼為 `invalid_arguments`，程序不結束

**Given** `table` 不在該連線的資料表清單中
**When** Agent 以整數 `id` 呼叫 `get`
**Then** 結果與今天的 Session `get` 相同，回應碼不是 `unknown_table`

**Given** Story 1.2 的連線規則
**When** Agent 呼叫 `get`
**Then** `connectionId` 的省略、未知名稱，以及 `connectionString` 參數，與 `tables` 使用同一套調度結果

### Story 1.5: 用等值條件查列、計數、判斷存在

As a 營運除錯呼叫者,
I want 用等值條件取列、取得列數，並判斷有沒有符合的列,
So that 除錯時讀到的結果與直接呼叫 Session 相同，而且篩選值不會被接進 SQL 文字。

這些工具不在 Discovery，除錯清單與 Discovery 分開。除錯路徑未開啟時，公開端點對 `query`、`count`、`exists` 回 `unknown_tool`。

**Acceptance Criteria:**

**Given** 一個可連線的具名連線
**When** 外部 MCP 客戶端查看 `query`、`count`、`exists` 的輸入結構
**Then** 必填參數是 `connectionId` 與 `table`，等值條件只放在可省略的 `filters` 物件，沒有連線字串欄位

**Given** 資料表 `products` 有貨號 `A1` 的列
**When** Agent 以該 `connectionId`、`table` 為 `products`、`filters` 含一個以上的等值條件呼叫 `query`
**Then** 宿主去掉 `connectionId`，把 `filters` 撲平成 Session 參數字典，以 opcode `Call`、函式名稱 `query` 執行
**And** 回傳該次 `QueryResult` 的 `columns` 與 `rows`，且與直接呼叫 Session 的 `query` 相同
**And** 篩選值是 Session 參數，不出現在宿主組出的 SQL 文字裡

**Given** 沒有列符合 `filters`
**When** Agent 呼叫 `query`
**Then** 回傳空的列清單，這是成功結果，程序不結束

**Given** 可連線的具名連線，Session 的 `query`、`count` 或 `exists` 回傳 `QueryResult.Kind` 為 `Error`
**When** Agent 呼叫對應工具
**Then** 回應碼為 `execution_failed`，不是空列、數字 0 或布林 false，程序不結束

**Given** 同一組等值條件
**When** Agent 呼叫 `count`
**Then** 回傳一個 JSON 數字，數值與直接呼叫 Session 的 `count` 相同
**And** 沒有符合列時這個數字是 0

**Given** Agent 呼叫 `count` 或 `exists` 且省略 `filters`
**When** 宿主執行
**Then** 結果與現有函數在沒有等值條件時相同

**Given** 有列符合 `filters`
**When** Agent 呼叫 `exists`
**Then** 回傳 JSON 布林值 true
**And** 沒有符合列時回傳 false

**Given** `table` 不在該連線的資料表清單中
**When** Agent 呼叫 `query`、`count` 或 `exists`
**Then** 結果與今天的 Session 呼叫相同，回應碼不是 `unknown_table`，而且不先執行 `tables`

**Given** Agent 未帶 `table`，或在參數裡放入 `connectionString` 或 `connectString`
**When** 宿主處理這次呼叫
**Then** 回應碼為 `invalid_arguments`，中文 `message` 不含該值，不依該值建立連線，程序不結束

**Given** `filters` 裡有一個鍵名為 `connectionId`
**When** Agent 呼叫 `query`
**Then** 那個鍵是欄位等值條件，連線仍由頂層的 `connectionId` 決定

**Given** Story 1.2 的連線規則
**When** Agent 呼叫這三個工具
**Then** `connectionId` 的省略與未知名稱，與 `tables` 使用同一套調度結果

### Story 1.6: 每次呼叫留下可讀的稽核

As a 整合方,
I want 每一次進入調度器的呼叫都在宿主本機留下一筆可事後閱讀的紀錄,
So that 我能回答是誰、對哪個連線、用了什麼條件、回了多少列或哪個錯誤，而且連線字串不會出現在紀錄裡。

以 `tables`、`fields`、`get`、`query`、`count`、`exists` 舉例的條文只覆蓋除錯路徑。公開目錄上成功呼叫的 `tool` 是模組視圖名稱，欄位語意見 Story 2.5。

**Acceptance Criteria:**

**Given** 每一次進入調度器的呼叫
**When** Agent 完成一次呼叫，不論成功或被拒絕
**Then** 宿主在單一 JSONL 檔追加一行完整的 JSON
**And** 預設路徑是內容根目錄下的 `audit/mcp.jsonl`，可用 `Audit:Path` 改到別的路徑
**And** 這個檔不是 MCP 工具，也不是 `Connections` 的一筆

**Given** 這一行 JSON
**When** 讀取它的鍵
**Then** 鍵恰好是 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`
**And** 沒用到的鍵是 null
**And** `timestamp` 是 UTC 的 ISO-8601
**And** `arguments` 是去掉 `connectionString` 與 `connectString` 之後的工具參數，且 `connectionId` 已填入解析後的名稱

**Given** `tables`、`fields`、`get` 或 `query` 成功
**When** 寫入該行
**Then** `rowCount` 是回傳列數，`value` 與 `error` 是 null，列資料本身不在這一行

**Given** `count` 或 `exists` 成功
**When** 寫入該行
**Then** `value` 是回傳的數字或布林值，`rowCount` 與 `error` 是 null

**Given** 呼叫被拒絕，包括 `tool_disabled`、`unknown_tool`、`unknown_connection`、`connection_required`、`connection_failed`、`invalid_arguments`、`unknown_table`、`execution_failed`
**When** 寫入該行
**Then** `error` 是該錯誤碼，`rowCount` 與 `value` 是 null
**And** 全文沒有連線字串與密碼

**Given** 這次呼叫的結果已決定，但稽核那一行寫不進去
**When** 宿主回應客戶端
**Then** 回應碼為 `execution_failed`，不回傳本次的列、數字或布林，程序不結束

**Given** 請求帶有標頭 `X-DBServices-Caller`
**When** 寫入該行
**Then** `caller` 是該標頭的值
**And** 標頭空白或不存在時，`caller` 是 `anonymous`，呼叫仍執行

**Given** Agent 呼叫 `fields`，宿主因而在內部又呼叫了一次 `tables`
**When** 這次 `fields` 結束
**Then** 稽核只有一行，工具名稱是 `fields`

**Given** 兩個呼叫同時完成
**When** 兩者都寫入稽核檔
**Then** 檔案裡是兩行各自完整的 JSON，沒有一行被寫斷

### Story 1.7: 換 ServerType，除錯路徑上的同一組工具仍成立

As a 營運除錯呼叫者,
I want 只改伺服器組態的連線字串與 `ServerType`，就用同一組除錯工具再查一次,
So that 除錯端的工具程式不用改，公開的資料存取簽名也不用改。

這些工具不在 Discovery，除錯清單與 Discovery 分開。公開的換引擎驗收是 Story 2.5。本故事只證明除錯路徑上的 `tables`、`fields`、`query` 在另一個 `ServerType` 仍使用同一組名稱與參數鍵。

**Acceptance Criteria:**

**Given** Story 1.5 的 `tables`、`fields`、`query` 已能對一個可連線的具名連線成功
**When** 整合方只另外登記一筆具名連線，`ServerType` 換成五個工廠名稱中的另一個，且該引擎有可連線的測試庫
**Then** 同一組工具名稱與參數鍵對新連線完成 `tables`、`fields`、`query`
**And** Agent 不傳入引擎種類，引擎只出現在伺服器組態

**Given** 五個 `ServerType` 名稱 `Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`
**When** 每個有可連線測試庫的名稱都走上述三步
**Then** 每一步都成功，回傳與該連線上直接呼叫對應 Session Call 相同的列集合
**And** 沒有測試庫的引擎不把這一則判成失敗

**Given** 宿主執行上述呼叫
**When** 檢查 `McpService` 與 `DbServices.Mcp` 的程式
**Then** 資料服務仍只來自 `DbServiceFactory.Create`，宿主裡沒有依引擎抄寫的查詢
**And** 公開的 `IDbService` 與 `IDbServiceAsync` 簽名與 P0 起點相比沒有變更
**And** 既有 REST 與 GraphQL 路徑仍可呼叫

**Given** 組態裡的 `ServerType` 拼錯
**When** Agent 對該 `connectionId` 呼叫 `tables`
**Then** 回應碼為 `connection_failed`，宿主程序仍在，其他具名連線的 `tables` 仍可成功

## Epic 2: 人手寫的模組視圖成為 Agent 的公開目錄

營運者放入一條已寫好的模組視圖，人確認解說後才發布。外部 Agent 的 Discovery 只有這份視圖與它的解說，帶上視圖自己的參數取得列。執行仍是唯讀，不在來源庫建立檢視表。沒有拖拉設計器。

**FRs covered:** FR-16, FR-17, FR-18, FR-19, FR-20, FR-21

這則史詩不包含拖拉設計器、與 ExportData 共用的關聯程式庫，也不包含營運除錯路徑的開關。Story 1.1 負責清單裡沒有六個表級工具。Story 2.1 負責一條人手寫視圖如何進入清單。

### Story 2.1: 發布一條人手寫視圖後，Discovery 只有它

As a 營運者,
I want 在沒有拖拉設計器時放入一條已寫好的模組視圖並發布,
So that Agent 的 Discovery 能靠這一份解說選到它。

**Acceptance Criteria:**

**Given** 產品目錄裡恰好一條已發布模組視圖
**When** 外部 MCP 客戶端列出工具
**Then** Discovery 只有這一筆，含名稱、短述，以及可讀的長解說
**And** 長解說含來源表、關聯依據、輸出欄與參數
**And** 說明不承諾接受 SQL，也不要求 Agent 傳入表名來選擇資料

**Given** 這一條視圖已發布
**When** 拿掉發布後，客戶端再次列出工具
**Then** Discovery 不再列出它
**And** 已取得的舊清單在下次列出時對得上新目錄

### Story 2.2: 解說未確認不得發布

As a 營運者,
I want 沒有人確認過的解說不能進入 Discovery,
So that Agent 不會呼叫一份尚未說清楚的視圖。

**Acceptance Criteria:**

**Given** 解說為空或仍屬草稿
**When** 營運者嘗試發布
**Then** 發布被拒絕，Discovery 不含該視圖

**Given** 解說已經人確認並發布
**When** 客戶端讀取該視圖的長解說
**Then** 讀到的內容與確認後的解說一致

**Given** 已發布視圖的確認被撤回
**When** 客戶端再次列出工具
**Then** 該視圖離開 Discovery
**And** 已在進行的表級除錯呼叫不被當成這份視圖的替代

### Story 2.3: 定義留在產品目錄，不寫回來源庫

As a 營運者,
I want 模組視圖的定義留在產品自己的目錄,
So that 發布與呼叫都不會改到來源庫的結構。

**Acceptance Criteria:**

**Given** 一條模組視圖定義
**When** 讀取它的內容
**Then** 含目的、解說、來源表、關聯路徑、輸出欄與參數
**And** 不含連線字串，也不含資料列
**And** 定義放在檔案、SQLite 或其他媒介，留給架構，這則故事不指定

**Given** 發布這條定義，或 Agent 呼叫已發布的視圖
**When** 比對來源庫的檢視表數量
**Then** 兩次都不執行 `CREATE VIEW` 或任何結構變更，呼叫前後數量不變

**Given** 呼叫帶入未宣告的表名或未宣告的欄位
**When** 宿主收到這次呼叫
**Then** 宿主拒絕，不執行

**Given** 呼叫的參數都屬於該視圖已宣告的篩選
**When** 呼叫成功
**Then** 回傳列的欄集合等於定義的輸出欄
**And** 沒有符合列時回傳空結果

### Story 2.4: 來源表不連通就不能存

As a 營運者,
I want 多張來源表之間必須有連通的關聯路徑,
So that 不能把沒有路徑的表拼成一份視圖。

**Acceptance Criteria:**

**Given** 定義含兩張以上來源表
**When** 路徑上的每一段要麼是目錄外鍵，要麼是定義裡已寫明並由人接受的關聯
**Then** 這份定義可以保存
**And** 未接受的推測不構成路徑

**Given** 來源表之間沒有連通路徑
**When** 營運者嘗試保存或發布
**Then** 定義被拒絕，不發布，也不執行出笛卡兒積

**Given** 只有一張來源表
**When** 保存這份定義
**Then** 不需要關聯路徑
**And** 仍須通過 Story 2.2 才能發布

### Story 2.5: 呼叫視圖仍是唯讀，換引擎不改參數鍵

As a 整合方,
I want 已發布視圖的呼叫不寫入、不接受自由 SQL，而且換引擎時名稱與參數鍵不變,
So that 公開目錄的成功路徑不是掃表，也不是寫入。

**Acceptance Criteria:**

**Given** 一次成功的模組視圖呼叫
**When** 讀取該次稽核
**Then** 含時間、呼叫者、連線名稱、模組視圖名稱、參數、列數或錯誤碼
**And** 不含連線字串，也不含列內容

**Given** Agent 呼叫一個已發布視圖
**When** 宿主執行
**Then** 這次呼叫不執行 `insert`、`update`、`delete`、`sql`

**Given** 視圖名稱未知
**When** Agent 呼叫該名稱
**Then** 宿主拒絕，不改落到表級 `query`

**Given** 同一份視圖定義先對一個可連線的具名連線成功，再只改對另一個 `ServerType` 的具名連線
**When** Agent 以相同參數鍵呼叫
**Then** 工具名稱與參數鍵不變且成功
**And** 引擎只出現在伺服器組態

