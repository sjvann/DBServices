---
title: '三個入口的檢視與報表'
type: 'feature'
created: '2026-10-20'
status: 'done'
baseline_revision: '338d4aab4b74ca5a6527d3e125ffc2b9c4e84a63'
review_loop_iteration: 0
followup_review_recommended: true
context: []
warnings:
  - multiple-goals
  - oversized
deferred:
  - summary: >-
      多段關聯若同一對來源有兩條以上的鍵，合併可能只套到其中一條。
    evidence: |-
      審查指出 ViewDispatch 的 JoinOrder 併入來源後沒有把剩餘連到該來源的 link 都當成條件。這次沒有逐行再跑兩條鍵的資料，所以尚未證實列會不會多留。
    location: >-
      DbServices.Mcp/ViewDispatch.cs
    severity: "medium (unverified)"
  - summary: >-
      沒有微軟正黑體時，PDF 寫入器可能在型別初始化失敗。
    evidence: |-
      實作註冊本機 msjh.ttc。這台機器的測試能抽出中文。沒有該字型的環境尚未執行匯出。
    location: >-
      Workbench.Adapters/SnapshotPdfWriter.cs
    severity: "medium"

---

<intent-contract>

## Intent

**Problem:** 工作台仍是單一左軌：群組、design/view、確認清單、活資料預覽、常用項目與會啟動的側車。三種人無法各自完成檢視、樣式與語句報表。

**Approach:** 以同一組 Workbench 專案改成三個入口。檢視目錄只經 `McpService` 的 `/views`。語句在核心比對，就緒後恰好呼叫已發布檢視一次。生成紀錄與 PDF 不再呼叫 MCP。

## Boundaries & Constraints

**Always:**
- 登入、入口、樣式、比對、動態報表與生成紀錄只經過 `Workbench.Core`。Blazor 進程內呼叫。Avalonia 只打工作台 HTTP，bearer 與 cookie 都只帶 `sessionId`。
- `Workbench.Web` 與 `Workbench.Avalonia` 不參考 `Workbench.Adapters` 或 `DbServices.Mcp`。Core 不參考 ASP.NET、Avalonia、Sqlite、MCP。
- 工作台 SQLite 只放操作者、入口指派、樣式範本、生成紀錄。檢視文件只在 `DbServices.Mcp`。
- 目錄寫入要求 `X-DBServices-Workbench` 等於兩邊的 `Workbench:CatalogKey`，並拒絕空白 `X-DBServices-Caller`。工具路由不讀 CatalogKey。
- Discovery 的名稱集合等於已發布檢視的 `name`。目錄沒有已發布檢視時，Discovery 是空清單，不回退成 `tables`、`fields`、`get`、`query`、`count`、`exists`。
- 就緒呼叫的工具名稱是檢視 `name`。引數是頂層 `connectionId` 與該檢視的純量參數，沒有 `filters`。恰有一筆 `Available` 連線才送出。
- 零筆或多筆連線：不呼叫，讀列次數為 0，訊息為「無法決定連線，沒有讀取資料。」，不含連線字串。`ErrorDto.Code` 維持空字串，不新增錯誤碼常數。
- 區塊 `type` 只許 `title`、`text`、`parameter`、`table`。PDF 用既有 QuestPDF，預設 A4，換頁以區塊為界。
- 介面與工作台訊息用繁體中文。MCP 的 `code` 與 `message` 原樣。

