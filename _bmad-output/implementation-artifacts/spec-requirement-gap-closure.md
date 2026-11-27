---
title: '補上審查指出的契約落差'
type: 'bugfix'
created: '2026-11-11'
status: 'done'
baseline_revision: '929b24dae9cf39fb313732d3f92348341d0d0690'
review_loop_iteration: 0
followup_review_recommended: true
context: []
warnings:
  - multiple-goals
  - oversized
deferred:
  - summary: >-
      檢視表單沒有解說輸入。空白目的會改填檢視名稱後通過非空白檢查。
    evidence: |-
      Views.razor 的 _purpose 沒有對應輸入欄。Draft() 在目的為空白時改填檢視名稱。這是原本就存在的表單，契約允許任何非空白目的。
    location: >-
      Workbench.Web/Components/Views.razor
    severity: low
  - summary: >-
      已發布檢視少了已宣告參數時，訊息仍是「未提供資料表名稱。」
    evidence: |-
      Program.cs 的 ParameterMissing 沿用 TableDispatchResult.InvalidArguments()。契約的「參數無法接受。」只規範秘密鍵與未宣告鍵，沒有改這句既有文案。
    location: >-
      McpService/Program.cs
    severity: medium
---

<intent-contract>

## Intent

**Problem:** 公開 MCP 呼叫把未知名稱與已關閉工具都說成查詢失敗，Discovery 說明只有目的，空解說也能發布，多鍵合併可能漏掉第二條鍵，結構預覽會讀出業務列，PDF 在沒有微軟正黑體時於型別初始化失敗，模型探測的逾時與重新導向沒有被鎖住。

**Approach:** 依已經寫明的契約補上這些可觀察行為。不新造除錯開關、CSV、稽核保留天數，也不在目錄進程重做樣式凍結。

## Boundaries & Constraints

**Always:**
- 公開 `tools/call` 的名稱是 `insert`、`update`、`delete`、`sql`（不區分大小寫）時，回 `tool_disabled`，訊息「這個工具已關閉。」，不執行查詢。
- 其他不在已發布清單的名稱回 `unknown_tool`，訊息「沒有這個工具。」。表級名稱 `tables`、`fields`、`get`、`query`、`count`、`exists` 走這條，不進入 `TableDispatch`。
- 上述拒絕與連線、參數拒絕都寫稽核。稽核含 `timestamp`、`caller`、`connectionId`、`tool`、`arguments`、`rowCount`、`value`、`error`。不得寫入連線字串、`connectionString`、`connectString` 的值，也不得寫入列內容。
- 空白呼叫者仍記 `anonymous`，呼叫照做。
- `connectionId` 去掉頭尾空白後是空的：恰有一筆 `Available` 連線就用它，否則 `connection_required`，訊息「請指定連線。」。有文字但目錄沒有該名稱：`unknown_connection`，訊息「找不到連線。」。名稱在但連線字串是空白：`connection_failed`，訊息「無法建立連線。」，回傳沒有 `rows`。不得丟出未處理的 `KeyNotFoundException`。
- 頂層鍵名為 `connectionString` 或 `connectString`（不區分大小寫），或不是 `connectionId` 與該檢視已宣告參數名：`invalid_arguments`，訊息「參數無法接受。」，不回顯該值。
- 發布要求目的去掉空白後非空，且 `confirmed` 為真。否則目錄不改發布狀態，回應碼 `unconfirmed`，訊息「解說尚未確認。」。`confirmed` 被存成假時，`published` 必須為假。目的文字與已存文件不同時，這次保存把兩者都清成假。
- Discovery 的 `description` 含目的、來源表、每條關聯的兩端與欄位、輸出欄名稱、參數名稱。不寫接受 SQL，不要求傳入表名。
- 同一對來源有兩條以上關聯時，合併列必須同時滿足每一條鍵。只符合其中一條的列不出現。
- `POST /schema/preview` 不呼叫 `ViewDispatch`，不讀業務列。回應 `preview_unavailable`，訊息「不讀取業務列。」，沒有 `rows`。`GET /schema/graph` 仍只回表、欄與外鍵。
- 沒有 `msjh.ttc` 時，PDF 寫入器的型別初始化不丟例外，仍能產出 PDF 位元組。
- 模型探測用的 `HttpClient` 逾時是 8 秒，且不跟隨重新導向。探測失敗或位址字串變更時，畫面上的模型名稱清單清空。測試進行中，測試與儲存按鈕維持不可按。

