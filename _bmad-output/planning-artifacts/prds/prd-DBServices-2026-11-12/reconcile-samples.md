# Reconcile — Samples.xlsx / LH_TXN_MLST.sample.dbml

對照驗收樣本與 PRD／addendum。不改 PRD。SM-1 要求匯出 DBML 的表名、欄名、型別、索引與 note／Note 對上 `LH_TXN_MLST.sample.dbml`。

## What the input says

`Samples.xlsx` 只有工作表 `LH_TXN_MLST`，範圍 A1:N15。沒有名為 `00.資料總表` 的工作表。具名範圍只有列印標題 `LH_TXN_MLST!$1:$1`。

第 1 列是表頭。A–N 依序為：H_檔案名稱、H_欄位序號、H_欄位名稱、H_資料類型長度、資料描述、H_個資去識別化處理、S_檔案名稱、S_欄位序號、S_欄位名稱、S_資料類型、S_資料長度、S_唯一索引、資料備註、允許空白。與 addendum 契約表相同。

第 2–12 列是欄位列。A、G 多為公式 `=$A$2`、`=$G$2`，快取值是 `T_LH_TXN_MLST`、`LH_TXN_MLST`。J（S_資料類型）在第 2–6 列是 `=REPT("X",K)` 的快取遮罩；第 7 列 MLST_NO 的 J 是字面 `XXXX`（沒有公式）。第 8–11 列的 J 也是字面遮罩。F（個資）全空。

L 為 `V` 的只有第 2–7 列，S 序 1–6：OFFICE_CODE、CNTR_NO、ACC_DATE、TOUR_NO、SEGC_NO、MLST_NO。N 為 `Y` 的只有第 10 列 SSO_ID。

第 12 列 TABLE_DATE：C=`TABLE_DATE`，D=`STRING`，E 為兩行「資料轉入時間」與「(yyyy-MM-dd hh:mm:ss:sss)」，G–L 皆空，M=`紀錄ETL建立該筆紀錄的時間，以利日後處理資料`，N 空。

第 13 列不是欄位。N13 為 `=COUNTIF(N2:N12,"Y")`，快取值 `1`。第 15 列只有空的 G。

第 14 列是表級說明列。A14 的儲存格文字與超連結都是 `'00.資料總表'!A1`，目標工作表不在此活頁簿。說明正文在 D14（H_資料類型長度欄），不是 E 或 M：

`Hadoop中郵務局號+段別代號+帳務日期+班次+區段代碼+清單號碼非UK,因為是異動append`

H 名與 S 名不同的只有 OFFICE_CODE（H=`POST_BRH_NO`，S=`OFFICE_CODE`）。MLST_NO：D=`CHAR(7)`，K=`4`，J=`XXXX`。ACC_DATE：D=`DATE`，K=`7`，J 快取 `XXXXXXX`；E 含「西元年YYYY-MM-DD」，M 含「民國年YYYMMDD」。

`LH_TXN_MLST.sample.dbml` 的表名是 `LH_TXN_MLST`，11 欄，順序與第 2–12 列相同。型別為 `char(n)`、`date`、`varchar(10)`，TABLE_DATE 為 `text`。除 SSO_ID 外皆 `[not null]`。欄位 note 用全形 `；` 連接；儲存格內換行已折成 `；`；資料描述與資料備註相同的片段只留一次。

- OFFICE_CODE：`郵務局號；H 欄名 POST_BRH_NO；畫面格式 XXXXXX`
- ACC_DATE：`帳務日期；西元年YYYY-MM-DD；民國年YYYMMDD；畫面格式 XXXXXXX`（沒有 `S 資料長度 7`）
- MLST_NO：型別仍 `char(7)`，note 為 `清單號碼；靠左，右補空白；畫面格式 XXXX；S 資料長度 4`
- SSO_ID：`varchar(10)`，沒有 `not null`
- TABLE_DATE：`資料轉入時間；(yyyy-MM-dd hh:mm:ss:sss)；紀錄ETL建立該筆紀錄的時間，以利日後處理資料`（沒有 `H 欄名`）

索引名 `ix_lh_txn_mlst_append`，欄為上述六個 V 欄，note 為 `S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append`。這句不在活頁簿任何儲存格。

表 Note 為：

