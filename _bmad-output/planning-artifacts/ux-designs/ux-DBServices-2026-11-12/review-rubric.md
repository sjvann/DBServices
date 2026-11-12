# Spine Pair Review — DBServices

## Overall verdict

這對骨架可以當註記站的下游合約：11-12 PRD 的四條旅程都有主角、編號步驟、高潮與失敗路徑，色票與主要元件也能對上。還不能乾淨抽取的是表註沒有元件、成功與進行中訊息被錯誤樣式吃掉、UJ-3 與 FR-6 的邊界結果沒寫進流程，以及資料平面的同名 UJ／FR 沒有劃界。

## 1. Flow coverage — adequate

對過 `prd.md` 的 UJ-1 至 UJ-4、FR-1 至 FR-8。四條旅程與 FR-1 都有具名主角、編號步驟、高潮與失敗路徑；FR-2 至 FR-8 掛在對應旅程上。addendum 沒有獨立的 UJ／FR 編號。資料平面 PRD 的 UJ-1 至 UJ-3、FR-1 至 FR-21，以及架構脊柱綁定的同一套編號，是另一份產品的工具契約，不計入本節缺漏；編號碰撞見第 7 節。

### Findings

- **high** UJ-3 的失敗路徑只寫了沒有已確認包。來源要求「要的關聯不在已聲明清單裡時，保存被拒絕，既有檢視定義不變」（`prd.md` UJ-3 edge、FR-7）。`EXPERIENCE.md` Key Flows UJ-3 失敗段只寫空清單；Interaction Primitives 只寫不能選包外表欄、關聯端點必須等於已聲明關聯，沒有拒絕保存與定義不變。*Fix:* 在 UJ-3 失敗路徑寫明保存被拒、既有檢視定義不變，並給出人讀得到的原因。
- **high** FR-6 的再讀入規則沒有進 Key Flow。來源規定 Excel 再匯入與資料庫再讀共用比對鍵（去首尾空白、區分大小寫），草稿缺的同鍵註記從上一份已確認包抄入，改名等於刪除再新增、註記不跟著搬家（`prd.md` FR-6）。`EXPERIENCE.md` UJ-4 只走 read-structure；diff-list 與 import-excel 只寫同名註記留在草稿。*Fix:* 在 UJ-4（或 import-excel 與 read-structure 共用的一步）寫明兩種再讀入、抄入與改名規則。
- **medium** UJ-1 的失敗路徑沒有「沒有恰好一筆可對帳的二代平台連線時拒絕確認」。來源把這條放在 UJ-1 edge 與 FR-5。`EXPERIENCE.md` UJ-1 失敗段只寫對不上實體名、說明空白、零張表或零個欄；Voice 的零筆／多筆句只說沒有讀取結構。*Fix:* 把確認當下的連線筆數與待轉庫拒絕寫進 UJ-1 失敗路徑。

## 2. Token completeness — strong

`DESIGN.md` frontmatter 的顏色、字級、圓角、間距與元件 token 都有值。13 個顏色皆為 hex；深色模式已在 Brand & Style 聲明不做。正文與元件物件裡的 `{path.to.token}` 都能對上 frontmatter。Colors 已點名承重配對（正文、主要按鈕、錯誤、尚未註記），並聲明不新增前景／背景組合。

### Findings

無缺漏。

## 3. Component coverage — adequate

DESIGN.md Components 與 frontmatter `components`、EXPERIENCE.md Component Patterns 對過。rail-link、package-bar、purpose-field、import-excel、read-structure、table-list、column-editor、link-editor、diff-list、dbml-block、confirm-dialog、status-message、unmarked-badge、empty-catalog 兩邊都有具體規則。工作台既有按鈕、輸入、表格、對話框已聲明沿用，不另立列。

### Findings

- **high** 四層註記裡的表註沒有元件。`EXPERIENCE.md` UJ-1 步驟 4、UJ-2 步驟 4 要求改表註；IA 區塊表與 `DESIGN.md` Layout、Components 的右欄只有整包功能說明、欄註、關聯、差異與 DBML。*Fix:* 為表註增加視覺列與行為列（屬於選中的表、可空白、不阻擋保存與確認），並放進 IA。
- **high** status-message 的視覺固定為危險色（`DESIGN.md` Components，既有 `.error`），行為卻包含確認結果（`EXPERIENCE.md` Component Patterns）。確認成功與「正在讀取結構。」不是錯誤；讀取中寫的是沒有列名的「status 區」。*Fix:* 錯誤維持 status-message。成功句與進行中句指定元件，並寫明它們不用 danger。
- **low** rail-link-active 有 frontmatter 與 `DESIGN.md` rail-link 視覺句，Component Patterns 沒有同名規則。*Fix:* 在 rail-link 行為列寫明目前站為註記時使用 rail-link-active。

## 4. State coverage — adequate

走過 IA 的登入、換工作、註記站、入口指派、模組設計工具。註記站的未登入／無權限、冷開啟、讀取中、草稿、匯入被拒、零筆多筆與待轉庫、差異、確認被拒、確認成功、唯讀舊包、空設計清單、空白欄註、焦點都有列。離線已聲明不在這一輪。

### Findings