**Never:**
- 不改 `IDbService`、`IDbServiceAsync`、`FunctionRegistry`、Provider、WebService 與 GraphQL 的路徑和查詢形狀。不建立 `IDbService`，不呼叫 `QuerySession`。
- 不實作表結構瀏覽、不讀待轉資料庫 `GET /meta`、不把 Web API 的列組成動態報表、不做 CSV、不實作稽核保留天數。
- 不啟動 `DbServices.ModelSidecar`，不做模型設定、詞庫編輯、取數軌跡、確認清單、常用項目、群組 design/view、計數區塊。
- 不在來源庫執行 `CREATE VIEW`。不渲染連線清單，不讓人輸入連線字串。
- 六個表級工具的參數契約與 `TableDispatch` 既有 opcode 保持可呼叫的程式，但不進入 Discovery。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 種子管理者 | SQLite 零個操作者，且有 InitialAdmin 名稱與密碼 | 恰好一個 `admin`；`passwordVerifier` 不是明文；再次啟動不重讀 | 缺密碼則不提供入口，代碼 `workbench_unavailable` |
| 未登入 | 無有效 sessionId，要求檢視、樣式、紀錄或列 | 內容不回傳，不呼叫 MCP | `unauthenticated` |
| 入口 | 管理員建立只有名稱與秘密的操作者並指派入口 | 只有一個入口時直接進該站；多個入口整頁換站 | 非管理員或沒有該入口：`forbidden`，讀列次數 0 |
| 第二個管理者 | 畫面上建立管理者或把操作者改成管理者 | 拒絕 | 資料不變 |
| 保存檢視 | 分析師入口；一張來源表，或每段關聯為 foreign-key 或 accepted | `PUT /views/{id}` 寫入目錄，無列、無連線字串 | 連不起來或目錄內名稱衝突：拒絕，既有文件不變 |
| 發布 | 分析師發布 | Discovery 名稱集合等於已發布 `name` | 被樣式 `viewId` 引用時，核心先拒絕刪除、取消發布與結構變更；目的可改 |
| 樣式 | 設計師接受建議並標私有或共享 | 四種區塊，`order` 從 0；`viewId` 是目錄 `id`；無列、無參數值 | 未知欄不能接受；忽略不改既有區塊 |
| 未就緒 | 終端使用者語句對不到、多筆或少參數 | 指出原因；讀列次數 0；不啟動側車 | 不可讀樣式識別：拒絕，讀列次數 0 |
| 就緒 | 恰好一個可讀樣式、參數齊、恰一筆 Available 連線 | 恰好一次 tools/call，名稱為檢視 `name` | 呼叫失敗不產生報表，不改樣式與紀錄；零列是成功 |
| 連線筆數 | 就緒但 Available 連線不是一筆 | 不呼叫 | 空 `Code`，訊息「無法決定連線，沒有讀取資料。」 |
| 生成紀錄 | 存私有或共享；再開 | 含當時區塊、欄、列、純量參數；再開不呼叫 MCP | 非擁有者不能改或刪；私有對其他人不可讀 |
| 存回樣式 | 終端使用者存回版面 | 新樣式擁有者是該人；不呼叫 MCP | 不含列與參數值 |
| PDF 與列印 | 目前這份報表或已開啟的紀錄 | 文字與數字相同；預設 A4；區塊換頁 | 不呼叫 MCP；未保存就離開後不能匯出 |
| 兩殼 | 同一 sessionId，任一殼登出 | 兩邊同時失效 | Avalonia 不開啟 SQLite |

</intent-contract>

## Code Map

