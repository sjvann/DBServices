---
title: Sprint Change Proposal — 模組視圖 Discovery
status: approved
created: 2026-10-15
project: DBServices
mode: incremental
scope: moderate
---

# Sprint Change Proposal：模組視圖 Discovery

日期：2026-10-15。編輯方式：逐項批准。十項修改提案皆已批准，並已於同日寫入來源文件。`DbServices.Mcp/ToolCatalog.cs` 與測試尚未修改。

## 1. Issue Summary

2026-10-15 已確認三層歸屬：資料表只留 Web Server；模組視圖與 MCP 留在 DBServices；ExportData 不接 MCP，兩個專案不合併。Agent 的 Discovery 只廣告已發布模組視圖。表級 Call 若保留，只作營運除錯，不進公開目錄，也不得在視圖尚未發布時代替它。

資料平面 PRD 已寫入 FR-16 至 FR-21，MVP 改為 UJ-3。史詩、兩份架構、報表工作台 PRD 正文、UX、規格與對外說明仍要求公開清單恰好是 `tables`、`fields`、`get`、`query`、`count`、`exists`。

資料平面 Epic 1 的 Story 1.1 至 1.7 都在 `review`。`sprint-status.yaml` 裡 `epic-1` 為 `in-progress`，最後更新是 2026-10-11。若依舊驗收接受，Agent 會繼續掃表。

證據：

- 資料平面 PRD 文首與第 4.6 節：Discovery 只列已發布模組視圖；沒有已發布視圖時清單是空的，不回退。
- 報表工作台 PRD 第 0 節把願景、UJ-1、FR-4 至 FR-10、FR-12、FR-14、FR-20、SM-1 標成待改寫。
- `_bmad-output/planning-artifacts/epics.md` 的 FR-3 與 Story 1.1 仍要求清單恰好是那六個工具。
- 2026-10-11 架構 AD-2：公開表面恰好六個工具。2026-10-12 架構沿用：工作台只呼叫 `tables`、`fields`、`query`、`count`。

## 2. Impact Analysis

### Epic Impact

資料平面 Epic 1「外部 Agent 安全讀取具名業務庫」不能照原計畫做完。完成條件從「發現六個唯讀工具」改為「Discovery 只列已發布模組視圖」。宿主骨架、`DbServiceFactory`、具名連線、稽核，以及拒絕 `insert`／`update`／`delete`／`sql` 仍保留。

新增 Epic 2，承接 FR-16 至 FR-21。現有覆蓋圖只到 FR-15。

Story 1.2 至 1.5、1.7 改為營運除錯路徑，不再是 Agent 的公開成功路徑。Story 1.6 的稽核鍵保留；公開呼叫的 `tool` 是模組視圖名稱。

報表工作台三個史詩尚未進 sprint。改範圍，不刪除：

- Epic 1：表級瀏覽只留 Web API。MCP 的高潮改成呼叫一個已發布模組視圖。
- Epic 2：MCP 範本綁一個已發布模組視圖，不按元件掃表。
- Epic 3：一次確認對上該範本所綁的模組視圖呼叫。

不新增拖拉設計器史詩，也不新增與 ExportData 共用的關聯程式庫史詩。

順序：先改資料平面的公開目錄，再讓工作台消費它。工作台的登入與 Web API 單表瀏覽不必等新史詩。工作台的 MCP 填數必須等模組視圖目錄先存在。

### Story Impact

| 故事 | 處置 |
|------|------|
| 資料平面 1.1 | 改寫。Discovery 等於已發布模組視圖的名稱集合 |
| 資料平面 1.2–1.5、1.7 | 角色改為營運除錯呼叫者。參數與錯誤碼先留 |
| 資料平面 1.6 | 稽核鍵不變。表級範例只覆蓋除錯路徑 |
| 資料平面 2.1–2.5 | 新增。人手寫視圖的發布、解說、目錄、連通與唯讀呼叫 |
| 工作台 1.5、1.6 | 表級瀏覽與看列只走 Web API |
| 工作台 1.7、1.8 | 改成模組視圖呼叫與對得上的稽核 |
| 工作台 2.2、2.3、3.1、3.3 | 繫結與確認清單改成一個模組視圖 |
| 工作台 1.1–1.3、2.1、2.4、3.2、3.4–3.8 | 不改 |