**Never:**
- 不新增會把表級工具放進 Discovery 的除錯開關。
- 不實作 CSV、稽核保留天數、拒絕 `anonymous`。
- 不讓 `McpService` 讀工作台 SQLite 來重做樣式凍結。
- 不改工作台「零筆或多筆連線」既有的空錯誤碼與「無法決定連線，沒有讀取資料。」。
- 不改 `IDbService` 簽名、REST 與 GraphQL 路徑。不在來源庫執行 `CREATE VIEW`。
- 沒有設定 `DBSERVICES_TEST_SQLSERVER`、`DBSERVICES_TEST_MYSQL`、`DBSERVICES_TEST_ORACLE`、`DBSERVICES_TEST_POSTGRESQL` 時，不把對應引擎判失敗，也不宣稱該引擎已跑過。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 關閉工具 | `tools/call` 名稱 `insert` | `tool_disabled`，資料不變，稽核有該碼且無連線字串 | 不執行寫入 |
| 未知工具 | `tools/call` 名稱 `tables` 或任意未發布名稱 | `unknown_tool`，稽核有該碼 | 不查表 |
| 省略連線 | 已發布檢視，沒有 `connectionId`，恰一筆可用連線 | 用該連線呼叫 | 零筆或多筆回 `connection_required` |
| 空白連線字串 | 已發布檢視，該連線字串為空白 | `connection_failed`，沒有 `rows` | 不看起來像空庫 |
| 秘密參數 | 頂層帶 `connectionString` | `invalid_arguments`，訊息無該值 | 稽核不寫該值 |
| 未確認發布 | 目的空白或 `confirmed` 為假 | 發布被拒，Discovery 不變 | `unconfirmed` |
| 已確認發布 | 目的有文字且 `confirmed` 為真 | Discovery 只有該名稱，說明含來源與輸出欄 | 取消確認後清單不再有它 |
| 第二條鍵 | 同兩表兩條關聯，一列只中第一條 | 該列不在結果 | 兩條都中才留下 |
| 結構預覽 | 分析師對已選來源按預覽 | 不讀列，畫面沒有業務列 | `preview_unavailable` |
| 缺字型 | 字型檔不存在 | PDF 位元組仍產出，型別初始化不丟例外 | 不要求該機先安裝正黑體 |
| 探測 | 位址回 302，或探測失敗，或改位址 | 不跟隨到下一個網址；清單清空；逾時 8 秒 | 按鈕在探測中不可按 |

</intent-contract>

## Code Map

