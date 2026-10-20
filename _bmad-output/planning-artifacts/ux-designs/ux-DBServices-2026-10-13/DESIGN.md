---
name: 檢視與報表
description: 三個獨立入口共用的淺色視覺身份。系統分析師定義檢視，報表設計師排樣式，終端使用者用語句得到動態報表。
status: draft
updated: 2026-10-18
sources:
  - "{planning_artifacts}/prds/prd-DBServices-2026-10-12/prd.md"
  - "{planning_artifacts}/prds/prd-DBServices-2026-10-12/addendum.md"
colors:
  surface-base: '#F3F5F7'
  surface-raised: '#FFFFFF'
  surface-sunken: '#E7ECF1'
  ink-primary: '#1B2430'
  ink-secondary: '#4E5D6C'
  ink-disabled: '#8B98A5'
  border-hairline: '#D0D7DE'
  action: '#0F4C5C'
  action-foreground: '#FFFFFF'
  danger: '#8E1D2C'
  danger-foreground: '#FFFFFF'
  caution: '#7A4E00'
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
  sm: 4px
  md: 6px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  rail: 240px
components:
  commit-button:
    background: '{colors.action}'
    foreground: '{colors.action-foreground}'
    radius: '{rounded.sm}'
  secondary-button:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  style-block:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  report-well:
    background: '{colors.surface-sunken}'
    foreground: '{colors.ink-secondary}'
    radius: '{rounded.sm}'
  error-block:
    background: '{colors.surface-raised}'
    foreground: '{colors.danger}'
    border: '{colors.danger}'
    radius: '{rounded.sm}'
  account-row:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  entry-badge:
    background: '{colors.surface-sunken}'
    foreground: '{colors.ink-secondary}'
    radius: '{rounded.full}'
  intent-status:
    foreground: '{colors.ink-primary}'
    radius: '{rounded.sm}'
  visibility-mark:
    foreground: '{colors.ink-secondary}'
    radius: '{rounded.sm}'
---

## Brand & Style

這是內部工具，不是對外產品。三個入口共用這一套視覺，各自只做一站：模組設計工具定義檢視，AI 報表設計工具引用檢視來排樣式範本，Chat 報表生成器用語句產生動態報表。人在畫面上不選通道，也不看見連線字串。

視覺姿勢是淺色文件頁。密度夠讀表結構與區塊順序，不做成儀表板。語句還沒就緒時，報表區是空的；就緒並送出之後，動態報表才鋪開。樣式範本裡不放業務列。

沒有命名的元件庫。Blazor 與 Avalonia 共用這一組權杖。介面文字用繁體中文。帳號與入口在畫面上分開：建立帳號的表單不出現入口；入口徽章只出現在入口指派。管理者帳號不是一枚可勾選的入口徽章。

## Colors

淺色已確認。色票服務「還沒有報表」與「這一站的提交」。

- **紙面 (`{colors.surface-base}`)** 是應用底。略冷的灰，讓資料表不要漂在純白上。
- **抬升面 (`{colors.surface-raised}`)** 是樣式區塊、清單列、已產生的動態報表。用色塊區分，不用陰影。
- **空報表井 (`{colors.surface-sunken}`)** 只給尚未產生的動態報表。它必須看起來還沒裝進資料。產生成功後，報表改鋪在 `{colors.surface-raised}`。
- **墨色 (`{colors.ink-primary}`)** 是標題與內文。在 `{colors.surface-raised}` 上對比約 15.6:1，在 `{colors.surface-base}` 上約 14.3:1，在 `{colors.surface-sunken}` 上約 13.2:1。
- **次墨 (`{colors.ink-secondary}`)** 是輔助說明、入口徽章與共享範圍。在三個表面上的對比約 6.8:1、6.2:1、5.7:1，高於 4.5:1。
- **停用墨 (`{colors.ink-disabled}`)** 在白底只有約 2.9:1。只用在已停用的控制項，而且不能當唯一狀態；停用原因要用 `{typography.body}` 寫出來。
- **髮線 (`{colors.border-hairline}`)** 分隔列與區塊。
- **提交色 (`{colors.action}` / `{colors.action-foreground}`)** 只用於這一站的提交：發布檢視、儲存樣式、送出語句。白字在其上約 9.5:1。不用來做換站、連結或裝飾。
- **危險色 (`{colors.danger}` / `{colors.danger-foreground}`)** 只用於刪除與拒絕。白字在其上約 8.9:1。讀取失敗不用整面紅底。
- **警示墨 (`{colors.caution}`)** 只用於意圖狀態「多筆候選」的文字。在白底約 7.2:1，在紙面約 6.6:1。不做色塊徽章。

沒有深色模式權杖。這一版只有淺色。成功不要另做綠色。

## Typography

