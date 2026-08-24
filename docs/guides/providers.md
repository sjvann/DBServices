# 資料庫提供者

每個引擎一個專案，繼承 `DataBaseService` 並提供 `SqlProviderBase` 方言與 `Func<DbConnection>` 工廠。

## 對照表

| 引擎 | 專案 | 工廠 | DI | 連線字串範例 |
|------|------|------|----|--------------|
| SQLite | `DbServices.Provider.Sqlite` | `UseSQLite` | `AddSQLiteDbService` | `Data Source=app.db` |
| SQL Server | `DbServices.Provider.MsSql` | `UseMsSQL` | `AddSqlServerDbService` | `Server=.;Database=App;Trusted_Connection=true;TrustServerCertificate=true` |
| MySQL | `DbServices.Provider.MySql` | `UseMySQL` | `AddMySQLDbService` | `Server=localhost;Database=app;Uid=root;Pwd=***` |
| Oracle | `DbServices.Provider.Oracle` | `UseOracle` | `AddOracleDbService` | `Data Source=localhost:1521/XE;User Id=hr;Password=***` |
| PostgreSQL | `DbServices.Provider.PostgreSQL` | `UsePostgreSQL` | `AddPostgreSQLDbService` | `Host=localhost;Database=app;Username=postgres;Password=***` |

`DatabaseProvider` 列舉：`SQLite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。

## SQLite（本機／測試）

```csharp
var db = MainService.UseSQLite("Data Source=app.db");
```

記憶體庫請注意連線生命週期：每次新連線會看到空庫，測試時共用同一檔案或同一連線字串並開啟連線池。

## SQL Server

```csharp
var db = MainService.UseMsSQL(
    "Server=localhost;Database=App;User Id=sa;Password=***;TrustServerCertificate=true");
```

## MySQL

使用 `MySql.Data` 驅動。時區與 charset 請寫在連線字串（例如 `CharSet=utf8mb4`）。

## Oracle

使用 `Oracle.ManagedDataAccess.Core`。識別名稱會依方言引用。

## PostgreSQL

JSON／JSONB 對應見 [PostgreSQL JSON](postgresql-json.md)。主鍵與引號識別字由 `SqlProviderForPostgreSQL` 處理。

## 連線池

`DbServiceOptions.MinPoolSize`／`MaxPoolSize` 會由 `ConnectionStringHelper` 合併進連線字串（各引擎關鍵字不同，Helper 會轉換）。實際 pooling 由 ADO.NET 驅動負責；DBServices 不自管 pool。

## 刻意不做

提供者**不**長活持有 `DbConnection`。工廠只負責 `() => new XxxConnection(cs)`，執行路徑見 [連線範圍](../architecture/connections.md)。
