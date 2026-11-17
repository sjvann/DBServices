---
title: DBServices 檢視輔助、樣本驗證與發布註記 — 技術附錄
status: draft
created: 2026-11-16
updated: 2026-11-16
---

# Addendum: 檢視輔助路徑的技術邊界

本附錄承接 [prd.md](prd.md)。它記錄兩份目錄檔怎麼分工、檢視文件要加哪些鍵、SQL 如何與已接受結構對齊、樣本為何不留存，以及執行期如何分支。它不是畫面設計，也不是專案配置。架構可以改媒介與行程，不能改這裡與 PRD 已寫死的約束。

來源註記的技術附錄仍有效，見 [../prd-DBServices-2026-11-12/addendum.md](../prd-DBServices-2026-11-12/addendum.md)。檢視與報表的技術附錄見 [../prd-DBServices-2026-10-12/addendum.md](../prd-DBServices-2026-10-12/addendum.md)。資料平面約束見 [docs/planning/agent-data-plane/addendum.md](../../../../docs/planning/agent-data-plane/addendum.md)。

## 已決定的約束

1. **兩份檔、兩種語意。** 來源註記在 `catalog/annotations.json`；檢視在 `catalog/views.json`。工作台 SQLite 不放這兩樣。檔內沒有連線字串，也沒有業務列。
2. **新關聯不回寫註記包。** 僅此檢視的關聯只寫在 `ViewDocument`；接受、驗證、發布都不得改 `AnnotationPackage.links`。
3. **不新增第三種 `ViewLink.kind`。** 沿用 architecture-DBServices-2026-11-12 AD-5：`foreign-key` 與 `accepted`。僅此檢視用額外標記表達，不發明第三個 kind 字面。
4. **SQL 跟結構走。** 建議 SQL 可被提案帶出，但持久化與驗證以依已接受結構重編的句子為準。結構一改，SQL 與樣本作廢。
5. **樣本不留存。** 樣本列只存在於當次驗證回應。不寫入 `views.json`、不進 MCP、不進報表生成紀錄。
6. **舊路徑保留。** `sql` 空白的檢視仍走現有 [`ViewDispatch`](../../../../DbServices.Mcp/ViewDispatch.cs)（單表 query；多表記憶體 join）。這一輪不遷移舊文件。
7. **註記站仍不讀業務列。** 樣本驗證只發生在分析師的驗證動作，連線仍是 `Annotation:PlatformConnection`。

## 目錄檔分工

| 檔案 | 組態鍵 | 擁有者 | 本輪是否改寫鍵集合 |
|------|--------|--------|-------------------|
| `catalog/annotations.json` | `Catalog:AnnotationsPath` | `DbServices.Mcp.AnnotationCatalog` | 否。不新增鍵，不因提案改 `links` |
| `catalog/views.json` | `Catalog:Path` | `DbServices.Mcp.ViewCatalog` | 是。為輔助路徑增加下列產品欄位 |

啟用包讀取表面維持：

- HTTP：`GET /annotations/active`
- MCP 保留名：`source_annotation`（僅在有啟用包時出現）

模組設計工具的左側清單與提案輸入，都只讀啟用包。

## 檢視文件加欄（產品形狀）

既有 [`ViewDocument`](../../../../DbServices.Mcp/ViewCatalog.cs) 欄位維持：`id`、`name`、`purpose`、`confirmed`、`published`、`sources`、`links`、`parameters`、`columns`。

本輪增加（名稱可由架構微調，語意不可改）：

| 鍵 | 語意 |
|----|------|
| `sql` | 已接受結構編成的單句 SELECT；空白表示走舊 `ViewDispatch` |
| `sqlValidated` | 樣本驗證是否通過；結構一改必須回到 false |
| `rowGrain` | 檢視註記：一列代表什麼 |
| `viewPurpose` | 檢視註記：這份檢視回答什麼問題（可與既有 `purpose` 並存；發布閘門讀這一欄或架構指定的等同欄） |
| `columnNotes` | 輸出欄註解字典；鍵是輸出欄名，值可空白 |
| `linkScope` 或等同標記 | 標出哪些 `links` 是僅此檢視；對應的 `kind` 仍是 `accepted` |

### 關聯 kind 相容

