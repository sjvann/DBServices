---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
updated: 2026-10-20
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-DBServices-2026-10-12/prd.md
  - _bmad-output/planning-artifacts/prds/prd-DBServices-2026-10-12/addendum.md
  - _bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-12/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/EXPERIENCE.md
  - _bmad-output/specs/spec-report-workbench/SPEC.md
  - _bmad-output/specs/spec-report-workbench/glossary.md
  - _bmad-output/specs/spec-report-workbench/workbench-shapes.md
  - _bmad-output/specs/spec-report-workbench/surfaces.md
  - _bmad-output/specs/spec-report-workbench/stack.md
  - _bmad-output/specs/spec-report-workbench/architecture-diagrams.md
  - _bmad-output/specs/spec-report-workbench/stories.yaml
---

# DBServices 檢視與報表 - Epic Breakdown

2026-10-20 重寫。上一版正文描述單一工作台（活資料預覽、確認清單、五種元件含計數、常用項目、群組 design/view、模型設定）。那個正文已撤回。故事編號與 `stories.yaml` 的 id 相同，由上到下執行。

產品範圍是 PRD 的 FR-1 至 FR-16。實作邊界以架構 AD 與 SPEC 的 CAP-9 至 CAP-13 為準。

## Overview

三個入口各自一整頁：模組設計工具、AI 報表設計工具、Chat 報表生成器。不收成同一條左軌。檢視回答業務問題，樣式只負責版面，交談只負責填進版面。

## Requirements Inventory

### Functional Requirements

FR-1: 未登入不可進入三個入口。未帶有效工作階段的請求不回傳檢視、樣式範本、報表生成紀錄或業務列。三個入口都不以 `anonymous` 呼叫 MCP。沒有公開註冊端點。

FR-2: 管理員可建立操作者，並指派或移除系統分析師、報表設計師、終端使用者入口。非管理員不能建立操作者，也不能變更入口。同一人可同時持有多個入口。持有哪個入口就只出現該站。

FR-3: 沒有該入口就不能打開該站。直接指定另一站時拒絕，不建立檢視、不修改樣式、不產生動態報表，讀列次數為 0。

FR-4: 系統分析師在規劃期可看到二代平台資料源的資料表與欄位。結構不來自待轉資料庫，畫面沒有連線字串，沒有自由 SQL，也不寫來源庫。沒有資料表時清單是空的。通道仍未定，不在下面八則裡實作。

FR-5: 系統分析師可建立、修改與刪除尚未被樣式引用的檢視。一筆檢視含名稱、目的、來源表、關聯、輸出欄與參數。參數只有欄位與標籤。一張來源表不必有關聯；兩張以上時每一段要麼是外鍵，要麼是分析師接受的關聯，否則拒絕保存，既有定義不變，不產生笛卡兒積。定義沒有業務列與連線字串。名稱在目錄內不重複，比對不區分大小寫。仍被樣式 `viewId` 引用時不能刪除、不能取消發布，也不能改名稱、輸出欄、參數、來源表或關聯。目的可以改。

FR-6: 發布之後，MCP 目錄才出現該檢視。目錄的名稱集合等於已發布檢視的名稱集合。未發布不在目錄。目錄是空的時候，不改列資料表工具。樣式繫結穩定 `id`，不繫結名稱。發布與呼叫都不在來源庫建立檢視。

FR-7: 這一輪，已發布檢視的列由 MCP 依檢視定義即時讀取一次。不用 Web API 的列組成動態報表。先落地成另一份資料表不在這一輪。呼叫失敗時不產生動態報表。連線字串不進入三個入口。

FR-8: 報表設計師可建立由標題、文字、參數、資料表四種區塊組成的樣式範本，綁定恰好一筆已發布檢視。順序由上到下。不保存座標、業務列、連線字串、具名連線與參數值。沒有計數區塊。筆數若要出現，必須是該檢視的一個輸出欄。

FR-9: 選定已發布檢視後，工具依輸出欄與參數提出區塊建議。建議不含業務列與參數值。至少一個資料表區塊的欄名全集等於該檢視的輸出欄，並為每個已宣告參數各有一個參數區塊。未知欄名不能接受。忽略或建議失敗不改既有區塊。接受才把區塊加到末尾。修改後再接受，採用修改後的版本。

