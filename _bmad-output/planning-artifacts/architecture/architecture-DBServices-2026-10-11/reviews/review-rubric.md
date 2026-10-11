# Good-spine 審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices Agent 資料平面，2026-10-11）
- 高度：initiative（約束的是其下的功能，沒有父脊）
- 審查日：2026-10-11
- 鏡頭：good-spine 清單
- 判定：**revise**
- 未改骨幹

這份脊把 P0 工具契約、具名連線、分層與後續側車邊界寫成可執行規則，FR-1 至 FR-15 都有歸屬。它還不能往下拆功能：Session 的實際失敗通道沒有合法錯誤碼，而且 Deferred 裡有一項會讓兩個功能對「同一次呼叫算不算成功」做出相反選擇。

## 清單對照

| 清單項 | 結果 |
| --- | --- |
| 擋下層真實分歧、沒有漏 | 未過。工具清單、參數、連線目錄與稽核鍵有擋住；錯誤通道與稽核寫入失敗沒有。 |
| 每條 AD 的 Rule 可執行，且真能阻止其 Prevents | 未過。AD-7 的封閉碼表蓋不住現有 `QueryResult`；AD-1 的「只有兩個新專案」超出它要阻止的分歧；AD-2／AD-5 沒鎖住與 starter 相反的預設。 |
| Deferred 不會讓兩個單位分叉 | 未過。稽核追加失敗明示兩種行為都未採用。側車呼叫方式與範本格式亦同，但只影響 P1 之後。 |
| 點名的技術是現行版 | 過。2026-10-11 查 NuGet：`ModelContextProtocol` 與 `ModelContextProtocol.AspNetCore` 的最新穩定版都是 2.2.0（2026-08-13）。`net10.0` 與現有專案一致。`HttpServerSessionMode.Stateless` 存在，且為該版預設。 |
| 追認棕地，而不是反過來改寫 | 未過。工廠五個 `ServerType`、`QueryOpcode.Call`、函式名稱與 `count`／`exists` 儲存格、WebService 的 `ConnectionStrings:default` 與 `ServerType:Name` 都追認正確。AD-8 把 `get` 的缺表說成空結果，與 `GetRecordById` 丟出例外、`QuerySession.Execute` 收成 `Kind == Error` 不符。 |
| 規格驅動時蓋住該規格的能力 | 過，附一處載體未定。FR-1 至 FR-15、P0 非目標，以及第 10 節後續能力的資料邊界都有 AD。成功 JSON 落在 MCP 的哪一種 content 還沒寫。 |
| 父脊的 AD 不被削弱 | 不適用。沒有父脊，也沒有 Inherited Invariants。 |
| 這個高度擁有的每個面向都已決定、延後或列為開放問題 | 部分過。部署（單一程序、位址在部署時決定、TLS 在環境終止、稽核留在同一台機器）、基礎設施（不新增雲端供應商、引擎仍走既有五個 Provider）已決定。授權的 P0 界線在 AD-6。營運只寫了重啟、失敗隔離與稽核保留；健康檢查、指標與日誌出口整段沒有著落。 |

棕地對過且脊沒有改寫的部分：方案是 net10.0 分層程式庫；`DbServiceFactory.Create(connectString, serverName)` 只認 `Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`，其餘回 null；`FunctionRegistry` 的六個唯讀函式與四個關閉函式名稱、中文說明，以及 `fields` 的 `name`／`type`／`pk`／`notNull`、`count` 與 `exists` 的單格欄名，都和 AD-2、AD-9 一致；`WebService` 仍是單一 `ConnectionStrings:default` 加 `ServerType:Name`，脊明確不讀它們。宿主專案不直接參考五個 Provider 專案，這點與 `WebService` 相同。

## Critical

### C1. Session 失敗沒有合法表示，而且脊把棕地的錯誤通道寫反了

`QuerySession.Execute` 會把例外收成 `QueryResult.Kind == Error` 而不是往外丟，`get` 的 `GetRecordById` 會丟 `DbServiceException`，`query` 的 `GetRecordByKeyValues` 則吞掉 SQL 例外並變成空結果；AD-7 的封閉碼表沒有這條通道，AD-8 卻要求 `get` 缺表時沿用空結果，AD-9 又只定義成功形狀，Deferred 還把「Session 丟出」留到第一個實機錯誤再定碼，於是兩個功能可以各自把同一次失敗做成空成功或自造錯誤碼。