- `E:\sjvann\DBServices\Workbench\Program.cs` L35–72 — 目前註冊群組、活預覽、側車、常用項目。改成入口、樣式、比對、報表、紀錄。不要註冊 `SidecarLauncher`。
- `E:\sjvann\DBServices\Workbench\appsettings.json` L2–18 — 已有 `AllowedHosts`、`Workbench:SqlitePath`、`InitialAdmin`、`Surfaces:Mcp:BaseUrl`。沿用 `SqlitePath`（不是 StorePath）。補 `Workbench:CatalogKey`。
- `E:\sjvann\DBServices\Workbench.Storage.Sqlite\SqliteStores.cs` L30–74 — 七表是舊模型。改為 `operators`、`entries`、`styles`、`generation_records`。
- `E:\sjvann\DBServices\Workbench.Core\Ports\Ports.cs` L5–9 — `IPasswordHasher` 留用。其餘端口改成目錄、連線筆數、檢視呼叫、樣式、紀錄、工作階段。
- `E:\sjvann\DBServices\Workbench.Adapters\Pbkdf2PasswordHasher.cs` L7 — 留用。`SnapshotPdfWriter.cs` L13–40 — QuestPDF 留用，輸入改為這次報表的區塊與列。
- `E:\sjvann\DBServices\Workbench.Contracts\Dtos.cs` L3–5 — `ErrorDto.Code` 可為空。舊的群組、軌、確認清單 DTO 不要再給新頁使用。
- `E:\sjvann\DBServices\McpService\Program.cs` L29–81 — Discovery 來自 `ToolCatalog.Definitions` 六個表級工具。改為只註冊已發布檢視。L84–101 — `GET /connections` 與 `MapMcp("/mcp")` 留在原位，旁邊加 `/views`。
- `E:\sjvann\DBServices\DbServices.Mcp\ToolCatalog.cs` L17–25、L42–48 — 六個定義與 `Available` 留在類別內，供除錯路徑與連線筆數。不要把它們放進 `tools/list`。
- `E:\sjvann\DBServices\DbServices.Mcp\TableDispatch.cs` L29–34 — 檢視執行重用 `GetSession().Execute` 的 `query` Call，等值參數進 `Arguments`。不新增 SQL 字串 API。
- `E:\sjvann\DBServices\McpService\Program.cs` L228–232 — `ReadCaller`：空白成為 `anonymous` 且工具仍執行。目錄路由另拒空白呼叫者，不要改這個工具行為。
- `E:\sjvann\DBServices\Workbench.Web\Components\` — 舊頁 `/live`、`/favorites`、`/model`、`/assignments`。改成登入、換站、檢視、樣式、交談、生成紀錄、帳號、入口指派。`Routes.razor` 維持互動轉譯。
- `E:\sjvann\DBServices\Workbench.Avalonia\Program.cs` L9–43 — 基底網址已是 `http://localhost:5099`。`ShellApi` 改打新的 HTTP，不嵌 Blazor。
- `E:\sjvann\DBServices\Workbench.Tests\Host.cs` L17–132 — `WebApplicationFactory<Program>` 與 `LoginAsync` 留用，假資料表面改成記錄檢視呼叫次數與工具名稱。
- `E:\sjvann\DBServices\McpService.Tests\ToolListTests.cs` L21–29 與 `ConnectionListTests.cs` L16–24 — 廣告斷言改成已發布檢視的名稱集合。不要改 `IDbService` 測試。
- `E:\sjvann\DBServices\Directory.Build.props` L8–17 — `Workbench.Storage.Sqlite` 已在 SQLite 3.53.3 釘選內，不要重釘。

## Tasks & Acceptance

