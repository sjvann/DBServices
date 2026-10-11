---
title: 'Story 1.7 換 ServerType，同一組工具仍成立'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: 'b19041bd7dd3a17621b47ae1730f30a3fa1f37b5'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 換 `ServerType` 或拼錯時，Agent 仍要用同一組工具名稱與參數鍵。拼錯現在會在建立服務時丟出例外，而不是 `connection_failed`。

**Approach:** 服務仍只經 `DbServiceFactory.Create`。名稱不是五個精確字串時，建立前回 `connection_failed`。有測試庫的引擎，`tables`、`fields`、`query` 與該連線的 Session 相同。

## Boundaries & Constraints

**Always:**
- 五個名稱是 `Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`，大小寫必須一致。Agent 不傳引擎。工具名稱與參數鍵不含引擎。
- 選到的連線名稱不在這五個字串時，`tables` 在 `Create` 前回應 `connection_failed`，訊息是「無法建立連線。」鍵只有 `code` 與 `message`，不含連線字串。程序不結束，另一筆可連線的 `tables` 仍成功。
- 其他工具若參數已會是 `invalid_arguments`，仍先那樣拒絕且不建立服務。參數可接受時，同樣在 `Create` 前給「無法建立連線。」
- 有可連線測試庫時，`tables`、`fields`、`query` 的列集合與該連線上直接 Session Call 相同。沒有測試庫、建立失敗，或 Session 的 `tables` 是 `Kind` Error，這一則仍通過。
- Sqlite 由測試自己建庫並準備資料。進入調度器的拒絕仍寫稽核，`error` 是 `connection_failed`。

**Never:**
- 不實作未登記或空白 `connectionId`、省略 `connectionId`、`connection_required`、`tool_disabled`、`unknown_tool`。不改空白連線字串的既有行為，也不做建立失敗的快取或恢復。
- 宿主與 `DbServices.Mcp` 不依引擎抄寫查詢，也不加成功連線快取。不改 `DbServiceFactory`、Core、Providers、WebService、GraphQL、兩個 `IDbService`。`DbServices.Mcp` 不參考 ASP.NET 或 MCP。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 同一組工具 | 可連線的 Sqlite；Agent 不傳引擎 | `tables`、`fields`、`query` 與該連線的 Session 相同；參數鍵不含引擎 | N/A |
| 另一個引擎 | 該名稱另有可連線測試庫 | 同樣三步與該連線的 Session 相同 | 沒有測試庫、建立失敗或 Session `tables` 為 Error：仍通過 |
| 拼錯 | `ServerType` 不是那五個字串；另一筆 Sqlite 可連 | `tables` 為 `connection_failed`，訊息「無法建立連線。」，不含連線字串；接著另一筆 `tables` 成功 | 不結束 |
| 其他工具 | 參數可接受，但名稱拼錯 | `Create` 前同樣是「無法建立連線。」 | 參數本就無效時仍是 `invalid_arguments`，不建立 |
| 稽核 | 拼錯的呼叫進入調度器 | 一行，`error` 是 `connection_failed` | 不結束 |

</frozen-after-approval>

## Code Map

- `DbServiceFactory.Create` 只接受那五個精確名稱，其餘回 null。不要改這個檔。
- `ToolCatalog.KnownServerTypes` 是 `Ordinal`。空白連線字串也會讓 `Available` 為 false；不要改那個情況。
- `TableDispatch` 在 `Connections[connectionId]` 之後立刻 `Create`。拼錯時在那之前回傳，沿用 `Error(code, message)`。缺表名或非整數 Id 的 `invalid_arguments` 仍在 `Create` 之前。
- `Program.cs` 的 `Finish` 已寫稽核與 `CallToolResult`。不要在這裡依引擎分支。
- `ToolListTests.cs` 沿用 `CreateFactory`、`CallToolAsync`、`ExecuteTables`、`ExecuteFields`、`ExecuteNamed` 與現有的 Session、錯誤、稽核斷言。連線字串含 `mcpsecretmarker`。其他引擎只看 `DBSERVICES_TEST_SQLSERVER`、`DBSERVICES_TEST_MYSQL`、`DBSERVICES_TEST_ORACLE`、`DBSERVICES_TEST_POSTGRESQL`；沒有或空白就跳過。準備資料的 SQL 只放在測試裡。
- 不要改 Core、Providers、WebService、GraphQL、`DbServiceFactory`、兩個 `IDbService`。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/TableDispatch.cs` -- `ServerType` 不是五個精確名稱時，在 `Create` 前回 `connection_failed`。既有的參數拒絕仍在建立之前。不依引擎抄寫查詢。
- [x] `McpService.Tests/ToolListTests.cs` -- 覆蓋矩陣。Sqlite 的三步與 Session 相同。拼錯之後另一筆 `tables` 仍成功。沒有測試庫的引擎不失敗。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、`DbServiceFactory`、Providers、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- medium — 允許清單漏掉 `SqlServer`、`MySQL`、`Oracle` 或 `PostgreSQL` 時，平常測試仍綠。`Other_engines` 在沒有環境變數時直接返回。驗證層已核對。
- false — 沒有測試庫、準備失敗，或 Session 的 `tables` 是 `Kind` Error 時仍通過。規格寫明這種情況這一則仍通過。
- low — 檔案不存在不能證明完全沒呼叫 `Create`。未知名稱的 `Create` 本來就回 null、不建檔。要證明呼叫順序需要新的接縫。駁回。
- false — `ListTools` 的 schema 已檢查參數鍵不含引擎名稱。六個工具名稱由既有測試鎖定。
- false — 規格的三步是 `tables`、`fields`、`query`。`get`、`count`、`exists` 對 Session 不在這一則。
- low — 其他引擎的 `query` 沒有強制看到植入列，刪表失敗可能留下探針表。沒有環境變數時不跑，規格要求的是與 Session 相同。駁回。
- false — `ReadAudit` 已拒絕 `mcpsecretmarker`。矩陣只要求拼錯那次的 `error` 是 `connection_failed`。空白連線字串是核准意圖不改的行為。
- low — 四個引擎串在同一則測試，PostgreSQL 的 schema 也沒有分開斷言。沒有環境變數時不跑。駁回。
- false — 矩陣寫的是 `ServerType`。程式拒絕的也是 `ServerType`。五個名稱再寫一份，是因為不能改 `DbServiceFactory`，也不能用 `Available`，那會改到空白連線字串。
- low — `sprint-status.yaml` 的這一則仍是 `in-progress`，規格已是 `in-review`。改成 `review` 是一行。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過
- `dotnet build DBServices.sln` -- expected: 成功
