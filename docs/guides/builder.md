# 建構器

`MainService.CreateBuilder` 回傳 `IDbServiceBuilder`（`DbServiceBuilder`）。

```csharp
IDbService db = MainService.CreateBuilder("Data Source=app.db")
    .WithTimeout(45)
    .WithQueryCache(enabled: true, expirationMinutes: 15)
    .WithConnectionPool(minPoolSize: 2, maxPoolSize: 20)
    .BuildSQLite();
```

## 可選相依

| 方法 | 作用 |
|------|------|
| `WithLogging(ILogger)` | 僅當型別為 `ILogger<DataBaseService>` 時，Provider 才走「選項 + logger」建構子 |
| `WithValidation(IValidationService)` | 表名／欄位名檢查 |
| `WithRetryPolicy(IRetryPolicyService)` | 暫時性錯誤重試 |
| `WithTimeout` | `CommandTimeout` |
| `WithQueryCache` | `EnableQueryCache`、`CacheExpirationMinutes` |
| `WithConnectionPool` | `MinPoolSize`、`MaxPoolSize` |

## 建置

`BuildSQLite`、`BuildSqlServer`、`BuildMySQL`、`BuildOracle`、`BuildPostgreSQL`。

若沒有正確型別的 logger，會退回「只傳連線字串」的 Provider 建構子，驗證與重試不會套用。單元測試若要斷言這些行為，請注入 `ILogger<DataBaseService>`。

也可從現成 `DbServiceOptions` 建立：

```csharp
MainService.CreateBuilder(options).BuildPostgreSQL();
```
