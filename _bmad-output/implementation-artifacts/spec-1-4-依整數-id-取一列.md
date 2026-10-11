---
title: 'Story 1.4 依整數 Id 取一列'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: '2d8bbe547f9fbf932cc74f39692c6641aed3e1a6'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 外部 Agent 看得到 `get`，但還不能用整數 Id 取回同一列。找不到時不該變成未處理錯誤，缺表也不該被改成 `unknown_table`。

**Approach:** 客戶端帶可連線的 `connectionId`、表名與整數 `id` 時，直接執行 Session `get`。呼叫前去掉 `connectionId`。不先執行 `tables`。成功回傳該次 `columns` 與 `rows`，與直接 Session 相同。沒有該列是空的 `rows`。

## Boundaries & Constraints

**Always:**
- `tools/list` 裡 `get` 的參數是 `connectionId`、`table`、`id`。沒有連線字串欄位。`table` 未帶或只有空白、`id` 未帶、或 `id` 不是整數，是 `invalid_arguments`，不呼叫 Session，也不建立資料庫檔。執行時不要靠 schema 的 required 擋下。
- 整數是 JSON 數字，而且能精確放進 `Int64`，含 0 與負數。`1.0` 算整數。分數、字串、布林、物件與陣列不是。
- 成功的唯一文字 content 是 JSON，鍵恰好 `columns`、`rows`。空的 `rows` 是成功。整數儲存格是 JSON 數字。`IsError` 為 false。
- `Kind` 為 `Error`：`execution_failed`。唯一文字 content 是 JSON，鍵恰好 `code`、`message`。中文 `message` 不含連線字串，也不轉述例外原文。不是空列，不是 `unknown_table`，不是 HTTP 500。程序不結束。
- 表名不在資料表清單時，結果與直接 Session `get` 相同，回應碼不是 `unknown_table`。
- `tables` 與 `fields` 維持既有行為。`query`、`count`、`exists` 仍不執行，也不為它們建立資料庫檔。

**Never:**
- 不先呼叫 `tables`。不把缺表改成 `unknown_table`。
- 不處理省略 `connectionId`、未登記或空白名稱、`connectionString`、`connectString`，也不驗證建立失敗後再試。`get` 不另做一套。
- 不註冊、不執行 `insert`、`update`、`delete`、`sql`。不寫稽核檔。不改 Core、Providers、WebService、GraphQL。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 輸入結構 | `tools/list` 的 `get` | 有 `connectionId`、`table`、`id`；沒有連線字串 | N/A |
| 有列 | `products` 有整數 Id | 不先 `tables`；Session `get`；結果與直接 Session 相同 | N/A |
| 無列 | 該 Id 沒有列 | 空的 `rows`，成功 | 不結束 |
| Session 錯誤 | `get` 的 `Kind` 為 Error | `execution_failed`，不是空列 | 不結束 |
| 參數不合法 | 未帶 `table` 或 `id`，或 `id` 不是整數 | `invalid_arguments`，不呼叫 Session，不建檔 | 不結束 |
| 缺表 | 表名不在清單，Id 是整數 | 與直接 Session `get` 相同，不是 `unknown_table` | 不結束 |
| 其餘工具 | query、count、exists | 不執行，不為該呼叫建立資料庫檔 | 程序繼續 |

</frozen-after-approval>

## Code Map

