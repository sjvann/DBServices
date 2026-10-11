---
title: DBServices Agent 資料平面 — 技術附錄
status: draft
created: 2026-10-11
updated: 2026-10-11
---

# Addendum: 技術邊界

本附錄承接 [brief.md](brief.md) 與 [prd.md](prd.md)。它記錄已決定的約束、被拒絕的做法，以及留給架構技能的項目。它不是實作設計。

## 已決定的約束

1. **資料路徑維持在現有 C#。** `IQuerySession`、`IFunctionRegistry`、Provider、公開的 `IDbService`／`IDbServiceAsync` 簽名不因本產品線改寫。SQL、連線、方言留在這條路徑。
2. **宿主不綁定引擎。** REST、GraphQL、MCP 取得 `IDbService` 的方式是 [DbServices.Api.Shared/DbServiceFactory.cs](../../../DbServices.Api.Shared/DbServiceFactory.cs)。工廠依 `ServerType` 呼叫既有的 `MainService.UseSQLite`、`UseMsSQL`、`UseMySQL`、`UseOracle`、`UsePostgreSQL`。新宿主不直接 new 某一個 Provider，也不在工具描述裡寫死引擎。
3. **P0 的新程式是 API 層的 MCP 宿主。** 它與 `WebService`、`GraphQLService`、CLI 並列，共用 `DbServices.Api.Shared`。它把 PRD 的唯讀工具轉成既有 Session Call。
4. **P0 不部署 Python。** 語句解析、版面建議、敘事從 P1 才需要。那些步驟使用編譯後的 Python 側車，由 C# 呼叫。
5. **連線字串不進工具、錯誤、稽核、側車輸入。** 側車以後只接收連線名稱、工具名稱、結構摘要、語句與範本草稿。

## MCP 宿主放在哪裡

放在新的宿主專案，而不是把 Core 改成會話伺服器。宿主讀取自己的具名連線組態（見 PRD FR-10），對每一筆呼叫 `DbServiceFactory.Create`，再把 `IDbService` 上已有的函數註冊表暴露為工具。

[DbServices.Core/Functions/FunctionRegistry.cs](../../../DbServices.Core/Functions/FunctionRegistry.cs) 的 `tables`、`fields`、`get`、`query`、`count`、`exists` 就是 P0 工具的行為來源。宿主不複製這些函數的 SQL。`insert`、`update`、`delete`、`sql` 保持不廣告、不轉發。

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
- **P0 就開放 `sql` 或寫入，以便 Agent 更好答題。** 完成度看 SM-1 至 SM-5，不看工具數量。見 PRD 的 SM-C1。

## 留給架構技能的項目

下列項目不在本附錄做決定。決定時不得打開上面的約束。

- MCP 傳輸與新專案的組態格式（如何表達多筆具名連線）。
- 稽核的實體格式、保留天數，以及是否拒絕 `anonymous`。
- 側車的編譯產物形式（可執行檔或其他凍結產物）、C# 呼叫方式、逾時與版本對齊。
- 報表範本的檔案格式與設計器所在的前端專案。

治理在架構階段要展開的只有三件已定方向：預設唯讀、秘密留在伺服器、每次呼叫可追溯。授權模型、資料分類與外送政策仍是 PRD 第 8 節的開放問題。
