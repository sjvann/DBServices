---
name: 報表工作台
description: 已登入操作者用來預覽資料、排範本、用語句套版，並在確認後才看到列的內部工作台。此檔是 Blazor 與 Avalonia 兩殼共用的視覺身份。
status: final
updated: 2026-10-13
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
  confirm-button:
    background: '{colors.action}'
    foreground: '{colors.action-foreground}'
    radius: '{rounded.sm}'
  secondary-button:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  canvas-block:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  preview-well:
    background: '{colors.surface-sunken}'
    foreground: '{colors.ink-secondary}'
    radius: '{rounded.sm}'
  call-list:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  error-block:
    background: '{colors.surface-raised}'
    foreground: '{colors.danger}'
    border: '{colors.danger}'
    radius: '{rounded.sm}'
  trace-row:
    background: '{colors.surface-base}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  connection-row:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  account-row:
    background: '{colors.surface-raised}'
    foreground: '{colors.ink-primary}'
    border: '{colors.border-hairline}'
    radius: '{rounded.sm}'
  role-badge:
    background: '{colors.surface-sunken}'
    foreground: '{colors.ink-secondary}'
    radius: '{rounded.full}'
  intent-status:
    foreground: '{colors.ink-primary}'
    radius: '{rounded.sm}'
---

## Brand & Style

報表工作台是給已登入操作者的內部工具。三類工作共用這套視覺：資料層級 MCP 的規劃設計師核對具名連線與工具；善用 MCP 制訂智慧報表的範本編輯師在畫布上排元件與繫結；終端使用者用語句取得動態報表，確認之後才進入預覽頁。範本與常用項目都不保存資料列。連線字串不出現在畫面上。

視覺姿勢是淺色的文件頁加上一次確認。密度偏高，因為規劃與編輯要同時看見連線、欄位與確認清單。確認前，預覽頁是空的；確認後，列才出現在那一頁。匯出與列印放在預覽頁。註解、背景與圖片留待以後，這一版不畫。

沒有命名的元件庫。Blazor 與 Avalonia 共用這一組權杖。介面文字用繁體中文。帳號與角色在畫面上分開：建立帳號的表單不出現角色；角色徽章只出現在角色指派。管理者帳號不是一枚可勾選的工作徽章。

## Colors

淺色已確認。整組色票服務「確認才取業務列」。

- **紙面 (`{colors.surface-base}`)** 是應用底。略冷的灰，讓資料表不要漂在純白上。
- **抬升面 (`{colors.surface-raised}`)** 是畫布區塊、確認清單、連線列。用色塊區分，不用陰影。
- **預覽井 (`{colors.surface-sunken}`)** 只給尚未確認的預覽。它必須看起來還沒裝進資料。確認成功後，預覽改鋪在 `{colors.surface-raised}`。
- **墨色 (`{colors.ink-primary}`)** 是標題與內文。在 `{colors.surface-raised}` 上對比約 15.6:1，在 `{colors.surface-base}` 上約 14.3:1，在 `{colors.surface-sunken}` 上約 13.2:1。
- **次墨 (`{colors.ink-secondary}`)** 是輔助說明與角色徽章。在三個表面上的對比約 6.8:1、6.2:1、5.7:1，高於 4.5:1。
- **停用墨 (`{colors.ink-disabled}`)** 在白底只有約 2.9:1。只用在已停用的控制項，而且不能當唯一狀態；停用原因要用 `{typography.body}` 寫出來。
- **髮線 (`{colors.border-hairline}`)** 分隔列與區塊。
- **確認色 (`{colors.action}` / `{colors.action-foreground}`)** 只用於會發出填數呼叫的按鈕。白字在其上約 9.5:1。不用來做導覽選取、連結或裝飾。
- **危險色 (`{colors.danger}` / `{colors.danger-foreground}`)** 只用於刪除與拒絕。白字在其上約 8.9:1。查詢失敗不用整面紅底。
- **警示墨 (`{colors.caution}`)** 只用於意圖狀態 `ambiguous` 的文字。在白底約 7.2:1，在紙面約 6.6:1。不做色塊徽章。

沒有深色模式權杖。這一版只有淺色。成功不要另做綠色；確認按鈕可按，就是就緒。

## Typography

不引入網頁專用展示字體。兩殼都用系統介面字：西文 Segoe UI，中文 Microsoft JhengHei。等寬只用在工具名、錯誤碼、`ServerType` 與呼叫參數鍵。

- `{typography.title}`：表面名稱、範本名稱。
- `{typography.body}`：控制項、確認清單、空狀態、錯誤的人話。
- `{typography.meta}`：時間、軌跡次要欄、輔助說明。
- `{typography.code}`：`query`、`count`、`tables`、`fields`、錯誤碼、`mcp` / `web`。

不要用展示級大標。這是工作台，不是首頁。

## Layout & Spacing

間距是 4 / 8 / 12 / 16 / 24 / 32。相關控制項用 `{spacing.2}`；表面區塊之間用 `{spacing.5}`。

