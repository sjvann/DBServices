---
title: DBServices Agent 資料平面
status: draft
created: 2026-10-11
updated: 2026-10-15
---

# PRD: DBServices Agent 資料平面

本 PRD 給產品、架構與後續故事拆解使用。定位與取捨見 [brief.md](brief.md)。技術邊界見 [addendum.md](addendum.md)。詞彙以第 3 節為準。功能需求承諾既有表級行為，以及 2026-10-15 起的模組視圖公開目錄。P1 之後仍寫在第 10 節，沒有 FR 編號。推論標成 `[ASSUMPTION]`，並彙總在第 9 節。

> **2026-10-15 歸屬已確認。** 資料表只留 Web Server。模組視圖與其 MCP 留在 DBServices。ExportData 不接 MCP，兩個專案不合併。Agent 的 Discovery 只廣告已發布的模組視圖。表級唯讀 Call 若仍留在宿主，只作營運除錯，不進公開目錄。第 4.6 節覆蓋第 4.2 節對 Agent 公開清單的承諾；第 4.2 節保留為營運除錯路徑的行為說明。

## 1. Vision

DBServices 成為可被外部 Agent 安全呼叫的資料平面。整合方把已接上的業務庫，以具名連線交給 MCP 宿主；Agent 發現唯讀工具並查詢。資料仍由 `IQuerySession` 與現有提供者執行。

這個增量要證明的是工具面，不是報表產品。對談、範本與敘事都使用同一組工具，但它們不在 P0 的完成定義裡。現在做，是因為 Agent 已經會呼叫工具，而繼續讓每個專案直接組 SQL，會繞過已註冊的 Call，也無法稽核。

2026-10-15 起，Agent 看見的那組工具不再是資料表。表級 Web Server 仍服務內部應用與日後的設計器。Agent 依模組視圖的解說做 Discovery，呼叫的是已發布的視圖，不是 `tables` 再自己拼表。拖拉設計應用與共用關聯程式庫都還不做；要等一條人手寫的模組視圖證明 Agent 靠解說選對工具。

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

這是營運除錯旅程，不是 Agent 的公開驗收。公開驗收是 UJ-3。除錯路徑未開啟時，Agent 的 Discovery 看不到這六個工具，也不得用它們代替尚未發布的模組視圖。

- **Persona + context：** 林可維護公司庫存系統的 DBServices 服務，庫在 SQL Server。她要把已上線的桌面 Agent 接到這座庫，又不想把連線字串放進 Agent 的提示。
- **Entry state：** MCP 宿主已啟動。伺服器組態裡有一個具名連線 `inventory`，`ServerType` 為 `SqlServer`。林可在 Agent 裡指到這個宿主。她已能操作 Agent，P0 不另做登入畫面。`[ASSUMPTION]` 呼叫者身分來自 MCP 傳輸層的客戶端識別；沒有識別時記為 `anonymous`，呼叫仍可執行。
- **Path：** Agent 列出工具，看到 `tables`、`fields`、`get`、`query`、`count`、`exists`。它對 `inventory` 呼叫 `tables`，再對 `products` 呼叫 `fields`，再以貨號鍵值呼叫 `query`。
- **Climax：** Agent 把列資料顯示給林可。她對得上在資料庫裡看到的同一批貨號。
- **Resolution：** 稽核留下這三次呼叫的連線名稱、工具、參數、列數與呼叫者。連線字串不在紀錄裡。
- **Edge case：** Agent 改呼叫 `insert`。宿主回傳 `tool_disabled`，資料表沒有新列。Agent 若在參數裡放連線字串，宿主拒絕且不建立連線。

**UJ-2. 林可把同一組工具接到 PostgreSQL。**

這是營運除錯旅程，不是 Agent 的公開驗收。公開驗收是 UJ-3。除錯路徑未開啟時，Agent 的 Discovery 看不到這六個工具，也不得用它們代替尚未發布的模組視圖。