- `McpService/Program.cs` L40–81 `WithCallToolHandler`：找不到已發布名稱就 `QueryExecutionFailed()`，且在 `Finish` 之前返回，所以這條沒有稽核。L58 空 `connectionId` 直接往下傳。L257–285 `ToTool` 的說明只有 `Purpose`，`required` 只有 `connectionId`。L318–333 `ReadParameters` 不拒絕秘密鍵與未宣告鍵。L375–401 `Finish` 是既有稽核入口，拒絕路徑要改走它。
- `DbServices.Mcp/TableDispatch.cs` L703–750 `TableDispatchResult`：既有 `Error`、`ConnectionFailed`、`InvalidArguments`。尚無 `unknown_tool`、`tool_disabled`、`connection_required`、`unknown_connection`。新碼沿用同一個 `Error`。
- `DbServices.Mcp/ViewCatalog.cs` L9–18 `ViewDocument` 無 `Confirmed`。L148–171 `SetPublished` 與 L174–206 `Check` 只看名稱與連通。
- `DbServices.Mcp/ViewDispatch.cs` L45–71 每一步只用 `step.Link` 一條鍵。L90–122 `JoinOrder` 用掉一條後就把同對的其他 link 留在刪除清單外，不再當條件。
- `DbServices.Mcp/CallAudit.cs` L57–82、L194–196：欄位與秘密鍵名已可沿用。
- `DbServices.Mcp/SchemaGraph.cs` L72–96 `Preview` 呼叫 `ViewDispatch.Execute` 並回傳列。`GET /schema/graph` 在 `McpService/Program.cs` L186–193，只回 `tables`。
- `Workbench.Contracts/Dtos.cs` L71–80 `ViewDocumentDto` 無 `Confirmed`。`Workbench.Web/Components/Views.razor` L120 發布、L820–854 `RunPreview` 把 `Rows` 畫出來。
- `Workbench.Adapters/SnapshotPdfWriter.cs` L12–25 `ResolveFont`：沒有正黑體就 `InvalidOperationException`。
- `Workbench.Adapters/ModelEndpoint.cs` L11 `Limit` 8 秒。L211–215 `Open` 設 `Timeout`，未設 `AllowAutoRedirect`。L161–164 `OpenPull` 是下載用的無限逾時，不要改成 8 秒。
- `Workbench.Web/Components/Environment.razor` L306–307 探測中已停用測試與儲存。L614–649 `ProbeModel` 失敗時不清 `_names`。
- `McpService.Tests/ViewDiscoveryTests.cs`：檢視呼叫只用 Sqlite。`McpService.Tests/ToolListTests.cs` 已有 `DBSERVICES_TEST_*` 的可選真庫模式，視圖測試沿用同一組變數。
- `Workbench.Tests/ModelEndpointTests.cs` 只斷言 `ModelEndpoint.Limit` 常數。

## Tasks & Acceptance

**Execution:**
- `DbServices.Mcp/TableDispatch.cs` -- 加上四個結果工廠，訊息用本規格的中文 -- 公開呼叫與連線拒絕要有穩定碼。
- `DbServices.Mcp/ViewCatalog.cs` -- `Confirmed` 與發布門檻、目的變更時清掉確認與發布 -- Story 2.2 的可觀察結果。
- `McpService/Program.cs` -- 關閉與未知名稱、參數拒絕、連線決策都經 `Finish` 回傳；`ToTool` 寫長說明並拿掉 `connectionId` 必填；`/schema/preview` 不查列 -- 這是 Agent 與預覽看得到的表面。
- `DbServices.Mcp/ViewDispatch.cs` -- 同對來源的每一條 link 都是合併條件 -- 避免只中一條鍵的列漏進結果。
- `DbServices.Mcp/SchemaGraph.cs` -- `Preview` 不呼叫 `ViewDispatch` -- 預覽端點即使用別的宿主呼叫也不能讀列。
- `Workbench.Contracts/Dtos.cs` 與 `Workbench.Web/Components/Views.razor` -- 文件帶 `Confirmed`；畫面有「解說已確認」；預覽失敗時不畫列，註記用伺服器訊息 -- 分析師看得到確認與「不讀取業務列」。
- `Workbench.Adapters/SnapshotPdfWriter.cs` -- 字型檔不在時改用庫內建家族，靜態初始化不丟例外 -- PDF 在沒有正黑體的環境仍能產出。
- `Workbench.Adapters/ModelEndpoint.cs` 與 `Workbench.Web/Components/Environment.razor` -- 探測客戶端不跟隨重新導向且逾時 8 秒；失敗或位址變更清空 `_names` -- 下載客戶端維持無限逾時。
- `McpService.Tests/ViewDiscoveryTests.cs` -- 矩陣列與第二條鍵、發布門檻、說明文字；有環境變數的引擎才比對同一組參數鍵 -- 鎖住公開表面。
- `Workbench.Tests/ModelEndpointTests.cs` 與現有工作台測試 -- 預覽不讀列、缺字型不丟例外、探測客戶端逾時與不跟隨重新導向、失敗或改位址後清單清空、探測中測試與儲存不可按 -- 鎖住工作台表面。
- `docs/mcp/index.md` -- 錯誤碼表補上這次公開碼，並寫明說明文字含來源與輸出欄 -- 對外文件與呼叫結果一致。

