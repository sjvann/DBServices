# DBServices

**現代化多資料庫 ORM 工具包** — 基於 Dapper，統一存取 SQLite、SQL Server、MySQL、Oracle 與 PostgreSQL。

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Dapper](https://img.shields.io/badge/Dapper-2.1.66-blue.svg)](https://github.com/DapperLib/Dapper)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/sjvann/DBServices/blob/master/LICENSE.txt)

## 為什麼選擇 DBServices

- **一個介面、五種資料庫**：`IDbService` 對齊 CRUD、結構探索與參數化 SQL。
- **Session 協定**：CLI、Web、C# SDK 都走 `IQuerySession`（Query／Call／Utility）。
- **連線用完即還**：一般操作從 pool 取連線、用完 Dispose；只有事務期間才持有連線。
- **現代化整合**：依賴注入、建構器、重試、驗證、快取、遷移與多資料庫彙整。

## 三分鐘上手

```csharp
using DBServices;

var db = MainService.UseSQLite("Data Source=app.db");
string[]? tables = db.GetAllTableNames();
var user = db.GetRecordById(1, "Users");
```

```bash
dotnet run --project DbServices.Cli -- --provider sqlite --connection "Data Source=app.db"
dbsql> \dt
dbsql> SELECT * FROM Users;
```

## 文件導覽

| 章節 | 內容 |
|------|------|
| [入門](getting-started/index.md) | 安裝、套件、第一個程式 |
| [指南](guides/index.md) | CRUD、查詢、Session、事務、遷移、安全、效能 |
| [架構](architecture/index.md) | 分層、連線範圍、刻意不做的事 |
| [CLI](cli/index.md) | `dbsql` 互動與批次模式 |
| [Web API](web/index.md) | REST 端點與啟動方式 |
| [API 參考](api/index.md) | 由原始碼產生的型別文件 |

## 支援的資料庫

| 資料庫 | 工廠方法 | 連線字串範例 |
|--------|----------|--------------|
| SQLite | `UseSQLite` | `Data Source=app.db` |
| SQL Server | `UseMsSQL` | `Server=localhost;Database=App;Trusted_Connection=true` |
| MySQL | `UseMySQL` | `Server=localhost;Database=app;Uid=root;Pwd=***` |
| Oracle | `UseOracle` | `Data Source=localhost:1521/XE;User Id=hr;Password=***` |
| PostgreSQL | `UsePostgreSQL` | `Host=localhost;Database=app;Username=postgres;Password=***` |

## 原始碼與授權

- 儲存庫：[github.com/sjvann/DBServices](https://github.com/sjvann/DBServices)
- 授權：MIT
