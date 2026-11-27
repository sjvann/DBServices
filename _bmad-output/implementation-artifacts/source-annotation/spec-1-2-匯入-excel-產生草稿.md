---
title: 'Story 1.2 匯入 Excel 產生草稿'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
baseline_commit: '627734fdc10e5922164b3bc47737a8637393b5dc'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 持有資料治理的人進得了註記站，卻不能從字典活頁簿做出一份草稿。

**Approach:** 沒有工作中草稿時，匯入符合 `Requirements/Samples.xlsx` 契約的活頁簿，存成唯一草稿，並在註記站看見表、欄與註解。

## Boundaries & Constraints

**Always:**
- 第 1 列表頭必須依序是：H_檔案名稱、H_欄位序號、H_欄位名稱、H_資料類型長度、資料描述、H_個資去識別化處理、S_檔案名稱、S_欄位序號、S_欄位名稱、S_資料類型、S_資料長度、S_唯一索引、資料備註、允許空白。工作表多於一張，或表頭不是這 14 個，就拒絕。
- 轉換規則用史詩上下文的 Excel 段。驗收對照 `Requirements/LH_TXN_MLST.sample.dbml`。`TABLE_DATE` 用 H 側欄名與 `text`，欄註不含「H 欄名」。`SSO_ID` 沒有 not null。六個 `V` 組成 `ix_lh_txn_mlst_append`，索引說明是那句固定文字。
- 拒絕文案是「無法匯入。這份活頁簿對不上字典契約。既有草稿沒有改。」代碼 `import_rejected`。已有工作中草稿再匯入，文案是「已有工作中的草稿，沒有改。」代碼 `draft_exists`。都是 `status-message`，訊息在上、代碼在下，`role="alert"`。
- 包名是活頁簿檔名去掉副檔名。`Samples.xlsx` 顯示 Samples。整包功能說明留空。匯入不要求連線，不讀業務列，草稿不含連線字串。進行中只寫「正在匯入。」，字色 ink-secondary，匯入控制項停用。
- 寬於 1099px 時左欄表清單、右欄該表的欄，左軌 240px。其下單欄，左軌仍 240px。720px 以下左軌 200px。控制項至少 36px，焦點沿用 `:focus-visible`，減少動態時取消過渡。

**Never:**
- 不讀結構、不編輯保存、不匯出、不確認、不放棄、不再讀合併。那些是後面的故事。
- 不把「00.資料總表」做成第二種版型。不新增套件、色票或錯誤碼。不改 Avalonia、`/environment`、`IDbService`、`ViewDocument`、既有 REST 與 GraphQL。
- 不讀業務列，不把連線字串放進請求、草稿或畫面。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 匯入樣品 | 沒有草稿，匯入 `Samples.xlsx` | 一份 `draft`、`active` 為 false 的包。package-bar 是 Samples 與「草稿」。表清單有 `LH_TXN_MLST`，欄、型別、not null、欄註、表 Note、索引對上樣本 | 不建立第二份 |
| 契約不符 | 表頭不對、工作表多於一張，或檔案打不開 | 沒有新草稿。已有草稿時內容不變 | `import_rejected` 與上面那句 |
| 已有草稿 | 再匯入一份對得上的活頁簿 | 草稿位元組不變 | `draft_exists` 與上面那句 |
| 未持有 | 沒有資料治理入口仍呼叫匯入 | 不寫檔 | 沿用「沒有權限。」與 `forbidden` |

</frozen-after-approval>

## Code Map

- `Workbench.Web/Components/Governance.razor` — 故事 1.1 的空站。這裡改成冷開啟與匯入後的註記站。只注入閘門與註記埠。
- `Workbench.Core` — 活頁簿剖析放這裡，用框架的壓縮與 XML，不參考 `DbServices.Mcp`。錯誤沿用 `WorkbenchMessages.Text`。
- `Workbench.Core/Ports/Ports.cs` — 既有埠清單。註記目錄的埠加在這裡，比照 `IViewCatalog`。
- `DbServices.Mcp/ViewCatalog.cs` — 單檔 JSON、鎖、寫入結果的形狀。註記目錄照這個寫 `catalog/annotations.json`，不要寫進檢視檔。
- `McpService/Program.cs` — `/views` 在 `/mcp` 之外，寫入要兩個標頭。註記路由照同一道門。這一則只做讀目前草稿與寫入新草稿。
- `Workbench/Program.cs` — 檢視目錄在這裡接到 HTTP 客戶端。註記目錄同樣接。`Workbench.Web` 不加專案參考。
- `Workbench.Tests/Host.cs` — 測試宿主用替身目錄。樣品對照放核心測試；拒絕與權限走既有 `WorkbenchHost`。
- 不要改 `SchemaGraph.Read`、`Environment.razor`、Avalonia、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `Workbench.Core` — 剖析活頁簿成來源形狀；樣品對上 DBML；壞檔回 `import_rejected` — 核心不碰目錄檔
- [x] `DbServices.Mcp` — 新增註記目錄。無草稿才寫入；已有草稿回 `draft_exists` 且檔不變 — 唯一寫入者
- [x] `McpService/Program.cs` — 讀草稿與寫入草稿兩條路由，門檻與 `/views` 相同 — 工作台不直接碰檔
- [x] `Workbench.Core/Ports/Ports.cs` 與 `Workbench.Adapters`、`Workbench/Program.cs` — 埠與客戶端 — 網頁只呼叫埠
- [x] `Workbench.Web/Components/Governance.razor` 與 `tokens.css` — 冷開啟、匯入、package-bar、表清單與欄註。進行中那一行用既有 ink-secondary — 不新色票
- [x] `Workbench.Tests` — 方法名含 `Excel_draft`：樣品對照、契約拒絕、已有草稿、未持有、讀列次數為 0 — 鎖住矩陣