**Acceptance Criteria:**
- Given 沒有已發布檢視, when 呼叫 `insert` 與 `tables`, then 前者是 `tool_disabled`、後者是 `unknown_tool`，稽核各有對應 `error`，且檔案內沒有連線字串。
- Given 目的有文字且已確認的一筆檢視, when 列出工具, then 名稱只有它，說明含來源表與輸出欄，且不含「SQL」。
- Given 同一對來源兩條鍵, when 呼叫該檢視, then 只回傳兩條鍵都相符的列。

## Spec Change Log

## Review Triage Log

### 2026-11-11 — Review pass
- verdicts: 34 findings — high 0, medium 20, low 4, false 10, maybe-false 0
- findings:
  - `[low]` `[defer]` 檢視畫面沒有解說輸入，空白目的會改填檢視名稱 — 表單原本就沒有綁定 `_purpose` 的欄位；`Draft()` 以名稱補上非空白目的，符合契約「目的去掉空白後非空」。這次只加了確認勾選，沒有拿掉解說欄。
  - `[false]` `[reject]` 未確認仍可按發布；結構變更不清 `confirmed` — `SetPublished` 在未確認或目的空白時回 `unconfirmed`。契約只在目的文字不同時把確認與發布清成假。
  - `[medium]` `[patch]` 舊檔沒有 `confirmed` 時，`published: true` 仍留在 Discovery — `Published()` 與 `FindPublishedByName()` 只看 `Published`。缺欄位的布林是假，已發布的舊檔仍會被列出、被呼叫。新建時空白目的只把 `published` 壓成假、`confirmed` 可留真，這部分符合契約。已修：兩處都加上 `Confirmed`，並測試 `tools/list` 沒有該檢視。
  - `[false]` `[reject]` 已發布檢視若名叫 `tables` 會進 `ViewDispatch`；`insert ` 不會命中關閉清單 — 矩陣把 `tables` 當成未發布名稱的例子。已發布名稱走檢視。`IsClosedTool` 只含 `insert`、`update`、`delete`、`sql`，尾端空白不是這四個名稱。
  - `[medium]` `[defer]` 少了已宣告參數時訊息仍是「未提供資料表名稱。」 — `ParameterMissing` 沿用既有 `InvalidArguments()`。契約的「參數無法接受。」只規範秘密鍵與未宣告鍵。
  - `[false]` `[reject]` 第二條鍵把缺值收成空字串；沒有三表測試；多引擎缺變數就略過 — `ToMaps` 與第一條鍵本來就把缺值存成空字串再做相等。`KeepMatching` 對每一條剩餘 link 都比較。契約寫明沒有測試用環境變數時不把該引擎判失敗。
  - `[low]` `[patch]` 預覽區仍寫會出現資料、正在取資料 — `SchemaGraph.Preview` 固定失敗，`preview.Ok` 的 `rows` 分支到不了。JSON 解析失敗仍是既有的「請先選擇資料表。」。畫面上「下方會出現資料。」「勾選欄位後，這裡會出現資料。」「正在取資料。」在預覽不再讀列之後會說錯。已修：三句改成不承諾會出現業務列。
  - `[medium]` `[patch]` 字型檔在、家族名對不上正黑時回 `Lato` 卻不關缺字例外 — `ResolveFontFamily` 缺檔與例外分支有關旗標，名稱對不上的那次直接回 `BuiltInFamily`。中文寫入仍可能丟例外。旗標保持關閉才是契約要的「沒有正黑體仍能產出」；測試只開頁數，沒有讓 `Write` 用回退家族。已修：凡回傳 Lato 都先關閉兩個缺字設定。
  - `[medium]` `[patch]` 探測完成或本機預設位址會把模型清單寫回 — `ProbeModel` 與 `RefreshInstalled` 在 `await` 之後無條件指定 `_names`。`ApplyLocalAddress` 改空位址時不經 `EndpointChanged`，也不清清單。契約只要探測進行中按鈕不可按，沒有要求測完再斷言恢復。已修：回來時位址不同就不寫名稱；預設位址有變更才清空。暫停中改位址的測試已加上。
  - `[low]` `[reject]` 注入的 `ToolCatalog` 在 `Save` 之後是舊實例 — 公開 `tools/call` 讀的是 `directory.Catalog`。全庫沒有另一個生產路徑注入這個單例。改成即時轉接要加一層型別，不是直接修正。
  - `[false]` `[reject]` 未知連線把請求的 `connectionId` 寫進稽核 — 契約要求稽核含 `connectionId`。寫入的是名稱，不是連線字串。測試沒有多斷言 `value` 為 null，沒有因此寫出列內容。
  - `[false]` `[reject]` 錯誤表把目錄與預覽的碼和 `tools/call` 放在一起；驗收條目較短；diff 裡狀態是 `in-progress` — 表的 when 欄已分開端點。行為在 Always 與矩陣。磁碟上的狀態是 `in-review`。
  - `[medium]` `[patch]` `SnapshotPdfWriter.cs:31-33` 家族名對不上時中文 PDF 可能丟例外 — 與上一則字型回退是同一處：回 `Lato` 時沒關 `ThrowOnMissingTextGlyphs` 與 `ThrowOnMissingFontFamilies`。已修：`UseBuiltInFamily` 先關閉兩個設定。
  - `[medium]` `[patch]` `ViewCatalog.cs` 舊檔未確認仍留在 Discovery — 與舊檔那一則同一處：列出與依名稱尋找已發布檢視時沒有要求 `Confirmed`。已修：兩處都要求 `Confirmed`。
  - `[medium]` `[patch]` `Environment.razor` 探測成功仍寫回上一個位址的名稱 — `ProbeModel` 成功路徑沒有比對送出時的位址。已修：回來時位址不同就不寫 `_names`。
  - `[medium]` `[patch]` `RefreshInstalled` 在位址變更後仍寫回名稱 — 重新整理探測同樣在完成時指定 `_names`。已修：同樣比對送出時的位址。
  - `[medium]` `[patch]` `ApplyLocalAddress` 填入預設位址時留下舊名稱 — 只在位址為空白時寫入預設值，沒有清空 `_names`。已修：位址有變更才清空清單。
  - `[low]` `[patch]` 解析連線與執行查詢各讀一次 `directory.Catalog` — 兩次讀取之間沒有 `await`，但 `Save` 會換成新實例。新目錄沒有該連線時，`ViewDispatch` 會丟出契約禁止的 `KeyNotFoundException`。已修：解析與執行共用同一個 `catalog` 區域變數。
  - `[medium]` `[patch]` 失敗或改位址應清空清單，成功探測仍可能寫回 — 與探測寫回是同一組：完成時指定 `_names`，`ApplyLocalAddress` 改位址也不清空。已修：與上面三處同一組修正。
  - `[medium]` `[patch]` 新建的空白目的或未確認文件，測試刪掉守衛仍會通過 — 現有發布測試先寫入另一段目的，空白結果來自 `PurposeChanged`。要另建兩筆新文件鎖住 `published` 被壓成假，且未確認的不出現在 `tools/list`。已修：兩筆新建文件的測試已加入。
  - `[medium]` `[patch]` 未宣告頂層鍵沒有被呼叫測試鎖住 — 秘密參數測試在 `IsSecret` 就返回。刪掉未宣告鍵判斷後，帶 `filters` 的檢視呼叫仍會執行。已修：已補帶 `filters` 的呼叫。
  - `[medium]` `[patch]` `update`、`delete`、`sql` 沒有被鎖成 `tool_disabled` — 目前只有 `insert` 走進公開 handler 的關閉判斷。拿掉另外三個名稱，現有測試仍通過。已修：三個名稱都有呼叫與稽核斷言。
  - `[medium]` `[patch]` 連線拒絕沒有被斷言寫進稽核 — `Connection_decision_follows_available_names` 只看 RPC。把 `Finish` 改回直接回傳後，錯誤碼測試仍過，稽核沒有這三個碼。已修：三個連線錯誤碼都核對稽核。
  - `[medium]` `[patch]` 省略已宣告參數的稽核沒有被跑到 — `出貨` 的呼叫都帶了 `名稱`。少參數分支改回不經 `Finish`，現有測試不會失敗。已修：省略「名稱」的呼叫會檢查稽核。訊息仍是「未提供資料表名稱。」
  - `[medium]` `[patch]` 空白工具名稱改回拒空白後，現有測試仍通過 — 空名稱會進 `Finish`。`ThrowIfNullOrWhiteSpace` 會讓這次呼叫變成未處理例外，而不是可稽核的 `unknown_tool`。已修：空名稱呼叫斷言 `unknown_tool` 與稽核。
  - `[medium]` `[patch]` 缺字型測試的 `Write` 沒用回退家族 — `Write` 用的是靜態家族。刪掉關缺字例外的兩行，回傳 `Lato` 的斷言與本機有正黑體時的頁數斷言都還在。已修：測試把靜態家族設成缺檔回傳的 Lato 再寫檔，結束時還原。
  - `[medium]` `[patch]` 「解說已確認」沒有鎖住勾選與儲存讀回 — 測試只找標籤文字。拿掉載入時寫入 `_confirmed`，標籤還在，儲存會把 `Confirmed` 送成假。已修：斷言核取方塊已勾選，並從畫面儲存後讀回 `Confirmed`。
  - `[medium]` `[defer]` 少參數的呼叫端看到「未提供資料表名稱。」 — 與缺參數文案是同一則既有訊息，契約沒有改這句。
  - `[false]` `[reject]` 規格檔與 `docs/mcp` 超出「只補程式」 — 這次的契約任務寫明要改錯誤碼表；規格檔是這條建置流程的產物。
  - `[false]` `[reject]` 公開探測與字型接點超出最小補洞 — 設計備註要求測試能看見 handler 的 `AllowAutoRedirect`、逾時與字型家族。單例分叉已在前面另行駁回。
  - `[false]` `[reject]` `CallAudit` 接受空白工具名稱不在 Always 字面 — 空名稱不在已發布清單，必須回 `unknown_tool` 並寫稽核。空白即拒會讓 `Finish` 拋出，呼叫端拿不到該碼。
  - `[medium]` `[patch]` PDF 契約落在型別初始化，測試打的是可傳路徑的方法 — 與缺字型 `Write` 是同一缺口：測試要讓實際寫檔使用缺檔呼叫回傳的家族。已修：寫檔使用該回傳家族，測試結束還原靜態欄位。
  - `[false]` `[reject]` 工作台預覽測試打的是替身 — `Schema_preview_does_not_read_business_rows` 直接呼叫 `SchemaGraph.Preview` 與 HTTP。畫面測試只負責伺服器訊息與不畫列。
  - `[false]` `[reject]` 文件沒有被測試執行 — 錯誤碼由宿主測試鎖住。Markdown 不是這次要執行的表面。