- **Persona + context：** 同一位林可，測試環境的庫存庫在 PostgreSQL。Agent 端的工具呼叫已經寫好。
- **Entry state：** 她只改宿主組態：另一個具名連線 `inventory_pg`，`ServerType` 為 `PostgreSQL`。
- **Path：** 她讓 Agent 對 `inventory_pg` 再走一次 `tables` → `fields` → `query`。
- **Climax：** 工具名稱與參數名稱不變，列資料來自 PostgreSQL。
- **Resolution：** 她不修改 Agent 的工具程式，也不修改 DBServices 的公開資料存取簽名。
- **Edge case：** `ServerType` 拼錯時，該連線不可用，宿主本身仍在。對該名稱的呼叫回傳可讀的連線錯誤，而不是未處理例外。

**UJ-3. 林可的 Agent 靠解說選到銷貨視圖，不掃整座庫。**

- **Persona + context：** 同一位林可。業務問題是「這個月的銷貨」。庫裡有多張相關表。她不要 Agent 先列出所有表名再猜 join。
- **Entry state：** MCP 宿主已啟動。具名連線 `inventory` 可連線。目錄裡有一份已發布的模組視圖，目的與解說寫明它回答銷貨，並標出來源表、關聯依據、輸出欄與參數。解說已經人確認。未確認的另一份視圖不在目錄。表級 Call 即使宿主仍實作，也不出現在 Discovery。
- **Path：** Agent 列出工具，只看到已發布的模組視圖。它讀取該視圖的短述與長解說，帶上視圖自己宣告的參數呼叫它。
- **Climax：** 回傳的列屬於這份視圖，不是某一張表的原始掃描。Agent 沒有呼叫 `tables`、`fields` 或 `query`。
- **Resolution：** 稽核留下模組視圖名稱、參數、列數與呼叫者。連線字串不在紀錄裡。
- **Edge case：** 解說仍是草稿時，該視圖不在 Discovery。Agent 不能改呼叫表級工具來代替這份尚未發布的視圖。

## 3. Glossary

- **資料平面** — Agent 用來讀取（未來才寫入）業務資料的工具面。執行仍落在既有 Session 與提供者。
- **MCP 宿主** — P0 新增的 API 層程式。它向 MCP 客戶端廣告工具、接收呼叫、轉給 Session Call、寫稽核。它不是新的資料庫引擎。
- **具名連線** — 伺服器組態中的一筆連線。包含名稱、`ServerType`、連線字串。工具只接受名稱。
- **ServerType** — 交給 `DbServiceFactory` 的引擎名稱：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。
- **Session Call** — `IQuerySession` 的 Call 路徑上已註冊的函數。P0 使用其中的唯讀子集。
- **唯讀工具** — 表級的 `tables`、`fields`、`get`、`query`、`count`、`exists`。2026-10-15 起它們不是 Agent 的公開目錄；若宿主仍實作，只作營運除錯。
- **關閉工具** — `insert`、`update`、`delete`、`sql`。不廣告、不執行。
- **模組視圖** — 一份有特定目的、可結合多張資料表的視圖定義。它存在產品自己的目錄，含已確認的詳細解說、來源表、連通的關聯路徑、輸出欄與參數。不是來源庫裡的 DDL `VIEW`。不是 ExportData 為了出圖而圈選的「模組」。
- **Discovery** — Agent 列出可呼叫工具時看到的目錄。每一筆是一個已發布模組視圖的名稱、短述，以及可讀的長解說（來源表、關聯依據、輸出欄、參數）。
- **已發布** — 解說已經人確認，因此出現在 Discovery。草稿不得發布。
- **營運除錯** — 表級唯讀工具若被保留時的用途。不對 Agent 廣告，也不能在模組視圖尚未發布時代替它。
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

- 除錯路徑上，`tables`、`fields`、`query` 的名稱與參數鍵不因從 `SqlServer` 換成 `PostgreSQL` 而改變。
- 公開路徑上，同一份模組視圖的名稱與參數鍵不變，對應 FR-21 與 SM-2。
- 宿主不要求 Agent 傳入引擎種類。引擎只出現在伺服器組態。

