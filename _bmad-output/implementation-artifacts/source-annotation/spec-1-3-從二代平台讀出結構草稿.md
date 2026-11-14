---
title: 'Story 1.3 從二代平台讀出結構草稿'
type: 'feature'
created: '2026-11-14'
status: 'done'
baseline_commit: '84fea1bdae0febde8f44d123f237a6037cf9d8c7'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 沒有 Excel 時，持有資料治理的人無法從二代平台做出草稿，也不該自己挑連線或讀到業務列。

**Approach:** 沒有工作中草稿時，只在恰有一筆組態完整、且名稱等於 `Annotation:PlatformConnection` 的連線上，經 `SchemaGraph.Read` 做成唯一草稿，並在註記站看見表、欄、主鍵與外鍵。

## Boundaries & Constraints

**Always:**
- 判定在 `McpService`。組態完整沿用既有規則：`ServerType` 是 `Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL` 之一，且 `ConnectionString` 非空白。名稱比對不區分大小寫。零筆、多於一筆，或那一筆名稱不等，都不呼叫 `SchemaGraph.Read`。路由忽略請求裡的 `connectionId`。
- 讀到的表、欄、型別與主鍵，和同一次 `SchemaGraph.Read` 的結果一致，含它帶回的檢視。表註、欄註、個資留空。欄註空白處顯示「尚未註記」，用既有 `surface-sunken` 與 `caution`，不新增色票。主鍵為真時該格寫「主鍵」，否則空白。不把主鍵寫進 `UniqueIndex`，也不替結構草稿發明 `not null`。
- 外鍵預填成關聯。`origin` 是 `foreign-key`，理由空白。兩端必須都在這次讀到的表與欄裡，否則這條不寫入。畫面只讀列出兩端，不提供新增或刪除。
- 零筆是 `no_connection`：「沒有讀取結構。」多於一筆是 `connection_unselectable`：「註記站不能選擇連線，沒有讀取結構。」不接連線名稱。名稱不等是 `not_platform_source`：「不能用{連線名稱}讀結構或確認。」已有草稿是 `draft_exists`：「已有工作中的草稿，沒有改。」讀取失敗沿用 `connection_failed`「無法建立連線。」或 `execution_failed`「讀取資料表結構失敗。」都是 `status-message`，訊息在上、代碼在下，`role="alert"`。
- 包名是對上的那筆連線在組態裡的名稱，保留原本大小寫。名稱含 `\ / : * ? " < > |` 時仍照名稱顯示，檔名限制留到匯出。沒有表仍建立草稿，表清單是空的，這不是錯誤。`purpose`、`dbml` 留空，`status` 是 `draft`，`active` 是 false。進行中只寫「正在讀取結構。」，字色 ink-secondary，匯入與讀結構停用。業務列讀取次數為 0。草稿與畫面都沒有連線字串。

**Never:**
- 不編輯保存、不匯出、不確認、不放棄、不再讀合併。那些是後面的故事。
- 不呼叫 `SchemaGraph.Preview`、`GET /schema/graph`、待轉庫的 `GET /meta`。不對來源庫做新增、更新、刪除，也不把註解寫回。
- 不新增套件、色票或錯誤碼。不改 `SchemaGraph.Read`、Excel 匯入、Avalonia、`/environment`、`IDbService`、既有 REST 與 GraphQL。工作台進程不載入連線字串。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 讀結構 | 沒有草稿；恰一筆完整連線，名稱等於平台鍵 | 一份草稿。表、欄、型別、主鍵與 `SchemaGraph.Read` 一致。外鍵成為關聯，理由空白。package-bar 是該連線名稱與「草稿」 | 不建立第二份。讀列次數 0 |
| 沒有表 | 同上，但讀回的表清單是空的 | 仍建立草稿，表清單空 | 不當成失敗 |
| 零筆連線 | 完整連線是零筆 | 不呼叫 `Read`，沒有草稿 | `no_connection` 與上面那句 |
| 多筆連線 | 完整連線多於一筆 | 不呼叫 `Read` | `connection_unselectable` 與上面那句，不接名稱 |
| 待轉庫 | 恰有一筆，名稱不等 | 不呼叫 `Read`，不用該庫的 `GET /meta` | `not_platform_source`，句中是該連線名稱 |
| 讀取失敗 | 名稱對上，但 `Read` 失敗 | 不產生草稿 | 沿用 `connection_failed` 或 `execution_failed` 的既有句 |
| 已有草稿 | 再按讀結構 | 草稿位元組不變 | `draft_exists` 與上面那句 |
| 未持有 | 沒有資料治理入口仍呼叫 | 不寫檔 | 沿用「沒有權限。」與 `forbidden` |

</frozen-after-approval>

## Code Map

