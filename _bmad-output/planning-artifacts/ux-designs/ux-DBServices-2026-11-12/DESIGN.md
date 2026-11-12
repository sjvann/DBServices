---
name: 資料來源註記
description: 報表工作台裡的資料治理入口。沿用 Workbench.Web 的 tokens.css，只為註記站補狀態，不另做品牌。
status: draft
sources:
  - "{planning_artifacts}/prds/prd-DBServices-2026-11-12/prd.md"
  - "{planning_artifacts}/prds/prd-DBServices-2026-11-12/addendum.md"
  - "{project-root}/docs/planning/agent-data-plane/prd.md"
  - "{planning_artifacts}/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md"
updated: 2026-11-12
colors:
  surface-base: '#F8FAFC'
  surface-raised: '#FFFFFF'
  surface-sunken: '#E9EEF6'
  ink-primary: '#0F172A'
  ink-secondary: '#475569'
  ink-disabled: '#94A3B8'
  border-hairline: '#D5DEE8'
  action: '#1E40AF'
  action-soft: '#E8EEF9'
  action-foreground: '#FFFFFF'
  danger: '#DC2626'
  danger-foreground: '#FFFFFF'
  caution: '#92400E'
typography:
  title:
    fontFamily: '"Segoe UI", "Microsoft JhengHei", sans-serif'
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.35'
  body:
    fontFamily: '"Segoe UI", "Microsoft JhengHei", sans-serif'
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  meta:
    fontFamily: '"Segoe UI", "Microsoft JhengHei", sans-serif'
    fontSize: 12px
    fontWeight: '400'
    lineHeight: '1.4'
  code:
    fontFamily: '"Cascadia Mono", Consolas, ui-monospace, monospace'
    fontSize: 13px
    fontWeight: '400'
    lineHeight: '1.45'
rounded:
  sm: 6px
  md: 8px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  rail: 240px
  rail-narrow: 200px
components:
  rail-link:
    background: '{colors.surface-base}'
    foreground: '{colors.ink-primary}'
    radius: '{rounded.sm}'
    padding: '{spacing.2} {spacing.3}'
  rail-link-active:
    background: '{colors.action-soft}'
    foreground: '{colors.action}'
    radius: '{rounded.sm}'
  package-bar:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
    padding: '{spacing.4}'
  purpose-field:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
    font: '{typography.body.fontFamily}'
  table-note:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
    font: '{typography.body.fontFamily}'
  import-excel:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  read-structure:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  table-list:
    background: '{colors.surface-base}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
  column-editor:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  link-editor:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  diff-list:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  dbml-block:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
    font: '{typography.code.fontFamily}'
  confirm-dialog:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    radius: '{rounded.md}'
  status-message:
    background: '{colors.surface-raised}'
    foreground: '{colors.danger}'
    border: '{colors.danger}'
    radius: '{rounded.sm}'
    padding: '{spacing.3}'
  progress-line:
    foreground: '{colors.ink-secondary}'
    font: '{typography.body.fontFamily}'
  result-line:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
    padding: '{spacing.3}'
  unmarked-badge:
    background: '{colors.surface-sunken}'
    foreground: '{colors.caution}'
    radius: '{rounded.full}'
    font: '{typography.meta.fontFamily}'
  empty-catalog:
    foreground: '{colors.ink-secondary}'
    font: '{typography.body.fontFamily}'
---

## Brand & Style

資料來源註記是報表工作台裡的另一個入口，不是另一個產品。持有資料治理入口的人打開註記站，把 Excel 字典或資料庫結構補上表、欄、關聯與整包功能說明，確認後的 DBML 才進入設計目錄。

視覺身份就是工作台現有的 `Workbench.Web/wwwroot/tokens.css`。淺色表面、髮絲邊線、左軌 240px、繁體中文。這一輪不換品牌色，不加深色模式，也不為註記站做第二套字體。新增的只是狀態怎麼借用既有色：動作色代表正在使用與新增，危險色代表刪除與錯誤，警示色代表尚未註記與型別改變。

