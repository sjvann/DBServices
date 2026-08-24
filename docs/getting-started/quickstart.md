# 快速開始

以下三種方式等價：都能得到 `IDbService`。新功能（函數呼叫、工作階段指令）請再用 `GetSession()`。

## 1. 工廠方法

```csharp
using DBServices;
using DbServices.Core.Models.Interface;

IDbService db = MainService.UseSQLite("Data Source=app.db");

string[]? tables = db.GetAllTableNames();
var user = db.GetRecordById(1, "Users");
```

其他提供者：

```csharp
MainService.UseMsSQL(cs);
MainService.UseMySQL(cs);
MainService.UseOracle(cs);
MainService.UsePostgreSQL(cs);
```

進階版可傳 `DbServiceOptions` 與 `IServiceProvider`（用來解析 logger、驗證、重試）：

```csharp
var options = new DbServiceOptions
{
    ConnectionString = "Data Source=app.db",
    CommandTimeout = 30,
    EnableQueryCache = true,
    CacheExpirationMinutes = 10,
    MaxRetryCount = 3,
    RetryDelaySeconds = 1
};
IDbService db = MainService.UseSQLite(options, serviceProvider);
```

非同步探測連線：

```csharp
var db = await MainService.CreateAndTestAsync(
    "Data Source=app.db",
    DatabaseProvider.SQLite);
```

## 2. 建構器

```csharp
using DBServices;

var db = MainService.CreateBuilder("Data Source=app.db")
    .WithTimeout(30)
    .WithQueryCache(enabled: true, expirationMinutes: 10)
    .WithConnectionPool(minPoolSize: 5, maxPoolSize: 50)
    .BuildSQLite();
```

對應方法：`BuildSQLite`、`BuildSqlServer`、`BuildMySQL`、`BuildOracle`、`BuildPostgreSQL`。

需要 logger／驗證／重試時，傳入實作型別：

```csharp
MainService.CreateBuilder(connectionString)
    .WithLogging(logger)
    .WithValidation(validationService)
    .WithRetryPolicy(retryService)
    .BuildSQLite();
```

`WithLogging` 需要 `ILogger<DataBaseService>` 才會走到帶選項的 Provider 建構子。

## 3. 依賴注入

```csharp
using DBServices.Extensions;

builder.Services.AddSQLiteDbService(
    builder.Configuration.GetConnectionString("Default")!,
    options =>
    {
        options.MaxRetryCount = 3;
        options.MinPoolSize = 2;
        options.MaxPoolSize = 20;
    });
```

注入 `IDbService` 即可。多資料庫請見 [多資料庫](../guides/multi-database.md)。

## 第一筆 CRUD

```csharp
db.InsertRecord(
    [
        new("Name", "Ada"),
        new("Age", 36)
    ],
    "Users");

var found = db.GetRecordByKeyValue(
    new KeyValuePair<string, object?>("Name", "Ada"),
    tableName: "Users");

db.UpdateRecordById(
    id: 1,
    [new("Age", 37)],
    "Users");

db.DeleteRecordById(1, "Users");
```

非同步對應在 `IDbServiceAsync`：`InsertRecordAsync`、`GetRecordByIdAsync` 等。`DataBaseService` 同時實作兩個介面。

## Session（建議的新入口）

```csharp
using DbServices.Core.Session;

var session = db.GetSession();

var tables = session.Execute(new QueryRequest
{
    Text = "tables",
    Opcode = QueryOpcode.Call
});

var rows = session.Execute(new QueryRequest
{
    Text = "SELECT Id, Name FROM Users WHERE Age > @Age",
    Opcode = QueryOpcode.Query,
    Arguments = new Dictionary<string, object?> { ["Age"] = 18 }
});
```

完整協定見 [Session](../guides/session.md)。
