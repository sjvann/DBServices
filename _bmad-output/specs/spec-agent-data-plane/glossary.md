# 詞彙

- **資料平面** — Agent 用來讀取業務資料的工具面。執行落在既有 Session 與提供者。寫入不在 P0。
- **MCP 宿主** — P0 新增的 API 層程式。向 MCP 客戶端廣告工具、接收呼叫、轉成 Session Call、寫稽核。不是新的資料庫引擎。
- **具名連線** — 伺服器組態中的一筆連線，含名稱、`ServerType`、連線字串。工具只接受名稱。
- **connectionId** — 工具參數裡的具名連線名稱。
- **ServerType** — 交給 `DbServiceFactory` 的引擎名稱：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。
- **組態完整** — `ServerType` 是上列五個名稱之一，且連線字串非空白。
- **Session Call** — `IQuerySession` 的 Call 路徑上已註冊的函數。P0 使用其中的唯讀子集。
- **唯讀工具** — `tables`、`fields`、`get`、`query`、`count`、`exists`。
- **關閉工具** — `insert`、`update`、`delete`、`sql`。P0 不廣告、不執行。
- **filters** — `query`、`count`、`exists` 上的等值篩選物件。每個鍵是欄位名，值是等值條件，不是 SQL 片段。
- **稽核紀錄** — 一次工具呼叫的事後紀錄。要能回答誰、哪個連線、哪個工具、什麼條件、多少列或什麼錯誤。
- **呼叫者** — 傳輸層提供的客戶端識別。沒有或空白時為 `anonymous`，呼叫仍執行。
- **報表範本** — 後續能力。描述版面、元件與參數繫結，不含查詢結果。
- **Python 側車** — 後續能力。編譯後由 C# 呼叫，負責語句解析、版面建議與敘事。P0 不部署。