`review` 中的 Story 1.1 至 1.7 不得依六個工具的驗收標成 `done`。

### Artifact Conflicts

| 文件 | 衝突 |
|------|------|
| `docs/planning/agent-data-plane/prd.md` | FR-16 至 FR-21 已寫入。UJ-1、UJ-2 的路徑仍像公開主路徑 |
| 報表工作台 `prd.md` 與 `addendum.md` | 第 0 節標了待改寫，正文與 MCP 工具表仍是表級填數 |
| `epics.md` | FR-3 與 Story 1.1 鎖定六個工具 |
| 工作台 `epics.md` | 填數轉發與故事驗收鎖定四個表級工具 |
| 2026-10-11 `ARCHITECTURE-SPINE.md` | AD-2 公開表面恰好六個工具 |
| 2026-10-12 `ARCHITECTURE-SPINE.md` | 繼承該 AD-2，確認清單按元件 `query`／`count` |
| `EXPERIENCE.md`、`DESIGN.md` | 高潮與等寬字範例是 `query`、`count`、`tables`、`fields` |
| 資料平面與工作台規格、Story 1.1 實作規格、`docs/` 導覽 | 對外說明仍是六個唯讀工具 |

`docs/web/index.md` 的 `SetupServer` 不在這次範圍。`AGENTS.md` 沒有這條契約。不改部署腳本或 CI。不在這次修改 `DbServices.Mcp/ToolCatalog.cs` 與測試。

### Technical Impact

已審查的宿主仍是底座。之後要改的是公開目錄與模組視圖呼叫，不是拆掉工廠、具名連線或稽核。視圖定義的媒介，以及除錯路徑的開關形式，留在架構未決，不得讓 Discovery 看見表級工具。測試策略要補上 SM-1：Discovery 等於已發布視圖的名稱集合；空目錄不回退。

## 3. Recommended Approach

選直接調整，並沿用資料平面第 6 節已改寫的 MVP。不回退已審查的宿主。

| 選項 | 判定 | 工作量 | 風險 |
|------|------|--------|------|
| 直接調整故事並新增 Epic 2 | 採用 | 中 | 中。主要風險是七則 `review` 故事若依舊驗收被接受 |
| 回退 Story 1.1 至 1.7 | 不採用 | 中 | 高。會連工廠、具名連線、稽核與 `tool_disabled` 一起拿掉 |
| 再砍 MVP | 不採用 | 低 | 低，但會和已確認的歸屬相反 |

資料平面 MVP 已是 UJ-3：一條人手寫、解說已確認、關聯連通的模組視圖。UJ-1、UJ-2 只在除錯路徑成立。拖拉設計器與共用關聯程式庫維持不做。報表工作台的三片驗收仍在，MCP 填數的對象改成已發布模組視圖。

時程：資料平面 Epic 1 不能結案。Epic 2 排在工作台 MCP 填數之前。工作台登入與 Web API 單表瀏覽可以並行。

## 4. Detailed Change Proposals

下列十項都已在逐項審查中批准。

### 4.1 資料平面史詩：公開清單

**檔案：** `_bmad-output/planning-artifacts/epics.md`  
**段落：** FR-3、Epic 1 成果、Story 1.1 的標題與 Discovery 驗收

OLD：FR-3 的預設清單恰好是六個表級工具。Epic 1 的成果是發現唯讀工具並完成查詢。Story 1.1 要求外部 Agent 只看見這六個工具；未廣告的其他名稱回 `unknown_tool`。

NEW：