## Design Notes

連線決策只看目錄裡 `Available` 的名稱。空白連線字串的那筆不是可用連線，所以不能當成「恰有一筆」被自動選上。

合併時先照現在的 `JoinOrder` 決定表的順序。兩端都已進入列之後，其餘連結同一對來源的 link 逐條做等值比較，全部成立才留下該列。

`Open()` 在沒有注入 handler 時使用 `AllowAutoRedirect = false` 的 `HttpClientHandler`。測試要能看到這個 handler 的 `AllowAutoRedirect` 與客戶端的 `Timeout`，不要只讀 `Limit` 常數。`OpenPull()` 保持無限逾時。

## Verification

**Commands:**
- `dotnet test McpService.Tests/McpService.Tests.csproj` -- expected: 通過，輸出不含連線字串
- `dotnet test Workbench.Tests/Workbench.Tests.csproj` -- expected: 通過

## Auto Run Result

公開 `tools/call` 對已關閉名稱回 `tool_disabled`、對未發布名稱回 `unknown_tool`，連線與參數拒絕都寫稽核。發布要求目的非空且已確認；Discovery 說明含來源與輸出欄。同一對來源的其餘關聯鍵會再過濾。結構預覽不讀業務列。沒有正黑體時 PDF 改走 Lato。模型探測逾時 8 秒、不跟隨重新導向，失敗或位址變更會清空模型清單。

