---
name: DBServices 文件站
description: 公開 DocFX 文件的儀器面板。冷色表面、青色訊號、髮絲邊線，讓工程師在長文與程式碼之間停留。
status: draft
updated: 2026-10-28
sources: []
colors:
  surface-base: '#F4F7FB'
  surface-raised: '#FFFFFF'
  surface-sunken: '#E7EEF6'
  ink-primary: '#0B1220'
  ink-secondary: '#44556B'
  border-hairline: '#D3DDE8'
  signal: '#0A6274'
  signal-ink: '#F4FBFC'
  signal-bright: '#22D3EE'
  danger: '#B42318'
  surface-base-dark: '#070B12'
  surface-raised-dark: '#101826'
  surface-sunken-dark: '#0B1220'
  ink-primary-dark: '#E8EEF7'
  ink-secondary-dark: '#A3B3C7'
  border-hairline-dark: '#2A3A50'
  signal-dark: '#8EF3E4'
  signal-ink-dark: '#042028'
  code-bg: '#0B1220'
typography:
  brand:
    fontFamily: '"IBM Plex Sans", "Segoe UI", "Noto Sans TC", sans-serif'
    fontSize: 16px
    fontWeight: '600'
    lineHeight: '1.2'
    letterSpacing: 0.04em
  title:
    fontFamily: '"IBM Plex Sans", "Segoe UI", "Noto Sans TC", sans-serif'
    fontSize: 32px
    fontWeight: '650'
    lineHeight: '1.25'
    letterSpacing: -0.02em
  heading:
    fontFamily: '"IBM Plex Sans", "Segoe UI", "Noto Sans TC", sans-serif'
    fontSize: 22px
    fontWeight: '600'
    lineHeight: '1.3'
    letterSpacing: -0.02em
  body:
    fontFamily: '"IBM Plex Sans", "Segoe UI", "Noto Sans TC", sans-serif'
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.7'
  kicker:
    fontFamily: '"JetBrains Mono", ui-monospace, "Cascadia Code", monospace'
    fontSize: 12px
    fontWeight: '500'
    lineHeight: '1.4'
    letterSpacing: 0.08em
  code:
    fontFamily: '"JetBrains Mono", ui-monospace, "Cascadia Code", monospace'
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.55'
rounded:
  sm: 2px
  md: 4px
  lg: 6px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  measure: 74ch
components:
  button-primary:
    background: '{colors.signal}'
    foreground: '{colors.signal-ink}'
    radius: '{rounded.md}'
    minHeight: 44px
  button-ghost:
    background: transparent
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.md}'
  card:
    background: '{colors.surface-raised}'
    border: '{colors.border-hairline}'
    radius: '{rounded.lg}'
  code-block:
    background: '{colors.code-bg}'
    rail: '{colors.signal}'
    radius: '{rounded.lg}'
  hero:
    background: '{colors.surface-raised}'
    border: '{colors.border-hairline}'
    radius: '{rounded.lg}'
---

## Brand & Style

[ASSUMPTION] 這是給外部 .NET 工程師讀的公開文件，不是產品行銷頁，也不是檢視與報表那套淺色工作台。先進科技感在這裡是儀器面板：冷、準、可掃讀。訊號色只標出可以走的下一步，正文保持安靜。

視覺繼承 DocFX modern（Bootstrap）。本檔只寫差值。淺色是冷色紙面，深色是同一套系統的夜讀面。主題仍跟 DocFX 的 auto，跟作業系統走。

## Colors

[ASSUMPTION] 訊號從原本的綠色改成青色。綠色在這站讀起來像狀態標籤；青色用來標連結、作用中的導覽與程式碼軌道。

- `{colors.surface-base}` 是淺色紙面。`{colors.surface-base-dark}` 是深色底，不是純黑。
- `{colors.surface-raised}` / `{colors.surface-raised-dark}` 抬起卡片、hero、頂欄。
- `{colors.ink-primary}` 與 `{colors.ink-primary-dark}` 是正文。次要說明用 `{colors.ink-secondary}` / `{colors.ink-secondary-dark}`。
- `{colors.signal}` 是淺色上的連結與主按鈕。深色連結用 `{colors.signal-dark}`。主按鈕在深色改填 `{colors.signal-bright}`，字用 `{colors.signal-ink-dark}`。
- `{colors.code-bg}` 兩種主題的程式碼區塊都用同一塊終端底。
- `{colors.danger}` 只留給 DocFX 既有的錯誤與警告色，不拿來做裝飾。

對比已核過：淺色訊號字在紙面上 6.49:1，深色訊號字在底面上 15.10:1，深色按鈕字在亮青上 9.35:1。

## Typography

[ASSUMPTION] 沿用已載入的 IBM Plex Sans 與 JetBrains Mono。中文後備 Noto Sans TC、Microsoft JhengHei。不另加展示用字體。

`{typography.kicker}` 只給分類標籤與小標籤。`{typography.title}` 給頁面第一個標題。正文行長停在 `{spacing.measure}`。程式碼用 `{typography.code}`。

## Layout & Spacing

桌面是三欄：左側目錄、中間文章、右側本頁目錄。這是 DocFX modern 的骨架，不改。

文章寬度 `{spacing.measure}`。區塊間距沿 `{spacing.4}` 到 `{spacing.6}`。窄於 1100px 時首頁卡片兩欄；窄於 640px 時一欄，主按鈕拉滿。頂欄在捲動時黏住。

## Elevation & Depth

深度靠表面色差與 `{colors.border-hairline}`，不用投影。頂欄可以用一層半透明底加模糊，讓長文從下面穿過時仍讀得出品牌。程式碼區塊用內凹的 `{colors.code-bg}`，左邊一條訊號軌，不外發光。

## Shapes

[ASSUMPTION] 圓角收在 `{rounded.sm}` 到 `{rounded.lg}`。卡片與程式碼用 `{rounded.lg}`，按鈕與搜尋框用 `{rounded.md}`，小標籤用 `{rounded.sm}`。不把標籤做成膠囊。

## Components

- **頂欄**：品牌用 `{typography.brand}`。作用中的頂層項目用訊號色，底下一條 2px 訊號線。搜尋框是內凹輸入，邊線是髮絲。
- **目錄與本頁目錄**：作用中項目左邊 2px 訊號軌，底是訊號色 10% 混在表面上。其餘項目用次要墨色。
- **Hero**：只在首頁。格線只鋪在這個區塊，向右淡出。分類列用 `{typography.kicker}` 與 `{colors.signal}`。
- **卡片**：髮絲邊。滑過時邊線改成訊號色，底混一層很淡的訊號色。
- **主按鈕**：填 `{colors.signal}`，字 `{colors.signal-ink}`。最低高度 44px。幽靈按鈕只有髮絲邊。
- **程式碼**：區塊一律 `{colors.code-bg}`，左邊訊號軌。行內程式碼用訊號色淡底，圓角 `{rounded.sm}`。
- **表格**：表頭用次要墨色與稍寬的字距。列滑過時混一層淡訊號色。

## Do's and Don'ts

- 訊號色只用在連結、作用中導覽、主按鈕、程式碼軌道、kicker。
- 格線只出現在首頁 hero。
- 正文不加文字陰影，程式碼不加外發光。
- 不改文章句子，不改目錄結構。
- 保留 DocFX 的搜尋、主題切換、目錄與本頁目錄。
---