| 來源 | 寫入 `ViewLink.kind` | 僅此檢視標記 |
|------|----------------------|--------------|
| 啟用包 `origin: foreign-key` | `foreign-key` | 否 |
| 啟用包 `origin: declared` | `accepted` | 否 |
| 提案新造、人接受 | `accepted` | 是，且理由必填 |

手儲存路徑若寫入未聲明且未帶「經接受提案」標記的關聯，仍回 `link_undeclared`。

## SQL 與結構對齊

編成規則（產品層）：

- 恰好一句 `SELECT`。
- `FROM`／`JOIN` 的表集合等於已接受 `sources`。
- 每一段 `JOIN` 的兩端等於已接受 `links` 的表與欄（允許方向對調，與十一月 AD-5 相同）。
- `SELECT` 清單等於已接受輸出欄；不投影未接受的欄。
- `WHERE`／參數對應已接受 `parameters`；值以綁定傳入，不拼接字面。
- 不產生 `INSERT`／`UPDATE`／`DELETE`／`DDL`。

方言：

- 依平台連線的 `ServerType`：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。
- 引號、參數佔位、列數上限語法（`LIMIT`／`TOP`／`FETCH`）由架構 AD 收束。
- 產品驗收先打 Sqlite。

對齊檢查：若檔內 `sql` 與依結構重編的結果不一致，以重編為準並作廢 `sqlValidated`。

## 樣本驗證

| 項目 | 規則 |
|------|------|
| 連線 | 僅 `Annotation:PlatformConnection`；規則同十一月 AD-2 |
| 列數上限 | 20 |
| 逾時 | 必有；毫秒數由架構 AD 定 |
| 成功 | 回傳 ≤20 列給當次畫面；不寫檔 |
| 失敗 | 超過上限、語法錯誤、權限錯誤、逾時、連線不符 → 不得發布 |
| 個資 | 不遮罩；沿用註記包處理文字；因不留存而降低殘留面 |

建議錯誤代碼形狀（架構可微調字面，須穩定且進測試）：

| 代碼 | 時機 |
|------|------|
| `proposal_no_active` | 沒有啟用包 |
| `proposal_outside` | 表或欄在包外 |
| `link_reason_required` | 僅此檢視的關聯理由空白 |
| `sample_unavailable` | 連線零筆／多筆／非平台來源 |
| `sample_failed` | 語法、權限、逾時或其他執行失敗 |
| `sample_truncated` | 結果超過上限 |
| `publish_not_validated` | 尚未通過樣本驗證 |
| `publish_note_incomplete` | 列粒度或用途空白 |

既有 `link_undeclared`、`confirm_blocked`、`forbidden` 等代碼維持。

## ViewDispatch 分支

```text
已發布檢視被呼叫
  ├─ sql 非空白且 sqlValidated
  │    → 以綁定參數執行該 SQL 一次，投影輸出欄
  └─ 其餘
       → 既有 ViewDispatch（單表 query / 多表記憶體 join）
```

不在這一輪改寫無 SQL 的舊文件。不在註記包再確認時重跑 SQL。

## 提案埠

| 項目 | 產品約束 |
|------|----------|
| 輸入 | 啟用包（表、欄、註解、已聲明關聯）+ 分析師問題 |
| 輸出 | 結構、理由、建議 SQL |
| 衝突 | 建議 SQL ≠ 依結構重編 → 採重編 |
| 測試 | 固定提案實作：只用已聲明關聯；提出僅此檢視關聯；包外表欄被拒 |
| 廠商 | 不綁定；可替換 |

宿主行程、提示詞與模型組態留給架構；PRD 不指定供應商。

## 與再確認的接點

`POST /annotations/confirm` 的檢視落點檢查（既有 `confirm_blocked`）必須把僅此檢視的關聯端點算進去：來源表、輸出欄、`links` 兩端表與欄。確認成功不改 `views.json`，不重設或重跑 `sqlValidated`。

## 明確不做的技術動作

- 不把檢視落地成資料庫實體 `VIEW` 或實體表。
- 不在 `AnnotationPackage` 增加「來自檢視的關聯」集合。
- 不把樣本列快取到磁碟或第二份 JSON。
- 不讓設計站呼叫待轉庫的 `GET /meta` 來通過驗證。
- 不新增第三種 `ViewLink.kind` 字面。