### 4.2 唯讀工具

**Description：** 表級唯讀 Session Call 的行為仍沿用既有函數。`query` 的多鍵等值篩選沿用函數註冊表現況；這比 REST 的單一 `Key`／`Value` 寬，仍不是自由 SQL。自 2026-10-15 起，這些 Call 不再構成 Agent 的公開目錄。實現 UJ-1 的營運除錯讀法；Agent 的發現路徑改為 UJ-3 與第 4.6 節。

**Functional Requirements：**

#### FR-3: 表級工具不是公開目錄

Agent 的 Discovery 不列出表級唯讀工具。第 4.6 節的已發布模組視圖才是公開清單。

**Consequences (testable)：**

- Discovery 不含 `tables`、`fields`、`get`、`query`、`count`、`exists`。
- Discovery 不含 `insert`、`update`、`delete`、`sql`。
- 沒有任何已發布模組視圖時，Discovery 是空清單，不回退成上面六個表級工具。
- `[ASSUMPTION]` 宿主若仍實作表級唯讀工具，只在營運除錯路徑可呼叫，該路徑的工具清單與 Discovery 分開。除錯路徑的開關形式留給架構，不得讓外部 Agent 的 Discovery 看見它們。

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

### 4.6 模組視圖是 Agent 的公開表面

**Description：** 一個模組視圖回答一個特定目的，並可結合多張連得起來的資料表。人確認解說之後才發布。Agent 用 Discovery 依解說挑選，再帶這份視圖自己的參數呼叫。執行仍是唯讀，不在來源庫建立檢視表。第一刀用一條人手寫的定義證明這件事。實現 UJ-3。

**Functional Requirements：**

#### FR-16: Discovery 只列出已發布的模組視圖

Agent 列出工具時，每一筆都是一個已發布的模組視圖。

**Consequences (testable)：**

- 每一筆至少有名稱、短述，以及一份可讀的長解說。長解說含來源表、關聯依據、輸出欄與參數。
- 清單裡的名稱集合等於已發布模組視圖的名稱集合，不多不少。
- 未發布的定義不出現。表級工具不出現。
- 工具說明不承諾接受 SQL 語句，也不要求 Agent 傳入表名來選擇資料。

#### FR-17: 人手寫的一條視圖就能完成發現

在沒有拖拉設計器時，營運者仍可放入一條已寫好的模組視圖並發布。

**Consequences (testable)：**

- 恰好一條已發布視圖時，UJ-3 的 Discovery 只有這一筆。
- 這一條的關聯路徑、輸出欄與參數來自該定義，不由 Agent 在呼叫時現拼。
- 拿掉這條發布後，Discovery 不再列出它。已取得的舊清單在下次列出時對得上新目錄。

#### FR-18: 視圖定義在產品目錄，不寫回來源庫

模組視圖是伺服器上的定義。執行時依該定義唯讀取出列。

**Consequences (testable)：**

- 定義含目的、解說、來源表、關聯路徑、輸出欄與參數。不含連線字串，不含資料列。
- 發布與呼叫都不在來源庫執行 `CREATE VIEW` 或任何結構變更。呼叫前後，來源庫的檢視表數量不變。
- 參數是這份視圖宣告的篩選。呼叫若帶入未宣告的表名或未宣告的欄位，宿主拒絕，不執行。
- 回傳列的欄集合等於定義的輸出欄。沒有符合列時回傳空結果。

#### FR-19: 解說未確認不得發布

沒有人確認過的解說不能進入 Discovery。

**Consequences (testable)：**

- 解說為空或仍屬草稿時，發布被拒絕，Discovery 不含該視圖。
- 確認後的解說與 Discovery 讀到的長解說一致。
- 撤回確認後，該視圖離開 Discovery。已在進行的表級除錯呼叫不因此被當成這份視圖的替代。

#### FR-20: 來源表必須沿關聯連通

多張來源表之間必須有連通的關聯路徑。沒有路徑的組合不能存成模組視圖。

**Consequences (testable)：**

