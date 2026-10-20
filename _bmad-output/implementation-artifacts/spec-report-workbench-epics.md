---
title: 報表工作台史詩已改對齊
status: superseded
updated: 2026-10-20
superseded_by:
  - _bmad-output/planning-artifacts/epics/epics-DBServices-2026-10-13/epics.md
  - _bmad-output/specs/spec-report-workbench/stories.yaml
  - _bmad-output/specs/spec-report-workbench/SPEC.md
withdrawn_revision: dd8b0906994406207fc8e47af46dffbd3df3a3aa
---

# 報表工作台：對齊後的故事

2026-10-13 的實作規格已撤回。那一版要求單一工作台、確認清單、五種元件含計數、常用項目、群組 design/view、活資料預覽，以及會啟動側車的模型設定。那些任務不要再實作。

現行故事在 `stories.yaml`，拆解在 `epics.md`。契約是 SPEC 的 CAP-9 至 CAP-13，產品範圍是 PRD 的 FR-1 至 FR-16。

| id | 標題 | 範圍 |
| --- | --- | --- |
| 1 | 登入與入口 | CAP-9。只做 Blazor 與單一宿主 |
| 2 | 檢視目錄與發布 | CAP-10 已定的目錄與發布。不做表結構瀏覽 |
| 3 | 樣式範本 | CAP-11 的報表設計師路徑。終端使用者存回不在這一則 |
| 4 | 未就緒不讀列 | CAP-12 尚未就緒。比對只在 `Workbench.Core`，不啟動側車 |
| 5 | 就緒後呼叫一次檢視 | CAP-12 的就緒路徑。頂層 `connectionId`，沒有 `filters` |
| 6 | 生成紀錄與存回樣式 | CAP-13 的保存與再開，以及 CAP-11 的存回樣式庫 |
| 7 | 匯出 PDF 與列印 | CAP-13 的 PDF 與列印。不含 CSV |
| 8 | Avalonia 對齊 | 桌面殼只經工作台 HTTP，共用同一筆 `sessionId` |

未排入：規劃期表結構通道、先落地轉置、CSV 寫法、零筆或多筆連線的錯誤碼拼法、稽核保留天數、側車啟動。

`GET /connections` 仍在，三個入口不渲染清單。側車專案留在倉儲，三個入口不啟動它。