FR-10: 報表設計師保存時指定私有或共享。擁有者可改內容與共享範圍，其他人不能。私有只有擁有者可讀。共享對持有報表設計師或終端使用者入口的人可讀。重新開啟時順序與繫結與上次保存相同，不需要連線也能看版面。

FR-11: 終端使用者提交語句後，候選只含此人可讀的樣式：自己擁有的，以及共享的。語句含樣式名稱才進入候選。含參數欄位或標籤時，其後第一個不以空白拆開的詞為值。對不到的不補猜，也不從語句取出連線名稱。時間範圍只有在同一個詞能當某個參數的一個等值時才成為值。

FR-12: 未就緒、候選超出本次輸入，或缺少參數時不呼叫 MCP，讀列次數為 0，並指出是多筆、缺哪個欄位或標籤，或對不到。改選候選不讀列。指定不可讀樣式的識別時拒絕，讀列次數為 0。沒有確認清單。比對在核心完成，不啟動側車。

FR-13: 就緒後直接呼叫該樣式所綁的已發布檢視恰好一次。參數是純量，且屬於該檢視已宣告的參數。工具引數是頂層 `connectionId` 與這些參數，沒有 `filters`。人不選擇連線。恰有一筆組態完整的具名連線才呼叫；否則不呼叫，讀列次數為 0，訊息可讀且不含連線字串。不呼叫資料表工具，也不另發計數。空列是成功。呼叫失敗時不產生動態報表，不用舊列湊成成功報表，也不改樣式與生成紀錄。

FR-14: 終端使用者可把這次動態報表存成生成紀錄，並指定私有或共享。紀錄含擁有者、共享範圍、當時的區塊順序與文字、該次欄與列、純量參數，不含連線字串。再開顯示保存的那一份，不回讀樣式，也不呼叫 MCP。私有只有擁有者可讀。共享對持有終端使用者入口的人可讀。其他人不能修改或刪除。產品介面不讀他人的私有生成紀錄。

FR-15: 終端使用者可把這次版面存回樣式庫，並指定私有或共享。寫入的是樣式：版面、所綁檢視與參數定義。不含業務列、參數值與連線字串。新範本的擁有者是該人。存回不呼叫 MCP。

FR-16: 匯出 PDF、列印與 CSV 的數字和文字等於目前這份動態報表，或已開啟的生成紀錄，包含空結果，且不發出新的 MCP 呼叫。離開這次報表，或同一人產生下一份時，尚未寫入的那份不能再匯出。PDF 預設 A4，換頁以區塊為界。CSV 的具體寫法未定，不在下面八則裡實作。

### NonFunctional Requirements

NFR-1: 操作者看得到的文字、空狀態與工作台自己的錯誤使用繁體中文。工作台錯誤是穩定英文蛇形碼加繁體中文訊息。MCP 的 `code` 與 `message` 原樣顯示。訊息不含連線字串、金鑰、密碼與堆疊秘密。

NFR-2: 明文密碼不寫入 SQLite 與日誌。密碼只存 `passwordVerifier`。

NFR-3: 比對失敗、建議失敗或呼叫失敗時，已保存的檢視、樣式與生成紀錄不變。單一檢視呼叫失敗不停止宿主。

NFR-4: 部署恰好一個 `Workbench` 進程。只有它開啟工作台 SQLite，並在記憶體持有工作階段與尚未保存的動態報表。進程重啟後工作階段清空。`AllowedHosts` 出貨值為 `*`。

NFR-5: 三個入口不讀寫模型設定、不測試連線、不提供詞庫編輯、不渲染取數軌跡，也不寫第二份 MCP 稽核，不實作稽核保留天數。

### Additional Requirements