[ASSUMPTION] 只交付 Workbench.Web。Avalonia 殼這一輪不出現資料治理入口。

## Colors

色票與 `tokens.css` 的自訂屬性一一對應。註記站不新增色名。

- **表面** `{colors.surface-base}` 是頁面與左軌。`{colors.surface-raised}` 是編輯區、對話框與 DBML 區塊。`{colors.surface-sunken}` 是表頭、次要底與徽章底。
- **文字** `{colors.ink-primary}` 是正文。`{colors.ink-secondary}` 是說明與空狀態。`{colors.ink-disabled}` 是停用控制項。
- **動作** `{colors.action}` 與 `{colors.action-foreground}` 只用在主要按鈕（確認、保存）與左軌目前項目。`{colors.action-soft}` 是該項目與「新增」差異列的底。
- **危險** `{colors.danger}` 是錯誤框與「刪除」差異列的字色。按鈕上的白字用 `{colors.danger-foreground}`。註記站沒有危險色的實心主按鈕；確認被拒時用字色與框線。
- **警示** `{colors.caution}` 只用在「尚未註記」與型別、鍵、可空性改變的差異列。

對比沿用工作台已上線的配對，本合約不創造新的前景／背景組合：正文是 `{colors.ink-primary}` 在 `{colors.surface-base}` 或 `{colors.surface-raised}`；主要按鈕是 `{colors.action-foreground}` 在 `{colors.action}`；錯誤是 `{colors.danger}` 在 `{colors.surface-raised}`；尚未註記是 `{colors.caution}` 在 `{colors.surface-sunken}`。

## Typography

四個角色都來自工作台，不另載網頁字型。

- `{typography.title}` 用於頁面標題、左軌品牌「報表工作台」、註記站標題。
- `{typography.body}` 用於說明、欄註、關聯理由、對話框。
- `{typography.meta}` 用於狀態、表頭、徽章「尚未註記」。
- `{typography.code}` 用於實體表名、實體欄名、型別與 DBML 區塊。表名與欄名不改用標題字。

## Layout & Spacing

間距是 4、8、12、16、24、32。左軌寬 `{spacing.rail}`。註記站沿用工作台 `.studio` 的桌面結構：左欄是表清單，右欄是整包功能說明、表註、欄註、關聯、差異與 DBML。內容區在桌面貼齊工作台，不另設 920px 閱讀寬；欄位表單仍可在右欄內分段，避免一列排開 Excel 的十四個來源欄。

寬度大於 1099px 時，註記站是左欄表清單、右欄編輯，左軌寬 `{spacing.rail}`。寬度在 1099px 以下時，註記站改成單欄由上到下：工具列、表清單、表註、欄註、關聯、差異、DBML，左軌仍是 `{spacing.rail}`。寬度在 720px 以下時，表單改一欄，左軌改為 `{spacing.rail-narrow}`。這三條與工作台 `tokens.css` 的媒體查詢相同。

## Elevation & Depth

工作台幾乎不浮起。註記站同樣靠底色與髮絲邊線分層。唯一的浮層是既有確認對話框：遮罩與 `box-shadow: 0 0 16px rgba(27, 36, 48, 0.12)` 維持現況，不把這組陰影收成新色票。差異列、徽章與錯誤框都貼在表面上。

## Shapes

`{rounded.sm}` 用於左軌項目、輸入、表卡、錯誤框與 DBML 區塊。`{rounded.md}` 只用於確認對話框。`{rounded.full}` 只用於「尚未註記」徽章。不做圓形圖示按鈕。

## Components

工作台既有的左軌、按鈕、輸入、表格、錯誤框、對話框與空狀態維持原樣。下面只寫註記站要辨認的物件。行為見 `EXPERIENCE.md`。