- FR-3：Discovery 不列出表級唯讀工具。公開清單只含已發布模組視圖。沒有已發布視圖時清單是空的，不回退。
- Epic 1 成果：外部 Agent 發現的是已發布模組視圖，並完成該視圖的唯讀呼叫。寫入與自由 SQL 被拒絕。表級 Call 不是公開成功路徑。覆蓋範圍仍是 FR-1 至 FR-15。
- Story 1.1 標題改為「啟動宿主，Discovery 只列出已發布模組視圖」。列出工具時，名稱集合等於已發布模組視圖，不多不少。清單不含六個表級工具，也不含 `insert`、`update`、`delete`、`sql`。沒有已發布視圖時清單是空的。四個關閉名稱仍回 `tool_disabled`。其餘未出現在 Discovery 的名稱回 `unknown_tool`。

宿主骨架、簽名不變、REST／GraphQL 仍可呼叫，維持不變。Story 1.2 至 1.7 不在這項裡改寫。

### 4.2 新增 Epic 2

**檔案：** 同上  
**段落：** FR 覆蓋圖與史詩清單

OLD：覆蓋圖停在 FR-15。沒有 FR-16 至 FR-21 的故事。

NEW：Epic 2「人手寫的模組視圖成為 Agent 的公開目錄」，覆蓋 FR-16 至 FR-21。

- Story 2.1：發布一條人手寫視圖後，Discovery 只有它。含名稱、短述與長解說。拿掉發布後，下次列出不再有它。
- Story 2.2：解說為空或仍是草稿時不得發布。撤回確認後離開 Discovery。表級除錯不得代替尚未發布的視圖。
- Story 2.3：定義留在產品目錄，不含連線字串與資料列。不在來源庫執行 `CREATE VIEW`。未宣告的表名或欄位拒絕執行。回傳欄等於輸出欄。媒介留給架構。
- Story 2.4：多張來源表必須有連通路徑，否則不發布、不做出笛卡兒積。單表不需要路徑，仍須通過 Story 2.2。
- Story 2.5：呼叫仍唯讀。未知視圖不落到表級 `query`。換 `ServerType` 時名稱與參數鍵不變。稽核含視圖名稱，不含連線字串與列內容。

不包含拖拉設計器、共用關聯程式庫，也不包含除錯路徑的開關。

### 4.3 表級故事改為營運除錯

**檔案：** 同上  
**段落：** FR-4 至 FR-9 的清單說明；Story 1.2、1.3、1.4、1.5、1.7；Story 1.6 的適用範圍

OLD：這些故事的角色是外部 Agent，成功路徑是呼叫六個表級工具。Story 1.7 用這組工具證明換引擎。Story 1.6 的前提是宿主已能執行 Story 1.1 到 1.5 的工具。

NEW：FR-4 至 FR-9 與 Story 1.2、1.3、1.4、1.5、1.7 描述營運除錯路徑，不是 Discovery。開關形式留給架構。除錯未開啟時，這些名稱在公開端點上遵守 Story 1.1 的 `unknown_tool`。角色改為營運除錯呼叫者。參數、錯誤碼與成功形狀先不改。

Story 1.7 只證明除錯路徑上的換引擎。公開的換引擎驗收是 Story 2.5。

Story 1.6 的稽核鍵不變。前提改為每一次進入調度器的呼叫。表級範例只覆蓋除錯路徑。公開成功呼叫的 `tool` 是模組視圖名稱，語意見 Story 2.5。

### 4.4 報表工作台 PRD

**檔案：** `_bmad-output/planning-artifacts/prds/prd-DBServices-2026-10-12/prd.md` 與同目錄 `addendum.md`  
**段落：** 第 0、1、2.1、2.2、3 節，UJ-1、UJ-2，FR-4 至 FR-10、FR-12、FR-14、FR-20，第 5、6、7、11、13 節，SM-1、SM-6，附錄 MCP 工具表