變更檔案：
- `DbServices.Mcp/CallAudit.cs`：空白工具名稱可以寫入稽核。
- `DbServices.Mcp/TableDispatch.cs`：補上這次的錯誤碼工廠。
- `DbServices.Mcp/ViewCatalog.cs`：發布門檻，且列出與尋找已發布檢視時要求已確認。
- `DbServices.Mcp/ViewDispatch.cs`：同一對來源的其餘鍵都要相符。
- `DbServices.Mcp/SchemaGraph.cs`：預覽固定不讀業務列。
- `McpService/Program.cs`：公開呼叫的拒絕、連線決策、長說明，以及同一次呼叫共用一份目錄。
- `Workbench.Contracts/Dtos.cs`：檢視文件帶 `Confirmed`。
- `Workbench.Web/Components/Views.razor`：確認勾選，預覽文案不再承諾業務列。
- `Workbench.Adapters/SnapshotPdfWriter.cs`：回退到 Lato 時關閉缺字例外。
- `Workbench.Adapters/ModelEndpoint.cs`：探測客戶端 8 秒且不跟隨重新導向。
- `Workbench.Web/Components/Environment.razor`：位址變了就不把舊的模型名稱寫回。
- `McpService.Tests/ViewDiscoveryTests.cs`、`Workbench.Tests/GapClosureTests.cs`、`Workbench.Tests/ModelEndpointTests.cs`：鎖住上述表面。
- `docs/mcp/index.md`：錯誤碼與說明文字。

