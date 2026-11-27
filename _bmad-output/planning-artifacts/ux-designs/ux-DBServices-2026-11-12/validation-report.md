# Validation Report — DBServices

- **DESIGN.md:** `_bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-11-12/DESIGN.md`
- **EXPERIENCE.md:** `_bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-11-12/EXPERIENCE.md`
- **Run at:** 2026-11-12

## Overall verdict

這對骨架可以當註記站的下游合約：11-12 PRD 的四條旅程都有主角、編號步驟、高潮與失敗路徑，色票與主要元件也能對上。走查當下還不能乾淨抽取的是表註沒有元件、成功與進行中訊息被錯誤樣式吃掉、UJ-3 與 FR-6 的邊界結果沒寫進流程，以及資料平面的同名 UJ／FR 沒有劃界。

走查之後，上述 high 與對得上的 medium 已寫回兩份骨架。這份報告保留走查當下的判定，不把修正後的檔案重新評分。

## Category verdicts

- Flow coverage — adequate
- Token completeness — strong
- Component coverage — adequate
- State coverage — adequate
- Visual reference coverage — strong
- Bloat & overspecification — strong
- Inheritance discipline — adequate
- Shape fit — strong

## Findings by severity

### Critical (0)

無。

### High (5)

- UJ-3 的失敗路徑只寫了沒有已確認包。來源要求關聯不在已聲明清單時保存被拒，既有檢視定義不變。（EXPERIENCE.md Key Flows UJ-3）
- FR-6 的再讀入規則沒有進 Key Flow。比對鍵、從已確認包抄入、改名等於刪除再新增，當時只留在來源。（EXPERIENCE.md UJ-4）
- 表註沒有元件。旅程要求改表註，右欄當時沒有這一塊。（DESIGN.md Components、EXPERIENCE.md IA）
- status-message 的視覺是危險色，行為卻包含確認成功與進行中。（DESIGN.md Components、EXPERIENCE.md Component Patterns）
- 11-12 PRD 與資料平面共用 UJ／FR 編號，骨架當時沒有劃界。（兩份 frontmatter 與 Foundation）

### Medium (6)

- UJ-1 的失敗路徑沒有「沒有恰好一筆可對帳的二代平台連線時拒絕確認」。
- 「換工作」與「入口指派」沒有 State Patterns 列。
- 註記站沒有「重開已保存草稿」與「放棄草稿之後」。
- 旅程與需求標題沒有逐字沿用來源。
- 詞彙清單與 11-12 PRD 第 3 節不是同一份。
- 斷點 1099／1100 不一致，左軌 200px 沒有 token。

### Low (2)

- rail-link-active 沒有行為列。
- 匯出與保存草稿的伺服器失敗沒有狀態。

## Mechanical notes

- 走查時兩份都是 `status: draft`。沒有 Mermaid，沒有斷掉的檔案連結。
- `{檢視名稱}`、`{舊包名稱}`、`{包名}` 是文案插槽，不是色票路徑。