- **medium** 「換工作」與「入口指派」沒有 State Patterns 列。單入口直接進入註記站只在 IA 的目的欄；非管理員「沒有權限。」只在 FR-1 流程。焦點列的「所有上述表面」對不到這兩個表面。*Fix:* 各補一列：換工作含單入口與多入口；入口指派含無權限與保存失敗。
- **medium** 註記站沒有「重開已保存草稿」與「放棄草稿之後」。冷開啟只寫尚無草稿。confirm-dialog 有放棄文案，沒有放棄後的畫面。*Fix:* 寫明重開還原四層與徽章；放棄後回到哪一個畫面（冷開啟或唯讀已確認包）。
- **low** 匯出與保存草稿的伺服器失敗沒有狀態。State Patterns 末段只寫讀結構與確認失敗時，已確認包與已保存檢視不變。*Fix:* 補一列：這兩次失敗不改草稿與已確認包，並指定訊息元件。

## 5. Visual reference coverage — strong

`mockups/`、`wireframes/`、`imports/` 都不存在，沒有檔案。`.memlog.md` 記錄快路：跳過視覺工具、不補畫面。兩份骨架沒有指向缺席的圖。`EXPERIENCE.md` 開頭已寫衝突時以這兩份骨架為準，只出現一次。沒有孤兒，也沒有未說明的引用。

### Findings

無缺漏。

## 6. Bloat & overspecification — strong

DESIGN.md 的語氣停在視覺決定；像素多半在 token 或既有工作台規則上。EXPERIENCE.md 用表寫 IA、文案、元件、狀態與斷點，流程長度對得上來源旅程。沒有下游用不到的裝飾段。末節「敏感資料」把確認對帳只核對實體名、以及個資文字沒有執行按鈕收成一處，值得留下。

### Findings

無缺漏。

## 7. Inheritance discipline — adequate

四條 `sources` 都能對到檔案。元件的 kebab 名稱在兩份骨架的主表一致。`EXPERIENCE.md` 的 `{colors.ink-primary}` 對得到 `DESIGN.md`。`{檢視名稱}`、`{舊包名稱}`、`{包名}` 是文案插槽，不是色票。

### Findings

- **high** 11-12 PRD 與資料平面 PRD、架構脊柱共用 UJ-n、FR-n，指的是不同契約。骨架的 Key Flows 只用註記 PRD 的意思（FR-1 是指派資料治理入口，不是「只經工廠建立資料服務」）。兩份 frontmatter 都沒有寫編號範圍。*Fix:* 在 Foundation 或 frontmatter 寫明本骨架的 UJ-1 至 UJ-4、FR-1 至 FR-8 只指向 `prd-DBServices-2026-11-12`；資料平面的同名編號是約束來源（不讀業務列、連線字串不進畫面），不是本介面的流程編號。
- **medium** 旅程與需求標題沒有逐字沿用來源。來源 UJ-4 是「來源結構變了，方晴再讀一次並保留舊註記。」骨架寫成「方晴再讀一次並保留舊註記」。來源 FR-1 是「指派資料治理入口」，骨架流程標題是「管理員把資料治理入口指派給方晴」。UJ-1 至 UJ-3 只少了來源的句點，並在標題後加了 FR 括號。*Fix:* 標題改回來源原文，FR 編號留在括號。
- **medium** 詞彙與來源第 3 節不是同一份清單。`EXPERIENCE.md` 宣稱與 PRD 第 3 節相同，但只列名稱、不列定義，並把「實體表名／實體欄名」拆成兩條，漏了「模組設計工具／檢視／樣式範本／MCP」那一條裡的檢視、樣式範本、MCP。DESIGN.md 沒有詞彙。資料平面第 3 節是另一份詞彙，不應併進這份清單。*Fix:* 寫明定義以 11-12 PRD 第 3 節為準，條目名稱與該節逐條相同；不要在骨架重寫定義。
- **medium** 斷點與左軌寬度對不齊。`DESIGN.md` Layout 寫窄於 1100px、並與工作台 1099px 相同，左軌 token 只有 `{spacing.rail}` 240px。`EXPERIENCE.md` Responsive 寫大於 1099px 為雙欄、1100px 以下為單欄，並在 720px 以下把左軌收到 200px。寬度 1100px 的欄數相反，200px 沒有 token。*Fix:* 兩份都寫成同一條媒體查詢；若 200px 要保留，放進 DESIGN.md 的間距 token。

## 8. Shape fit — strong

DESIGN.md 順序是 Brand & Style、Colors、Typography、Layout & Spacing、Elevation & Depth、Shapes、Components、Do's and Don'ts，沒有插段。EXPERIENCE.md 的 Foundation、Information Architecture、Voice and Tone、Component Patterns、State Patterns、Interaction Primitives、Accessibility Floor、Key Flows 都在。來源有明確拒絕項，Inspiration & Anti-patterns 有著落。有斷點，Responsive & Platform 有著落。末節「敏感資料」承担交叉禁則，值得留下。

### Findings

無缺漏。

## Mechanical notes

- 兩份 frontmatter 都有 `name`（資料來源註記）、`status: draft`、四條 `sources`、`updated: 2026-11-12`。DESIGN.md 另有 `description`。EXPERIENCE.md 與範例一樣不必有 `description`。四條來源路徑都對得到檔案。
- 沒有 Mermaid。沒有斷掉的檔案連結。
- 名稱不一致：UJ-4 與 FR-1 標題、rail-link-active 只在 DESIGN.md、讀取中的「status 區」對不到 status-message、左軌 240px 與 200px、斷點 1099／1100。見第 1、3、4、7 節。
- `{檢視名稱}`、`{舊包名稱}`、`{包名}` 是文案插槽。抽取 token 時不要把它們當成 `DESIGN.md` 的路徑。