- 登入、入口、樣式、語句比對、動態報表與生成紀錄只經過 `Workbench.Core`。Blazor 在進程內呼叫。Avalonia 只經工作台 HTTP 進入同一組端口。控制器不另寫業務判斷。
- 兩個殼與 `Workbench.Web` 不參考 `Workbench.Adapters`、`DbServices.Mcp`、`DBService`、Provider 或 `Microsoft.Extensions.AI`，也不持有 MCP 或 Web API 的基底網址。Core 不參考 ASP.NET、Avalonia 與模型套件。
- 工作階段是宿主記憶體中的一筆 `sessionId` 與操作者 `id`。cookie 與 bearer 都只帶 `sessionId`。登出刪除該筆，兩殼同時失效。
- 檢視目錄只在 `DbServices.Mcp`。目錄 HTTP 在 `/mcp` 之外：`GET /views`、`GET`／`PUT`／`DELETE /views/{id}`、`POST /views/{id}/publish`、`POST /views/{id}/unpublish`。要求 `X-DBServices-Workbench` 等於 `Workbench:CatalogKey`，並拒絕空白的 `X-DBServices-Caller`。MCP 工具路由不讀該鍵。
- `GET /connections` 維持現況，只回組態完整連線的 `name` 與 `serverType`。三個入口不渲染這份清單。適配器只用它決定筆數，以及在恰有一筆時要送的 `connectionId`。
- 零個操作者時，宿主讀 `Workbench:InitialAdmin:DisplayName` 與 `Password`，寫入恰好一個管理員。已有操作者後不再讀這兩個鍵。缺密碼且零個操作者時，不提供三個入口。畫面不能建立第二個管理者，也不能把操作者改成管理者。
- 被樣式引用時，`Workbench.Core` 先拒絕結構變更、取消發布與刪除，再呼叫目錄。
- 目標框架 `net10.0`。這一輪不新增套件。PDF 使用既有 QuestPDF。

### UX Design Requirements

UX-DR1: 三個入口各自一整頁。一人多個入口時整頁換站。只有一個入口時不出現換站。沒有任何入口、也不是管理者時，登入後只有「尚未指派入口。」

UX-DR2: 表面是登入、換站、檢視清單、檢視定義、樣式清單、樣式編輯、交談、動態報表、生成紀錄、帳號、入口指派。帳號與入口指派只有管理者看得到。對話框只疊一層。

UX-DR3: 會讀業務列的動作只有交談裡就緒後的「送出語句」。語句欄的 Enter 等於送出語句。未就緒時不讀列。

UX-DR4: 樣式區塊只有標題、文字、參數、資料表。拖曳只改上下順序。每個區塊另有上移與下移。沒有座標。

UX-DR5: 視覺只依 `DESIGN.md`。只有淺色。兩個殼同一資訊架構。Avalonia 最小視窗 1100×720。Blazor 低於 1100px 不做手機版，允許橫向捲動。

UX-DR6: 畫面參考是 `mockups/key-view.html`、`mockups/key-style.html`、`mockups/key-chat.html`。骨架與畫面衝突時，以 `EXPERIENCE.md` 與 `DESIGN.md` 為準。

### FR Coverage Map

FR-1、FR-2、FR-3: Story 1
FR-4: 未排入。通道未定
FR-5、FR-6: Story 2
FR-7: Story 5 的一次即時呼叫。先落地不排入
FR-8、FR-9、FR-10: Story 3
FR-11、FR-12: Story 4
FR-13: Story 5
FR-14、FR-15: Story 6
FR-16: Story 7 只含 PDF 與列印。CSV 未排入
兩殼同一工作階段: Story 8

## Epic List

### Epic 1: 登入與入口
已登入且持有該入口才能打開該站。管理員建立操作者並指派入口。單一宿主與工作階段。
**FRs covered:** FR-1, FR-2, FR-3

### Epic 2: 檢視目錄與發布
系統分析師定義檢視並發布。目錄名稱集合等於已發布檢視。不含表結構瀏覽。
**FRs covered:** FR-5, FR-6

### Epic 3: 樣式範本
報表設計師把恰好一筆已發布檢視排成四種區塊，並標私有或共享。被引用的檢視先被核心拒絕結構變更。
**FRs covered:** FR-8, FR-9, FR-10

### Epic 4: 語句未就緒不讀列
候選只來自可讀樣式。未就緒時讀列次數為 0。比對在核心，不啟動側車。
**FRs covered:** FR-11, FR-12

### Epic 5: 就緒後呼叫一次檢視
恰好呼叫所綁的已發布檢視一次。空列是成功。失敗不產生動態報表。
**FRs covered:** FR-7, FR-13

