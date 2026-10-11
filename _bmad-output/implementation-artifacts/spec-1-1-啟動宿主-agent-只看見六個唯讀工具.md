---
title: 'Story 1.1 啟動宿主，Agent 只看見六個唯讀工具'
type: 'feature'
created: '2026-10-11'
status: 'done'
route: 'dispatch'
baseline_commit: '889d4686c8f4e3d6aaf32e06786e5d883070ac32'
review_loop_iteration: 1
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
- [x] `DbServices.Mcp/ToolCatalog.cs` -- 六個工具的名稱、說明、唯讀與非破壞性，以及 `Connections` 字典快照。鍵是 `connectionId`。未知引擎或空白連線字串標成不可用，不呼叫 `Create`，不建立關閉名稱的處理函式 -- 沒有寫入入口，名稱在繫結時保留。
- [x] `McpService/McpService.csproj` -- Web SDK，只參考 `DbServices.Mcp`，套件 `ModelContextProtocol.AspNetCore` 2.2.0。
- [x] `McpService/Program.cs` -- 公開 partial `Program`。綁定 `Connections` 字典。`MapMcp("/mcp")`，Stateless，關舊 SSE。只註冊六個工具。無 OpenAPI、Scalar、CORS。註冊用的工具與 DI 裡的 catalog 是同一份。
- [x] `McpService/appsettings.json` -- `AllowedHosts` 為 `*`。`Connections` 是字典，鍵是 `connectionId`，值只有 `ServerType` 與 `ConnectionString`。
- [x] `McpService/Properties/launchSettings.json` -- `http://localhost:5291`。
- [x] `DBServices.sln` -- `dotnet sln add` 兩個專案與測試專案。
- [x] `McpService.Tests/McpService.Tests.csproj` -- MSTest，對齊 `DBServiceTest`（Test.Sdk 18.10.1、MSTest 4.5.1）。`WebApplicationFactory` 覆蓋矩陣三列，連線鍵使用 `connectionId`。斷言四個關閉名稱不在清單、沒有 `Create`。呼叫已廣告工具不會建立資料庫檔。`Host` 不是 `localhost` 時清單仍成功。

**Acceptance Criteria:**
- Given 這一則的 diff，when 比對 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、`WebService` 與 `GraphQLService`，then 這些檔案沒有變更。
- Given 方案建置，when 建置 `WebService` 與 `GraphQLService`，then 它們仍成功。

## Implementation Notes

## Spec Change Log

- 觸發：`Connections` 被綁成沒有名稱的陣列，字典鍵 `connectionId` 會變成空清單，未知引擎也不會被標成不可用。
- 修改：`ToolCatalog`、`Program.cs`、`appsettings.json` 與測試任務改為字典，鍵是 `connectionId`。測試補上呼叫已廣告工具，以及非 `localhost` 的 `Host`。
- 避免：操作者依史詩使用具名連線時，宿主靜默當成零筆。
- KEEP：六個工具的名稱與說明、唯讀與非破壞性、`MapMcp("/mcp")`、Stateless、關閉舊 SSE、`AllowedHosts` `*`、埠 5291、不呼叫 `Create`、未知引擎標成不可用、不改 Core／WebService／GraphQL、MSTest 版本、四個關閉名稱不註冊。

## Review Triage Log