- 路徑上的每一段要麼是目錄外鍵，要麼是定義裡已寫明、並標成由人接受的關聯。未接受的推測不構成路徑。
- 來源表之間沒有連通路徑時，定義被拒絕，不發布，也不執行出笛卡兒積。
- 只有一張來源表時，不需要關聯路徑，仍須滿足 FR-19 才能發布。

#### FR-21: 呼叫模組視圖仍是唯讀

已發布視圖的呼叫不寫入、不接受自由 SQL。

**Consequences (testable)：**

- 一次成功呼叫的稽核含時間、呼叫者、連線名稱、模組視圖名稱、參數、列數或錯誤碼。不含連線字串，不含列內容。
- 對該視圖的呼叫不執行 `insert`、`update`、`delete`、`sql`。
- 視圖名稱未知時拒絕，不改落到表級 `query`。
- 換 `ServerType` 時，同一份視圖定義的工具名稱與參數鍵不變。引擎仍只出現在伺服器組態。

## 5. Non-Goals (Explicit)

- 不把 Core、Provider 或公開的 `IDbService`／`IDbServiceAsync` 簽名改寫成另一種語言或另一套 API。
- 不自製跨廠商 SQL planner，也不把自由 `sql` 開放給 Agent。
- 不在產品內附帶或綁定單一語言模型。
- 不做像素級報表引擎。
- 不在 P0 交付對談介面、報表範本、Python 側車。
- 不把 REST 的八條路由當成 MCP 工具全集。REST 維持單鍵查詢與整數 Id，並繼續作為資料表層的 Web Server。
- 不讓工具參數攜帶連線字串。
- 不把本增量放進 ExportData，也不把 DBServices 與 ExportData 合併成一個專案。ExportData 的「模組」仍只是出圖範圍。
- 不在來源庫建立 DDL 檢視表來充當模組視圖。
- 不在第一條人手寫視圖證明 Discovery 之前，交付拖拉設計應用，或抽出與 ExportData 共用的關聯讀取程式庫。
- 不讓 Agent 用表級 `query` 掃表來回答一個應由模組視圖回答的跨表目的。

## 6. MVP Scope

### 6.1 In Scope

- FR-1 至 FR-21。
- 外部 MCP 客戶端能完成 UJ-3：Discovery 只有已發布模組視圖，呼叫該視圖得到列，且全程不呼叫表級工具。
- 一條人手寫、解說已確認、關聯連通的模組視圖足以作為上述驗收。
- 五個 `ServerType` 上，同一份視圖的工具名稱與參數鍵一致。有可連線測試庫的引擎都要跑通 UJ-3。
- 資料表層的 Web Server 維持既有 REST 形狀。
- UJ-1 與 UJ-2 只在營運除錯路徑成立，不作為 Agent 公開目錄的驗收。

### 6.2 Out of Scope for MVP

- **拖拉設計應用。** 理由：它驗證的是畫布。要等一條人手寫模組視圖先證明 Agent 靠解說選對工具，而不是靠 `tables`。
- **與 ExportData 共用的關聯讀取程式庫。** 理由：第一條視圖的路徑可以手寫。設計器真正要讀目錄關聯時，再在資料表 Web 上加唯讀端點；現在不合併倉庫，也不抽程式庫。
- **P1 對談查資料與產表。** 理由：公開目錄要先固定在模組視圖。`[NOTE FOR PM]` 對談日後選的是已發布視圖，不是表級 Call。
- **P2 拖拉範本設計。** 理由：報表範本若仍綁單表 `query`，會和本節的公開目錄衝突。工作台的改寫見報表工作台 PRD 第 0 節，不在本增量交付。
- **P3 對談套版。** 理由：依賴公開目錄與工作台改寫。
- **結構說明、結果敘事、跨來源簡報、寫入提案。** 理由：模組視圖的解說已是 Discovery 的說明。不另做「掃完整座庫再口述」的結構說明來代替它。
- **Python 側車。** 理由：這一刀沒有語句要解析。
- **完整登入與授權。** 理由：呼叫者仍以傳輸層識別寫入稽核。是否拒絕 `anonymous` 見第 8 節。
- **修正 [docs/web/index.md](../../web/index.md) 的 Sqlite 敘述。** 理由：那是文件漂移，不改變本增量行為。附錄記錄，文件另案更新。

