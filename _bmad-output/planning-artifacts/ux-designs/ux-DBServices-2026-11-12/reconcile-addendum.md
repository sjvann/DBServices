# Reconcile — addendum.md

來源：`_bmad-output/planning-artifacts/prds/prd-DBServices-2026-11-12/addendum.md`

## 已進入骨架

- 註記站走 `WorkbenchLayout` 的獨立導覽，不與檢視、樣式、報表共用左軌工作內容。
- 狀態至少分草稿與已確認。v1 設計目錄只讀一份已確認包。
- 人不能選擇具名連線。待轉資料庫不能用來讀結構或通過確認。
- 欄位契約的結果會進編輯區：表、欄、型別、允許空白、唯一索引勾記、描述、個資文字、關聯。

## 未寫進骨架的質性內容

- Excel 列如何收成 DBML note 的細則（H 欄名、畫面格式、S 資料長度、全形分號、索引名稱 `ix_` + 表名 + `_append`、索引固定說明）留在 addendum，沒有復寫進體驗骨架。實作與驗收仍以 addendum 和 `LH_TXN_MLST.sample.dbml` 為準。
- 表級 Note 要忽略計數公式列與指向其他工作表的超連結。骨架沒有單獨寫這條。
- `piiNote` 與欄位 note 的分工、索引在設計清單是否出現，addendum 自己標成未寫。骨架只讓個資文字可編、索引勾記不可在註記站改。
