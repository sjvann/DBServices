---
title: DBServices Agent 資料平面 — 技術附錄
status: draft
created: 2026-10-11
updated: 2026-10-15
---

# Addendum: 技術邊界

本附錄承接 [brief.md](brief.md) 與 [prd.md](prd.md)。它記錄已決定的約束、被拒絕的做法，以及留給架構技能的項目。它不是實作設計。

## 已決定的約束

1. **資料路徑維持在現有 C#。** `IQuerySession`、`IFunctionRegistry`、Provider、公開的 `IDbService`／`IDbServiceAsync` 簽名不因本產品線改寫。SQL、連線、方言留在這條路徑。
2. **宿主不綁定引擎。** REST、GraphQL、MCP 取得 `IDbService` 的方式是 [DbServices.Api.Shared/DbServiceFactory.cs](../../../DbServices.Api.Shared/DbServiceFactory.cs)。工廠依 `ServerType` 呼叫既有的 `MainService.UseSQLite`、`UseMsSQL`、`UseMySQL`、`UseOracle`、`UsePostgreSQL`。新宿主不直接 new 某一個 Provider，也不在工具描述裡寫死引擎。
3. **MCP 宿主留在 DBServices 的 API 層。** 它與 `WebService`、`GraphQLService`、CLI 並列，共用 `DbServices.Api.Shared`。2026-10-15 起，它向 Agent 廣告的是已發布模組視圖，不是表級 Call。表級 Call 若仍轉發，只作營運除錯，且與 Discovery 清單分開。
4. **P0 不部署 Python。** 語句解析、版面建議、敘事從 P1 才需要。那些步驟使用編譯後的 Python 側車，由 C# 呼叫。
5. **連線字串不進工具、錯誤、稽核、側車輸入。** 側車以後只接收連線名稱、工具名稱、結構摘要、語句與範本草稿。

## MCP 宿主放在哪裡

放在新的宿主專案，而不是把 Core 改成會話伺服器。宿主讀取自己的具名連線組態（見 PRD FR-10），對每一筆呼叫 `DbServiceFactory.Create`，再把 `IDbService` 上已有的函數註冊表暴露為工具。

[DbServices.Core/Functions/FunctionRegistry.cs](../../../DbServices.Core/Functions/FunctionRegistry.cs) 的 `tables`、`fields`、`get`、`query`、`count`、`exists` 仍是表級行為的來源。它們不是 Agent 的 Discovery。宿主不複製這些函數的 SQL，也不為模組視圖另寫五段引擎專用 SQL。`insert`、`update`、`delete`、`sql` 保持不廣告、不轉發。

模組視圖的定義存在產品自己的目錄。執行時由宿主依該定義做唯讀讀取，不在來源庫下 `CREATE VIEW`。定義如何存放、關聯路徑如何表示，留給架構；產品約束是：解說未確認不發布、來源表不連通不發布、Discovery 與除錯清單分開。

## 為什麼不放進 ExportData、也不合併兩個專案

ExportData 的產品是本機分析包。它的「模組」是為了出圖圈選的一群表，不出查詢，不給 Agent。那邊已有四種引擎的目錄關聯讀取，那是設計時的輸入，不是服務表面。

DBServices 已有資料表 Web Server、具名連線、稽核與 MCP 宿主。模組視圖的發布與呼叫接在這條服務上。

合併倉庫不會產生視圖定義，只會把「一位分析師、localhost、分析包」和「可被 Agent 呼叫的宿主」綁成同一個發布。共用的只有目錄與外鍵讀取。第一條視圖的路徑由人寫進定義。拖拉設計器以及是否抽出共用程式庫，都等這條手寫視圖先讓 Agent 只靠解說選對工具之後再做。分析包若以後要提供已確認用途，用檔案交接，不共用行程。

傳輸（stdio、HTTP 或兩者）未指定，見 PRD 第 8 節。選定時仍須滿足：外部 MCP 客戶端能完成 UJ-1，且連線字串不出現在傳輸的工具結構裡。

## 為什麼工具不是那八條 REST

[WebService/DbWebService.cs](../../../WebService/DbWebService.cs) 的路由是資料表 CRUD：

- `GET /{resource}` 的查詢是單一 `Key` 與 `Value`
- `GET /{resource}/{id}` 的 `id` 是整數
- 沒有對等的 `count`、`exists`，也沒有一次帶多個等值條件的查詢

