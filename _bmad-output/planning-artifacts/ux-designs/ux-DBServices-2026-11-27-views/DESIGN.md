---
name: 模組設計工作台
description: 既有檢視頁的畫布。左樹只到資料表，畫布上的表展開到欄位，整體屬性在畫布右邊，SQL 在畫布正下方。
status: draft
updated: 2026-11-27
sources:
  - "c:/Users/user/.cursor/plans/模組設計工作台_86280d4f.plan.md"
  - "imports/views-current.png"
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
  pk:
    fontFamily: '"Segoe UI", "Microsoft JhengHei", sans-serif'
    fontSize: 14px
    fontWeight: '700'
    lineHeight: '1.5'
  sql:
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
  library: 280px
components:
  table-tree:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    meta: '{colors.ink-secondary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  canvas-table:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.md}'
  column-row:
    foreground: '{colors.ink-primary}'
    pk: '{typography.pk}'
    meta: '{colors.ink-secondary}'
  join-line:
    stroke: '{colors.ink-primary}'
  property-sheet:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
  sql-band:
    background: '{colors.surface-sunken}'
    foreground: '{colors.ink-primary}'
    font: '{typography.sql}'
  result-grid:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
  join-menu:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
---

# 模組設計工作台 — Design Spine

視覺身份沿用工作台既有的 `Workbench.Web/wwwroot/tokens.css`。本頁不另做一套顏色或字級。下面的 token 就是該檔目前的值。衝突時以這份骨架為準，不以 `imports/views-current.png` 為準。

## Brand & Style

這一頁是系統分析師在既有報表工作台裡定義檢視的畫布。它要看起來像一件會畫表、拉線、改查詢的工具，而不是說明書。

左欄只負責找出資料表。表的欄位、主鍵、勾選與關聯都發生在畫布上的表卡。整份查詢的條件放在畫布右邊的屬性表。編成的 SQL 始終貼在畫布下面，不另換一種畫面。

`imports/views-current.png` 是被退回的現況：表名底下把欄位說明整段攤開，右邊用虛線空區代替畫布。那個畫面不要再被當成視覺參考。

## Colors

沒有新的品牌色。表面、文字、動作、危險與警告都用 `tokens.css` 已有的名稱。

| Token | 用在 |
|---|---|
| `{colors.surface-base}` | 頁面底 |
| `{colors.surface-raised}` | 樹、表卡、屬性表、結果表、右鍵選單 |
| `{colors.surface-sunken}` | SQL 區塊 |
| `{colors.ink-primary}` | 表名、欄名、JOIN 線 |
| `{colors.ink-secondary}` | 型別、次要說明 |
| `{colors.ink-disabled}` | 不可用 |
| `{colors.border-hairline}` | 區分隔線、表卡外框 |
| `{colors.action}` | 主要動作。不用來把主鍵塗成另一個顏色 |
| `{colors.danger}` | 阻擋儲存或編譯失敗 |

主鍵只用字重區分，不另配顏色。

## Typography

字族沿用 `{typography.body.fontFamily}`。標題用 `{typography.title}`，內文用 `{typography.body}`，次要用 `{typography.meta}`。

主鍵欄名用 `{typography.pk}`：與欄名同一字級，字重 700。其他欄名維持 `{typography.body}`。

SQL 區塊用 `{typography.sql}`。

## Layout & Spacing

沿用 `.studio` 的兩欄：左欄寬 `{spacing.library}`，右邊吃剩下一欄。間距用 `{spacing.1}` 到 `{spacing.6}`。

右邊由上到下是工具列、設計區、查詢結果。設計區裡，畫布在左、屬性表貼在畫布右邊。SQL 只佔畫布的正下方，不伸進屬性表那一欄，也不取代畫布。

寬度 1099px 以下，計畫裡的單欄規則仍在：樹、設計區、結果由上到下。設計區內部仍是畫布、畫布下的 SQL、再來才是屬性表。

## Elevation & Depth

沿用工作台現有的平面。表卡與屬性表靠 `{colors.border-hairline}` 和 `{colors.surface-raised}` 分開，不另加陰影層。右鍵選單浮在線的上方，底色 `{colors.surface-raised}`。

## Shapes

圓角只用 `{rounded.sm}`、`{rounded.md}`。表卡用 `{rounded.md}`。樹列與右鍵選單用 `{rounded.sm}`。

## Components

### 資料表樹

左欄是 TreeView。節點只到資料表，樹裡不出現欄位。列上是表名。搜尋仍可用，只過濾表名。

### 畫布表卡

被拖上畫布的資料表是一張卡。卡首是表名。卡身列出該表的欄位。欄位列前方是勾選框，接著是欄名。主鍵欄名用 `{typography.pk}`。

### JOIN 線

線連的是兩張表卡上的兩個欄位，線色 `{colors.ink-primary}`。線上不放「拉線」按鈕。

### JOIN 選單

右鍵點在線上，選單列出 inner、left join、right join。選單用 `{components.join-menu}`。

### 屬性表

貼在畫布右邊，底色 `{colors.surface-raised}`，框線 `{colors.border-hairline}`。裡面是整份查詢的筆數、不重複、篩選、分組、彙總、排序。不在表卡裡重複這組欄位。

### SQL 區塊

在畫布正下方，底色 `{colors.surface-sunken}`，字用 `{typography.sql}`。與畫布同時在。

### 查詢結果

在 SQL 區塊下面，沿用工作台的表格式樣：`{components.result-grid}`。

## Do's and Don'ts

- 左欄用 TreeView，停在資料表。
- 欄位只出現在畫布上的表卡，主鍵用粗體。
- 關聯是欄位到欄位的線。種類用右鍵選單。
- 整體屬性只放在畫布右邊的屬性表。
- SQL 與畫布同時呈現。
- 不要把欄位說明整段鋪在左欄。
- 不要用每欄一顆「拉線」按鈕代替連線。
- 不要為了看 SQL 把畫布換成另一個畫面。
- 不要為這一頁另做一套顏色。
