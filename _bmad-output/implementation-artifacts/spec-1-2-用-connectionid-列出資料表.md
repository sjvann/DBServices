---
title: 'Story 1.2 用 connectionId 列出資料表'
type: 'feature'
created: '2026-10-11'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 外部 Agent 看得到 `tables`，但給了已登記的連線名稱仍不會列出該庫的資料表。

**Approach:** 客戶端帶可連線的 `connectionId` 時，經 `DbServiceFactory.Create` 與 `QuerySession.Execute`（opcode `Call`、函式名 `tables`）執行。呼叫前去掉 `connectionId`。成功回傳該次 `columns` 與 `rows`。

## Boundaries & Constraints

**Always:**
- `tools/list` 裡 `tables` 的參數是 `connectionId`，以及可省略的 `includeView`。沒有連線字串欄位。
- 省略 `includeView` 視為 true。`false` 原樣送進 Session。結果與直接呼叫 Session 的 `tables` 相同。
- 查找名稱用序數忽略大小寫。`ServerType` 仍與工廠分支序數比對。
- 成功：`IsError` 為 false。唯一文字 content 是 JSON，鍵恰好 `columns`、`rows`。空庫是空的 `rows`，程序不結束。
- `Kind` 為 `Error`：`execution_failed`。`IsError` 為 true。唯一文字 content 是 JSON，鍵恰好 `code`、`message`。中文 `message` 不含連線字串。不是空列，也不是 HTTP 500。不另放 structured content。
- 其餘五個工具仍不執行，也不為它們建立資料庫檔。六個工具仍唯讀、非破壞性。

**Never:**
- 不處理省略 `connectionId`、未登記或空白名稱、`connectionString`、`connectString`，也不驗證建立失敗後再試。
- 不註冊、不執行 `insert`、`update`、`delete`、`sql`。不寫稽核檔。不實作另外五個工具。
- 不改 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Providers、WebService、GraphQL。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。不讀 `ConnectionStrings`。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 輸入結構 | `tools/list` 的 `tables` | 有 `connectionId`；`includeView` 可省略；沒有連線字串欄位 | N/A |
| 有表 | 可連線的 `connectionId`，省略 `includeView` | 經 `Create` 與 Session `tables`；結果含檢視名稱，與直接 Session 相同 | N/A |
| 不含檢視 | `includeView` 為 false | 與既有函式在 false 時相同 | N/A |
| 空庫 | 可連線但沒有表 | `rows` 為空陣列，程序繼續 | N/A |
| Session 錯誤 | `tables` 的 `Kind` 為 Error | `execution_failed`，不是空列 | 不結束 |
| 其餘工具 | fields、get、query、count、exists | 不執行，不為該呼叫建立資料庫檔 | 程序繼續 |

</frozen-after-approval>

## Code Map

- `DbServices.Core/Functions/FunctionRegistry.cs` — `tables` L25–34；`ReadBool` L168–172 缺鍵或 null 為 true。不要改。
- `DbServices.Core/DataBaseService.cs` — `GetAllTableNames` L344 吃掉 `SqlException`。`null` 變成空列，不是 `Kind` Error。
- `DbServices.Core/Session/QuerySession.cs` — `Execute` L9；例外在 L29 收成 `FromError`。
- `DbServices.Core/Session/QueryRequest.cs` — 送入前不要留 `connectionId`。
- `DbServices.Api.Shared/DbServiceFactory.cs` — `Create` L11–21，未知引擎回 null。Sqlite 建構會建檔。
- `DbServices.Mcp/ToolCatalog.cs` — 連線字典現為 `Ordinal`（L30）。查找改為忽略大小寫。
- `McpService/Program.cs` — L26–48 都走 `NotExecuted`。只替換 `tables`，回傳 `CallToolResult`。領域錯誤不要丟一般例外。
- `McpService.Tests/ToolListTests.cs` — `tables` 會建 Sqlite 檔；其餘五支不建檔。
- 不要改：`DbServices.Core/`、`DbServices.Provider.*`、`WebService/`、`GraphQLService/`、兩個 `IDbService` 介面。

## Tasks & Acceptance

**Execution:**
- [ ] `DbServices.Mcp/TableDispatch.cs` -- 依 `connectionId` 建立服務並執行 Session `tables`，回傳列或 `execution_failed`。不參考 MCP。 -- 結果與直接 Session 相同。
- [ ] `DbServices.Mcp/ToolCatalog.cs` -- 連線鍵忽略大小寫。 -- `Inventory` 找得到 `inventory`。
- [ ] `McpService/Program.cs` -- `tables` 回傳上述 JSON。schema 只有 `connectionId` 與可省略的 `includeView`。其餘五個工具不執行。 -- 客戶端看到工具結果。
- [ ] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣。有表與 `includeView` false 跟直接 Session 比對。錯誤本文不含連線字串。 -- 沒執行或把 Error 當成空列時會失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。
- Given 呼叫 `tables`，when 檢查內容根，then 沒有新增稽核檔。

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