## 7. Success Metrics

**Primary**

- **SM-1：** 驗收客戶端列出 Discovery，只看到一條已發布模組視圖；讀完它的解說後呼叫該視圖，得到定義所承諾的輸出欄，且這次列出與呼叫都沒有 `tables`、`fields`、`query`。驗證 FR-3、FR-16、FR-17、FR-18。
- **SM-2：** 將 SM-1 改對另一個 `ServerType` 的具名連線執行，視圖名稱與參數鍵不變且成功。五個工廠名稱中，每一個有測試庫者都通過。驗證 FR-1、FR-2、FR-21。

**Secondary**

- **SM-3：** 本增量合併範圍內，`IDbService` 與 `IDbServiceAsync` 的公開方法簽名 diff 為空。驗證 FR-1。
- **SM-4：** Discovery 不含六個表級唯讀工具，也不含 `sql` 與 `insert`。對 `sql` 與 `insert` 的呼叫得到 `tool_disabled`，資料列數不變。來源庫的檢視表數量在發布與呼叫前後不變。驗證 FR-3、FR-15、FR-18、FR-21。
- **SM-5：** 抽樣一筆成功的模組視圖呼叫與一筆拒絕呼叫，稽核含 FR-21 所列欄位，且全文搜尋不到該連線的連線字串。驗證 FR-11、FR-21。
- **SM-6：** 解說為草稿，或來源表沒有連通路徑時，該定義不在 Discovery，呼叫次數是 0。驗證 FR-19、FR-20。

**Counter-metrics (do not optimize)**

- **SM-C1：** 不以工具總數、可執行的 SQL 種類，或寫入是否開放，作為完成度。這會把 SM-1 做成「開放 `sql` 或退回掃表讓查詢更容易成功」。
- **SM-C2：** 不以在宿主內複製五段引擎專用 SQL 來換取 SM-2。引擎差異留在既有 Provider。
- **SM-C3：** 不以先做出拖拉設計器，或先合併 ExportData，作為 SM-1 的前提。一條人手寫視圖必須夠用。

## 8. Open Questions

1. 稽核紀錄的保留天數與存放媒體。P0 只要求本機可查。`[ASSUMPTION]` 見 FR-14。
2. 是否在 P0 拒絕 `anonymous`。目前假設放行並記入稽核。
3. 傳輸要先做 stdio、HTTP，或兩者同時。完成定義是外部 MCP 客戶端能走完 UJ-1，不在本 PRD 指定傳輸。
4. 具名連線清單與 WebService 單一 `ConnectionStrings:default` 要不要在實作時共用同一個組態區段。產品要求是 MCP 能表達多個名稱；不要求這一版改 REST 的組態。
5. 營運除錯路徑如何與 Discovery 分開，留給架構。產品要求是外部 Agent 的工具清單看不到表級 Call。

## 9. Assumptions Index

- 第 2.3 節 UJ-1 — 呼叫者來自傳輸層識別；缺省為 `anonymous` 且仍可呼叫。
- 第 4.3 節 — MCP 具名連線組態獨立於 WebService 現有的單一預設連線，不強制同時改 REST 組態。
- FR-12 — 未指定連線名稱時，僅在恰有一個具名連線時自動選用。
- FR-14 — 稽核先寫入宿主本機可查的儲存，保留天數未定。
- 第 6.2 節與第 10 節 — 對談、拖拉設計、敘事、跨來源簡報、寫入提案、Python 側車、共用關聯程式庫都不在這一刀的完成定義。
- 第 1 節 — Agent 是外部客戶端。產品不附帶模型。
- FR-3 — 表級唯讀工具若保留，只存在與 Discovery 分開的營運除錯路徑。
- 第 4.6 節 — 模組視圖是產品目錄裡的定義，執行不在來源庫建立檢視表。第一條定義可以由人寫好後放入。