OLD：MCP 是表級表面。活資料預覽用 `tables`、`fields`、`query`、`count`。每個元件各發一次 `query` 或 `count`。這些句子標著待改寫。

NEW：第 0 節改成短記，註明改寫已寫入正文。拖拉設計器與共用關聯程式庫仍延後。正文的待改寫標記拿掉。

- `MCP` 只承載已發布模組視圖。表級瀏覽只走 Web API。
- MCP 填數是該範本所綁的一個已發布模組視圖。Web API 填數仍是資料表列的 GET。
- FR-4 至 FR-7：表級瀏覽與等值預覽只留 Web API。MCP 不提供那四個表級工具。沒有表級計數動作，也不讀全表自己算。
- FR-8、SM-1、UJ-1：換引擎證明的是同一份模組視圖的名稱與參數鍵。單表預覽只走 Web API。
- FR-9：對得上稽核的是模組視圖呼叫。
- FR-10：工作台的 MCP 呼叫只會是已發布模組視圖。
- FR-12、FR-14、UJ-2：一份 MCP 範本綁恰好一個已發布模組視圖。資料表元件的欄來自該視圖的輸出欄。計數元件不另發 `count`；筆數必須是該次回傳列裡的一個輸出欄。建議來自輸出欄與參數。Web API 範本仍可綁一張表，建議來自 `fieldSet`；含計數元件時 Web API 整次拒絕。不新增跨表元件，也不在工作台裡設計 join。
- FR-20：MCP 上一次確認恰好呼叫那一個模組視圖。
- 第 5、6.1、6.2、11、13 節：任意 join 與自由 SQL 仍排除；已發布模組視圖所回答的跨表目的不在排除之列。簽名、REST 與 GraphQL 路徑仍不改。相容邊界不再保護六個表級工具的公開廣告。
- SM-6：MCP 範本的欄名等於視圖輸出欄；Web API 範本等於 `fieldSet`。
- 附錄確認清單改成一次視圖呼叫。

FR-1 至 FR-3、FR-11、FR-13、FR-15 至 FR-19、FR-21、FR-22 不改。三片驗收仍是完成定義。

### 4.5 報表工作台史詩

**檔案：** `_bmad-output/planning-artifacts/epics/epics-DBServices-2026-10-13/epics.md`  
**段落：** FR-4 至 FR-10、FR-12、FR-14、FR-20；填數轉發、確認清單、繼承的資料平面不變量；Epic 1 成果；Story 1.4 至 1.8、2.2、2.3、3.1、3.3

OLD：填數轉發只呼叫四個表級工具。廣告清單維持恰好六個。活資料與範本確認按 `query`／`count` 計次。

NEW：需求清單的那些 FR 改成與第 4.4 節相同。MCP 只呼叫所綁的已發布模組視圖。確認清單在 MCP 上恰好一筆視圖呼叫。繼承的不變量改為 Discovery 只列已發布模組視圖；工作台不呼叫六個表級工具。`GET /connections` 不進入 Discovery。

- Story 1.4：仍可切換表面與看連線。MCP 這側是為了呼叫模組視圖。
- Story 1.5、1.6：瀏覽表、欄與確認後看列只走 Web API。
- Story 1.7：改成確認一個已發布模組視圖。換連線時名稱與參數鍵不變。沒有表級計數按鈕。
- Story 1.8：稽核對上該次視圖呼叫。
- Story 2.2、2.3：五種元件類型不變。MCP 的繫結與建議來自該視圖的輸出欄與參數。
- Story 3.1、3.3：MCP 呼叫清單恰好是那一個模組視圖。Web API 的單鍵限制與計數拒絕維持不變。

Story 1.1 至 1.3、2.1、2.4、3.2、3.4 至 3.8 不改。

### 4.6 資料平面架構

**檔案：** `_bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-11/ARCHITECTURE-SPINE.md`  
**段落：** AD-2、AD-3、AD-4、AD-7、AD-8、AD-9、AD-10，能力對照，Deferred。新增一則模組視圖決策。

