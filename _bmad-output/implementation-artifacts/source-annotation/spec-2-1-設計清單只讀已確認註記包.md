---
title: 'Story 2.1 設計清單只讀已確認註記包'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 6d57a2356e121315e964ec906089a420ff6168de
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 檢視頁的資料表清單來自活庫結構，林可看得到方晴還沒確認的表，空白欄註也沒被標出來。

**Approach:** 環境就緒後，資料表清單改讀唯一啟用的已確認包。沒有啟用包就只留空目錄。檢視的打開、單表保存、預覽與發布維持現況。

## Boundaries & Constraints

**Always:**
- 持有系統分析師入口、環境就緒時，`aside.library` 的表、欄、型別、表註、欄註與關聯只來自 `GET /annotations/active` 的啟用包。忽略 `others` 與 `changes`。草稿與 `active` 為 false 的包不出現。
- 每個表下列出欄名與型別。欄註空白顯示 `unmarked-badge`，文字固定「尚未註記」。表註只有非空白才顯示，空白表註不另做徽章。已聲明關聯含 `origin` 為 `foreign-key` 與 `declared`、且兩端表都在這份包裡的關聯，列在面板底部，`aria-label` 是「已聲明關聯」，格式沿用 `From.FromColumn → To.ToColumn`。理由空白不另寫。
- 沒有啟用包時，該面板只有 `empty-catalog`：「資料來源尚未註記」。沒有表按鈕，搜尋也不會冒出表。這句與「尚未註記」不同。
- 打開頁面與「重新讀取」不呼叫 `SchemaService.GraphAsync` 或 `SchemaGraph.Read`，也不讀業務列、不顯示連線字串。重新讀取只再要一次啟用包。讀取失敗沿用 `execution_failed`「沒有保存。」，用既有 `status-message`：訊息在上、代碼在下，`role="alert"`。
- 系統分析師沒有資料治理入口也讀得到啟用包。寫入、草稿、確認、放棄仍只給資料治理。未持有系統分析師入口仍是「沒有權限。」與 `forbidden`。環境未就緒仍是既有的設定提示。
- 已保存檢視的打開、單表放入、儲存、預覽與發布維持現況。鄰近表仍走既有兩層規則，但邊只來自這份包。一欄多條關聯時，畫面上仍是一欄；多出來的邊要能被鄰近建議走到。

**Never:**
- 不新增 `link_undeclared`，不拒絕多表保存，不改 `ViewDocument` 欄位，不改發布、樣式、Chat、報表生成紀錄，不加 MCP 的 `source_annotation`。那些是 2.2 與 2.3。
- 不改 `AnnotationService` 既有寫入的資料治理門檻，不改 `SchemaNeighborhood` 的公開語意，不改 `SchemaGraph.Read`、Avalonia、`/environment`、`IDbService`、既有 REST 與 GraphQL。
- 不新增套件、色票或錯誤碼。不把個資文字、索引、DBML 或差異列畫進這頁。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 有啟用包 | 包內一表，一欄註解空白，另有草稿與一份已退出的包 | 清單只有啟用包的表、欄、型別與關聯。空白欄註是「尚未註記」 | 不呼叫 `GraphAsync`。讀列次數 0 |
| 沒有啟用包 | 只有草稿，或目錄是空的 | `empty-catalog` 為「資料來源尚未註記」，不能選表 | 不是錯誤 |
| 再讀未確認 | 啟用包仍是舊包，草稿是新形狀 | 清單仍是舊的啟用包 | 草稿的表不出現 |
| 單表保存 | 從清單放入該包的一張表並儲存 | 檢視來源是那張表。儲存、預覽、發布的其餘行為不變 | 不新增代碼 |
| 讀取失敗 | 啟用包讀取丟出連線類例外 | 清單不改用活庫 | `execution_failed`「沒有保存。」 |
| 未持有 | 沒有系統分析師入口 | 「沒有權限。」 | `forbidden`，不讀目錄 |

</frozen-after-approval>

## Code Map

