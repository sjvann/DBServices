---
title: 'Story 1.3 看欄位，不存在的表得到錯誤'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: '8ec81fc58c78c3a174e3569b7ad4655b36286456'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 外部 Agent 看得到 `fields`，但呼叫不會回傳欄位。缺表若變成空的欄位清單，Agent 會把它當成一張沒有欄位的表。

**Approach:** 客戶端帶可連線的 `connectionId` 與表名時，同一服務先執行 Session `tables`（`includeView` 為 true）。名稱以序數忽略大小寫命中後，才執行 Session `fields`，回傳該次 `columns` 與 `rows`。未命中是 `unknown_table`，不再呼叫 `fields`。

## Boundaries & Constraints

**Always:**
- `tools/list` 裡 `fields` 的參數是 `connectionId` 與 `table`。沒有連線字串欄位，也沒有 `includeView`。`table` 未帶或只有空白是 `invalid_arguments`，不呼叫 Session。執行時不要靠 schema 的 required 擋下。
- 探表與取欄位用同一次建立的服務。探表固定 `includeView` true。命中後去掉 `connectionId`，只把 `table` 送進 `fields`。
- 成功的唯一文字 content 是 JSON，鍵恰好 `columns`、`rows`。`pk` 與 `notNull` 是 JSON 布林，並與直接 Session `fields` 相同。
- 錯誤的唯一文字 content 是 JSON，鍵恰好 `code`、`message`。中文 `message` 不含連線字串。不另放 structured content。不是 HTTP 500。
- `tables` 維持上一則的行為。`get`、`query`、`count`、`exists` 仍不執行，也不為它們建立資料庫檔。

**Never:**
- 不處理省略 `connectionId`、未登記或空白名稱、`connectionString`、`connectString`，也不驗證建立失敗後再試。這些仍在延後清單，`fields` 不另做一套。
- 不註冊、不執行 `insert`、`update`、`delete`、`sql`。不寫稽核檔。不改 `IFunctionRegistry` 或 `IDbService` 來製造 `unknown_table`。
- 不改 Core、Providers、WebService、GraphQL。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。不讀 `ConnectionStrings`。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 輸入結構 | `tools/list` 的 `fields` | 有 `connectionId` 與 `table`；沒有連線字串與 `includeView` | N/A |
| 有表 | 可連線，`table` 為 `products` | 先 Session `tables`（含檢視）；命中後 Session `fields`；結果與直接 `fields` 相同 | N/A |
| 大小寫 | 表名只差大小寫 | 視為命中，仍呼叫 `fields` | N/A |
| 缺表 | 名稱不在探表清單 | `unknown_table`，不呼叫 `fields` | 不結束 |
| 探表失敗 | 探表 `Kind` 為 Error | `execution_failed`，不是 `unknown_table`，不呼叫 `fields` | 不結束 |
| 未帶表名 | 沒有 `table` 或只有空白 | `invalid_arguments`，不呼叫 Session | 不結束 |
| 其餘工具 | get、query、count、exists | 不執行，不為該呼叫建立資料庫檔 | 程序繼續 |

</frozen-after-approval>

## Code Map

- `DbServices.Core/Functions/FunctionRegistry.cs` — `fields` L37–47。缺 `table` 時 `Require` L158 丟例外，Session 收成 `Kind` Error。不要改。
- `DbServices.Core/DataBaseService.cs` — `GetFieldsByTableName` L367。缺表可變成空列，必須在呼叫前擋下。
- `DbServices.Core/Session/QuerySession.cs` — 例外在 L29 收成 `FromError`。探表用 `tables`，取欄位用 `fields`。
- `DbServices.Mcp/TableDispatch.cs` — 每次 `Create` 後釋放。`fields` 要在同一次服務上先探表再取欄位。
- `McpService/Program.cs` — 只有 `tables` 走調度。`fields` 回傳 `CallToolResult`。領域錯誤不要丟一般例外。
- `McpService.Tests/ToolListTests.cs` — `pk`、`notNull` 是布林，不要一律 `GetString`。健康 Sqlite 的探表不會自然變成 `Kind` Error。
- 不要改：`DbServices.Core/`、`DbServices.Provider.*`、`WebService/`、`GraphQLService/`、兩個 `IDbService` 介面。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/TableDispatch.cs` -- 同一服務先探表，命中才執行 `fields`。未命中、探表 Error、缺表名各回對應錯誤碼。不參考 MCP。 -- 缺表不會變成空欄位。
- [x] `McpService/Program.cs` -- `fields` 回傳上述 JSON。schema 只有 `connectionId` 與 `table`。 -- 客戶端看到工具結果。
- [x] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣。有表跟直接 Session 比對。探表 Error 用送入的結果，並確認不執行 `fields`。錯誤本文不含連線字串。 -- 缺表被當成空欄位時會失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`IFunctionRegistry`、`FunctionRegistry`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。
- Given 呼叫 `fields`，when 檢查內容根，then 沒有新增稽核檔。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- low — 探表失敗與取欄位失敗共用「列出欄位失敗。」。規格兩種都是 `execution_failed`，沒有要求兩句不同的中文。駁回。
- false — 探表不是 `Error`、或沒有 `name` 欄時會變成 `unknown_table`。既有 `tables` 只回 `Rows` 或 `Error`。吞掉的 SQL 例外是空列，不在清單裡就是 `unknown_table`。
- low — 沒有測試鎖住探表 arguments 裡的 `includeView` true。Core 目前不把這個旗標送進 SQL，檢視測試在送 false 時仍會過。要看見 arguments 得加觀測點。駁回。
- medium — 命中後 `fields` 回 `Kind` Error 沒有測試。拿掉檢查會變成成功的空欄位清單，現有測試仍過。驗證層已核對。
- false — 大小寫與檢視測試在空列時仍會過，拿掉探表也會過。空列對不上已種下的 `products`；拿掉探表會讓缺表測試失敗；大小寫改成序數比較會讓 `Products` 變成 `unknown_table`。
- false — `AssertErrorPayload` 用預期常數檢查 `mcpsecretmarker`。回應訊息已用 `AreEqual` 鎖定成那句不含標記的中文，洩漏會讓比對失敗。
- low — `sprint-status.yaml` 的這一則仍是 `in-progress`，規格已是 `in-review`。改成 `review` 是一行。
- low — 規格的 Code Map 仍寫只有 `tables` 走調度，Verification 沒有留下建置結果。修正方式是改這份規格。駁回。
- low — 非字串的 `table` 進不了 `invalid_arguments`。規格只要求未帶或空白。駁回。
- low — 直接呼叫 `FieldsAfterTables` 時空白表名會變成 `unknown_table`。客戶端路徑在建立服務前就回 `invalid_arguments`。駁回。
- medium — 未登記的 `connectionId` 會拋出查找例外。核准的意圖不處理未登記名稱，也不改成 `execution_failed`。駁回。
- medium — `Create` 回 null 會拋出 `NullReferenceException`。這是建立失敗，核准的意圖不在這一則處理。駁回。
- false — 表名只差大小寫時仍用呼叫端拼字查欄位，大小寫敏感的引擎會得到空欄位。規格要求與直接用同一個 `table` 呼叫 Session `fields` 相同，測試也是這樣比。
- false — 探表命中後資料表消失會回空欄位，而不是 `unknown_table`。命中後要回傳該次 `fields` 的結果，空列不是缺表。
- false — `fields` 的 `Kind` 不是 `Rows` 時仍當成成功。登錄的 `fields` 只回 `FromRows`，例外才是 `Error`。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
