---
title: 'Story 1.5 匯出 DBML 並確認成唯一啟用包'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: ebc63ca43e4371d329e4c3a0ec3056b251c6467f
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 草稿還不能匯出成 DBML，也不能在對帳通過後變成唯一啟用的包。

**Approach:** 匯出只產生文字並下載，不改檔。確認先對帳表名與欄名，再檢查已保存檢視的端點；通過後這一份成為唯一啟用包，舊的啟用包退出啟用但保留。

## Boundaries & Constraints

**Always:**
- 匯出不要求已對上活庫，也不要求已確認，也不改目錄檔。畫面上先顯示唯讀 `dbml-block`，同時下載。檔名是包名加 `.dbml`；包名含 `\ / : * ? " < > |` 時，只從下載檔名拿掉這些字元，畫面包名不變。拿掉後若是空的，檔名用 `annotation.dbml`。檔內不含連線字串或業務列。
- 未改過的 `Requirements/Samples.xlsx` 匯入結果，匯出的 `Table` 區塊與 `Requirements/LH_TXN_MLST.sample.dbml` 的 `Table` 區塊一致，不含樣本開頭那行註解。說明非空白時，放在表之前的 `Project` 註解。關聯寫成 `Ref`，理由空白就不寫 note。DBML 只由目錄產生。
- 確認只針對工作中草稿，而且先開一層對話框。沒有舊啟用包時寫「確認這份草稿？」。有舊包時寫「確認這份草稿？將取代{舊包名稱}。」Esc 或取消關閉且不確認。通過後 `result-line` 寫「已確認。設計目錄改看這一包。」，`role="status"`，不用 danger。有舊包再接「取代了{舊包名稱}。」
- 說明空白、零張表或零個欄是 `confirm_incomplete`：「無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。」零筆是 `no_connection`，多於一筆是 `connection_unselectable`，名稱不等是 `not_platform_source`，句子沿用既有。這三種都不呼叫 `SchemaGraph.Read`，也不用 `GET /meta`。對不上的表名，或「表名.欄名」，以頓號接在 `confirm_unmatched`：「無法確認。這些表或欄對不上來源：{名稱}。」已保存檢視的來源表、輸出欄或關聯端點不在新包時，檢視名稱以頓號接在 `confirm_blocked`：「無法確認。這些檢視會落到新包之外：{檢視名稱}。」拒絕時草稿、已確認包與檢視都不改。
- 通過後這一份改為 `confirmed` 且 `active` 為 true，並依當時的表、欄、型別、註解與關聯重算 `dbml` 存進該包。舊的啟用包仍是 `confirmed`，`active` 改 false，不刪。同時只有一份啟用包，也沒有留下第二份草稿。對帳只看表名與欄名是否存在，業務列讀取次數為 0。不改來源庫，不改寫檢視。
- 確認後註記站打開這一份，徽章是「已確認」，可再匯出。沒有草稿時用架構已列的 `GET /annotations/active` 讀啟用包；沒有啟用包是空結果，不是錯誤。其他已確認包以包名列在站上。打開被取代的包時，匯入、讀結構、編輯與確認都停用，只能匯出。在啟用包上開始改說明、表註、欄註、個資或關聯時，另寫一份草稿，不改已確認內容。已有工作中草稿時不再另建。欄註或表註空白不擋確認。整包說明去掉首尾空白後沒有內容，與零張表或零個欄相同。匯入或讀結構進行中，確認停用。未持有沿用「沒有權限。」與 `forbidden`，不寫檔。

**Never:**
- 不放棄草稿、不再讀合併、不改模組設計工具。那些是後面的故事。
- 不把確認前匯出的檔讀回設計目錄。不新增套件、色票或錯誤碼。不新增架構清單以外的路由。
- 不改匯入、讀結構、保存的拒絕規則，不改 `SchemaGraph.Read`、`ViewCatalog` 的發布規則、Avalonia、`/environment`、`IDbService`、既有 REST 與 GraphQL。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 匯出樣品 | 未改過的 Samples 草稿，說明可空白 | 唯讀 DBML，並下載 `{包名}.dbml`。`Table` 區塊對上樣本 | 不改目錄檔。不要求已確認 |
| 確認 | 說明非空白，至少一表一欄，名稱都在那一筆平台來源，沒有舊啟用包 | 這一份成為唯一啟用包。`dbml` 重算後存進包。result-line 是那句成功句 | 讀列次數 0。不改來源庫與檢視 |
| 取代 | 已有啟用包 | 對話框寫出舊包名。Esc 不確認。通過後成功句接「取代了{舊包名稱}。」舊包保留且退出啟用 | 檔內仍是兩筆已確認包 |
| 不能確認 | 說明空白、零表、零欄、零筆、多筆、名稱不等，或表欄對不上 | 草稿與已確認包不變 | 上面對應的代碼與句子。不呼叫不該呼叫的 `Read` |
| 檢視落在包外 | 對帳已過，但已保存檢視的來源表、輸出欄或關聯端點不在新包 | 草稿與檢視不變，檢視仍維持發布狀態 | `confirm_blocked`，名稱以頓號相接 |
| 唯讀舊包 | 確認完成後打開被取代的包，或在啟用包上開始編輯 | 舊包可匯出、不能編輯。開始編輯另寫一份草稿 | 不直接改已確認內容 |
| 未持有 | 沒有資料治理入口仍呼叫匯出或確認 | 不寫檔 | `forbidden`「沒有權限。」 |

