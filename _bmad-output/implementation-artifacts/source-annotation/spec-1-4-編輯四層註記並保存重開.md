---
title: 'Story 1.4 編輯四層註記並保存重開'
type: 'feature'
created: '2026-11-14'
status: 'done'
baseline_commit: '72a6067a76805b4f355275f03d6a2e9544d8785c'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/source-annotation/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 草稿上看得到表和欄，卻不能補上整包說明、表註、欄註、個資與關聯，也沒有保存後重開仍在的寫法。

**Approach:** 只在已有工作中草稿時開放這四層編輯。按下「保存」才寫回同一份草稿；重開後內容還在，狀態仍是草稿。

## Boundaries & Constraints

**Always:**
- 畫面上有草稿時才出現編輯。整包功能說明、表註、欄註、個資處理可改。空白說明與空白表註可以保存，框線維持 `border-hairline`，不改成危險色。欄註空白顯示「尚未註記」，底 `surface-sunken`、字 `caution`；清掉已寫的欄註後徽章回來。個資是普通文字，不執行遮罩，不用警示底。
- 唯一索引只顯示來源帶入的勾記，這一則不能改。人可以在說明裡寫它不是真正的唯一鍵，勾記仍保持原樣。表名、欄名、型別、主鍵、`not null` 與再讀線索也不能由這次保存改掉。
- 關聯可改理由、可刪。新的一條兩端都必須是本包的表與欄，理由可空白，`origin` 是 `declared`。與既有關聯兩端相同或對調，仍是同一條，保留原來的 `origin`。一端不在本包就整次拒絕，既有關聯不變，代碼 `link_outside`，訊息「關聯的一端不在這份註記包裡。」
- 換到另一張表時，上一張已寫、尚未保存的內容還在。保存成功後關掉再打開，狀態仍是「草稿」，四層與個資都在，空白欄註仍空白。同時只有這一份工作中草稿。保存不把草稿變成已確認包，也不改已確認包與已保存檢視。
- 沒有草稿時不能藉保存另建一份。保存失敗是 `execution_failed`「沒有保存。」畫面回到上次保存成功的內容。錯誤是 `status-message`，訊息在上、代碼在下，`role="alert"`。未持有沿用「沒有權限。」與 `forbidden`，不寫檔。
- 「保存」放在 package-bar，跟匯入、讀結構一起。焦點順序經過左軌、package-bar、整包功能說明、表清單、表註、欄註、關聯。編輯動作都看得到。草稿與畫面沒有連線字串，也不含業務列。

**Never:**
- 不匯出、不確認、不放棄、不再讀合併。那些是後面的故事。
- 不改匯入與讀結構的 `draft_exists`：新的一份仍被拒絕，檔不變。不改 `SchemaGraph.Read`、Excel 剖析、Avalonia、`/environment`、`IDbService`、既有 REST 與 GraphQL。
- 不新增套件、色票或錯誤碼。不新增架構清單以外的路由。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 換表 | 草稿至少兩張表；寫上說明、第一張表註與其中一欄欄註後換表 | 第二張顯示自己的表註與欄。第一張已寫的內容還在 | 不寫檔 |
| 空白可存 | 說明與表註留空，欄註先寫再清掉 | 保存成功。空框不是危險色。空欄註回到「尚未註記」 | 不阻擋保存 |
| 個資與勾記 | 改個資文字；勾記維持來源的值 | 個資照文字保存。勾記沒有因為說明或請求被改掉 | 不執行遮罩 |
| 關聯 | 補一條本包內的關聯，理由空白，並刪掉另一條 | 新關聯在，理由空白，`origin` 是 `declared`。被刪的不在 | 不新增第二份草稿 |
| 端點在包外 | 一端不是本包的表或欄 | 檔不變，既有關聯不變 | `link_outside` 與上面那句 |
| 保存重開 | 保存成功後重新打開 | 仍是草稿且未啟用。四層與個資還在。沒有連線字串與業務列 | 已確認包與檢視不變 |
| 保存失敗 | 寫入失敗 | 畫面回到上次保存成功的內容 | `execution_failed`「沒有保存。」 |
| 未持有 | 沒有資料治理入口仍呼叫保存 | 不寫檔 | `forbidden`「沒有權限。」 |

</frozen-after-approval>

## Code Map

