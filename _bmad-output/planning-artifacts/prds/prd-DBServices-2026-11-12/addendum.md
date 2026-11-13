---
title: DBServices 資料來源註記 — 技術附錄
status: final
created: 2026-11-12
updated: 2026-11-12
---

# Addendum: 資料來源註記的技術邊界

本附錄承接 [prd.md](prd.md)。它記錄 Excel 欄位如何收進 DBML、註記包與現有 `SchemaGraph`／檢視設計的接點，以及產品約束下的存放邊界。它不是畫面設計，也不是專案配置。架構可以改媒介與行程，不能改這裡與 PRD 已寫死的約束。

既有檢視與報表的技術附錄仍有效，見 [../prd-DBServices-2026-10-12/addendum.md](../prd-DBServices-2026-10-12/addendum.md)。資料平面約束見 [docs/planning/agent-data-plane/addendum.md](../../../../docs/planning/agent-data-plane/addendum.md)。

## 已決定的約束

1. **註記是設計期前置，不是執行期查詢。** 確認過的註記包供給模組設計工具當目錄，並取代檢視與報表 PRD 的 FR-4。已發布檢視的讀列仍走既有 MCP 路徑。確認前，實體表名與實體欄名必須存在於二代平台資料源；待轉資料庫不能用來讀結構或通過確認。
2. **連線字串不進入註記站與匯出。** 請求、草稿、已確認包、DBML 與日誌都不新增連線字串欄位。人不能選擇具名連線。
3. **註記站不讀業務列。** 從資料庫只讀結構（表、欄、型別、鍵、外鍵）。Excel 匯入只讀活頁簿。
4. **不把註解寫回來源庫。** 也不在來源庫執行 `CREATE VIEW` 或任何結構變更。
5. **不改寫檢視文件形狀。** `ViewDocument` 仍是 id、name、purpose、confirmed、published、sources、links、parameters、columns。本 PRD 不新增、不刪除、不改變 `Confirmed` 的既有意義。改變的是 sources／links／columns 的候選來自哪裡。
6. **Excel 驗收對照固定。** `Requirements/Samples.xlsx` → `Requirements/LH_TXN_MLST.sample.dbml`。

## Excel 欄位契約（Samples.xlsx）

工作表範例：`LH_TXN_MLST`。第 1 列為表頭，後續列為欄位列；表級說明可出現在欄位列之後。

| 欄 | 表頭 | 角色 |
|----|------|------|
| A | H_檔案名稱 | H 側來源檔名，例如 `T_LH_TXN_MLST` |
| B | H_欄位序號 | H 側序號 |
| C | H_欄位名稱 | H 側欄名，例如 `POST_BRH_NO` |
| D | H_資料類型長度 | 實體型別來源，例如 `CHAR(6)`、`DATE`、`VARCHAR(10)`、`STRING` |
| E | 資料描述 | 業務說明、代碼值、補位規則；可多行 |
| F | H_個資去識別化處理 | 自由文字；v1 只保存，不執行 |
| G | S_檔案名稱 | 實體表名，例如 `LH_TXN_MLST` |
| H | S_欄位序號 | S 側序號 |
| I | S_欄位名稱 | 實體欄名，例如 `OFFICE_CODE` |
| J | S_資料類型 | 在樣本品中實際承載**畫面格式**遮罩（`XXXXXX`、`X`、`XX` 等），不是 SQL 型別 |
| K | S_資料長度 | S 側長度；可與 H 型別長度不一致 |
| L | S_唯一索引 | 勾記（樣本品為 `V`）表示該欄參與「唯一索引」語意 |
| M | 資料備註 | 補充說明；可與資料描述合併進 note |
| N | 允許空白 | `Y` 表示可空；空白或非 Y 視為 not null |

### 列到 DBML 欄的對應