OLD：AD-2 的公開表面恰好六個工具，其餘名稱是 `unknown_tool`。AD-4 在零筆連線時仍廣告六個工具。能力對照只到 FR-15。

NEW：

- AD-2：Discovery 等於已發布模組視圖。空清單不回退。四個關閉名稱仍是 `tool_disabled`。未在 Discovery 且不是關閉名稱的回 `unknown_tool`。已發布視圖的名稱不是 `unknown_tool`。表級工具不在 Discovery。
- AD-3、AD-8、AD-9 只描述除錯路徑。開關形式記成未決。未開啟時，六個名稱在公開端點上是 `unknown_tool`。
- AD-4：零筆連線仍要啟動，廣告的是 Discovery，可以是空清單。
- AD-7：錯誤碼閉集不變。不再寫「不在六個名稱裡」。
- AD-10：稽核鍵不變。公開呼叫的 `tool` 是模組視圖名稱。
- 新增決策：一個已發布模組視圖是一個公開工具。參數是定義自己宣告的篩選，加上既有 `connectionId` 規則。成功結果是 `columns` 與 `rows`，欄集合等於輸出欄。不在來源庫 `CREATE VIEW`。媒介未決。未宣告的表名或欄位拒絕。未知視圖不落到 `query`。來源表不連通則不發布、不做出笛卡兒積。解說未確認不得發布。
- 能力對照補上 FR-16 至 FR-21。Deferred 補上拖拉設計應用與共用關聯程式庫。

AD-1、AD-5、AD-6 不改。

### 4.7 工作台架構

**檔案：** `_bmad-output/planning-artifacts/architecture/architecture-DBServices-2026-10-12/ARCHITECTURE-SPINE.md`  
**段落：** parent AD-2、parent AD-8、AD-3、AD-4、AD-7、AD-8、AD-13。AD-14 僅在它把元件寫成綁一張表或建議來自 `fields` 時改。

OLD：parent AD-2 要求工作台只呼叫四個表級工具。確認清單按元件發出 `query` 或 `count`。建議來自 `fields`。稽核對上 `query`。

NEW：繼承第 4.6 節的 AD-2。工作台只呼叫所綁的已發布模組視圖。Web API 的 GET 集合不變。`GET /connections` 不進入 Discovery。MCP 確認清單恰好一筆視圖呼叫。建議在 MCP 上來自分視圖輸出欄與參數，在 Web API 上來自分 `fieldSet`。稽核對上視圖名稱。五種元件與 `order` 不變。計數元件綁視圖的一個輸出欄，不另發呼叫。

AD-1、六角形邊界、AD-5、AD-6、AD-9 至 AD-12 不改。媒介與除錯開關沿用第 4.6 節的未決項。

### 4.8 工作台 UX

**檔案：** `_bmad-output/planning-artifacts/ux-designs/ux-DBServices-2026-10-13/EXPERIENCE.md`、`DESIGN.md`。同目錄 mockup 裡若寫了那四個表級工具名稱，一起改。

OLD：活資料預覽的 `call-list` 恰好一筆 `query` 或 `count`。UJ-1 高潮的工具名稱仍是這兩個。等寬字範例是 `query`、`count`、`tables`、`fields`。

NEW：Web API 瀏覽表與欄，確認後以零鍵或單鍵 GET 看列。MCP 選定一個已發布模組視圖，確認後恰好呼叫它。沒有表級計數按鈕。換引擎的高潮是視圖名稱與參數鍵不變。規劃設計師核對的是具名連線、Web API 的表與欄，以及已發布模組視圖。等寬字範例改成視圖名稱、錯誤碼、`ServerType`、`mcp`／`web`。MCP 錯誤碼仍原樣顯示。

無障礙、角色指派、確認才填數、繁體中文空狀態不改。

### 4.9 資料平面 PRD 的除錯旅程

