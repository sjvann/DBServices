---
title: 'Story 2.2 多張來源表必須用已聲明關聯'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: af3073330866a5d4f3ef1013c0c6a4ec8dd42e66
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 檢視可以存進啟用包沒有聲明的關聯。林可等於能在設計時自造關聯。

**Approach:** 兩張以上來源表時，每一段都要對上啟用包裡的已聲明關聯。對不上就整次拒絕，不改已保存的檢視。一張表仍沿用上一則。

## Boundaries & Constraints

**Always:**
- 已聲明關聯是啟用包上 `origin` 為 `foreign-key` 或 `declared`、且兩端表與欄都有值的關聯。草稿、`others`、`active` 為 false 的包不算。比對去掉首尾空白，區分大小寫。方向對調仍是同一條。理由不必相同，空白也可以。
- 來源表兩張以上時，文件上的每一段都要對上某一條。有一段對不上，整次保存拒絕，不是只丟掉那段。代碼 `link_undeclared`，句子「這段關聯不在註記包裡。」檢視檔、已確認包、草稿都不改。
- 畫面上這個拒絕用 `status-message`：`role="alert"`，句子在上、代碼在下。不含連線字串、密碼或堆疊。不呼叫 `SchemaService.GraphAsync` 或 `SchemaGraph.Read`，讀列次數為 0。
- 對得上時才寫入。`origin` 為 `foreign-key` 的 kind 維持 `foreign-key`；`declared` 寫成 `accepted`。加入建議時就照這條寫。沒有手打關聯的控制。
- 只有一張來源表時不跑這條比對，結果沿用 Story 2.1。沒有關聯的兩張表仍是既有的 `disconnected`「來源連不起來。」，不是這個新代碼。
- 畫面先比對，沒過就不呼叫保存。`PUT /views/{id}` 在寫檔前再比對一次。

**Never:**
- 不改 `ViewDocument` 的欄位集合，不改發布、樣式範本、Chat、報表生成紀錄，不加 `source_annotation`。那些是 2.3。
- 不改 `ViewService` 的建構子，不放寬既有 `blank_name`、`duplicate_name`、`disconnected`。不改 `SchemaNeighborhood` 的公開語意，不改 `SchemaGraph.Read`、Avalonia、`/environment`、`IDbService`。
- 不新增套件或色票。不新增這個代碼以外的錯誤碼。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 對得上 | 兩表，一段與啟用包方向相反，理由不同。origin 是 `declared` | 保存成功。kind 是 `accepted`。欄位集合不變 | 讀列次數 0 |
| 外鍵 | 兩表，兩端與 `foreign-key` 那條相同 | kind 維持 `foreign-key` | 不看理由 |
| 一張表 | 只有一張來源表，沒有關聯 | 保存沿用 2.1 | 不回 `link_undeclared` |
| 一段對不上 | 兩段裡只有一段的欄不在啟用包 | 整次不寫。兩段都還在這次編輯裡 | `link_undeclared`，檔與註記包不變 |
| 沒有啟用包 | 兩表且有一段關聯，目錄沒有啟用包 | 不改看活庫 | 同樣拒絕，不寫檔 |
| 接不起來 | 兩表但沒有任何關聯 | 既有「來源連不起來。」 | `disconnected` |

</frozen-after-approval>

## Code Map

