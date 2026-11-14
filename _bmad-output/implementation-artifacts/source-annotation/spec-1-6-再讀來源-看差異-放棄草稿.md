---
title: 'Story 1.6 再讀來源、看差異、放棄草稿'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 664945796a52514ae50b2846479be9bc9b4c84f6
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 已有草稿時再匯入或再讀結構會被拒絕，方晴看不到來源改了什麼，也不能放棄這份草稿。

**Approach:** 兩種再讀用同一條合併，只改目前草稿。差異留在這份草稿上，直到草稿關閉。放棄只刪草稿。

## Boundaries & Constraints

**Always:**
- 已有草稿時，再匯入對得上契約的活頁簿，或再按讀結構，都合併進這一份，不再回 `draft_exists`。沒有草稿、而畫面上是啟用包時，另寫一份草稿，啟用包的位元組不改。被取代的包上，匯入與讀結構仍停用。比對鍵是表名加欄名，去掉首尾空白後原樣比較，區分大小寫。`OFFICE_CODE` 與 `office_code` 是不同的鍵。
- 草稿已有的同鍵欄註、個資與同名表註留下，整包功能說明也留下。草稿沒有、上一份啟用包有的同鍵欄註、個資與同名表註，抄進草稿。再沒有才用這次來源帶進來的註記。表、欄、型別、可否空白、主鍵、外鍵、唯一索引勾記與再讀線索改成這次來源的形狀。包名與草稿 id 不變。
- 人補的已聲明關聯，兩端都還在新草稿裡就留下，含理由；兩端對調仍是同一條。外鍵關聯改成這次來源的形狀；同一條若草稿已有理由，留下理由。端點已消失的關聯不留。
- 差異寫在草稿的 `changes`，不另開檔。每一筆是 `added`、`removed` 或 `changed`，加上表名與欄名。沒有欄的表用表名、欄名留空。新增、刪除，或型別、可否空白、主鍵、外鍵、唯一索引勾記有變，才算一筆。人寫的註解不進這列，也不加刪線。沒有差異就不畫 `diff-list`。草稿還在，重開仍看得到。確認通過時清掉這列，不改對帳與啟用規則。
- 畫面上，新增列底是 `action-soft`，文字有「新增」。刪除列字色是 `danger`，文字有「刪除」，不用刪線。改變列字色是 `caution`，文字有「已改變」。`diff-list` 在關聯之後、有 DBML 時在 DBML 之前，可以用 Tab 走到。
- 對不上契約仍是 `import_rejected`。零筆、多筆、名稱不等仍是 `no_connection`、`connection_unselectable`、`not_platform_source`，句子沿用既有，而且不呼叫 `SchemaGraph.Read`。讀結構失敗沿用該方法的 `connection_failed` 或 `execution_failed`。這些失敗都不寫檔。進行中仍是「正在匯入。」或「正在讀取結構。」，匯入、讀結構、確認與放棄都停用。業務列讀取次數為 0。
- 放棄先開一層對話框，寫「放棄這份草稿？已確認的包維持原樣。」Esc 或取消關閉，草稿還在。確定後只刪草稿。還有啟用包就打開那一份唯讀，否則回到沒有包的註記站。已確認包與檢視都不改。未持有沿用「沒有權限。」與 `forbidden`，不寫檔。

**Never:**
- 不改確認對帳、檢視落點、DBML 產生、設計清單或 `source_annotation`。那些是別則。
- 不新增套件、色票或錯誤碼。不新增架構清單以外的路由。`DELETE /annotations/draft` 已在清單裡。
- 不改 `SchemaGraph.Read`、`ViewCatalog` 的發布、Avalonia、`/environment`、`IDbService`、既有 REST 與 GraphQL。從啟用包另寫而草稿已經在時，仍是 `draft_exists`。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 再讀 | 草稿裡 `OFFICE_CODE` 的欄註是「局號」，來源變成 `office_code`，同鍵的欄註與說明留著 | 只改這份草稿。`OFFICE_CODE` 是刪除，`office_code` 是新增，「局號」不在新欄上。已確認包不變 | 讀列次數 0 |
| 沒有差異 | 再讀的形狀與草稿相同 | 不畫 `diff-list` | 說明與同鍵註記仍在 |
| 再讀失敗 | 活頁簿對不上，或連線是零筆、多筆、待轉資料庫，或 `Read` 失敗 | 草稿與已確認包的位元組不變 | 沿用既有代碼與句子。不呼叫不該呼叫的 `Read` |
| 從啟用包再讀 | 沒有草稿，畫面上是啟用包 | 另寫一份草稿，同鍵註記從啟用包抄來。啟用包不改 | 被取代的包不能再讀 |
| 放棄 | 草稿還在 | 對話框是那句。Esc 後草稿還在。確定後草稿消失 | 有啟用包就唯讀打開，否則回到冷開啟 |
| 未持有 | 沒有資料治理入口仍再讀或放棄 | 不寫檔 | `forbidden`「沒有權限。」 |

</frozen-after-approval>

## Code Map