Session 的 `query`、`count`、`exists` 已經接受多個等值篩選。MCP 對齊這組 Call，REST 路徑與形狀維持不變。把八條路由包成工具，會讓 Agent 繼承單鍵查詢，同時仍解決不了稽核與「不收連線字串」這兩件事。

## 文件漂移：不是 Sqlite 綁定

[docs/web/index.md](../../web/index.md) 仍寫：`SetupServer` 只啟用 Sqlite，其他引擎名稱會略過端點。現行程式沒有 `SetupServer`。`UseDbWebService` 把 `ServerType` 交給 `DbServiceFactory`。

[WebService/Program.cs](../../../WebService/Program.cs) 在組態缺省時把名稱設為 `Sqlite`。那是預設值。未知名稱時工廠回傳 null，宿主應拒絕該連線並保持進程（PRD FR-1、FR-13）。

這份文件的修正不在 P0 完成定義內。後續改文件時刪除 `SetupServer` 那段，改寫成工廠的五個名稱。

## Python 側車（P1 起）

側車是編譯後的 Python 程式，由 C# 進程啟動並傳入一次請求、讀回一次結果。它不是第二條資料庫驅動，也不是嵌進 Core 的直譯器。

| 側車可以做 | 側車不做 |
|------------|----------|
| 把一句話收成連線名稱、唯讀工具名稱、等值篩選 | 組 SQL、開連線、執行查詢 |
| 依欄位清單建議範本元件與繫結 | 保存連線字串或業務列資料到自己的儲存 |
| 依已返回的列資料寫摘要 | 在 C# 呼叫失敗時改走自由 `sql` |

契約形狀留給架構技能。本附錄只固定方向：輸入與輸出是結構化資料；C# 驗證輸出是否為已廣告的唯讀工具與已登記的連線名稱，不合法就不執行。側車程序失敗時，對談得到可讀錯誤，資料層沒有副作用。

P0 沒有這個進程，也沒有對它的建置步驟。

## 被拒絕的做法

- **用 Python 重寫資料平面或 MCP 直接帶資料庫驅動。** 五種引擎的連線、方言與函數已經在 C#。再做一套會分裂秘密存放與稽核。
- **把語言模型放進 Core。** 模型可更換，資料簽名不應跟著改。P0 的模型在外部 Agent；P1 以後的模型在側車。
- **為了 MCP 在宿主寫五段引擎專用查詢。** 與「只經工廠」衝突，也違反不自製 SQL planner 的架構契約。
- **P0 就開放 `sql` 或寫入，以便 Agent 更好答題。** 完成度看 SM-1 至 SM-6，不看工具數量。見 PRD 的 SM-C1。
- **把 MCP 放進 ExportData，或把兩個專案合併成一個新倉庫。** 使用者不同，完成定義不同。見上一節。
- **在來源庫建立 DDL 檢視表來充當模組視圖。** 會把方言、權限與變更腳本拖進這一版，也和 ExportData 不寫回舊庫的邊界衝突。
- **在手寫視圖證明 Discovery 之前先做拖拉設計器或共用關聯程式庫。** 先驗證的是 Agent 是否靠解說選工具。畫布與程式庫抽取都不提前。
- **Discovery 沒有已發布視圖時，回退廣告六個表級工具。** 那會把掃表重新變成公開目錄。空目錄就是空目錄。

## 留給架構技能的項目

下列項目不在本附錄做決定。決定時不得打開上面的約束。

- MCP 傳輸與新專案的組態格式（如何表達多筆具名連線）。
- 稽核的實體格式、保留天數，以及是否拒絕 `anonymous`。
- 側車的編譯產物形式（可執行檔或其他凍結產物）、C# 呼叫方式、逾時與版本對齊。
- 報表範本的檔案格式。設計器所在的前端專案不在這一刀；啟動條件見 PRD 第 6.2 節。
- 模組視圖定義的存放格式，以及營運除錯清單如何與 Discovery 分開。
- 人手寫關聯路徑的表示。不在這一刀抽取 ExportData 的關聯 SQL。

治理在架構階段要展開的只有三件已定方向：預設唯讀、秘密留在伺服器、每次呼叫可追溯。授權模型、資料分類與外送政策仍是 PRD 第 8 節的開放問題。