左軌寬 `{spacing.rail}`，組名用 `{typography.meta}`。有被指派的工作角色才出現對應的組：規劃設計師是活資料預覽；範本編輯師是範本庫；終端使用者是交談套版、常用清單。預覽在擁有任一工作角色時出現，因為確認後都進入這一頁。編輯器從範本庫的一列進去，不佔軌上常駐位置。只有管理者帳號看得到另外一組：帳號、角色指派、模型設定與測試。

畫布是單欄由上到下，寬度隨視窗，不設閱讀用的窄欄上限。預覽是獨立頁，同樣由上到下排這一頁的報表，表可以橫向捲動。頁首是匯出 PDF、列印、匯出 CSV。這一頁的版面要容得下以後的註解、背景與圖片；這一版不放這些工具。取數軌跡在寬度足夠時貼在主欄右側；不足時改到主欄下方。Blazor 以 1280px 寬為舒適工作寬，低於 1100px 時軌跡改到下方。Avalonia 最小視窗 1100×720。不做手機版面。

頁面邊距 `{spacing.5}`。畫布上相鄰元件間距 `{spacing.2}`。

畫面參考：`mockups/key-live-preview.html`、`mockups/key-editor.html`、`mockups/key-preview.html`、`mockups/key-role-assign.html`。

## Elevation & Depth

深度靠色調，不靠陰影。紙面、抬升面、預覽井三層就夠。對話框是唯一允許陰影的表面：模糊 16px、透明度低、不另外上色。拖曳換序時，被拖的 `{components.canvas-block}` 只加髮線，不放大、不傾斜。

## Shapes

`{rounded.sm}` 用於按鈕、畫布區塊、預覽井、輸入框。`{rounded.md}` 只用於對話框。`{rounded.full}` 只用於 `{components.role-badge}`。不做圓形圖示鈕，不做卡片式儀表板圓角。

## Components

- **confirm-button** — `{colors.action}` 底、`{colors.action-foreground}` 字、`{rounded.sm}`。標籤只會是來源裡的確認動作：查詢、計數、範本預覽、確認。一個表面上，即將執行的那一顆才用這個樣式。
- **secondary-button** — 白底、髮線、墨色字。接受建議、忽略建議、加入關鍵字、匯出 PDF、列印、匯出 CSV、儲存範本、儲存常用項目都用它。接受建議不是確認，不能畫成 confirm-button。
- **canvas-block** — 白底髮線的一列。左側是順序把手與元件種類（標題、文字、參數、資料表、計數），右側是繫結摘要。沒有 x、y、寬、高的視覺殘留，例如沒有自由定位的選取框。
- **preview-well** — 預覽頁的本文。沒有快照時整頁用 `{colors.surface-sunken}`，內文用 `{colors.ink-secondary}`，寫明這裡還沒有業務列。確認成功後改為白底，由上到下放這一頁的報表。頁首的匯出與列印用 secondary-button。不在這一版畫註解、背景或圖片。
- **call-list** — 確認前列出將執行的填數呼叫。工具名用 `{typography.code}`。集合必須與按下 confirm-button 後真正發出的相同。
- **error-block** — 白底、危險色文字與 1px 危險色邊。第一行是錯誤碼（等寬），第二行是給人讀的訊息。不鋪滿危險色底。
- **trace-row** — 一筆結構呼叫或填數呼叫。含工具名、連線名稱、結果或錯誤碼。不含業務列，不含連線字串。
- **connection-row** — 只顯示連線名稱與 `ServerType`。沒有連線字串欄位，也沒有密碼欄。
- **account-row** — 白底髮線的一列，只顯示操作者的顯示名稱。不放角色徽章，不放工作種類。
- **role-badge** — 藥丸形，次墨字，底用預覽井色。只用在角色指派。工作角色的標籤是規劃設計師、範本編輯師、終端使用者。群組角色的標籤是設計角色、檢視角色。不做成「管理者」徽章。
- **intent-status** — `ready` 用 `{colors.ink-primary}`；`ambiguous` 用 `{colors.caution}`；`incomplete` 用 `{colors.ink-secondary}`。三者都是文字，不是色塊。

## Do's and Don'ts

| Do | Don't |
|---|---|
| 確認色只出現在會發出填數呼叫的按鈕 | 用確認色標示目前分頁、連結或「建議」 |
| 確認前讓預覽頁保持下凹、沒有列 | 用範例列或骨架列冒充尚未確認的預覽 |
| 預覽頁只放這一次的報表、匯出與列印 | 在這一版加上註解、背景或圖片工具 |
| 錯誤同時給出錯誤碼與人話 | 只靠紅底或圖示表示失敗 |
| 畫布區塊由上到下排，拖曳只改變順序 | 自由座標、對齊參考線、寬高把手 |
| 連線列只顯示名稱與 `ServerType` | 任何連線字串、密碼或金鑰輸入 |
| 等寬字只用於工具名、錯誤碼與參數鍵 | 把整段中文內文設成等寬 |
| 帳號列只顯示人的名稱 | 在建立帳號的表單上放角色勾選 |