### Epic 6: 生成紀錄與存回樣式
把這次動態報表存成生成紀錄。終端使用者把版面存回樣式庫。兩次都不重新呼叫 MCP。
**FRs covered:** FR-14, FR-15

### Epic 7: 匯出 PDF 與列印
數字和文字等於所開啟的那一份。PDF 預設 A4。不含 CSV。
**FRs covered:** FR-16

### Epic 8: Avalonia 對齊
桌面殼只經工作台 HTTP，與 Blazor 共用同一筆工作階段。
**FRs covered:** NFR-4 的兩殼約束

## Epic 1: 登入與入口

### Story 1: 登入與入口

As a 管理者,
I want 建立操作者並指派入口，未登入或沒有該入口的人進不了該站,
So that 之後每一次讀取都能對上一個操作者與他持有的入口。

這一則只做 Blazor 與單一宿主。Avalonia 專案可以建立，三站畫面留到 Story 8。殼與 `Workbench.Web` 不參考 `Workbench.Adapters` 與 `DbServices.Mcp`。

**Acceptance Criteria:**

**Given** 儲存裡零個操作者，且有 `Workbench:InitialAdmin:DisplayName` 與 `Password`
**When** 宿主啟動
**Then** 恰好建立一個管理員，`passwordVerifier` 不是明文
**And** 已有操作者後，這兩個鍵不再使用

**Given** 零個操作者且缺少初始密碼
**When** 有人開啟任一個入口
**Then** 不提供三個入口，也不建立工作階段

**Given** 沒有有效 `sessionId`
**When** 請求檢視、樣式範本、生成紀錄或業務列
**Then** 內容不回傳，也不呼叫 MCP，呼叫者不是 `anonymous`

**Given** 瀏覽器已登入
**When** 宿主建立工作階段
**Then** 記憶體中是一筆 `sessionId` 與操作者 `id`，cookie 只帶 `sessionId`
**And** 登出刪除該筆；進程重啟後工作階段清空

**Given** 管理員已登入
**When** 他建立只有顯示名稱與登入秘密的操作者，再指派一個或多個入口
**Then** 帳號列只有名稱；該人重新登入後只出現被指派的站
**And** 只有一個入口時不出現換站；多個入口時整頁換站

**Given** 非管理員，或沒有該入口的操作者
**When** 他建立操作者、變更入口，或直接進入沒有的站
**Then** 請求被拒絕；不建立檢視、不修改樣式、不產生動態報表，讀列次數為 0

**Given** 畫面上的帳號操作
**When** 有人建立第二個管理者，或把操作者改成管理者
**Then** 該操作被拒絕

## Epic 2: 檢視目錄與發布

### Story 2: 檢視目錄與發布

As a 系統分析師,
I want 定義一筆檢視並發布,
So that MCP 目錄的名稱集合等於當時的已發布檢視。

檢視只寫進 `DbServices.Mcp` 的目錄路由。不要讀待轉資料庫的 `GET /meta`，也不要在這一則做表結構瀏覽。

**Acceptance Criteria:**

**Given** 分析師持有系統分析師入口
**When** 他保存一筆含名稱、目的、來源表、輸出欄與參數的檢視
**Then** 定義寫進目錄，沒有業務列與連線字串
**And** 只有一張來源表時不必有關聯

**Given** 兩張以上來源表，且某一段既不是外鍵也不是分析師接受的關聯
**When** 他保存
**Then** 拒絕保存，既有定義不變，不產生笛卡兒積

**Given** 目錄裡已有同名檢視，比對不區分大小寫
**When** 他再用這個名稱保存
**Then** 拒絕，既有定義不變

**Given** 一筆檢視尚未發布
**When** 讀取 MCP Discovery
**Then** 目錄的名稱集合不含它
**And** 目錄是空的時候，不改列資料表工具

**Given** 他發布這筆檢視
**When** 再讀 Discovery
**Then** 名稱集合等於當時已發布檢視的名稱集合
**And** 發布不在來源庫執行 `CREATE VIEW`

**Given** 一筆樣式的 `viewId` 指向這筆檢視
**When** 有人刪除、取消發布，或改名稱、輸出欄、參數、來源表、關聯
**Then** `Workbench.Core` 先拒絕，目錄維持原樣
**And** 目的文字可以改