</frozen-after-approval>

## Code Map

- `DbServices.Mcp/AnnotationCatalog.cs` — 唯一寫入者。這裡產生 DBML、確認、以及從啟用包另寫草稿。`Import`、`Save`、`ReadStructure` 的拒絕不要改。確認失敗不得 `TryFlush`。
- `DbServices.Mcp/ViewCatalog.cs` — `ViewDocument` 約第 9 行。落點看 `Sources`、`Columns` 的 `Source` 與 `Name`、`Links` 的兩端。只讀，不呼叫 `SetPublished`。
- `McpService/Program.cs` — 依架構加 `GET /annotations/draft/dbml`、`POST /annotations/confirm` 與 `GET /annotations/active`。門檻與 `/views` 相同。連線判定沿用讀結構那段，不要呼叫 `GET /schema/graph`。`GET /annotations/active` 的啟用包之外，另附其餘已確認包的 `id` 與 `title`，給註記站點名打開；設計目錄只讀啟用包本體。
- `Workbench.Core/Ports/Ports.cs`、`Workbench.Adapters/AnnotationCatalogClient.cs`、`Workbench.Core/AnnotationService.cs`、`Workbench.Web/Controllers/Controllers.cs` — 匯出與確認的埠。工作台不載入連線字串。
- `Workbench.Web/Components/Governance.razor` — package-bar 加「匯出」與「確認」。對話框用既有 `.dialog`，主要動作用既有 `.confirm`，並把這一層放進 `DialogHost`，Esc 沿 `WorkbenchLayout` 關閉且不確認。成功用 `result-line`。被取代的包以包名打開，唯讀。
- `Workbench.Web/wwwroot/tokens.css` — `.dbml-block` 與 `.result-line` 只用既有 `surface-raised`、`border-hairline`、`ink-primary` 與程式字。`.dialog` 已在約第 218 行，不要另做一層樣式。
- `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Dbml_confirm`。樣品對照放會真的匯入 `Samples.xlsx` 的測試。檢視落點用一份已保存檢視，不斷言它被改寫。
- 不要改 `DictionaryWorkbook`、`SchemaGraph.Read`、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `AnnotationCatalog.cs` — 產生 DBML；確認通過才切換啟用並寫入 `dbml`；失敗不寫檔；從啟用包另寫草稿 — 唯一寫入者
- [x] `McpService/Program.cs` — `GET /annotations/draft/dbml` 與 `POST /annotations/confirm`。零筆、多筆、名稱不等都不呼叫 `Read` — 不改檢視檔
- [x] 埠、客戶端、`AnnotationService`、`Controllers.cs` — 匯出與確認 — 網頁不碰連線字串
- [x] `Governance.razor` 與 `tokens.css` — 唯讀 DBML、下載、確認對話框、成功句、唯讀舊包 — 不新色票，不加放棄
- [x] `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Dbml_confirm`：樣品對照、確認、取代、Esc、五種拒絕、檢視落點、唯讀與另寫草稿、未持有、讀列次數為 0 — 鎖住矩陣

