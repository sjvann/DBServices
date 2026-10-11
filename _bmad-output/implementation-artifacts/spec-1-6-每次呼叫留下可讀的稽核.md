---
title: 'Story 1.6 每次呼叫留下可讀的稽核'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: '768b7b0c0891d0ff71222b97c3ade1260e4f8071'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 工具呼叫結束後，本機沒有紀錄能回答是誰、對哪個連線、用了什麼條件、回了多少列或哪個錯誤。

**Approach:** 每一次進入調度器的呼叫，成功或既有拒絕都由函式庫往單一 JSONL 追加一行。

## Boundaries & Constraints

**Always:**
- 預設是內容根下的 `audit/mcp.jsonl`。相對的 `Audit:Path` 由宿主接在內容根後，只有函式庫開啟該絕對路徑。此檔不是工具，也不是 `Connections` 的一筆。`tools/list` 不寫行。
- 鍵恰好 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。沒用到的鍵是 null。`timestamp` 是 UTC ISO-8601，以 `Z` 結尾。
- `arguments` 去掉 `connectionString` 與 `connectString`（不區分大小寫）。選到連線時，這一行與 `arguments` 裡的 `connectionId` 都是目錄裡解析後的鍵。全文沒有連線字串，也沒有列資料。
- `tables`、`get`、`query` 成功：`rowCount` 為列數，`value` 與 `error` 為 null。`fields` 成功相同，但 `rowCount` 只算 `fields` 的列，不含探表，且只有一行、`tool` 是 `fields`。
- `count` 或 `exists` 成功：`value` 是該數字或布林，`rowCount` 與 `error` 為 null。既有拒絕：`error` 是該 `code`，`rowCount` 與 `value` 為 null。
- 行寫不進去：客戶端得到 `execution_failed`，不回傳本次的列、數字或布林。程序不結束。
- 標頭 `X-DBServices-Caller` 是 `caller`；空白或不存在是 `anonymous`，呼叫仍執行。函式庫不讀 HTTP。兩個同時完成的呼叫是兩行完整 JSON。
- 既有工具結果維持不變。

**Never:**
- 不實作 `tool_disabled`、`unknown_tool`、省略 `connectionId`、未登記或空白名稱、`connection_required`、`connection_failed`。尚未進入調度器的呼叫不另做稽核。
- 不另開第二個稽核儲存、健康檢查或指標。不改 Core、Providers、WebService、GraphQL。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 成功查列 | `tables`、`get` 或 `query` 成功 | 一行；`rowCount` 為列數；`value` 與 `error` 為 null；無列資料、無連線字串 | N/A |
| 計數或存在 | `count` 或 `exists` 成功 | `value` 為該數字或布林；`rowCount` 與 `error` 為 null | N/A |
| 既有拒絕 | `invalid_arguments` 或 `unknown_table` | `error` 為該碼；`rowCount` 與 `value` 為 null | 不結束 |
| 呼叫者 | 有標頭，或空白、不存在 | `caller` 為標頭值或 `anonymous`；呼叫仍執行 | N/A |
| 路徑 | 預設，或相對 `Audit:Path` | 寫入內容根下的該檔 | N/A |
| 探表 | `fields` 內部又呼叫 `tables` | 只有一行，`tool` 是 `fields`，`rowCount` 不含探表 | N/A |
| 寫入失敗 | 檔案寫不進去 | `execution_failed`，不回傳列、數字或布林 | 不結束 |
| 並行 | 兩個呼叫同時完成 | 兩行完整 JSON | 不寫斷 |

</frozen-after-approval>

## Code Map