**Given** 沒有系統分析師入口
**When** 有人呼叫目錄寫入
**Then** 拒絕，目錄不變

## Epic 3: 樣式範本

### Story 3: 樣式範本

As a 報表設計師,
I want 把恰好一筆已發布檢視排成樣式範本並標私有或共享,
So that 重新打開時只有版面與繫結，沒有業務列。

終端使用者存回樣式庫不在這一則。

**Acceptance Criteria:**

**Given** 一筆已發布檢視，設計師持有報表設計師入口
**When** 他接受建議並保存為共享或私有
**Then** 區塊只有標題、文字、參數、資料表，`order` 從 0 連續
**And** 至少一個資料表區塊的欄名全集等於該檢視的輸出欄，每個已宣告參數各有一個參數區塊
**And** 樣式不含業務列、連線字串、具名連線與參數值，`viewId` 是目錄的穩定 `id`

**Given** 建議含有該檢視沒有的欄名
**When** 他嘗試接受
**Then** 不能接受；忽略或建議失敗都不改既有區塊

**Given** 他拖曳、上移或下移
**When** 再次打開
**Then** 順序與上次保存相同，不需要連線也能看版面
**And** 畫面不保存 x、y、寬與高；除了拖曳，上移與下移都在

**Given** 樣式是私有
**When** 另一位持有報表設計師或終端使用者入口的人讀取
**Then** 清單不含它；以識別直接讀取被拒絕

**Given** 樣式是共享
**When** 持有報表設計師或終端使用者入口的其他人讀取
**Then** 可以讀，不能改內容，也不能改共享範圍

**Given** 非擁有者
**When** 他修改或刪除這筆樣式
**Then** 拒絕，樣式不變

## Epic 4: 語句未就緒不讀列

### Story 4: 未就緒不讀列

As a 終端使用者,
I want 語句還沒對上可讀樣式與參數時不要讀到業務列,
So that 未就緒的交談讀列次數是 0。

比對只在 `Workbench.Core`。不要啟動 `DbServices.ModelSidecar`。

**Acceptance Criteria:**

**Given** 此人可讀的樣式只有自己擁有的與共享的
**When** 他送出不含樣式名稱的語句，或語句對不到任何可讀樣式
**Then** 指出對不到，讀列次數為 0，不啟動側車

**Given** 語句對上多筆可讀樣式，或缺少某個參數值
**When** 他送出
**Then** 指出是多筆、缺哪個欄位或標籤
**And** 讀列次數為 0；沒有確認清單

**Given** 他改選候選
**When** 選取完成、尚未再次送出就緒語句
**Then** 不讀列

**Given** 他指定一筆不可讀樣式的識別
**When** 要求產生報表
**Then** 拒絕，讀列次數為 0

**Given** 比對失敗
**When** 這一輪結束
**Then** 已保存的檢視、樣式與生成紀錄不變

## Epic 5: 就緒後呼叫一次檢視

### Story 5: 就緒後呼叫一次檢視

As a 終端使用者,
I want 語句就緒後直接得到該筆檢視的列,
So that 動態報表的列來自恰好一次已發布檢視呼叫。

工具引數使用頂層 `connectionId`，不要使用 `filters`。零筆或多筆連線時不要自造錯誤碼名稱。

**Acceptance Criteria:**

**Given** 恰好一個可讀樣式，每個已宣告參數都有純量值，且恰有一筆組態完整的具名連線
**When** 他送出語句
**Then** 恰好呼叫該檢視一次，引數是頂層 `connectionId` 與該檢視的純量參數
**And** 不呼叫 `tables`、`fields`、`get`、`query`、`count`、`exists`，也不另發計數
**And** 呼叫者標頭是這位操作者的 `id`

**Given** 該次呼叫回傳零列
**When** 動態報表顯示
**Then** 空列是成功，不是錯誤

**Given** 該次呼叫失敗
**When** 畫面更新
**Then** 不產生動態報表，不用舊列湊成成功報表，也不改樣式與生成紀錄