- `DbServices.Mcp/AnnotationCatalog.cs` — `Import` 約第 111 行、`ReadStructure` 約第 200 行，已有草稿時現在回 `draft_exists`。這兩處改成合併。`ForkFromActive` 約第 254 行，草稿已在時仍拒絕。合併與 `changes` 只在這裡寫。確認通過時把 `changes` 清成空清單。
- `McpService/Program.cs` — `POST /annotations/read-structure` 約第 309 行，不要再在連線判定前直接回 `draft_exists`。連線不合仍不呼叫 `Read`。依架構加 `DELETE /annotations/draft`。門檻與 `/views` 相同。
- `Workbench.Core/Ports/Ports.cs`、`Workbench.Adapters/AnnotationCatalogClient.cs`、`Workbench.Core/AnnotationService.cs`、`Workbench.Web/Controllers/Controllers.cs` — 放棄走 `DELETE`。再讀仍走既有匯入與讀結構。工作台不載入連線字串。
- `Workbench.Web/Components/Governance.razor` — package-bar 加「放棄草稿」。對話框沿用 `.dialog`、`.confirm` 與 `DialogHost`。`diff-list` 只在草稿有差異時出現。
- `Workbench.Web/wwwroot/tokens.css` — `.diff-list` 只用既有 `surface-raised`、`border-hairline`、`action-soft`、`danger`、`caution`。
- `Workbench.Tests/ExcelDraftTests.cs`、`StructureDraftTests.cs`、`McpService.Tests/AnnotationDraftTests.cs`、`StructureDraftTests.cs`、`NoteEditTests.cs` — 第二筆匯入或第二次讀結構不再斷言 `draft_exists`，改成合併成同一份草稿。`DbmlConfirmTests` 裡從啟用包另寫而草稿已在的 `draft_exists` 不要改。
- `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Reread_abandon`。
- 不要改 `DictionaryWorkbook`、`SchemaGraph.Read`、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `AnnotationCatalog.cs` — 再讀合併、寫入 `changes`、放棄只刪草稿、確認時清掉差異 — 已確認包不改
- [x] `McpService/Program.cs` — 讀結構在已有草稿時改走合併，並加 `DELETE /annotations/draft` — 連線不合不呼叫 `Read`
- [x] 埠、客戶端、`AnnotationService`、`Controllers.cs` — 放棄 — 網頁不碰連線字串
- [x] `Governance.razor` 與 `tokens.css` — 差異列、放棄對話框、Esc — 不新色票，沒有差異不佔版面
- [x] 既有匯入與讀結構測試，加上兩邊的 `Reread_abandon` — 改名不搬家、失敗不寫檔、放棄後啟用包還在、未持有、讀列次數為 0 — 鎖住矩陣

**Acceptance Criteria:**
- Given 再讀通過，when 檢查目錄檔，then 仍是原來那一份草稿，已確認包的內容與 `active` 不變，沒有連線字串與業務列。
- Given 這一輪，when 檢查確認對帳與 `ViewCatalog` 的發布，then 沒有為再讀改動那些規則。

## Implementation Notes

## Spec Change Log

## Review Triage Log

- high — 讀結構把 `NotNull`、`UniqueIndex`、索引與再讀線索寫成來源沒有的空值。規格要形狀改成這次來源。結構讀本來就沒有這些欄，不是漏抄。false
- high — `ColumnInside` 放寬確認時的檢視落點。空白來源改看同名欄。規格寫明不改確認落點。route: patch，恢復只認 `Source` 加欄名，並拿掉為放寬而加的測試
- medium — 已聲明關聯留下舊的端點字串。來源欄名去掉空白後，關聯仍指著舊字，保存會對不上。route: patch
- false — 端點一度消失的關聯，再讀補回端點時不從啟用包復活。規格只留草稿上、兩端都還在的已聲明關聯。
- false — 沒有草稿時，新草稿的功能說明用來來源。規格只從啟用包抄同鍵欄註、個資與表註，沒有把整包說明列進去。
- high — 放棄對話框打開時仍可保存；放棄刪掉草稿後，進行中的保存會把草稿畫回來。`CommitAbandon` 沒有推進 `_revision`。route: patch
- medium — 差異用的外鍵含有不會被存下的端點，畫面寫已改變，檔內沒有那條關聯。route: patch
- medium — 再讀成功後，畫面上的 DBML 仍是上一輪。route: patch
- low — 頁面替身不重算差異。真正規則在目錄，服務測試有鎖。rejected
- low — `diff-list` 沒有名稱。Tab 停在沒有名字的清單上。route: patch，加上 `aria-label`「差異」
- false — 不明的 `kind` 畫成「已改變」。目錄只寫 `added`、`removed`、`changed`。
- medium — 同一張表從空表變成有欄，或欄被拿光，會同時出現新增與刪除。route: patch
- false — 來源名稱帶前後空白時確認對不上。存下來的是這次來源的名稱；對不上的是關聯端點，已併進上面那條。
- low — 修剪後同名的兩欄，後者蓋掉前者。日常來源不會有這種欄。rejected
- false — 未保存的畫面註記被再讀蓋掉。再讀合併的是已保存的草稿，保存仍是既有的「保存」。
- medium — 讀結構回來時若修訂號已變，畫面留在舊形狀。併進放棄與讀取期間停用編輯那一條。route: patch
- false — 再讀結構清掉索引與字典欄。規格要再讀線索改成這次來源的形狀。
- false — 形狀沒變的再讀把差異清成空。沒有差異就不畫；這一輪的比較結果就是空的。
- patch — 只有啟用包、沒有草稿時的讀結構，沒有測試從啟用包帶入註記與差異。補測試。
- patch — `Seed` 寫入的 `changes` 沒有被斷言。補測試。
- patch — 工作台放棄的 `DELETE` 用戶端與 API 成功路徑沒有被測到。補測試。
- false — 空白來源、檢視也沒有來源時的放行沒有測試。這條放行不該存在，恢復舊判斷即可。

## Design Notes

註記的優先順序是草稿、上一份啟用包、這次來源。外鍵是否改變，看這一欄在 `origin` 為 `foreign-key` 的關聯上，另一端變了沒有；已聲明關聯不進差異列。`changes` 跟著草稿走，放棄就一起刪掉。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Reread_abandon` -- expected: 通過
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Reread_abandon` -- expected: 通過
