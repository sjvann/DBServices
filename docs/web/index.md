# Web API

`WebService` 是 ASP.NET Core 最小 API，透過 `UseDbWebService` 把資料表暴露成 REST 資源。開發環境提供 OpenAPI 與 Scalar UI（`/scalar`）。

## 啟動

```bash
cd WebService
dotnet run
```

設定（`appsettings.json`）：

```json
{
  "ConnectionStrings": {
    "default": "Data Source=c://temp//Test.db;"
  },
  "ServerType": {
    "Name": "Sqlite"
  }
}
```

`Program.cs` 讀取 `ConnectionStrings:default` 與 `ServerType:Name`。目前 `SetupServer` **只啟用 Sqlite** 分支；其他引擎名稱會記錄錯誤並略過對應端點。要支援 SQL Server／MySQL 等，需在 `DbWebService.SetupServer` 接上對應的 `MainService.Use*`。

## 端點

基底路徑相對於應用根目錄。`{resourceName}` 為資料表名稱。

| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/meta` | 所有資料表名稱 |
| GET | `/{resourceName}/fieldSet` | 欄位結構 |
| GET | `/{resourceName}/valueSet?fieldName=` | 某欄位值集合 |
| GET | `/{resourceName}` | 整表或 `?Key=&Value=` 鍵值查詢 |
| GET | `/{resourceName}/{id}` | 依整數 Id |
| POST | `/{resourceName}` | JSON 物件新增 |
| PUT | `/{resourceName}/{id}` | JSON 物件更新 |
| DELETE | `/{resourceName}/{id}` | 刪除 |

有 `IDbServiceAsync` 時走非同步實作，否則退回同步。失敗回傳 ProblemDetails 或 404。

## 範例

```bash
curl -s http://localhost:5000/meta
curl -s http://localhost:5000/Users/1
curl -s -X POST http://localhost:5000/Users -H "Content-Type: application/json" -d "{\"Name\":\"Ada\",\"Age\":36}"
```

連接埠以 `Properties/launchSettings.json` 為準。