**Given** 組態完整的具名連線是零筆或多筆
**When** 語句已就緒
**Then** 不呼叫，讀列次數為 0，訊息可讀且不含連線字串
**And** 這一則不新增錯誤碼名稱

**Given** 他沒有選擇連線的控制項
**When** 三個入口繪製
**Then** 不渲染連線清單，也不能輸入連線字串

## Epic 6: 生成紀錄與存回樣式

### Story 6: 生成紀錄與存回樣式

As a 終端使用者,
I want 留下這次動態報表，或把版面存回樣式庫,
So that 再開與存回都不重新呼叫 MCP。

再開只讀紀錄裡的區塊與列。

**Acceptance Criteria:**

**Given** 這次動態報表已產生
**When** 他存成私有或共享的生成紀錄
**Then** 紀錄含擁有者、共享範圍、當時的區塊順序與文字、該次欄與列、純量參數
**And** 不含連線字串；這次保存不呼叫 MCP

**Given** 一筆已保存的生成紀錄
**When** 擁有者或，在共享時，另一位持有終端使用者入口的人開啟它
**Then** 顯示保存的那一份，不回讀樣式，也不呼叫 MCP

**Given** 紀錄是私有
**When** 其他人讀取
**Then** 看不到；產品介面不讀他人的私有生成紀錄

**Given** 非擁有者
**When** 他修改或刪除這筆紀錄
**Then** 拒絕，紀錄不變

**Given** 這次使用的版面
**When** 終端使用者把它存回樣式庫
**Then** 新範本的擁有者是該人，不含業務列、參數值與連線字串
**And** 這次存回不呼叫 MCP

**Given** 他離開這次報表，或產生下一份，且尚未寫入生成紀錄
**When** 他要求匯出剛才那份
**Then** 尚未寫入的那份不能再匯出

## Epic 7: 匯出 PDF 與列印

### Story 7: 匯出 PDF 與列印

As a 終端使用者,
I want 把目前這份報表匯出 PDF 並列印,
So that 紙上的數字和文字等於所開啟的那一份。

CSV 不在這一則。PDF 使用 QuestPDF，預設 A4。

**Acceptance Criteria:**

**Given** 目前這份動態報表，或一筆已開啟的生成紀錄，包含空結果
**When** 他匯出 PDF 或列印
**Then** 數字和文字等於所開啟的那一份
**And** 不發出新的 MCP 呼叫

**Given** 同一份連續匯出兩次
**When** 比較 PDF
**Then** 業務數字相同

**Given** 報表含多個區塊
**When** 產生 PDF
**Then** 預設 A4，換頁以區塊為界，不對位預印表單

## Epic 8: Avalonia 對齊

### Story 8: Avalonia 對齊

As a 操作者,
I want 在桌面殼做與瀏覽器相同的事,
So that 兩個殼共用同一筆工作階段，桌面殼不另開一份資料。

Avalonia 只呼叫工作台 HTTP，使用與 Blazor 同一筆 `sessionId`。

**Acceptance Criteria:**

**Given** Avalonia 與 Blazor 都指向同一個工作台
**When** 桌面殼登入
**Then** bearer 只帶與瀏覽器 cookie 相同來源的 `sessionId`
**And** 任一殼登出後，兩邊同時失效

**Given** 桌面殼已登入且持有入口
**When** 他走 Story 1 至 Story 7 已交付的動作
**Then** 請求進入與 Blazor 相同的端口，控制器不另寫業務判斷
**And** 桌面殼不嵌入 Blazor，不開啟工作台 SQLite，組態只有工作台基底網址

**Given** 兩個殼繪製同一站
**When** 使用 `DESIGN.md` 的權杖
**Then** 只有淺色；最小視窗 1100×720；資訊架構與 `EXPERIENCE.md` 相同

## 未排入

- FR-4 的表結構瀏覽。通道未定。不得用待轉資料庫的 `GET /meta`，也不得為了結構讀業務列。
- FR-7 的先落地轉置。現行讀列是 Story 5 的一次即時呼叫。
- FR-16 的 CSV。寫法未定。
- 零筆或多筆連線的錯誤碼拼法。Story 5 只固定行為。
- MCP 稽核保留天數。三個入口不實作。
- 模型設定、測試連線、詞庫編輯、取數軌跡、側車啟動。