**Acceptance Criteria:**
- Given 確認通過，when 檢查目錄檔，then 只有一份 `active` 為 true，沒有連線字串與業務列，來源庫沒有被改寫。
- Given 這一輪，when 檢查匯入、讀結構、保存與 `ViewCatalog` 的發布，then 沒有為確認改動那些拒絕或發布規則。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- high — 離開草稿後工具列沒有回去的按鈕。`NamedPackages` 只列啟用包與 `others`。route: patch
- false — 確認與匯出不帶未按保存的畫面修改。`POST /annotations/confirm` 沒有內文，匯出讀的是目錄裡的草稿；保存仍是既有的「保存」。
- high — 啟用包第一次輸入就 `Fork`，往返中的後續字被丟掉；`AddLink` 在沒寫入時也清掉理由。`Fork` 一進 `_forking` 就返回。route: patch
- medium — 確認送出後按鈕立刻又能按，第二次失敗會清掉成功句。`CanConfirm` 沒看 `_confirming`。route: patch
- false — 對話框背後仍可操作。`.scrim` 是 `position: fixed; inset: 0`，點擊落在遮罩上。
- low — 全站共用的 `DialogHost` 會讓另一個線路的 Esc 關掉這一層。同時開著兩層確認才會遇到，關掉也不會確認。修正要改堆疊的擁有者。rejected
- medium — 切換套件或確認成功後，DBML 區塊與成功句還留著上一包。`OpenPackage` 沒清 `_dbml` 與 `_result`。route: patch
- medium — 註解裡的換行會拆開 DBML；非 ASCII 名稱用單引號，`Project '平台'` 不是識別字。`Quote` 只處理反斜線與單引號，`Ident` 對非識別字呼叫 `Quote`。route: patch
- medium — 下載把檔名拼進 script，標題裡的換行會弄斷，失敗被空的 catch 吃掉。route: patch
- false — `others` 帶整包，工作台路由包成 `{ package, others }`。註記站要打開並匯出，只給 id 與 title 不夠；頁面走的客戶端對的是 MCP 攤平形狀，而且測試有打到。
- false — 測試替身另寫時沒保留 `origin`。正式寫入是 `MergeLinks`，啟用包上的 origin 留在檔內。替身不是寫入者。
- low — `ActiveAsync` 遇到壞掉的成功 JSON 會丟出 `JsonException`，頁面的 catch 沒接。成功回應不是 JSON 不是日常路徑。rejected
- low — 草稿裡的表或欄是 null 時，確認、落點或保存會丟例外。目錄檔由這個型別寫入，不會寫出 null 項。rejected
- low — 兩份已確認包都標成啟用時，`Active` 只回第一份。唯一寫入者在確認時會把其他包的 `active` 清掉。rejected
- low — 對帳讀結構的那段沒有鎖，來源或檢視若在中途改變，用的是這一輪讀到的結果。確認前已對這次讀到的名稱與呼叫時的檢視清單再查一次。rejected
- false — 客戶端中止後確認仍可能寫入。這次請求已經送出；規格沒有要求確認跟讀結構一樣在中止時放棄。
- medium — 另寫還在飛時仍可匯入或讀結構，修訂號一變就不還原畫面。`OnImport`、`OnRead` 與 `BusyAttributes` 都不看 `_forking`。route: patch
- low — 確認成功後再讀啟用包失敗，被取代的包暫時不在切換列。檔已經寫對，重新打開會列出。rejected
- low — 套件 id 是 null 時，`other.Id.Length` 會讓頁面壞掉。這條清單來自分目錄寫出的 id。rejected
- patch — 確認失敗時頁面有沒有警示與草稿，現有測試沒打到 `ConfirmFailure`。補測試。
- patch — 另寫失敗時畫面有沒有回到原說明，現有測試只打成功。補測試。
- patch — 沒有啟用包時 `GET /annotations/active` 應為 204，現有測試只打 200 或客戶端自己模擬 204。補測試。
- patch — 齊備草稿遇上讀結構失敗時，確認應沿用 `execution_failed` 與「讀取資料表結構失敗。」且不寫檔。補測試。
- patch — 從啟用包另寫時，包外關聯應是 `link_outside` 且不寫檔。現有測試走的是 `Save`。補測試。
- patch — `Quote` 與非識別字名稱的輸出沒有被比對。補測試。
- patch — 啟用包上改欄註的另寫結果沒有被斷言。補測試。
- patch — 匯出傳給 `eval` 的內容沒有被斷言。補測試。

## Design Notes

確認的順序是：先看說明、表與欄是否齊，再看連線筆數與名稱，然後才 `SchemaGraph.Read`，最後看檢視端點。任一步失敗都不寫檔。檢視端點是來源表、輸出欄所屬的表與欄名，以及關聯的兩端表與欄；不要求這段關聯已經寫在註記包裡。已確認包再匯出時用包內已存的 `dbml`，草稿匯出只回傳文字、不寫進 `dbml` 欄。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Dbml_confirm` -- expected: 通過
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Dbml_confirm` -- expected: 通過