- medium — `epic-1-context.md` 刪掉後續故事仍要用的句子（單一文字 content、user secrets 順序、UTC／Base64、探表不另記稽核、沒有測試庫不算失敗）。diff 對得到刪除。還原句子即可。
- low — `sprint-status.yaml` 在審查時仍是 `in-progress`。規格已是 `in-review`。改狀態是一行。
- medium — `ToolCatalog` 與 `Program.cs` 把 `Connections` 綁成 `List`，沒有 `connectionId`。`Get<List>` 對字典組態得到空清單。bad_spec。
- false — 傳遞參考 Provider。`DbServices.Mcp.csproj` 沒有 Provider 的 ProjectReference；任務明文要求參考 `DbServices.Api.Shared`。
- false — 兩個 catalog 的工具清單會分叉。兩邊的 `Tools` 都指向同一個靜態陣列，目前不會分叉。
- false — 位址只寫在 launchSettings。任務把 `http://localhost:5291` 放在該檔。
- medium — 沒有 `tools/call` 測試。`NotExecuted` 改成回傳成功時，現有四個測試仍過。驗證層已核對。
- low — 零筆測試沒清掉環境裡的 `Connections`。這台機器上測試已通過；清掉來源是直接修正。
- false — 只測了 Sqlite、大小寫、混合清單。矩陣只要求一筆已知與一筆未知；`Ordinal` 與工廠的五個字面名稱一致。
- low — 五個引擎名稱寫了兩份。這一則不呼叫 `Create`，把清單抽成共用型別會多一層。駁回。
- low — `/sse`、`/message`、initialize 的 JSON-RPC error、清單不含連線字串、OpenAPI 負向測試。產品程式沒有註冊這些端點；補分支測試超過直接修正。駁回。
- false — 快照留著連線字串。`tools/list` 的 JSON 只有名稱、說明與註記，客戶端看不到這段字串。
- low — 測試把含 `data:` 的 JSON 當 SSE、非物件 SSE、缺 `result.tools` 會丟例外、initialize 只看 HTTP 200。測試仍會失敗或只在異常本文出現。駁回。
- low — `DbServiceFactory` 的 UTF-16 搜尋會漏。與缺少 `tools/call` 是同一件事，不另改搜尋。
- maybe-false — `Connections` 陣列裡的 null 項目可能在 `ToSnapshot` 丟 `NullReferenceException`。要確認設定繫結會不會產生 null 元素。若會，宿主起不來，嚴重性是 medium。未證實，不在這次 bad_spec 重做裡改行為。
- medium — `AllowedHosts` 為 `*` 沒有用非 `localhost` 的 Host 測到。改成 `localhost` 時現有測試仍過。驗證層已核對。
- carried medium — `epic-1-context.md` 仍沒有單一文字 content、user secrets 順序、UTC／Base64、探表不另記稽核、沒有測試庫不算失敗。與上一列同一處、同一主張，檔案仍是那樣。不再改。
- carried low — `sprint-status.yaml` 的故事仍是 `in-progress`，規格 frontmatter 是 `in-review`。與上一列同一處。不再改。
- low — `deferred-work.md` 用了絕對路徑。這次工作流程填的就是該路徑，日常在這台機器上對得到。駁回。
- low — 沒有 `UserSecretsId`。規格沒有要求 user secrets；宿主仍會讀 appsettings 與環境變數。補 id 是新設定，不是直接修正。駁回。
- false — 連線字典用 `Ordinal`，大小寫不同的鍵會併存。這一則不做名稱查找；查找忽略大小寫是後面的故事。保留兩個鍵不會把連線弄丟。
- false — `File.Exists` 看不見 `Create`。`ProviderService` 建構時 `EnsureDatabaseExists` 會建立檔案路徑上的資料庫檔。檔案路徑的 `Create` 會讓該斷言失敗。
- false — 六個工具沒有 `connectionId` 等參數。這一則的意圖是不執行、不建立會跑 Session 的處理函式。
- low — 沒測 `ConnectionStrings` 不會進目錄，也沒斷言 `Tools` 與 `Definitions` 是同一物件。程式只讀 `Connections` 區段，且 `Tools = Definitions`。駁回。
- low — 測試解析非物件 JSON、無效 JSON、initialize 的 JSON-RPC 錯誤時會丟例外。產品路徑已通過五個測試。駁回。
- 驗證層第二輪：沒有驗證缺口。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過，覆蓋矩陣三列
- `dotnet build DBServices.sln` -- expected: 成功