**Acceptance Criteria:**
- Given 只持有資料治理並匯入樣品，when 計算業務列讀取，then 次數為 0，畫面與草稿都沒有連線字串。
- Given 這一輪，when 檢查 Avalonia、`/environment` 與既有檢視路由，then 沒有為匯入改動。

## Implementation Notes

## Spec Change Log

## Review Triage Log

| # | Finding | Verdict | Evidence | Route |
|---|---------|---------|----------|-------|
| 1 | 同名表中間被打斷後會再追加一張 | medium | `Tables` 原本只跟上一列比對。已改成併進同名表 | patch |
| 2 | 工作表關聯失敗改讀 sheet1 | medium | `SheetPath` 原本回傳 `sheet1.xml`。關聯讀不到現在拒絕 | patch |
| 3 | 超連結範圍、壓縮展開、欄號溢位沒有上限 | medium | `Expand`、`Load`、`TryAddress` 都沒有上限。超過 4096 格、8MB 或欄號 16384 現在拒絕 | patch |
| 4 | 只有表頭也會佔住唯一草稿 | medium | 空欄清單原本成功。現在拒絕 | patch |
| 5 | 表註被中間空列鎖住，COUNT 只丟該格 | medium | 說明現在只取最後一個欄位列之後；含 COUNT 的列整列捨棄 | patch |
| 6 | 表清單 `aria-current` 與顯示中的表不一致 | false | `_selected` 與按鈕用同一份表名，選取的按鈕是 `true` | reject |
| 7 | 冷開啟把目錄代碼改成 `execution_failed` | medium | 用戶端失敗只留訊息。頁面現在固定顯示「沒有保存。」，不放例外文字 | patch |
| 8 | 匯入連線失敗沒有 `status-message` | medium | 原本只接 IO。現在連線與逾時顯示「沒有保存。」 | patch |
| 9 | 檔名含非法字元或 id 重複 | low | 一般選檔不會帶那些字元，新 id 是隨機值 | reject |
| 10 | MCP 路由與第一次建檔沒有測試 | medium | 測試換了替身。已補路由測試與檔案不存在時的匯入 | patch |
| 11 | 超連結範圍沒有上限，進行中沒有 `aria-live`，選檔按鈕外露 | medium | 範圍上限已修。其餘是日常看得到的外觀，不另加屬性 | patch |
| 12 | 目錄讀取失敗變成 500 | medium | `Load` 會丟出例外。路由現在回「沒有保存。」 | patch |
| 13 | 開啟中的草稿讀取蓋掉剛匯入的內容 | medium | 匯入會推進版次，舊讀取回來不再寫入 | patch |
| 14 | 工作台 API 連線失敗變成 500 | medium | `AnnotationService` 沒接住用戶端例外。現在回「沒有保存。」 | patch |
| 15 | 草稿的表或欄清單是 null 會讓頁面當掉 | low | 寫入端會補空清單，自己存的檔不會是 null | reject |
| 16 | HTTP 404 被當成沒有草稿 | medium | 用戶端把 404 與 204 都當空。現在只有 204 是沒有草稿 | patch |

## Design Notes

活頁簿是 zip。核心只讀第一張工作表的儲存格計算結果，不載入試算表套件。目錄檔的鍵沿用史詩上下文：`id`、`title`、`purpose`、`status`、`active`、`tables`、`links`、`dbml`。這一則 `links` 與 `dbml` 留空，欄上仍留下再讀要用的來源線索，畫面不另開一區。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Excel_draft` -- expected: 通過