`來源檔 T_LH_TXN_MLST。Hadoop 中郵務局號+段別代號+帳務日期+班次+區段代碼+清單號碼非 UK，因為是異動 append。`

sample DBML 沒有整包功能說明，也沒有 `'00.資料總表'!A1`。

## Covered

- 表頭 A–N、單表、表名取 G 的 `LH_TXN_MLST`、型別正規化（`CHAR(n)`→`char(n)`、`VARCHAR(10)`→`varchar(10)`、`DATE`→`date`、`STRING`→`text`）與 addendum 一致，也與 sample 一致。
- OFFICE_CODE 的 H 名：規則「H 名與 S 名不同才加 `H 欄名 {C}`」產生 `H 欄名 POST_BRH_NO` 與 `畫面格式 XXXXXX`。與 sample 一致。
- SSO_ID 允許空白為 `Y`，沒有 `[not null]`。與 sample 一致。其餘欄位列 N 為空白，sample 皆為 not null。
- MLST_NO 長度差：型別用 H 的 `char(7)`，note 另加 `S 資料長度 4` 與畫面格式 `XXXX`。與 sample 一致。
- 六個 `V` 依 S 序組成複合索引欄位清單。與 sample 的索引欄序一致。
- 個資欄有值才寫進 note。樣品 F 全空，sample 沒有個資句子。此方向一致，但沒有非空 F 可當預期字串（見缺口 6）。

## Gaps (6)

### 1. 欄位 note 合併規則與 sample 字串不同

severity: high

樣本：同一欄的資料描述（E）與資料備註（M）都要進入 note。換行折成 `；`，相同片段只留一次，再接畫面格式等後綴。ACC_DATE 的 E 是「帳務日期／西元年YYYY-MM-DD」，M 是「帳務日期／民國年YYYMMDD」，sample 成為 `帳務日期；西元年YYYY-MM-DD；民國年YYYMMDD；畫面格式 XXXXXXX`。TABLE_DATE 同時留下 E 的時間格式與 M 的 ETL 句。TOUR_NO、TRF_TYPE、TRANS_CODE 的多行代碼與補位規則在 sample 裡也是 `；`，不是儲存格原文換行。

PRD／addendum：note 規則寫的是「資料描述或資料備註」的業務文案，再以 `；` 接 H 欄名、畫面格式、S 長度、個資。這是二選一，不是兩邊去重合併。也沒寫儲存格內換行要折成 `；`。另寫「代碼值與補位規則保留在 note 原文」，依字面會留下換行。

測試者若只取 E，ACC_DATE 會掉「民國年YYYMMDD」，TABLE_DATE 會掉 ETL 句。若只取 M，ACC_DATE 會掉「西元年YYYY-MM-DD」。若保留換行，SM-1 的 note 全文比對失敗。

### 2. 表 Note 的儲存格、標點，以及懸空總表連結

severity: high

樣本：表 Note 的材料在第 14 列 D 欄，不是資料描述或資料備註。原文沒有「來源檔」前綴，`Hadoop` 與「中」之間無空白，`非UK` 無空白，逗號是半形 `,`，`異動append` 無空白，也沒有結尾句號。sample 改寫成 `來源檔 T_LH_TXN_MLST。Hadoop 中…非 UK，因為是異動 append。`。A14 是指向不存在工作表的超連結 `'00.資料總表'!A1`。第 13 列的 COUNTIF 結果 `1` 不是欄位，也不是 Note。

PRD／addendum：只說表級說明可出現在欄位列之後，Note 來自 H_檔案名稱與「表級說明列」，並用自己的說法「Hadoop 組合鍵非 UK、因異動 append」帶過。沒有寫正文在 D14、要忽略 A14 超連結、要忽略 N13 計數列，也沒有前綴模板 `來源檔 {H檔名}。` 與空白／全形逗號／句號的改寫規則。addendum 又寫樣品若只有單表，總表連結文字可併入整包功能說明。sample DBML 沒有這段連結文字。

測試者依「欄位列之後的儲存格」可能把 A14 或 N13 寫進 Note，或把 D14 原文原樣接上來源檔名。兩種結果都對不上 sample 的表 Note。

### 3. 索引名稱與索引 note 無法從活頁簿推出

severity: high