**檔案：** `docs/planning/agent-data-plane/prd.md`  
**段落：** UJ-1、UJ-2，以及第 4.1 節用三個表級工具描述換引擎的那一句

OLD：UJ-1、UJ-2 的路徑是 Agent 看見並呼叫六個表級工具。標題讀起來像公開主路徑。

NEW：兩條標題下各加一句：這是營運除錯旅程，不是 Agent 的公開驗收。公開驗收是 UJ-3。步驟先保留。第 4.1 節分成兩句：除錯路徑上那三個工具的名稱與參數鍵不因 `ServerType` 改變；公開路徑上同一份模組視圖的名稱與參數鍵不變，對應 FR-21 與 SM-2。

FR-3、FR-16 至 FR-21、第 6 節不改。

### 4.10 規格與說明

**檔案：**

- `_bmad-output/specs/spec-agent-data-plane/SPEC.md`
- `_bmad-output/specs/spec-agent-data-plane/readonly-tools.md`
- `_bmad-output/specs/spec-report-workbench/surfaces.md`
- `_bmad-output/implementation-artifacts/spec-1-1-啟動宿主-agent-只看見六個唯讀工具.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `docs/index.md`
- `docs/mcp/index.md`
- `docs/guides/index.md`
- `docs/guides/functions.md`
- `docs/guides/session.md`
- `docs/architecture/index.md`
- `docs/architecture/layers.md`

OLD：公開廣告寫成恰好六個唯讀工具。Story 1.1 實作規格的驗收相同。

NEW：Discovery 只列出已發布模組視圖。空清單不回退。四個寫入與 SQL 名稱不廣告、不執行。表級 Call 若仍被實作，只在與 Discovery 分開的除錯路徑。`readonly-tools.md` 把六個名稱移到除錯說明，並註明除錯未開啟時公開端點回 `unknown_tool`。`surfaces.md` 的 `GET /connections` 改為不進入 Discovery。Story 1.1 實作規格的標題與驗收改成第 4.1 節。`deferred-work.md` 的指向句一起改。檔名留到實作交接再改。

不修改 `ToolCatalog.cs` 與測試。`docs/web/index.md` 不在這項。

## 5. Implementation Handoff

範圍是 **中等，靠近重大**。公開契約變了，要新增史詩，並重開已採用的 AD-2。不需要重做產品目標，也不回退宿主。

| 角色 | 責任 |
|------|------|
| 產品 | 將第 4.4 與 4.9 節寫入兩份 PRD 與工作台附錄 |
| 架構 | 將第 4.6 與 4.7 節寫入兩份架構骨架。媒介與除錯開關維持未決，並寫明不得進入 Discovery |
| 產品負責人與開發 | 將第 4.1 至 4.3、4.5 節寫入史詩。更新 `sprint-status.yaml`：新增 Epic 2，狀態 `backlog`；Story 1.1 至 1.7 維持不得依舊驗收進入 `done` |
| 體驗 | 將第 4.8 節寫入 UX 與含舊工具名的 mockup |
| 開發 | 第 4.10 節的規格與 `docs/` 可與上述文件同一批改。`ToolCatalog` 與測試等契約寫入文件之後再動 |

成功標準：

- 沒有任何仍有效的故事要求 Agent 的公開清單恰好是那六個工具。
- Discovery 的驗收等於已發布模組視圖的名稱集合；空目錄不回退。
- 工作台的 MCP 填數寫的是一個已發布模組視圖；表級瀏覽只寫 Web API。
- Story 1.1 至 1.7 沒有依六個工具的舊驗收被標成 `done`。

動作順序：

1. 停止依舊驗收接受 Story 1.1 至 1.7。
2. 改資料平面史詩並新增 Epic 2。
3. 重開 2026-10-11 的 AD-2，再改 2026-10-12 的繼承與填數規則。
4. 改寫工作台 PRD，再改工作台史詩與 UX。
5. 對齊規格與 `docs/`。
6. 最後才改目錄實作與測試。
