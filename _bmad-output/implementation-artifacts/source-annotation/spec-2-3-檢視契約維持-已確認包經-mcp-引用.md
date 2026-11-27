---
title: 'Story 2.3 檢視契約維持，已確認包經 MCP 引用'
type: 'feature'
created: '2026-11-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fe5cae5f5a49dba701e0b9e7b22e318fbd0ca8cc'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 後續設計階段還沒有一個 MCP 工具可以讀到方晴確認過的那一份註記包，只會看到已發布檢視。

**Approach:** 有啟用包時，Discovery 多一個沒有參數的 `source_annotation`。沒有啟用包就不列、呼叫回 `unknown_tool`。檢視文件、發布與讀列維持原樣。

## Boundaries & Constraints

**Always:**
- 檢視文件仍是 `id`、`name`、`purpose`、`confirmed`、`published`、`sources`、`links`、`parameters`、`columns`。不含業務列與連線字串。
- Discovery 的名稱等於已發布且已確認的檢視。只在有啟用中的已確認包時，再加保留名 `source_annotation`。沒有啟用包，或註記檔讀不了時，不加入該名，已發布檢視仍在。草稿與 `active` 為 false 的包不造成該名出現。
- 呼叫這個名字不分大小寫。有啟用包就回該包的表、欄、型別、註解與已聲明關聯。文字物件的 `tables` 含 `name`、`note`、`columns`（`name`、`type`、`note`），`links` 含 `from`、`fromColumn`、`to`、`toColumn`、`origin`、`note`。欄註空白就空白。`links` 只留 `foreign-key` 與 `declared`、且兩端都有值的。不含草稿、`others`、連線字串與業務列。
- 工具沒有參數，schema 不出現 `connectionId`。帶了連線識別或 SQL 也不連線、不讀列、不呼叫 `SchemaGraph.Read` 或 `Preview`。`ReadOnly` 為 true，`Destructive` 為 false。沒有啟用包時回 `unknown_tool`「沒有這個工具。」，不回退成 `tables`、`fields`、`get`、`query`、`count`、`exists`。`insert`、`update`、`delete`、`sql` 仍是 `tool_disabled`。
- 檢視名稱去掉空白後與 `source_annotation` 不分大小寫相同，保存與發布都拒絕，沿用 `duplicate_name`「名稱已存在。」檔不改。未發布的檢視不進 Discovery。發布後才出現。讀列仍走該檢視。

**Never:**
- 不做 AI 輔助建立檢視，不做註記站的 Discovery 或 MCP 工具畫面。不改樣式範本、Chat、報表生成紀錄的讀取對象。
- 不改 `ViewDocument` 的欄位集合，不改 `link_undeclared` 的規則，不加 `source_annotation` 以外的工具。
- 不新增套件、色票或錯誤碼。不改 `SchemaGraph.Read`、Avalonia、`/environment`、`IDbService`、資料平面的 `sprint-status.yaml`。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 讀檢視 | 依 2.1 或 2.2 保存過 | 欄位就是那九個。沒有列、沒有連線字串 | 兩表關聯仍看 2.2 |
| 未發布 | 檢視已保存、未發布 | Discovery 沒有它 | 啟用包在不在都一樣 |
| 已發布 | 發布這一筆，另有一筆未發布 | 目錄有它，沒有未發布那筆。讀列仍走檢視 | 沒有啟用包時它還在，且沒有 `source_annotation` |
| 引用 | 啟用包有表、空白欄註、已聲明關聯；另有草稿與已退出包 | 回傳只有啟用包的表、欄、型別、註解與那兩種關聯 | 讀列次數 0。不呼叫 `Read` |
| 沒有啟用包 | 呼叫或列出 `source_annotation` | 清單沒有該名 | `unknown_tool`。不是表級工具 |
| 保留名 | 檢視名稱是 `Source_Annotation` | 保存與發布都被拒 | `duplicate_name`。檔不變 |

</frozen-after-approval>

## Code Map

- `McpService/Program.cs` — 列出工具約第 53 行只映射已發布檢視。有啟用包時附加 `source_annotation`。呼叫約第 59 行在 `FindPublishedByName` 之前處理這個名字。`IsClosedTool` 約第 606 行不要算進該名。這個工具的 schema 不要有 `connectionId`。
- `DbServices.Mcp/AnnotationCatalog.cs` — `Active()` 約第 245 行已只回啟用包。讀檔失敗要接住，已發布檢視仍列出。
- `DbServices.Mcp/ViewCatalog.cs` — `Check` 約第 189 行拒絕保留名。`Published()` 約第 81 行仍只認已發布且已確認。
- `DbServices.Mcp/ViewDispatch.cs` — 讀列仍只服務已發布檢視。
- 測試方法名含 `Source_annotation`。不要改樣式、Chat、報表生成紀錄。

## Tasks & Acceptance

**Execution:**
- [x] `Program.cs` — Discovery 與呼叫加上 `source_annotation` — 沒有啟用包就不列、不讀列
- [x] `ViewCatalog.cs` — 拒絕同名檢視 — 沿用 `duplicate_name`，不新增代碼
- [x] `McpService.Tests` — 方法名含 `Source_annotation` — 鎖住矩陣

**Acceptance Criteria:**
- Given 有啟用包，when 呼叫 `source_annotation`，then 回傳只有該包的表、欄、型別、註解與已聲明關聯，且沒有連線字串。
- Given 沒有啟用包，when 列出並呼叫該名，then 清單沒有它，呼叫是 `unknown_tool`，已發布檢視仍在。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- false — 引用沒有先修剪 `origin`。寫入端只存 `foreign-key` 與 `declared` 這兩個字，空白端點已被略過。規格要的就是這兩個字，不是修剪後再認。
- false — 端點帶首尾空白的關聯會原樣回傳。規格沒有要求引用時改寫端點。
- low — 檔案裡若已有已發布、名稱就是保留名的檢視，目錄會列出它，呼叫仍走引用。保存與發布已經拒絕這個名稱，日常寫不進去。要再濾目錄是多一個條件。rejected
- low — `SetPublished(false)` 也走 `Check`，既有保留名無法取消發布。同樣只有檔案事先寫入才碰得到。rejected
- false — 沒有啟用包時稽核仍記下呼叫端帶來的連線識別與 SQL。`Finish` 只寫稽核，不開連線、不呼叫 `SchemaGraph` 或 `ViewDispatch`。成功那次的 `rowCount` 為 null，是沒有 `rows` 的寫法，讀列次數是 0。
- false — 兩份同時啟用時只引用第一份。`Active()` 本來就只回第一份啟用中的已確認包，這次沿用它。
- false — 工具說明沒有輸出結構。規格沒有要求 output schema，工具也只在有啟用包時出現。
- false — 規格已是審查狀態、追蹤仍是進行中、Code Map 行號舊了。追蹤要等這則做完才改成 review；行號不影響執行。
- false — 三個保留名拼法打到同一個網址，也沒有鎖取消發布、空白 origin、兩份啟用。三個拼法在寫檔前都被拒，檔維持不存在。矩陣六列都有通過的測試。
- false — 註記檔陣列裡的 null 套件會讓列出工具擲回。寫入端不會寫出 null 套件；這種檔本來就會讓 `Active()` 擲回。
- false — 相信「只回已聲明關聯」的人會漏掉 origin 帶空白的關聯。規格寫的是 origin 等於那兩個字，程式用的是相同比對。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Source_annotation` -- expected: 通過