樣本：索引名稱是 `ix_lh_txn_mlst_append`。索引 note 是 `S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append`。活頁簿沒有這兩個字串。D14 是另一句 Hadoop／非 UK 文字，而且它進的是表 Note，不是索引 note。

PRD／addendum：對應表只要求把 `V` 欄依 S 序組成複合索引，note「需能表達」有勾記以及非 UK／append，「可來自表級說明」。名稱沒有生成規則。`ix_lh_txn_mlst_append` 與那句索引 note 只出現在 addendum 的 JSON 例子裡，不是映射算法。FR-4 允許人在說明裡寫「不是真正的唯一鍵」，但 SM-1 要求匯出結果直接對上 sample。

測試者若把表級說明放進索引 note，索引 note 會變成 Hadoop 那句（或再加上標點改寫），表 Note 與索引 note 內容撞在一起，且名稱仍無從產生。只抄 JSON 例子可以過這一張表，規則本身不能重現 sample。

### 4. TABLE_DATE 沒有 S 側時，主對應表與特例互相矛盾

severity: medium

樣本：第 12 列 G–L 皆空，仍收入 `LH_TXN_MLST`，欄名用 H 名 `TABLE_DATE`，型別 `text`，N 空所以 not null，排在最後。note 只有 E 與 M，沒有 `H 欄名 TABLE_DATE`，也沒有畫面格式。

PRD／addendum：主對應表寫欄名等於 S_欄位名稱（I）。這一列 I 是空的。特例只說「欄名用 H／既有對應規則」產出 sample 的 `TABLE_DATE text`，沒有定義空 I 時改取 C、空 S 名不算「H 名與 S 名不同」、以及欄序沿工作表列序把此列放最後。一般規則「H 名與 S 名不同就加 H 欄名」套到空的 S 名會多出 `H 欄名 TABLE_DATE`。sample 沒有這段。

測試者依主表會丟掉此欄或得到空白欄名；依一般 H 名規則會多一段 note。型別 `text` 與 not null 本身可由 STRING 與空白 N 推出。

### 5. J 要讀計算結果；沒有括號長度的型別不能當長度衝突

severity: medium

樣本：多數 J 是 `REPT("X",K)` 的快取值（ACC_DATE 為七個 X）。MLST_NO 的 J 是人手寫的 `XXXX`，不是公式，也不是七個 X。ACC_DATE 的 K 為 7、D 為 `DATE`，sample 只有畫面格式，沒有 `S 資料長度 7`。TABLE_DATE 的 K 為空，同樣沒有 S 長度片段。

PRD／addendum：把 J 描述成已是遮罩字串（`XXXXXX`、`X`）。沒有寫這些儲存格很多是公式，note 必須用快取值，不能寫入 `REPT("X",Kn)`。長度規則是「K 與 D 推得的長度不一致才加 S 資料長度」。DATE、STRING 推不出括號長度。規則沒有排除它們。MLST_NO 的 `CHAR(7)` 對 `4` 這條例外本身是對的（見 Covered）。

測試者若讀公式文字，畫面格式會變成 `REPT(...)`。若把 DATE 的 K=7 當成不一致，ACC_DATE 的 note 會多出 sample 沒有的 `S 資料長度 7`。

### 6. 活頁簿裡的產品構想，PRD 沒有接住

severity: low

樣本還帶這些結構，sample DBML 與 PRD 都沒有對應欄位或驗收句：

- N13 的 `COUNTIF` 是「允許空白 = Y」的筆數核對（結果 1，即只有 SSO_ID）。契約沒有此核對列。把它當成欄位列會多出一欄。
- A14 代表一份活頁簿級「00.資料總表」。PRD 開放問題 3 把這個名字當成另一種尚未採用的版型；本檔只有壞連結。addendum 允許把連結文字併入整包說明，但驗收 DBML 沒有整包說明，也沒有該連結文字。
- ACC_DATE 同時是 SQL `DATE`（描述為西元 YYYY-MM-DD）與七碼民國畫面（M 為 YYYMMDD）。PRD 沒有日曆種類，只會落在自由文字 note，而且還依賴缺口 1 的合併才不會掉其中一側。
- 個資欄 F 在契約裡有 note 子句，樣品卻全空。sample 給不出「F 有值時 note 要長什麼樣子」。