## 10. 後續能力

以下沒有 FR 編號。P0 驗收後才拆故事。順序沿用 brief 的地圖。

- **P1 對談查資料與產表。** 人在對談介面說一句查詢。解析結果是一個具名連線、一個已發布模組視圖、一組該視圖的參數。執行仍走 MCP。此階段的報表是結果表與匯出。不退回表級 `query`。
- **拖拉設計應用。** 人拖拉連得起來的資料表，寫下解說，再發布成模組視圖。啟動條件是 SM-1 已由一條人手寫視圖通過。在那之前不做。它讀資料表 Web 的結構；關聯端點與是否抽出共用程式庫也等到那時再定。
- **P2 拖拉範本設計。** 報表版面仍是另一個表面。它不得把 Agent 的填數假定成單表 `query`。工作台哪些條款要改，見報表工作台 PRD 第 0 節。
- **P3 對談套版。** 一句話解析成具名連線、一份報表範本、一組參數。人確認後才填入查詢結果。填數對象是模組視圖，不是掃表。
- **結構說明。** 不另做「列出所有表再口述」的說明。模組視圖的已確認解說就是 Discovery 的說明。
- **結果敘事。** 列資料返回後，產生摘要與異常提示。數字只來自該次工具結果。
- **跨來源簡報。** 對多個具名連線各做唯讀呼叫，再寫成一份簡報。不新增跨廠商 SQL planner。
- **寫入提案。** Agent 草擬 `insert` 或 `update` 的參數。人確認後才執行。在此之前 FR-15 維持關閉。

P1 起若需要語言模型，使用 addendum 所述的 Python 側車。側車不執行 SQL，不讀取連線字串。

## 11. Cross-Cutting NFRs

- 工具的錯誤是結構化物件，至少含穩定的 `code` 與給人讀的 `message`。`message` 不含連線字串、密碼與堆疊中的秘密。
- 宿主不把模組視圖的執行改寫成自由 SQL，也不在呼叫端再做一次彙總。列來自該視圖定義的唯讀執行。
- 未知工具、關閉工具、未知連線、未知模組視圖、未連通的定義，都走結構化錯誤，不讓未處理例外結束宿主進程。

## 12. Constraints and Guardrails

- **資料邊界：** 連線字串只存在伺服器組態與既有連線工廠。工具結構、錯誤、稽核不攜帶它。
- **執行邊界：** 模組視圖的執行是唯讀。不新增給 Agent 的自由 SQL。表級 Session Call 不再是公開目錄。
- **相容邊界：** 公開資料存取簽名與既有 REST、GraphQL 路徑保持可呼叫。資料表 Web Server 不改成 MCP。
- **歸屬邊界：** 模組視圖的定義、發布與 MCP 都在 DBServices。ExportData 維持分析包與出圖用的模組，兩者不是同一個詞所指的物件。
- **模型邊界：** 這一刀不呼叫語言模型，也不部署 Python 側車。解說由人確認。

## 13. Data Governance

P0 的治理是最小集合：預設唯讀、連線秘密留在伺服器、每次呼叫可追溯到連線名稱與呼叫者。資料分類、保存年限、正式授權模型未決，見第 8 節。在那些決定之前，不把業務列資料寫進日誌的自由文字以外的第三方服務。`[ASSUMPTION]` 稽核存放在營運者自己的宿主上，不預設外送到別的分析平台。

## 14. API Contracts / Public Surface

- Agent 的公開表面是已發布模組視圖的 MCP 工具。名稱是視圖名稱。Discovery 帶短述與長解說。
- 表級唯讀工具不是這個公開表面。
- 既有 REST 與 GraphQL 為相容表面。資料表層只走 Web Server，不變更其路徑與查詢形狀。
- 既有 `IDbService`／`IDbServiceAsync` 為相容表面。簽名變更視為破壞 SM-3。
- 關閉工具若將來開放，必須以新的 FR 打開，而不是留下可呼叫的隱藏路徑。