| DBML | 來源規則 |
|------|----------|
| `Table` 名稱 | 該表列的 `S_檔案名稱`（G）。同一 G 值聚成一張表。 |
| 欄名 | `S_欄位名稱`（I） |
| 型別 | 由 `H_資料類型長度`（D）正規化：`CHAR(n)`→`char(n)`，`VARCHAR(n)`→`varchar(n)`，`DATE`→`date`，`STRING`→`text`；其餘小寫保留。 |
| `[not null]` | `允許空白`（N）不是 `Y` |
| 欄位 `note` | 把資料描述（E）與資料備註（M）依換行拆段，相同片段只留一次，再用全形分號 `；` 接起來。S 側欄名有值且與 H 側欄名不同時，再加 `H 欄名 {C}`。J 用儲存格計算結果，不用公式文字；有值才加 `畫面格式 {J}`。只有 D 含括號長度、且 K 與該長度不同時，才加 `S 資料長度 {K}`。`DATE`、`STRING` 不加 S 資料長度。F 有值才加個資處理說明。 |
| 欄名與所屬表 | 欄名是 S 側欄名。S 側欄名空白時，欄名用 H 側欄名，且 note 不加 `H 欄名`。G 空白的列併入上一列的實體表，欄序沿工作表由上到下。 |
| `indexes` | 同一張表中 `S_唯一索引` 為 `V` 的欄，依 S 欄位序號組成複合索引。名稱是 `ix_` + 表名全小寫 + `_append`。索引 note 為 `S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append`。 |
| 表級 `Note` | `來源檔 {H_檔案名稱}。` 再接欄位列之後第一個有說明的 D 欄。中文與英數之間補空白，半形逗號改成全形逗號，缺句號就補上。忽略計數公式列，也忽略指向別張工作表的超連結。 |
| 整包功能說明 | 註記站的人工欄位。樣品 DBML 不含這段。壞掉的「00.資料總表」連結不寫進說明，也不寫進表 Note。 |

### 樣品對照要點（LH_TXN_MLST）

- `OFFICE_CODE`：H 名 `POST_BRH_NO` 不同，note 含 H 欄名與畫面格式 `XXXXXX`。
- `MLST_NO`：H 為 `CHAR(7)`，S 資料長度為 `4`，note 含 `S 資料長度 4`；DBML 型別仍用 H 的 `char(7)`。
- `SSO_ID`：允許空白為 `Y`，故無 `not null`。
- `TABLE_DATE`：G–L 為空，欄名用 H 側 `TABLE_DATE`，型別 `text`，note 只有描述與備註，沒有 `H 欄名`。
- 複合索引六欄皆勾 `V`。名稱 `ix_lh_txn_mlst_append`。索引 note 用上面的固定句，不用 D14 的 Hadoop 句子。D14 只進表 Note。

## 資料庫讀入與 SchemaGraph

現況：[`DbServices.Mcp/SchemaGraph.cs`](../../../../DbServices.Mcp/SchemaGraph.cs) 的 `Read` 經已登記連線取出表名、欄名、型別、主鍵、外鍵（`ReferencesTable`／`ReferencesColumn`）。[`SchemaColumnInfo`](../../../../DbServices.Mcp/SchemaGraph.cs) 沒有註解欄。

產品接點：

| 項目 | 行為 |
|------|------|
| 誰呼叫 | 僅資料治理／註記站。模組設計工具不再把 `SchemaService.GraphAsync` 的即時結果當規劃目錄。 |
| 帶入註記包 | 表、欄、型別、主鍵→可標示；外鍵→預填為已聲明關聯（可再編輯理由）。 |
| 註解 | 預設空字串，等人補。 |
| 連線規則 | 恰有一筆組態完整具名連線，且其名稱等於 `Annotation:PlatformConnection`，才讀取。零筆、多筆，或名稱不符時不讀取，讀列次數為 0。見 architecture-DBServices-2026-11-12 AD-2。 |
| 預覽 | 不讀業務列。現有 `SchemaGraph.Preview` 固定不讀列的約束繼續有效。 |

## 註記包形狀（產品欄位；放在資料庫裡的可管理 DBML）