審查：34 則。修補 12 組（medium 10、low 2）：未確認不得留在 Discovery、回退字型與寫檔測試、探測清單、單次目錄、預覽文案，以及八項測試缺口。延後 2 則：表單沒有解說輸入；少參數的訊息仍是「未提供資料表名稱。」。駁回 11 則：未確認仍可按發布與結構變更不清確認，是契約只在目的變更時清旗標；表級名稱碰撞與未 trim 符合已發布清單與四個關閉名稱；第二條鍵的空字串與多引擎略過是既有語意與契約；未知連線寫入的是名稱；錯誤表的 when 欄已分開端點；規格檔、文件與公開測試接點是這次契約與設計備註要求的；空白工具名稱必須能稽核；畫面預覽測試負責訊息，伺服器另有測試。注入的 `ToolCatalog` 單例在儲存後會過期，但公開呼叫讀的是目錄上目前的那一份，不另加轉接型別。

後續審查建議為真。這輪修補的 medium 有 10 組，沒有再送四層審查。未驗證的風險是：舊檔仍可能在磁碟上保持 `published: true`，只是 Discovery 不再列出；QuestPDF 的缺字設定是整個行程的。

驗證：`dotnet test McpService.Tests/McpService.Tests.csproj` 失敗 0、通過 71、略過 0，輸出沒有連線字串。`dotnet test Workbench.Tests/Workbench.Tests.csproj` 第一次失敗 1（缺字型測試把行程字型留在 Lato，後續 PDF 讀不到「出貨」），還原靜態字型後重跑，失敗 0、通過 66、略過 0。

殘餘風險：缺字例外的兩個設定一旦關閉就不會在這次請求結束時打開。探測完成時若位址已變，不會改寫清單，也不會再寫一次失敗訊息。
