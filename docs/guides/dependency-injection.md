# 依賴注入

主套件 `DBServices.Extensions.ServiceCollectionExtensions` 提供可運作的註冊。`DbServices.Core.Extensions` 裡同名方法目前為未完成骨架，**請不要用 Core 那份**。

## 單一資料庫

```csharp
using DBServices.Extensions;

builder.Services.AddSQLiteDbService(connectionString, options =>
{
    options.EnableQueryCache = true;
    options.MaxRetryCount = 3;
    options.MinPoolSize = 2;
    options.MaxPoolSize = 20;
});
```

其餘：`AddSqlServerDbService`、`AddMySQLDbService`、`AddOracleDbService`、`AddPostgreSQLDbService`。

生命週期為 **Scoped** 的 `IDbService`，並先呼叫 `AddDbServices` 註冊 `IValidationService`、`IRetryPolicyService` 與 logging。

```csharp
public class OrderService(IDbService db)
{
    public TableBaseModel? Find(long id) => db.GetRecordById(id, "Orders");
}
```

## 多資料庫（Keyed）

```csharp
builder.Services.AddMultipleDbServices(
    ("orders", DatabaseProvider.SqlServer, ordersCs, o => o.MaxPoolSize = 50),
    ("archive", DatabaseProvider.PostgreSQL, archiveCs, o => o.MinPoolSize = 2),
    ("local", DatabaseProvider.SQLite, "Data Source=local.db", null)
);

public class SyncWorker([FromKeyedServices("orders")] IDbService orders,
                        [FromKeyedServices("archive")] IDbService archive)
{ }
```

較高階的彙整 API 見 [多資料庫](multi-database.md)。

## 選項

`DbServiceOptions`：

| 屬性 | 預設 | 說明 |
|------|------|------|
| `ConnectionString` | 空 | 必填 |
| `CommandTimeout` | 30 | 秒 |
| `EnableQueryCache` | false | 資料表結構快取 |
| `CacheExpirationMinutes` | 5 | 快取 TTL |
| `MaxRetryCount` | 3 | 暫時性錯誤重試 |
| `RetryDelaySeconds` | 1 | 重試間隔 |
| `EnableDetailedLogging` | false | 較吵的日誌 |
| `MaxPoolSize` | 100 | 寫入連線字串 |
| `MinPoolSize` | 5 | 寫入連線字串 |

Core 的 `AddDbServices` 只註冊驗證與重試，**不會**註冊 `IDbService`。