- `DbServices.Mcp/SchemaGraph.cs` — `Read` 約第 12 行。回表名、欄名、型別、`PrimaryKey`、外鍵兩端。失敗句在第 20 與 63 行。不要改這個方法，也不要改 `Preview`。
- `DbServices.Mcp/ToolCatalog.cs` — `Available` 約第 44 行。完整連線只看這裡。`ConnectionSetting` 只有 `ServerType` 與 `ConnectionString`。
- `DbServices.Mcp/AnnotationCatalog.cs` — 唯一寫入者。已有 `draft` 就回 `draft_exists` 且檔不變。讀結構的合併加在這個型別，不要另開一份目錄檔。
- `McpService/Program.cs` — `GET`／`PUT /annotations/draft` 約第 253 行，門檻與 `/views` 相同。這裡加 `POST /annotations/read-structure`。`Annotation:PlatformConnection` 用 `IConfiguration` 讀。忽略 body 裡的 `connectionId`。
- `Workbench.Contracts/Dtos.cs` — `AnnotationColumnDto` 約第 206 行沒有主鍵旗標。在欄上加 `PrimaryKey`，不要借用 `UniqueIndex`。`AnnotationLinkDto` 已有兩端與 `Origin`。
- `Workbench.Core/Ports/Ports.cs` — `IAnnotationCatalog` 約第 110 行。讀結構的方法加在這裡。
- `Workbench.Adapters/AnnotationCatalogClient.cs` — 比照現有草稿呼叫，加上讀結構。404 仍是失敗，只有既有的 204 代表沒有草稿。
- `Workbench.Core/AnnotationService.cs` 與 `Workbench.Web/Controllers/Controllers.cs` — 權限沿用 `EntryNames.Governance`。工作台 API 不接收連線名稱。
- `Workbench.Web/Components/Governance.razor` — 故事 1.2 的匯入站。加次要按鈕「讀結構」。進行中那一行用既有 ink-secondary。冷開啟不要先放確認、匯出或放棄。
- `Workbench.Tests/Host.cs` — `RecordingAnnotations`、`RecordingCaller`、`RecordingConnections`。樣品對照放會真的呼叫 `SchemaGraph.Read` 的測試；零筆、多筆、待轉庫、已有草稿與權限走宿主。
- 不要改 `DictionaryWorkbook`、`Environment.razor`、Avalonia、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `DbServices.Mcp/AnnotationCatalog.cs` — 無草稿才把 `SchemaGraph.Read` 的結果寫成草稿；空表清單仍寫入；已有草稿回 `draft_exists` 且檔不變 — 唯一寫入者
- [x] `McpService/Program.cs` — `POST /annotations/read-structure`。零筆、多筆、名稱不等都不呼叫 `Read` — 判定留在服務裡
- [x] `Workbench.Contracts/Dtos.cs`、`Ports.cs`、`AnnotationCatalogClient.cs`、`AnnotationService.cs`、`Controllers.cs` — 埠與客戶端。欄上有 `PrimaryKey` — 網頁不碰連線字串
- [x] `Governance.razor` 與 `tokens.css` — 「讀結構」、進行中那一行、主鍵、「尚未註記」、只讀關聯 — 不新色票，不加確認
- [x] `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Structure_draft`：與 `Read` 一致、空表、零筆、多筆、待轉庫、讀取失敗、已有草稿、未持有、讀列次數為 0 — 鎖住矩陣

**Acceptance Criteria:**
- Given 只持有資料治理並讀結構，when 計算業務列讀取，then 次數為 0，畫面與草稿都沒有連線字串。
- Given 這一輪，when 檢查 `SchemaGraph.Read`、Excel 匯入、Avalonia 與 `/environment`，then 沒有為讀結構改動。

## Implementation Notes

## Spec Change Log

## Review Triage Log

| # | Finding | Verdict | Evidence | Route |
|---|---------|---------|----------|-------|
| 1 | 已有草稿時先回連線錯誤 | medium | 路由先數連線。現在先看草稿，有草稿就回 `draft_exists`，不呼叫 `Read` | patch |
| 2 | 讀結構佔住目錄鎖，中止後仍可能寫檔 | medium | `Read` 改在鎖外。已中止則回「沒有保存。」且不寫檔 | patch |
| 3 | 不存在的 Sqlite 檔會被建出來 | medium | 這是既有 `SchemaGraph.Read` 的行為，這一則不准改它 | defer |
| 4 | 同名表的外鍵對到後一份欄位 | low | 提供者回傳的表名不會重複。拒絕句也不在允許的代碼裡 | reject |
| 5 | 讀結構沒接 `InvalidOperationException`，兩次操作會互清忙碌 | medium | 讀結構已接住，並用序號忽略較早的結果 | patch |
| 6 | 關聯清單沒有標示，進行中沒有 `aria-live` | low | 兩端名稱已列出。日常使用看得到，不另加屬性 | reject |
| 7 | 新路由沒測呼叫者與金鑰；已確認包沒被鎖住 | medium | 已補 400、403，以及已確認包旁追加未啟用草稿 | patch |
| 8 | 判定後連線目錄被換掉仍會讀 | low | 組態在同一次請求裡不會被換成另一份目錄 | reject |
| 9 | 註記陣列含 null，或清單是 null | low | 自己寫出的檔不會是 null | reject |
| 10 | Excel 匯入被改過 | false | 匯入仍走原來的剖析與寫入。多的旗標只決定進行中那一句是哪句 | reject |
| 11 | 業務列次數只看 JSON 沒有列標記 | low | 這條路徑不呼叫 `Preview`，也不查列。要計次就得改提供者 | reject |

## Design Notes

外鍵的 `origin` 用 `foreign-key`，因為它來自 `SchemaGraph.Read`。畫面上它仍是已聲明關聯，不顯示這個代碼。人稍後補上、庫裡沒有的關聯才用 `declared`，那是下一則。指向這次沒讀到的表或欄的外鍵不寫入，避免關聯的一端不在包裡。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Structure_draft` -- expected: 通過
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Structure_draft` -- expected: 通過
