---
title: 'Story 1.5 用等值條件查列、計數、判斷存在'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: '721f36f18e00da5b7fa93a989f492d0bf7389006'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 外部 Agent 看得到 `query`、`count`、`exists`，但還不能用等值條件取列、計數或判斷存在。

**Approach:** 客戶端帶可連線的 `connectionId`、表名，以及可省略的純量 `filters`。呼叫前去掉頂層 `connectionId`，把 `filters` 撲平恰好一層，再執行 Session 的同名函式。

## Boundaries & Constraints

**Always:**
- `tools/list` 的參數是 `connectionId`、`table`、`filters`。沒有連線字串欄位。`filters` 可省略。`table` 未帶或只有空白是 `invalid_arguments`，不呼叫 Session，不建檔。不要靠 schema 的 required 擋下 `table`。
- 等值條件只在 `filters`，撲平恰好一層。純量是 `null`、布林、字串、能精確放進 `Int64` 的 JSON 數字，或其他 JSON 數字。`1.0` 算整數。`JsonElement` 不進調度器。篩選值以參數進 Session。宿主不組 SQL。
- `query` 成功的唯一文字 content 是 JSON，鍵恰好 `columns`、`rows`，與直接 Session 相同。空的 `rows` 是成功。整數儲存格是 JSON 數字。`IsError` 為 false。
- `count` 的唯一文字 content 是一個 JSON 數字，`exists` 是一個 JSON 布林，都等於直接 Session 的那個儲存格。沒有列時是 0 與 false。
- 省略 `filters` 或空物件時，三個工具都與只帶 `table` 的直接 Session 相同。
- `Kind` 為 `Error`：`execution_failed`。文字 content 的鍵恰好 `code`、`message`。中文不含連線字串與例外原文。不是空列、0、false、`unknown_table` 或 HTTP 500。程序不結束。
- 表名不在清單時與直接 Session 相同，不是 `unknown_table`，且不先執行 `tables`。`tables`、`fields`、`get` 維持既有行為。

**Never:**
- 不處理省略 `connectionId`、未登記或空白名稱，也不驗證建立失敗後再試。與目前 `tables` 相同。
- 不處理 `filters` 不是物件、篩選值為物件或陣列、保留鍵、只差大小寫的篩選鍵、未列出的頂層鍵，以及 `connectionString`、`connectString`。
- 不註冊、不執行 `insert`、`update`、`delete`、`sql`。不寫稽核檔。不改 Core、Providers、WebService、GraphQL。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 輸入結構 | `tools/list` | 有 `connectionId`、`table`、`filters`；沒有連線字串 | N/A |
| 查列 | 一個以上純量等值 | 不先 `tables`；與 Session `query` 的 `columns`、`rows` 相同 | N/A |
| 無列 | 沒有列符合 | 空的 `rows`，成功 | 不結束 |
| 計數 | 同一組條件 | JSON 數字與 Session 儲存格相同；沒有列是 0 | N/A |
| 存在 | 有列 / 無列 | JSON 布林 true / false | N/A |
| 省略篩選 | 不帶 `filters`，或空物件 | 與只帶 `table` 的 Session 相同 | N/A |
| Session 錯誤 | `Kind` 為 Error | `execution_failed`，不是空列、0 或 false | 不結束 |
| 缺表名 | 未帶 `table`，或只有空白 | `invalid_arguments`，不呼叫 Session，不建檔 | 不結束 |
| 缺表 | 表名不在清單 | 與直接 Session 相同，不是 `unknown_table` | 不結束 |

</frozen-after-approval>

## Code Map

- `FunctionRegistry.cs` — `query` L62–71、`count` L114–124、`exists` L127–140。沒有等值鍵時 `count`／`exists` 傳 null。缺 `table` 會變成 `Kind` Error。不要改。
- `GetRecordByKeyValues` L650 失敗時可成空列。`GetSqlByKeyValues` L160 把值拼進 SQL。不要改，宿主也不要組 SQL。`QuerySession.cs` L29 把例外收成 `FromError`。無列不是 `Kind` Error。
- `TableDispatch.cs` 沿用 `get`，不探表。純量在進調度器前轉好。`ToolCatalog.cs` L22–24 已有名稱。`Program.cs` 三個工具仍是 `NotExecuted`。
- 只帶 `connectionId` 的舊測試仍不建檔。不要改 Core、Providers、WebService、GraphQL、兩個 `IDbService`。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/TableDispatch.cs` -- 撲平純量 `filters` 並直接執行三個 Session 函式。缺表名回 `invalid_arguments`，`Kind` Error 回 `execution_failed`。不探表，不參考 MCP，不組 SQL。
- [x] `McpService/Program.cs` -- 回傳上述 JSON。schema 只有 `connectionId`、`table`、`filters`。
- [x] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣並與 Session 比對。`Kind` Error 用送入的結果。缺表改成 `unknown_table` 時會失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。
- Given 呼叫這三個工具，when 檢查內容根，then 沒有新增稽核檔。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- high — `ReadNumber` 在精確整數失敗後用 `decimal.TryParse`。實測 `1e-29` 與 `-1e-29` 變成 0，超長小數變成 1。`count`／`exists` 會拿這個整數去比對。
- medium — `1e29` 與 `1e309` 沒有測試。拿掉數字 fallback 會變成整表計數，現有 `1.5` 仍過。驗證層已核對。
- high — `1e309` 的 `double.TryParse` 得到無限大，而且回傳 true。程式會把無限大送進 Session。
- medium — JSON `false` 沒有送進 `query`。改成 true 或丟掉該鍵，現有測試仍過。種子裡 Id 0 與 Id 1 各一列。驗證層已核對。
- medium — `filters` 的 `table` 或只差大小寫會蓋掉表名。核准的意圖不處理保留鍵與大小寫差。駁回。
- medium — 未知伺服器類型使 `Create` 回 null，接著空參考。建立失敗不在這一則。駁回。
- medium — 未登記的 `connectionId` 會在查找時拋出。核准的意圖不處理未登記名稱。駁回。
- high — 純量沒有照字面送進 Session：`1e-29` 變成 0。與第一則同一缺陷。
- false — `1.5` 的 `query` 兩邊一起失敗也通過。規格要求與 Session 相同。Core 的 `MapForKeyValue` 不拼接 decimal，宿主沒有另組 SQL。
- false — `null` 在 `query` 與 `count` 的 SQL 形狀不同，是 Core 既有行為。宿主有把 null 送進去，count 已與 Session 比對。
- low — 物件、陣列與非物件 `filters` 被略過後查詢變寬。核准的意圖不在這一則拒絕。駁回。
- false — 篩選鍵被拼進 SQL 是 `GetSqlByKeyValues`。規格寫明不要改 Core，宿主也不組 SQL。
- false — 登錄的 `count`／`exists` 一定有一個儲存格。沒有列時測試已比對 0 與 false。空的 `QueryResult` 不是這兩個函式的結果。
- low — schema 沒有鎖 `filters` 的型別。規格只要三個參數名稱，而且不要靠 required 擋下 `table`。駁回。
- low — `sprint-status.yaml` 的這一則仍是 `in-progress`，規格已是 `in-review`。改成 `review` 是一行。
- low — Code Map 仍寫 `NotExecuted`，Verification 沒有建置輸出。修正方式是改這份規格。駁回。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