**Execution:**
- `E:\sjvann\DBServices\DbServices.Mcp\ViewCatalog.cs` -- 新增檢視文件的讀寫、發布與名稱唯一（不分大小寫）。持久化在 `McpService` 內容根的 `Catalog:Path`，預設 `catalog/views.json`。文件鍵為 `id`、`name`、`purpose`、`published`、`sources`、`links`、`parameters`、`columns`。`links[].kind` 只許 `foreign-key` 或 `accepted` -- 目錄不進工作台 SQLite。
- `E:\sjvann\DBServices\DbServices.Mcp\ViewDispatch.cs` -- 依已發布文件呼叫 `query`。一張來源表直接等值篩選。兩張以上只沿 `links` 合併，缺段就不執行。參數只作為已宣告欄位的等值 -- 不組自由 SQL，不呼叫 `count`。
- `E:\sjvann\DBServices\McpService\Program.cs` -- 註冊 `/views` 的列出、讀取、保存、刪除、發布、取消發布。寫入核對 CatalogKey 與非空白呼叫者。`tools/list` 只含已發布 `name`。呼叫該名稱時走 `ViewDispatch`，並沿用 `CallAudit` 與 `ReadCaller` -- Discovery 等於已發布名稱。
- `E:\sjvann\DBServices\McpService\appsettings.json` -- 補 `Catalog:Path` 與 `Workbench:CatalogKey` -- 與工作台同一鍵值才能寫目錄。
- `E:\sjvann\DBServices\McpService.Tests\ToolListTests.cs` -- `tools/list` 斷言改為無已發布檢視時是空集合、發布後等於該 `name`。原本透過 `tools/call` 打六個表級名稱的案例改為直接呼叫 `TableDispatch`，不再要求這些名稱出現在 Discovery。`ConnectionListTests.cs` 的廣告斷言同樣改成已發布名稱集合，`GET /connections` 仍只斷言 `name` 與 `serverType` -- 六工具廣告不再是驗收，opcode 行為仍有測試。
- `E:\sjvann\DBServices\Workbench.Contracts\Dtos.cs` -- 換成入口、檢視摘要、樣式區塊、語句狀態、動態報表、生成紀錄與既有 `ErrorDto` -- 兩殼共用，不含行為。
- `E:\sjvann\DBServices\Workbench.Core\Ports\Ports.cs` -- 端口改為密碼、操作者與入口、樣式、生成紀錄、工作階段、檢視目錄、連線筆數、檢視呼叫、PDF、時鐘。刪除側車、常用項目、群組、確認清單端口的註冊需求 -- Core 只依賴抽象。
- `E:\sjvann\DBServices\Workbench.Storage.Sqlite\SqliteStores.cs` -- 建立上述四類資料表；樣式存 `viewId` 與區塊 JSON，不含列與參數值；生成紀錄存區塊、欄、列與純量參數 -- 舊的 groups、work_roles、group_roles、favorites、keyword_rules 不再建立。
- `E:\sjvann\DBServices\Workbench.Adapters\ViewCatalogClient.cs` -- 呼叫 `/views`，帶 CatalogKey 與操作者 id -- 不參考 `DbServices.Mcp`。
- `E:\sjvann\DBServices\Workbench.Adapters\McpViewCaller.cs` -- 恰一筆 Available 連線時，以檢視 `name` 呼叫 MCP，引數為頂層 `connectionId` 與純量參數 -- 不送 `filters`，不呼叫六個表級工具。
- `E:\sjvann\DBServices\Workbench.Adapters\SnapshotPdfWriter.cs` -- 依區塊順序畫 A4，區塊邊界換頁 -- 渲染時不呼叫 MCP。
- `E:\sjvann\DBServices\Workbench.Core\UseCases.cs` -- 種子管理員、工作階段、入口、檢視保存與凍結、樣式建議與共享、語句比對、一次呼叫、生成紀錄、存回樣式、匯出 -- 比對只看語句與可讀樣式摘要。
- `E:\sjvann\DBServices\Workbench\Program.cs` -- 唯一組裝處改接新端口。不要註冊 `SidecarLauncher`、`FavoriteService`、`KeywordService`、`ModelSettingsService`、`LivePreviewService` -- 單一進程持有 SQLite 與未保存報表。
- `E:\sjvann\DBServices\Workbench.Web\Controllers.cs` -- HTTP 改為 sessions、me、operators、entries、views、styles、utterance、reports、records、export。每個動作轉進一個用例 -- 控制器不寫第二套判斷。移除 groups、plans、favorites、keyword-rules、model。
- `E:\sjvann\DBServices\Workbench.Web\Components\` -- Blazor 頁改為登入、換站、檢視定義、樣式編輯、交談、生成紀錄、帳號、入口指派。送出語句的 Enter 等於送出。樣式區塊有上移與下移。沒有該入口的路由拒絕 -- 三站不在同一條軌。
- `E:\sjvann\DBServices\Workbench.Avalonia\ShellApi.cs` -- 登入、三站動作、登出都呼叫同一組 HTTP，bearer 只帶 `sessionId` -- 不開啟 SQLite。
- `E:\sjvann\DBServices\Workbench.Tests\ReportWorkbenchTests.cs` -- 以 `WebApplicationFactory` 覆蓋矩陣每一列：未登入、入口、目錄發布與 Discovery、未就緒零次呼叫、就緒恰好一次、空列成功、失敗不寫紀錄、零筆或多筆連線、生成紀錄再開、PDF 不含新的 MCP 呼叫、兩殼同一 session 登出 -- 輸出不含密碼與連線字串。

**Acceptance Criteria:**
- Given 林可持有系統分析師入口並發布一筆檢視, when 讀取 Discovery, then 名稱集合等於當時已發布檢視，且不含六個表級工具名稱。
- Given 陳安的語句尚未就緒, when 他送出, then MCP 讀列次數是 0，畫面指出多筆、缺欄或對不到。
- Given 語句就緒且恰有一筆 Available 連線, when 他送出, then 恰好呼叫該檢視一次，列等於該次結果；零列仍是成功。
- Given 周寧保存的樣式與陳安保存的生成紀錄, when 抽樣這兩份存放, then 樣式沒有業務列與連線字串，紀錄沒有連線字串，再開不呼叫 MCP。
- Given 瀏覽器與 Avalonia 持有同一 sessionId, when 任一殼登出, then 另一殼的下一筆請求被拒絕。

## Spec Change Log

## Review Triage Log

## Design Notes

檢視執行只重用既有 `query` Call。兩表以上在 `ViewDispatch` 依 `links` 合併列，不把 SQL 交給工作台。

```csharp
session.Execute(new QueryRequest {
    Text = "query",
    Opcode = QueryOpcode.Call,
    Arguments = new Dictionary<string, object?> { ["table"] = source, [parameter] = value }
});
```

工作台錯誤沿用 `ErrorDto`。零筆或多筆連線時 `Code` 留空，只填繁體中文訊息。

## Verification

**Commands:**
- `dotnet test E:\sjvann\DBServices\Workbench.Tests\Workbench.Tests.csproj` -- expected: 矩陣與五條驗收通過，輸出不含密碼與連線字串
- `dotnet test E:\sjvann\DBServices\McpService.Tests\McpService.Tests.csproj` -- expected: Discovery 等於已發布檢視；`GET /connections` 仍只有 name 與 serverType

## Review Triage Log

### 2026-10-20 — Review pass
- verdicts: 18 findings — high 5, medium 6, low 1, false 4, maybe-false 2
- findings:
  - `[high]` `[patch]` 損壞的目錄 JSON 會被當成空清單並在下次保存覆寫 — 已改為讀取失敗不快取空清單，保存不會替換原檔。
  - `[medium]` `[patch]` 讀取 `/views` 不核對目錄金鑰與呼叫者 — GET 已改走與寫入相同的拒絕。
  - `[false]` `[reject]` 持有目錄金鑰可繞過樣式凍結 — 規格要求凍結在核心先做，McpService 不開啟工作台 SQLite，因此不能在目錄進程重做樣式引用檢查。
  - `[high]` `[patch]` 已發布檢視少帶參數仍會查出整表 — 缺參數或非純量改回既有的 invalid_arguments，不送 query。多來源的空白來源同樣失敗。
  - `[false]` `[reject]` 檢視名稱不得與六個表級工具相同 — 規格要的是 Discovery 等於已發布名稱，沒有另禁這些字。
  - `[medium]` `[patch]` PDF 表頭與儲存格依查詢順序錯位 — 匯出改依欄名放儲存格。
  - `[medium]` `[defer]` 沒有微軟正黑體時 PDF 初始化可能失敗 — 這台機器測試通過；沒有該字型的環境未跑，記在 deferred。
  - `[high]` `[patch]` 檢視表單再存一次會取消發布並清掉關聯 — 識別已存在時先讀回文件，保留 Published 與 Links。
  - `[high]` `[patch]` 入口勾選一開始是空的，保存會清空 — 載入該操作者目前的入口，換人時更新。
  - `[low]` `[reject]` 未就緒後畫面清掉表格但仍可能匯出上一份成功報表 — 未就緒與失敗不會產生新報表；上一份仍在，直到離開。交談頁改為未就緒時保留畫面上的上一份，並畫出標題、文字與參數區塊。
  - `[false]` `[reject]` 短樣式名稱會讓語句同時命中多筆 — 規格就是語句含樣式名稱才入候選，多筆時讀列次數為 0。
  - `[false]` `[reject]` 連線端點失敗與零筆連線共用同一句訊息 — 規格對不是恰好一筆可用連線的情況就用這句，不另造錯誤碼。
  - `[high]` `[patch]` 單表呼叫測試只有一列，看不出篩選 — 已再插入不該出現的列，並斷言只回傳符合的列。
  - `[medium]` `[patch]` 多來源合併沒有測試會執行 — 已加兩表外部鍵的 tools/call，斷言連結列而不是笛卡兒積。
  - `[medium]` `[patch]` 真實目錄沒有測到重複名稱、空白名稱與未連線 — 這些 PUT 現在斷言 HTTP 400，且原文件不變。
  - `[medium]` `[patch]` 就緒呼叫沒有核對 connectionId — RecordingCaller 記下名稱，就緒用例斷言等於那一筆可用連線。
  - `[maybe-false]` `[defer]` 同一對來源的第二條關聯鍵可能沒有參加合併 — 尚未用兩條鍵的資料證實，記在 deferred。
  - `[low]` `[reject]` 意圖只說落地，測試打的是 HTTP 而不是瀏覽器點擊 — 規格的驗收在 Discovery、讀列次數、存放與兩殼登出；Blazor 與 Avalonia 有改檔，這次沒有打開視窗。

## Auto Run Result

Status: done

Summary: 工作台改成系統分析師、報表設計師、終端使用者三個入口。檢視目錄在 McpService 的 `/views`。Discovery 只列出已發布檢視名稱。語句在核心比對，就緒後恰好呼叫該檢視一次。生成紀錄與 PDF 不再呼叫 MCP。

Files changed:
- `DbServices.Mcp/ViewCatalog.cs` — 檢視文件的保存、發布與名稱唯一
- `DbServices.Mcp/ViewDispatch.cs` — 依檢視定義呼叫既有 query，多表只沿關聯合併
- `McpService/Program.cs` — `/views` 與只含已發布名稱的 Discovery
- `Workbench.Core/UseCases.cs` — 登入、入口、樣式、語句、紀錄與匯出
- `Workbench.Storage.Sqlite/SqliteStores.cs` — 操作者、入口、樣式、生成紀錄
- `Workbench.Adapters/ViewCatalogClient.cs` 與 `McpViewCaller.cs` — 目錄與一次檢視呼叫
- `Workbench.Web/Components/` — 三站、帳號與入口指派
- `Workbench.Avalonia/ShellApi.cs` — 同一組 HTTP 與 sessionId
- `Workbench.Tests/ReportWorkbenchTests.cs` 與 `McpService.Tests/ViewDiscoveryTests.cs` — 矩陣與 Discovery

Review findings breakdown:
- Patches applied: 5 high、5 medium。目錄讀取失敗不再覆寫；讀取也要金鑰；缺參數不查表；PDF 依欄名；表單保留發布與關聯；入口勾選載入現況；測試補上篩選、兩表、目錄拒絕與 connectionId。
- Deferred: 第二條關聯鍵是否生效（未證實）；沒有正黑體時的 PDF。
- Rejected: 目錄進程不能重做樣式凍結；檢視名稱不必避開六個表級工具的字；短名稱命中多筆是規格；連線失敗與零筆共用同一句；未就緒不取代上一份已產生的報表。

Follow-up review recommendation: true。這次修了高嚴重度的目錄覆寫、缺參數查整表，以及 PDF 錯欄。尚未在瀏覽器點過三站，也沒有打開 Avalonia 視窗。

Verification:
- `dotnet test Workbench.Tests\Workbench.Tests.csproj`：16 通過，0 失敗。
- `dotnet test McpService.Tests\McpService.Tests.csproj`：54 通過，0 失敗。
- 測試輸出沒有密碼或連線字串。

Residual risks: 瀏覽器與桌面視窗沒有實際點過。PDF 依賴本機微軟正黑體。多段關聯的第二條鍵尚未用資料證實。