- **rail-link** — 左軌「現在的工作」裡的「註記」。未選時字色 `{colors.ink-primary}`，底透明。選中用 **rail-link-active**：底 `{colors.action-soft}`，字 `{colors.action}`，字重 600。
- **package-bar** — 註記站頂部。左為包名與狀態徽章，右為匯入、讀結構、匯出、放棄草稿、確認。狀態「草稿」用 `{colors.ink-secondary}` 徽章；「已確認」用 `{colors.action}` 字在 `{colors.action-soft}` 底。
- **purpose-field** — 整包功能說明。多行文字，字級 `{typography.body}`，寬度跟右欄走。空白時框線仍是 `{colors.border-hairline}`，不改成危險色；缺這段而不能確認時，由 **status-message** 說明。
- **table-note** — 選中那張表的表註。多行文字，樣式與 **purpose-field** 相同。可以空白。空白不改框線顏色，也不阻擋保存或確認。
- **import-excel** — 檔案選擇，樣式同工作台輸入。旁邊不出現連線字串欄位。
- **read-structure** — 次要按鈕。沒有連線下拉。進行中按鈕停用，字色改 `{colors.ink-disabled}`。
- **table-list** — 左欄表名用 `{typography.code}`。選中列框線 `{colors.action}`、底 `{colors.action-soft}`，與工作台 `.table-pick[aria-pressed="true"]` 相同。
- **column-editor** — 一欄一塊。實體欄名與型別用 `{typography.code}`，欄註是多行文字。欄註空白時，名稱旁放 **unmarked-badge**。個資處理是普通文字欄，不用警示底。
- **link-editor** — 每一條已聲明關聯一列：兩端表與欄用 `{typography.code}`，理由是單行文字，可以空白。
- **diff-list** — 再讀之後的另列。新增列底 `{colors.action-soft}`。刪除列字色 `{colors.danger}`，文字不刪線到無法讀。型別、可空、主鍵、外鍵、唯一索引勾記的改變用 `{colors.caution}`。人寫過的註解不在這列裡被劃掉。
- **dbml-block** — 唯讀。`{typography.code}`，底 `{colors.surface-raised}`，可橫向捲動。裡面不出現連線字串或業務列。
- **confirm-dialog** — 既有對話框尺寸。主要動作用實心 `{colors.action}`。次要動作用白底髮絲框。同時只允許一層。
- **status-message** — 只用在錯誤，沿用既有 `.error`。字與框都是 `{colors.danger}`，底仍是 `{colors.surface-raised}`。第一行是給人讀的繁體中文，第二行是穩定代碼，字級 `{typography.meta}`。
- **progress-line** — 進行中的一句，例如「正在讀取結構。」字色 `{colors.ink-secondary}`，沒有框，不用 `{colors.danger}`。
- **result-line** — 確認成功。字色 `{colors.ink-primary}`，框線 `{colors.border-hairline}`，底 `{colors.surface-raised}`。不用 `{colors.danger}`。
- **unmarked-badge** — 文字固定「尚未註記」。底 `{colors.surface-sunken}`，字 `{colors.caution}`，圓角 `{rounded.full}`。
- **empty-catalog** — 模組設計工具左側沒有已確認註記包時的句子，字色 `{colors.ink-secondary}`。沒有插圖。

## Do's and Don'ts

| Do | Don't |
|---|---|
| 沿用 `tokens.css`，註記站只借用 action、danger、caution | 為註記站新增色票、深色模式或第二套字體 |
| 實體表名與實體欄名用 `{typography.code}` | 把表名排成標題，或把 DBML 排成正文 |
| 「尚未註記」只用 `{colors.caution}` 徽章 | 用危險色表示空白欄註；空白欄註仍可確認 |
| 錯誤用 **status-message**；進行中用 **progress-line**；確認成功用 **result-line** | 把成功或進行中畫成危險色，或在錯誤、DBML、草稿、對話框放連線字串、密碼、堆疊秘密或業務列 |
| 左軌只在資料治理入口顯示「註記」 | 把註記和檢視、樣式、產生報表排在同一組工作內容 |
| 確認與拒絕都停在一層對話框或一則狀態 | 做連線選擇器、自由 SQL 框、列預覽或遮罩執行按鈕 |