```json
{
  "id": "pkg-lh-txn",
  "title": "郵務清單異動",
  "purpose": "整包功能說明",
  "status": "confirmed",
  "tables": [
    {
      "name": "LH_TXN_MLST",
      "note": "來源檔 T_LH_TXN_MLST。…",
      "columns": [
        {
          "name": "OFFICE_CODE",
          "type": "char(6)",
          "notNull": true,
          "primaryKey": false,
          "note": "郵務局號；H 欄名 POST_BRH_NO；畫面格式 XXXXXX",
          "piiNote": "",
          "source": {
            "hName": "POST_BRH_NO",
            "hType": "CHAR(6)",
            "screenFormat": "XXXXXX",
            "sLength": "6",
            "uniqueIndex": true
          }
        }
      ],
      "indexes": [
        {
          "name": "ix_lh_txn_mlst_append",
          "columns": ["OFFICE_CODE", "CNTR_NO", "ACC_DATE", "TOUR_NO", "SEGC_NO", "MLST_NO"],
          "note": "S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append"
        }
      ]
    }
  ],
  "links": [
    {
      "from": "child",
      "fromColumn": "parent_id",
      "to": "parent",
      "toColumn": "id",
      "kind": "declared",
      "note": "為什麼這樣連"
    }
  ],
  "dbml": "Table LH_TXN_MLST { ... }"
}
```

約束：

- `status` 至少區分 `draft` 與 `confirmed`。
- 無 `connectionString`、無 `rows`。
- `source` 區塊保留 Excel／結構對照用的原始線索，方便再讀與差異比對；設計端主要消費 `name`／`type`／`note`／`links`。
- v1 一次只啟用一份 `confirmed` 包供模組設計工具讀取。新確認若會使已保存檢視的來源表、輸出欄或關聯端點落到新包之外，拒絕確認。通過時新包成為唯一啟用包；先前的包保留，狀態不再是設計目錄所讀的那一份。不刪除檢視，也不取消發布。

## 與模組設計工具的接點

現況：[`Workbench.Web/Components/Views.razor`](../../../../Workbench.Web/Components/Views.razor) 經 [`SchemaService.GraphAsync`](../../../../Workbench.Core/SchemaService.cs) 載入左側資料表；檢視持久化在 [`ViewCatalog`／`ViewDocument`](../../../../DbServices.Mcp/ViewCatalog.cs)。

| 接點 | 變更 |
|------|------|
| 左側目錄 | 改讀已確認註記包的 tables／columns／notes／links |
| 空狀態 | 無已確認包時說明「資料來源尚未註記」。已確認包裡欄註為空白的欄，清單標「尚未註記」 |
| 選表選欄 | 必須落在註記包內 |
| 關聯 | 必須等於註記包 `links` 的兩端表與欄。分析師不在這一階段另造關聯 |
| `ViewDocument` | 欄位集合不變 |
| 即時 `SchemaGraph` | 留在註記站；設計站規劃目錄不再依賴它 |
| 入口導覽 | [`EntryNames`](../../../../Workbench.Contracts/Names.cs) 新增資料治理；[`WorkbenchLayout.razor`](../../../../Workbench.Web/Components/WorkbenchLayout.razor) 為該入口提供獨立導覽，不與檢視／樣式／報表共用工作內容 |

## 與既有 PRD 的關係

| 文件 | 關係 |
|------|------|
| 檢視與報表 PRD | 不改寫其檔案。本 PRD 取代其 FR-4、UJ-1 的規劃期目錄句、§11／§12／§13 的規劃期通道句，以及附錄「規劃期的表結構」。回答開放問題 8：目錄＝已確認註記包；讀結構與確認對帳只對二代平台資料源。檢視文件形狀、發布、樣式、Chat、報表生成紀錄維持。 |
| 資料平面 PRD | 執行期讀列與已發布檢視的工具目錄規則不變。已確認註記包另外經 MCP 提供給後續設計階段引用，該表面不讀業務列。 |
| 本 addendum | 機制與對應表。可管理的 DBML 放在 `catalog/annotations.json`。錯誤碼見 architecture-DBServices-2026-11-12 AD-1 與 AD-3。 |

## 明確不做

- 把 DBML 或 note 寫回資料庫 COMMENT。
- 在註記站或設計站用待轉庫 `GET /meta` 充當二代平台結構。
- 個資遮罩執行、自由 SQL、人選連線、讀業務列。
- 合併資料治理與模組設計為同一左軌工作內容。
