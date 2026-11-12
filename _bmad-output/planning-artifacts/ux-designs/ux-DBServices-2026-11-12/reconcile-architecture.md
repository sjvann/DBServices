# Reconcile — 資料平面架構 2026-10-11

來源：`_bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md`

## 已進入骨架

- 錯誤要能讓人讀，且不含連線字串、密碼與堆疊中的秘密。
- 連線字串留在伺服器程序的組態。註記站不收集它。

## 未寫進骨架的質性內容

- 這份架構的範圍是 MCP 宿主與共用工具契約的後續表面。它沒有寫資料來源註記、DBML、Excel、Blazor、Avalonia 或工作台。
- AD-7 的 MCP 錯誤碼清單沒有套用到註記站。註記站的錯誤碼拼法仍留給後續架構。
- stdio、側車、拖拉設計、稽核檔路徑沒有進入這份介面合約。