- `Workbench.Web/Components/Views.razor` — `AddSuggestion` 約第 579 行把 kind 寫死 `foreign-key`。改依 `_declared` 的 origin。`Write` 約第 935 行在呼叫 `SaveAsync` 前比對。`_error` 約第 90 行現在是代碼在上；這個代碼改走 `status-message`。
- `McpService/Program.cs` — `PUT /views/{id}` 約第 233 行，在 `views.Save` 前用 `AnnotationCatalog.Active()` 比對。沒過就 400，不寫檔。
- `DbServices.Mcp/ViewCatalog.cs` — `Check` 約第 189 行仍管名稱、接不起來與 kind 只許 `foreign-key`／`accepted`。不要把新代碼塞進 `disconnected`。
- `Workbench.Core/UseCases.cs` — `ViewService` 建構子不要加目錄依賴。記憶體目錄的舊測試不經過 `McpService`。
- `Workbench.Web/wwwroot/tokens.css` — `.status-message` 已在。不要新色票。
- 測試方法名含 `Declared_link`。不要改資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `Views.razor` — 建議的 kind 依 origin，保存前比對，拒絕用既有 `status-message` — 不新增手打關聯
- [x] `McpService/Program.cs` — 寫檔前再比對一次 — 沒過不寫檢視檔，也不改註記包
- [x] `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Declared_link` — 鎖住矩陣

**Acceptance Criteria:**
- Given 兩張以上來源表且有一段對不上，when 保存，then 已保存的檢視與註記包都不變，畫面是那句與 `link_undeclared`。
- Given 只有一張來源表，when 保存，then 不因為這一則被拒絕。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- medium — 文件端表名或欄名的首尾空白在 `Check` 就變成 `disconnected`，trim 後的比對沒有執行。對得上的欄名空白也沒寫回文件。route: patch
- false — 建議與 `Align` 忽略大小寫，保存卻區分大小寫。規格要求去掉空白後區分大小寫。畫面點得到、保存被拒，是這條規則，不是漏比。
- low — 同一對端點同時有外鍵與 `declared` 時採用清單第一筆。日常註記不會對同一對端點寫兩種 origin。要排優先順序是多一個分支。rejected
- medium — `RefuseUndeclared` 在保存回來之前就改了編輯器上的 kind。保存若被拒絕，檔沒變，畫面上的 kind 已經變了。route: patch
- false — 畫面與 `PUT` 各寫一份，結構不完整的關聯由既有 `Check` 先回答。這是規格要的兩處相同規則；origin 空白與包外表是下面的測試缺口，不是另一套規則。
- medium — 空白欄維持 `disconnected` 只在服務測試鎖住。拿掉畫面的提前返回，現有畫面測試仍過。route: patch
- medium — 對得上的那段只在草稿、啟用包沒有時，還沒被鎖住。route: patch
- false — `links` 裡的 null 會讓 `Check` 丟例外。`views.Save` 原本就會走到同一個 `Check`，不是這次比對新增的路徑。
- medium — 註記檔讀不了時，`PUT` 連一張來源表的保存也會丟例外。規格說一張表不因為這一則被拒絕。route: patch
- medium — 保存失敗後 kind 留在畫面上。與上面那條是同一個改寫。route: patch
- low — 比對之後、寫檔之前啟用包被換成另一份。要跨目錄加鎖才擋得住，日常單人操作碰不到。rejected
- false — 啟用包的關聯清單含 null 會讓設計畫面擲回。投影本來就會碰到該筆的端點，JSON 也不會把 null 放進清單。
- false — 檢視關聯清單含 null 會讓保存擲回。同樣是既有 `Check`。
- false — 一張表在註記檔讀不了時被拒絕。與上面讀檔那條相同，不是另一種規則。
- false — `KindFor` 在對不上時寫成 `foreign-key`。建議的端點來自同一批已聲明關聯，對得到 origin。
- medium — `origin` 不是 `foreign-key` 或 `declared` 時必須拒絕，測試沒有鎖。拿掉 origin 限制，現有測試仍過。route: patch
- medium — 兩端有值但表不在包的表清單裡，畫面仍應放行。現有測試只斷言側欄不顯示，沒有對這條按保存。route: patch

## Design Notes

`disconnected` 是表與表接不起來。`link_undeclared` 是接得起來，但兩端不是啟用包裡的那條。kind 在兩處都對得上之後才改，拒絕時不動檔。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Declared_link` -- expected: 通過
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Declared_link` -- expected: 通過
