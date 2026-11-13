---
title: 'Story 1.1 指派資料治理入口並打開註記站'
type: 'feature'
created: '2026-11-13'
status: 'in-review'
route: 'dispatch'
baseline_commit: '93352be80f80f8c6afc61ba4f6b3bfb263892197'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 管理員還不能指派資料治理，持有的人也沒有註記站可進。

**Approach:** 入口清單加上 `governance`／「資料治理」。登入與換工作沿用現有分流。

## Boundaries & Constraints

**Always:**
- 代碼 `governance`，名稱「資料治理」，路徑 `/governance`，標題「註記」。
- 指派只寫產品庫 `entries`。不寫業務庫，不新增操作者或管理員。
- 非管理員打開入口指派，或未持有者打開 `/governance`，只顯示「沒有權限。」
- 保存失敗用 `status-message`：第一行既有訊息，第二行既有代碼，`role="alert"`。勾選回到這次保存前。不含連線字串、密碼或堆疊。
- 只持有時登入直達 `/governance`。左軌「現在的工作」只有「註記」，並帶 `active` 與 `rail-link-active`。另有其他入口時由換工作進入；改開別站或未持有時，左軌沒有「註記」。
- 空站不注入環境、結構、目錄或 MCP，畫面沒有連線字串。

**Never:**
- 不改 `/environment`（`Environment.razor`、`Environment.cs`、左軌「環境設定」）。不改 Avalonia。
- 不做匯入、讀結構、編輯、匯出、確認、註記包或 `source_annotation`。
- 不新增錯誤碼、資料表、套件或色票。不把註記和檢視、樣式、產生報表放在同一組左軌。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 指派 | 管理員為既有操作者勾選並保存 | `entries` 寫入 `governance`。人數不變 | 不建立帳號 |
| 取消 | 取消勾選並保存 | 不再持有。再開 `/governance` 看不到站 | 其他入口保留 |
| 非管理員 | 打開 `/entries` | 只有「沒有權限。」，沒有核取方塊 | 不寫 `entries` |
| 保存失敗 | 勾選已改，指派回傳錯誤 | 先訊息、後代碼。勾選回到保存前 | 沿用該次錯誤，不新造代碼 |
| 未持有 | 直接前往 `/governance` | 「沒有權限。」左軌沒有「註記」 | 不讀業務列 |
| 只有資料治理 | 登入 | 落點 `/governance`，標題「註記」，左軌只有含 `rail-link-active` 的「註記」 | 無連線字串、不讀列 |
| 兩個入口 | 另持有系統分析師，換工作選資料治理 | 只見「註記」。改開檢視時沒有「註記」 | 不與其他站並列 |

</frozen-after-approval>

## Code Map

- `Workbench.Contracts/Names.cs` — 加入 `governance`。`Assign` 與 `ToMe` 已讀 `All`、`Home`，不要改 `UseCases.cs`。`entries` 表不用改。
- `Workbench.Web/Components/Entries.razor` — 保存失敗目前不還原勾選，也沒有代碼行。
- `Workbench.Web/Components/WorkbenchLayout.razor` — 左軌依網址只顯示一站。`ActiveClass` 要寫 `active rail-link-active`。未持有者不畫「註記」。環境設定分支不要動。
- `Workbench.Web/Components/Stations.razor` — `Describe` 補「資料治理」。`tokens.css` 讓 `.status-message` 共用 `.error`。
- 測試沿用 `ReportWorkbenchTests.cs` 的 `WorkbenchHost` 與 `GapClosureTests.cs` 的 bunit。不要改 `Environment.razor`、`Environment.cs`、Avalonia、資料平面追蹤。

## Tasks & Acceptance

**Execution:**
- [x] `Workbench.Contracts/Names.cs` -- 加入代碼、清單、名稱「資料治理」、首頁 `/governance` -- 指派與落點都讀這裡
- [x] `Workbench.Web/Components/Governance.razor` -- 空站。未持有顯示「沒有權限。」持有則標題「註記」。只注入閘門 -- 不讀列
- [x] `Workbench.Web/Components/WorkbenchLayout.razor` -- 持有且網址是 `governance` 時，左軌只放「註記」 -- 不改環境設定
- [x] `Workbench.Web/Components/Stations.razor` -- 換工作寫出「資料治理」並連到 `/governance`
- [x] `Workbench.Web/Components/Entries.razor` -- 失敗則還原勾選，並以 `status-message` 先訊息、後代碼。成功則更新該操作者入口並清掉錯誤
- [x] `Workbench.Web/wwwroot/tokens.css` -- `.status-message` 共用 `.error`
- [x] `Workbench.Tests/ReportWorkbenchTests.cs` -- 方法名含 `Governance_entry`：指派、取消、落點、拒絕、人數不變
- [x] `Workbench.Tests/GapClosureTests.cs` -- 方法名含 `Governance_entry`：失敗還原與兩行；空站；左軌只有「註記」

**Acceptance Criteria:**
- Given 這一輪，when 檢查 Avalonia 與 `/environment`，then 這兩個表面沒有為資料治理改動。
- Given 只持有資料治理並打開註記站，when 計算業務列讀取，then 次數為 0，畫面沒有連線字串。

## Implementation Notes

- 左軌在同一份 `WorkbenchLayout` 裡換頁時不會重跑初始化，所以訂閱 `LocationChanged` 後重算目前的站。環境設定那段文案沒改。
- 換工作卡片的說明句是「為資料來源補上註記。」標題與身份都是「資料治理」。
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Governance_entry`：通過 5、失敗 0、略過 0。

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Governance_entry` -- expected: 通過