- `Workbench.Web/Components/Governance.razor` — 草稿目前是純文字。這裡把說明、表註、欄註、個資改成輸入，勾記只顯示，關聯可改理由、刪除與新增。「保存」放在 package-bar。沿用 `_revision`，避免較早的匯入或讀結構蓋掉正在編的內容。
- `DbServices.Mcp/AnnotationCatalog.cs` — `Import` 約第 104 行、`ReadStructure` 約第 136 行，已有草稿都回 `draft_exists`。另加保存：只更新那一份草稿的說明、表註、欄註、個資與關聯。結構欄位以檔內原本的為準。沒有草稿不建立。
- `McpService/Program.cs` — `PUT /annotations/draft` 約第 267 行現在只走 `Import`。同一路由依「是不是目前這份草稿」分流：新的一份仍是匯入，同一份才是保存。不要另開路由。門檻仍是兩個標頭。
- `Workbench.Core/Ports/Ports.cs`、`Workbench.Adapters/AnnotationCatalogClient.cs`、`Workbench.Core/AnnotationService.cs`、`Workbench.Web/Controllers/Controllers.cs` — 保存沿用既有埠與 `PUT`。工作台不接收連線名稱。`link_outside` 的句子放在 `WorkbenchMessages`。
- `Workbench.Web/wwwroot/tokens.css` — 補 `.purpose-field`，只用既有 `border-hairline`、`surface-sunken`、`caution`。`.unnoted` 已在。
- `Workbench.Tests/Host.cs` — `RecordingAnnotations` 要能保存。換表與重開走畫面；端點在包外、保存失敗、未持有與檔內已確認包走目錄或宿主。
- 不要改 `DictionaryWorkbook`、`SchemaGraph.Read`、資料平面的 `sprint-status.yaml`。

## Tasks & Acceptance

**Execution:**
- [x] `AnnotationCatalog.cs` — 保存只改目前草稿的四層與個資；結構欄位與勾記保持檔內的值；沒有草稿不建立；`link_outside` 不寫檔 — 唯一寫入者
- [x] `McpService/Program.cs` — `PUT /annotations/draft` 對同一份草稿改走保存，新的一份仍走匯入 — 不新增路由
- [x] 埠、客戶端、`AnnotationService`、`Controllers.cs`、`Messages.cs` — 保存呼叫與 `link_outside` — 網頁不碰連線字串
- [x] `Governance.razor` 與 `tokens.css` — 輸入、勾記、關聯與「保存」。換表不丟未保存的內容 — 不新色票，不加確認
- [x] `Workbench.Tests` 與 `McpService.Tests` — 方法名含 `Note_edit`：換表、空白、個資與勾記、關聯、包外端點、重開、保存失敗、未持有 — 鎖住矩陣

**Acceptance Criteria:**
- Given 保存成功，when 檢查草稿檔，then 沒有連線字串與業務列，狀態仍是 `draft`，`active` 仍是 false。
- Given 這一輪，when 檢查匯入、讀結構、`SchemaGraph.Read` 與 Avalonia，then 沒有為編輯改動它們的拒絕規則。

## Implementation Notes

## Spec Change Log

## Review Triage Log

| # | Finding | Verdict | Evidence | Route |
|---|---------|---------|----------|-------|
| 1 | 保存進行中仍可改內容，成功不記住、失敗沒有訊息 | medium | 保存時已停用編輯、匯入與讀結構。傳輸失敗仍顯示「沒有保存。」並回到上次成功的草稿 | patch |
| 2 | 新增相同或對調的關聯會多一列 | medium | 現在留在原列，保留來源，只套用新理由 | patch |
| 3 | 只含空白的欄註存成空白字元 | medium | 畫面與目錄都存成空字串，仍顯示「尚未註記」 | patch |
| 4 | 說明欄被 28rem 的標籤寬度卡住 | medium | `.purpose-field` 改用所在欄的寬度，邊線仍是 `border-hairline` | patch |
| 5 | 同向重存沒鎖住 `foreign-key`，畫面理由、漏列表欄、傳輸例外都沒測 | medium | 四項都已補進 `Note_edit`，並通過 | patch |
| 6 | 空白欄註改成危險色，測試仍過 | medium | 測試只看 class 與第一段 CSS，元件測試量不到計算後的顏色 | defer |
| 7 | 未選端點時新增沒有 `link_outside` | low | 下拉只列出本包的表與欄。包外端點由目錄拒絕 | reject |
| 8 | 請求省略關聯會清掉第四層 | low | 註記站送出的是整份草稿，不是只送說明 | reject |
| 9 | 同名欄、null 元素、關聯鍵裡的符號 | low | 自己寫出的草稿沒有同名欄或 null。日常端點名稱不會撞鍵 | reject |

## Design Notes

儲存時以表名與欄名對上檔內的列，只套用說明、表註、欄註與個資。請求裡改過的表名、型別、主鍵或勾記都丟棄。新關聯的 `origin` 用 `declared`。兩端與既有某一條相同或對調時，留下那條的 `origin`，理由改成這次送出的值。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Note_edit` -- expected: 通過
- `dotnet test McpService.Tests/McpService.Tests.csproj --filter FullyQualifiedName~Note_edit` -- expected: 通過