- `McpService/Program.cs` — `CallTables`、`CallFields`、`CallGet`、`CallEquality` 進入 `TableDispatch`。在這裡讀內容根、`Audit:Path` 與 `X-DBServices-Caller`。
- `DbServices.Mcp/TableDispatch.cs` — 結果只有 `IsError` 與 `Json`。列數或純量從這次結果取，不要再查。`fields` 的探表在同一次 `ExecuteFields`，不另寫行。
- `ToolListTests.cs` 的 `AssertNoAuditFile` 要求沒有 `audit/mcp.jsonl`。進入調度器的呼叫改為檢查該行。只做 `tools/list` 的仍不建檔。連線字串含 `mcpsecretmarker`。寫入失敗用開不了的路徑製造。
- 不要改 Core、Providers、WebService、GraphQL、兩個 `IDbService`。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp` -- 追加一行 JSONL。寫不進去時不附帶本次結果。並行追加不把一行寫斷。不參考 ASP.NET 或 MCP。
- [x] `McpService/Program.cs` -- 進入調度器的呼叫都寫該行。解析內容根、`Audit:Path` 與呼叫者。寫入失敗時客戶端看到 `execution_failed`。
- [x] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣。進入調度器的呼叫改檢查稽核行。列資料或連線字串出現在該行時會失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- low — `AuditPath` 對絕對路徑或含 `..` 的相對路徑，正規化後會離開內容根。預設與一般相對路徑仍寫在內容根下。補上路徑監牢要加新判斷。駁回。
- medium — 測試宿主的內容根是 McpService 專案目錄。`CreateFactory` 刪掉那裡的 `audit/mcp.jsonl`，`ClassCleanup` 再刪整個 `audit` 目錄。跑測試會清掉本機稽核。
- low — `AssertEnteredAudit` 只對行數、`caller` 與 `connectionId`。零列的 `rowCount` 0 沒有專斷言。矩陣已有專用測試，`ReadAudit` 也要求恰好一個結果欄。把每個舊測試都加深，超過直接修正。駁回。
- medium — 寫不進去只測 `tables`、`count`、`exists`。`fields`、`get`、`query` 的失敗文案與不回列沒有鎖住。驗證層已核對。
- false — 並行測試沒有要求時間戳順序。規格只要兩行完整 JSON。時間戳在鎖外產生，不違反該句。
- low — 並行測試沒有迫使兩次 `Write` 在鎖內重疊。鎖已在 `TryAppend`。要另做壓測才證明交錯。駁回。
- false — 連線字串只依鍵名剔除。目錄裡的連線字串沒有送進 `CallAudit`。篩選值是呼叫者的條件；規格要求留下條件，並去掉 `connectionString` 與 `connectString`。
- false — 成功的列結果一定有 `rows` 陣列，純量是數字或布林，錯誤一定有字串 `code`。`Describe` 不會把列內容複製進 `value`，三個結果欄也不會同時是 null。
- false — `\uXXXX` 仍是合法 JSON，解碼後與原文相同。規格沒有要求不跳脫的 UTF-8。
- medium — `TryAppend` 在 `Write` 或 `Flush` 失敗後沒有把檔案截回寫入前的長度。半行會留在檔上，下一行接在後面。
- false — `ReadAudit` 的 `mcpsecretmarker`、`"rows"` 與 `"columns"` 會擋下列內容被整份寫入。現行程式不複製儲存格。固定舊時間不是現行程式，它用 `UtcNow`。
- low — `sprint-status.yaml` 的這一則仍是 `in-progress`，規格已是 `in-review`。改成 `review` 是一行。
- low — 絕對路徑或 `..` 寫出內容根，與第一則同一主張。駁回。
- medium — `Audit:Path` 含非法字元或超長時，`GetFullPath` 在寫入前拋出。客戶端不是 `execution_failed`。
- medium — 寫到一半失敗留下半行，與截斷那一則同一主張。
- false — 缺 id 的 `get` 回 `invalid_arguments` 並寫行。`Create` 回 null 的空參考是核准意圖不實作的建立失敗。
- false — 調度器拋出就不寫行，與上一則同一主張。駁回。
- medium — 非法稽核路徑不是 `execution_failed`，與路徑解析那一則同一主張。驗證層以外的主張，已沿呼叫端核對。
- false — 列資料若不含 `rows`、`columns` 或連線標記就可能通過。現行程式不把儲存格寫進該行；整份列內容會帶出那些字。
- medium — `fields`、`get`、`query` 在稽核寫不進去時沒有斷言。與寫入失敗那一則同一缺口。驗證層已核對。
- medium — `tables` 送出 `includeView` 時，稽核列沒有檢查這個鍵。拿掉寫入該鍵的那一行，現有測試仍過。驗證層已核對。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