不引入網頁專用展示字體。兩殼都用系統介面字：西文 Segoe UI，中文 Microsoft JhengHei。等寬只用在檢視名稱、錯誤碼與參數鍵。

- `{typography.title}`：入口名稱、檢視名稱、樣式範本名稱。
- `{typography.body}`：控制項、空狀態、錯誤的人話。
- `{typography.meta}`：共享範圍、時間、輔助說明。
- `{typography.code}`：檢視名稱、錯誤碼、參數鍵。

不要用展示級大標。這是工作工具，不是首頁。

## Layout & Spacing

間距是 4 / 8 / 12 / 16 / 24 / 32。相關控制項用 `{spacing.2}`；表面區塊之間用 `{spacing.5}`。

一個入口佔滿整個視窗。不把三站的功能排在同一條軌上。一人持有兩個以上入口時，頁首有換站；換站會換成那一站的整頁。只有一個入口時，不出現換站。管理者帳號另外看到帳號與入口指派，這兩頁也不混進三站的工作區。

樣式與動態報表都是單欄由上到下，寬度隨視窗。表可以橫向捲動。頁面邊距 `{spacing.5}`。相鄰區塊間距 `{spacing.2}`。Blazor 以 1280px 寬為舒適工作寬。Avalonia 最小視窗 1100×720。不做手機版面。

`{spacing.rail}` 只留給單一入口自己的清單與編輯並排，例如檢視清單在左、檢視定義在右。那條欄不是三站導覽。

畫面參考：`mockups/key-view.html`、`mockups/key-style.html`、`mockups/key-chat.html`。骨架與這些畫面衝突時，以骨架為準。

## Elevation & Depth

深度靠色調，不靠陰影。紙面、抬升面、空報表井三層就夠。對話框是唯一允許陰影的表面：模糊 16px、透明度低、不另外上色。拖曳換序時，被拖的 `{components.style-block}` 只加髮線，不放大、不傾斜。

## Shapes

`{rounded.sm}` 用於按鈕、樣式區塊、報表井、輸入框。`{rounded.md}` 只用於對話框。`{rounded.full}` 只用於 `{components.entry-badge}`。不做圓形圖示鈕，不做卡片式儀表板圓角。

## Components

- **commit-button** — `{colors.action}` 底、`{colors.action-foreground}` 字、`{rounded.sm}`。標籤只會是：發布、儲存樣式、送出語句。一個表面上，這一站的提交才用這個樣式。
- **secondary-button** — 白底、髮線、墨色字。接受建議、忽略建議、上移、下移、匯出 PDF、列印、匯出 CSV、存成生成紀錄、存回樣式庫都用它。這些都不是送出語句。
- **style-block** — 白底髮線的一列。左側是順序把手與區塊種類（標題、文字、參數、資料表），右側是繫結摘要。沒有 x、y、寬、高的視覺殘留。
- **report-well** — 動態報表區。還沒有這次報表時整區用 `{colors.surface-sunken}`，內文用 `{colors.ink-secondary}`。產生成功後改為白底，由上到下放這一次的報表。匯出與列印用 secondary-button。
- **error-block** — 白底、危險色文字與 1px 危險色邊。第一行是錯誤碼（等寬），第二行是給人讀的訊息。不鋪滿危險色底。
- **account-row** — 白底髮線的一列，只顯示操作者的顯示名稱。不放入口徽章。
- **entry-badge** — 藥丸形，次墨字，底用空報表井色。只用在入口指派。標籤只會是系統分析師、報表設計師、終端使用者。不做成「管理者」徽章，也不使用「檢視」當作權限標籤。
- **intent-status** — 就緒用 `{colors.ink-primary}`；多筆候選用 `{colors.caution}`；缺少條件用 `{colors.ink-secondary}`。三者都是文字，不是色塊。
- **visibility-mark** — `{typography.meta}` 的「私有」或「共享」。不是入口徽章，也不用提交色。

## Do's and Don'ts

| Do | Don't |
|---|---|
| 提交色只出現在發布、儲存樣式、送出語句 | 用提交色標示目前分頁、換站或「建議」 |
| 還沒有動態報表時讓報表區保持下凹、沒有列 | 用範例列或骨架列冒充尚未產生的報表 |
| 樣式區塊由上到下排，拖曳只改變順序 | 自由座標、對齊參考線、寬高把手 |
| 錯誤同時給出錯誤碼與人話 | 只靠紅底或圖示表示失敗 |
| 等寬字只用於檢視名稱、錯誤碼與參數鍵 | 把整段中文內文設成等寬 |
| 帳號列只顯示人的名稱 | 在建立帳號的表單上放入口勾選 |
| 共享範圍用次墨文字 | 用「檢視」當權限名稱，或把連線字串畫出來 |