- `Workbench.Web/Components/Views.razor` — `LoadSchema` 約第 397 行現在呼叫 `Schema.GraphAsync`。改讀啟用包，投影成既有 `SchemaTableDto` 供 `SchemaNeighborhood.Suggest` 與卡片使用。`OnInitializedAsync` 約第 379 行：沒有分析師入口或環境未就緒的分支不要動。
- `Workbench.Core/AnnotationService.cs` — `ActiveAsync` 約第 84 行經 `Closed`（約第 164 行）要求資料治理。另加只讀方法給系統分析師，內部仍呼叫 `IAnnotationCatalog.ActiveAsync`。不要放寬 `Closed`。
- `McpService/Program.cs` — `GET /annotations/active` 約第 352 行已回啟用包加 `others`，沒有包時 204。這一則不改路由。
- `Workbench.Core/SchemaNeighborhood.cs` — 一欄只認一個 `ReferencesTable`。多出來的關聯在呼叫 `Suggest` 前掛到不顯示的欄上。不要改這個型別的既有測試語意。
- `Workbench.Web/wwwroot/tokens.css` — 加 `.empty-catalog`（`ink-secondary`，無插圖）與 `.unmarked-badge`（`surface-sunken`、`caution`、`radius-full`）。不要改既有 `.badge`。
- `Workbench.Tests/GapClosureTests.cs` — 約第 81 行會渲染 `Views`。注入新的只讀依賴；沒有啟用包時不斷言活庫表名。預覽次數仍要大於 0。
- 不要改 `AnnotationCatalog`、`Governance.razor`、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `AnnotationService.cs` — 系統分析師可只讀啟用包 — 寫入門檻不變
- [x] `Views.razor` 與 `tokens.css` — 資料表清單改接啟用包，空白欄註與空目錄用既有色票 — 不呼叫 `GraphAsync`
- [x] `Workbench.Tests` — 方法名含 `Design_catalog`，並讓既有檢視渲染測得過 — 鎖住矩陣

**Acceptance Criteria:**
- Given 啟用包與草稿同時在，when 林可打開檢視，then 清單只有啟用包，畫面沒有連線字串。
- Given 她只放入該包的一張表並儲存，when 檢查檢視文件，then 欄位集合與發布行為與這一則之前相同。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- false — `Project` 把不是精確 `confirmed` 的啟用包也放進清單。目錄只把已確認包標成 `active`，狀態就是 `confirmed`；`draft` 與 `active` 為 false 已經被擋下。
- false — 重新讀取失敗會清掉上一份清單，打開中的卡片改寫「目前連線裡找不到」。失敗列要求的是錯誤訊息、而且不改看活庫。清單清掉就是這個錯誤狀態。
- medium — `DesignCatalogAsync` 沒有接 `JsonException`。成功回應不是 JSON 時會跳出 `LoadSchema`，左側沒有 `status-message`。把 HTTP 代碼收成 `execution_failed`「沒有保存。」是規格要求的句子。route: patch
- low — 同表兩個欄位修剪後只差大小寫，後一筆註記蓋掉前一筆徽章。日常字典與結構讀不會寫出這種欄。補上跳過重複欄是多一個分支。rejected
- false — 缺欄的關聯仍可被鄰近建議走到。規格只要求兩端的表在包內，並要求多出來的邊走得到。
- false — 從建議加入第二張表時，`declared` 仍寫成 `foreign-key`。這一則把 kind 的改寫留給 2.2。
- false — 搜尋把表濾光後，已聲明關聯還在。規格把這份清單放在面板底部，不是放在被搜尋濾過的表裡面。
- false — 來源表不在啟用包時仍寫「目前連線裡找不到」。規格要已保存檢視的打開維持現況，這句就是原本的句子。
- false — 重新讀取測試用 500 毫秒推論預覽沒有重跑。預覽延遲是 350 毫秒；反射只取消勾選欄位時排程的那一次。重新讀取若又排程，讀列次數會增加。
- false — 規格已是 `in-review`，sprint 仍是 `in-progress`。審查這一步本來就先改規格狀態；sprint 在呈現時才改成 review。
- false — 註記包裡的 null 表、欄或關聯會讓投影丟例外。JSON 反序列化在進 `Project` 之前就會失敗，目錄也不會塞 null。
- low — 同名欄共用最後一則註記。與上面重複欄那條相同。rejected
- medium — 無法反序列化的啟用包回應沒有轉成「沒有保存。」。與 `JsonException` 那條是同一個缺口。route: patch
- low — 環境尚未判定前可以按重新讀取。第一個 await 之前按鈕就會出現，這是原本的空窗；補門檻是新的守衛。rejected
- low — 較早的重新讀取可能覆蓋較新的結果。舊的讀結構也是這樣；補票號是新的狀態。rejected
- low — 登入者消失後重新讀取會空參考。日常操作不會在這個畫面裡把人拿掉；補上檢查是新的守衛。rejected
- medium — 有啟用包時，搜尋無符合表沒有測試。拿掉「沒有符合的資料表。」這個分支，現有測試仍過。route: patch
- medium — 重新讀取沒有把啟用包換成另一份或清空再斷言畫面。目錄有被呼叫，但第二次結果可以不被畫上。route: patch
- medium — 讀取失敗測試只丟 `HttpRequestException`。目錄的 HTTP 錯誤實際丟 `InvalidOperationException`，逾時是 `TaskCanceledException`。route: patch

## Design Notes

分析師頁不能走 `ActiveAsync`，那個方法把沒有資料治理入口的人擋成 `forbidden`。只讀方法只查 `EntryNames.Analyst`。`others` 即使含表也不進 `_tables`。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Design_catalog` -- expected: 通過