**處置：** autofix

## High

### H1. 稽核追加失敗時呼叫是否仍回傳，Deferred 明示兩種都未採用

這是客戶端看得到的契約：一個功能可以在寫檔失敗時仍回傳列資料，另一個功能可以讓同一次呼叫失敗，而 AD-10 與 FR-14 都沒有選邊。

**處置：** discuss

### H2. AD-1 的「新程式只有 McpService 與 DbServices.Mcp」沒有限定在 P0

範圍與 AD-11 已預告對談、側車與範本會有後續表面，這句永久禁令會讓那些功能在遵守 AD-1 與做出自己的專案之間必須違反其中一邊。

**處置：** autofix

### H3. AD-2 與 AD-5 只寫結果，沒有鎖住 SDK 2.2.0 與結果相反的 starter 預設

照官方入門範例，`MapMcp()` 的 pattern 預設是空字串而不是 `/mcp`，工具的 `ReadOnly` 預設是 false、`Destructive` 預設是 true，因此一個照規則、一個照 starter 的功能會在端點與「是否破壞性」上分叉。

**處置：** autofix

## Medium

### M1. AD-6 把呼叫者唯一來源鎖成 HTTP 標頭 `X-DBServices-Caller`

Deferred 的 stdio 宿主若要沿用同一套 `DbServices.Mcp`，只能一律記成 `anonymous`，否則就得削弱這條已採用的規則。

**處置：** autofix

### M2. `connectionId` 與 `Connections` 鍵的比對沒有大小寫規則

`filters` 碰撞與 `fields` 的表名都已規定不區分大小寫，連線名稱卻沒有，組態繫結與目錄查找可以一個忽略大小寫、一個用序數比較。

**處置：** autofix

### M3. 附錄指名要由這份脊決定的側車呼叫方式與範本檔格式仍在 Deferred

AD-11 鎖住了資料邊界，但編譯產物、C# 呼叫方式、逾時、版本對齊，以及範本檔與設計器所在專案仍未定，P1 對談與 P2 範本可以各選一套程序間契約與檔案格式。

**處置：** discuss

### M4. 成功與錯誤 JSON 沒有規定落在 MCP 的哪一種承載

AD-7 與 AD-9 只規定文件形狀，沒有規定 structured content、text content 或兩者同時，外部客戶端與 P1 對談可以各讀一種通道。

**處置：** discuss

## Low

### L1. 營運觀測既沒有決定，也沒有放進 Deferred 或開放問題

部署拓樸、供應商策略、設定快照與稽核保留都有著落，健康檢查、指標，以及稽核檔以外的日誌出口則整段沉默，兩個部署單位可以各自加 HTTP 路由或日誌槽。

**處置：** defer

### L2. AD-3 把 `DbServiceFactory.Create` 寫成只接受目錄裡的連線字串

現行簽章是 `Create(string connectString, string serverName)`，第二引數必須是該筆的 `ServerType`；只寫連線字串會讓一個實作漏傳引擎名稱。

**處置：** autofix

### L3. 結構種子寫「五個 Provider 專案只被 DBService 參考」

`DBServiceTest` 另外參考了 `DbServices.Provider.Sqlite`；宿主規則本身仍然成立，這句過寬不改變 P0 的依賴方向。

**處置：** ignore

### L4. 零筆組態完整的連線時，程序能否啟動並廣告六個工具沒有寫死

AD-4 只說單一壞連線不結束程序，FR-10 只保證至少一筆合法連線時可以啟動，因此一個功能可以拒絕開機，另一個可以照常廣告工具。

**處置：** autofix

## 不構成發現

- 沒有父脊，不存在新 AD 削弱繼承規則的問題。
- 稽核保留天數、P0 以後的授權模型、非 `fields` 的缺表是否改成 `unknown_table`、以及 `docs/web/index.md` 的文件漂移，都已把 P0 行為鎖住，可以留在 Deferred。
- stdio 日後必須包同一套 `DbServices.Mcp` 且不改工具參數，這句本身不會讓工具契約分叉；分叉點只在呼叫者，見 M1。
- 點名的套件版本經 NuGet 核對為 2026-10-11 的最新穩定版。starter 預設與規則不一致的部分見 H3，不是版本過期。