- `DbServices.Core/Functions/FunctionRegistry.cs` — `get` L49–58。`Convert.ToInt64` 對非整數丟例外，Session 收成 `Kind` Error。不要改。
- `DbServices.Core/DataBaseService.cs` — `GetRecordById` L551。沒有列回 null。L577 起的例外訊息含表名，不要交給客戶端。
- `DbServices.Core/Session/QueryResultMapper.cs` — `FromTable` L7。null 走 `Empty`（L38），`Kind` 是 `Rows`。
- `DbServices.Core/Session/QuerySession.cs` — 例外在 L29 收成 `FromError`。
- `DbServices.Mcp/TableDispatch.cs` — `fields` 會先探表。`get` 不要走那條。
- `McpService/Program.cs` — `id` 要用能分辨非整數的參數，不要讓 SDK 綁定失敗變成一般例外。
- `McpService.Tests/ToolListTests.cs` — 沒有該列的健康 Sqlite 是空列，不是 `Kind` Error。
- 不要改：`DbServices.Core/`、`DbServices.Provider.*`、`WebService/`、`GraphQLService/`、兩個 `IDbService` 介面。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/TableDispatch.cs` -- 直接執行 Session `get`。缺參數或非整數 `id` 回 `invalid_arguments`。`Kind` Error 回 `execution_failed`。不探表，不參考 MCP。 -- 找不到列是空結果。
- [x] `McpService/Program.cs` -- `get` 回傳上述 JSON。schema 只有 `connectionId`、`table`、`id`。 -- 客戶端看到工具結果。
- [x] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣。有列、無列、缺表跟直接 Session 比對。`Kind` Error 用送入的結果。錯誤本文不含連線字串。 -- 缺表被改成 `unknown_table` 時會失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。
- Given 呼叫 `get`，when 檢查內容根，then 沒有新增稽核檔。

## Implementation Notes

- `id` 用 `JsonElement?`。JSON 數字能精確放進 `Int64` 才送 Session，`1.0` 算整數。缺表名、空白表名或非整數在建立服務前回 `invalid_arguments`，訊息是「未提供資料表名稱或整數 Id。」
- Session `Kind` Error 回 `execution_failed`，訊息是「依 Id 取一列失敗。」不附例外原文，也不先呼叫 `tables`。

## Spec Change Log

## Review Triage Log

- high — `TryReadInt64` 用 `decimal.TryParse`。實測 `1e-29`、`1E-30`、`-1e-29` 變成 0，小數點後多出 `decimal` 精度的 `1.000…0001` 變成 1，仍回傳 true。分數會建立服務並取走別列。
- medium — 不合法測試沒有送出上述分數。只留 `1.5` 時，拿掉精確檢查仍會過。與上一則同一缺陷。
- false — `0.0`、`10.0`、`1e2`、`10e-1` 與 `Int64` 邊界的 `.0` 目前都會收成精確整數。缺測試不是這些值會失敗。
- false — 省略 `table`、空白表名、字串 `id`、JSON null `id` 已是 `invalid_arguments`。空字串 `id` 與字串 `"1"` 同是 `JsonValueKind.String`。
- false — 舊測試呼叫 `get` 只帶 `connectionId`。建立服務前回 `invalid_arguments`，檔案不會出現。
- false — 登錄的 `get` 只回 `FromTable`（`Rows`）或例外收成的 `Error`。`NonQuery` 與 `Catalog` 不會從這次 Session `get` 出來。
- false — Sqlite 缺表丟 `DbServiceException`，Session 收成 `Kind` Error。`Get_missing_table` 走 HTTP，並用 `AssertErrorPayload` 鎖定單一 text 與固定中文。
- false — `CallGet` 沒有設定 structured content。成功測試已鎖定單一 text，以及鍵恰好 `columns`、`rows`。
- false — 規格要在執行期拒絕非整數，不要靠 schema 擋下。把 `id` 收成單一型別會讓字串進不了 `invalid_arguments`。說明來自不能改的 FunctionRegistry。
- low — `sprint-status.yaml` 的這一則仍是 `in-progress`，規格已是 `in-review`。改成 `review` 是一行。
- low — Verification 沒有貼上建置輸出。修正方式是改這份規格。駁回。
- false — `query`、`count`、`exists` 在讀連線字串之前就丟「工具未執行。」。標記只在連線字串裡。不建檔已有斷言。
- false — 無列成功本文只有 `columns` 與 `rows`，連線字串不是儲存格。
- false — Sqlite 的 Id 是 `long`。通過的有列測試因此要求 JSON 數字。`ulong` 不是這張表的儲存格。
- medium — 未登記或空白 `connectionId` 會在查找時拋出。核准的意圖不處理未登記名稱。駁回。
- medium — `Create` 回 null 會空參考。建立失敗不在這一則。駁回。
- medium — `Create` 在 try 之外拋出會變成未處理例外。建立失敗不在這一則。駁回。
- high — 任務寫非整數回 `invalid_arguments`，但 `1e-29` 被收成 0。與第一則同一缺陷。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
