---
title: 'Story 1.1 啟動宿主，Agent 只看見六個唯讀工具'
type: 'feature'
created: '2026-10-11'
status: 'in-review'
route: 'dispatch'
baseline_commit: '889d4686c8f4e3d6aaf32e06786e5d883070ac32'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 外部 Agent 沒有唯讀工具入口。清單若含寫入或自由 SQL，會在查資料前走到寫入路徑。

**Approach:** 新增 `McpService` 與 `DbServices.Mcp`。只廣告六個唯讀、非破壞性工具，且不執行。`insert`、`update`、`delete`、`sql` 不註冊，也沒有處理函式。

## Boundaries & Constraints

**Always:**
- `McpService` 是 `net10.0` ASP.NET Core，只參考 `DbServices.Mcp`。套件 `ModelContextProtocol.AspNetCore` 2.2.0。`MapMcp("/mcp")`，`Stateless`，舊版 SSE 關閉。
- `DbServices.Mcp` 不參考 ASP.NET，也不參考任何 `DbServices.Provider.*`。
- 位址只來自組態。`AllowedHosts` 為 `*`。無 OpenAPI、Scalar、CORS。
- 清單恰好是 `tables`、`fields`、`get`、`query`、`count`、`exists`。說明為「列出資料表」「列出欄位」「依 Id 取一列」「鍵值查詢」「計數」「是否存在」，不含 SQL。皆唯讀、非破壞性。
- 清單不因連線筆數或無法辨識的 `ServerType` 而改變。未知 `ServerType` 或空白連線字串啟動時標成不可用，不呼叫 `Create`，程序不結束。

**Never:**
- 不註冊四個關閉名稱，不提供會執行 Session 的處理函式，不呼叫 `DbServiceFactory.Create`。
- 不修改 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Provider、WebService、GraphQLService。
- 不實作 `tool_disabled` 與 `unknown_tool`。該項在 `deferred-work.md`。
- 不寫稽核，不讀 `X-DBServices-Caller`，不做 stdio。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 列出工具 | 至少一筆已知 `ServerType` 與非空白連線字串 | 恰好六個名稱與上列說明，皆唯讀、非破壞性。四個關閉名稱不在清單 | 不呼叫 `Create` |
| 未知引擎 | 某一筆 `ServerType` 不在五個名稱內 | 該筆不可用。清單仍是六個。程序仍在 | 啟動時不呼叫 `Create` |
| 零筆連線 | 沒有組態完整的項目 | 宿主仍啟動，清單仍是六個 | 不呼叫 `Create` |

</frozen-after-approval>

## Code Map

- `DBServices.sln` — `dotnet sln add`。不要手改既有 GUID。
- `WebService/WebService.csproj`、`WebService/Program.cs` — 不要改，也不要複製 OpenAPI 與 Scalar。
- `WebService/appsettings.json` — 沿用 `AllowedHosts` `*`。不讀 `ConnectionStrings:default` 或 `ServerType:Name`。
- `WebService/Properties/launchSettings.json` — 現有埠 5133、7261。新宿主用 `http://localhost:5291`。
- `DbServices.Api.Shared/DbServiceFactory.cs` — `Create` 在 L11–21。這一則不呼叫。
- `DbServices.Core/Functions/FunctionRegistry.cs` — 說明在 L28、L39、L52、L63、L116、L129。不要改，不要廣告 L145 的 `sql`。
- `DbServices.Core/Models/Interface/IDbService.cs`、`IDbServiceAsync.cs` — 簽名不可改。
- `DBServiceTest/DBServiceTest.csproj` — 宿主測試不要放這裡。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/DbServices.Mcp.csproj` -- `net10.0` 類別庫，參考 Core 與 `DbServices.Api.Shared`。不參考 ASP.NET、Provider、OpenAPI、Scalar -- 目錄可脫離宿主測試。
- [x] `DbServices.Mcp/ToolCatalog.cs` -- 六個工具的名稱、說明、唯讀與非破壞性，以及 `Connections` 快照。未知引擎或空白連線字串標成不可用，不呼叫 `Create`，不建立關閉名稱的處理函式 -- 沒有寫入入口。
- [x] `McpService/McpService.csproj` -- Web SDK，只參考 `DbServices.Mcp`，套件 `ModelContextProtocol.AspNetCore` 2.2.0。
- [x] `McpService/Program.cs` -- 公開 partial `Program`。綁定 `Connections`。`MapMcp("/mcp")`，Stateless，關舊 SSE。只註冊六個工具。無 OpenAPI、Scalar、CORS。
- [x] `McpService/appsettings.json` -- `AllowedHosts` 為 `*`。`Connections` 的值只有 `ServerType` 與 `ConnectionString`。
- [x] `McpService/Properties/launchSettings.json` -- `http://localhost:5291`。
- [x] `DBServices.sln` -- `dotnet sln add` 兩個專案與測試專案。
- [x] `McpService.Tests/McpService.Tests.csproj` -- MSTest，對齊 `DBServiceTest`（Test.Sdk 18.10.1、MSTest 4.5.1）。`WebApplicationFactory` 覆蓋矩陣三列，並斷言四個關閉名稱不在清單、沒有 `Create`。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。

## Implementation Notes

- 六個工具註冊成 MCP 工具，共用一個不呼叫 Session 的委派；呼叫時丟出 `InvalidOperationException`。`tool_disabled` 與 `unknown_tool` 仍在 `deferred-work.md`。
- `Connections` 以陣列繫結，索引如 `Connections:0:ServerType`。值只有 `ServerType` 與 `ConnectionString`。
- `dotnet test McpService.Tests/McpService.Tests.csproj`：4 通過、0 失敗。矩陣三列都有對應測試。`dotnet build DBServices.sln` 成功，含 `WebService` 與 `GraphQLService`。

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過，覆蓋矩陣三列
- `dotnet build DBServices.sln` -- expected: 成功
