# DBServices 使用指南

## 基本使用方式

### 1. 傳統工廠方法
```csharp
// 簡單使用
var db = MainService.UseSQLite("Data Source=test.db");
var tables = db.GetAllTableNames();

// 使用進階設定
var options = new DbServiceOptions
{
    ConnectionString = "Data Source=test.db",
    CommandTimeout = 60,
    EnableQueryCache = true,
    MaxRetryCount = 5
};
var db = MainService.UseSQLite(options, serviceProvider);
```

### 2. 建構器模式
```csharp
var db = MainService.CreateBuilder("Data Source=test.db")
    .WithTimeout(60)
    .WithQueryCache(enabled: true, expirationMinutes: 10)
    .WithConnectionPool(minPoolSize: 2, maxPoolSize: 50)
    .BuildSQLite();
```

### 3. 依賴注入模式
```csharp
// Program.cs 或 Startup.cs
services.AddSQLiteDbService("Data Source=test.db", options =>
{
    options.CommandTimeout = 60;
    options.EnableQueryCache = true;
    options.MaxRetryCount = 5;
});

// 控制器或服務中
public class MyController : ControllerBase
{
    private readonly IDbService _dbService;
    
    public MyController(IDbService dbService)
    {
        _dbService = dbService;
    }
    
    public async Task<IActionResult> GetTables()
    {
        var tables = await _dbService.GetAllTableNamesAsync();
        return Ok(tables);
    }
}
```

### 4. 多資料庫支援
```csharp
// 註冊多個資料庫
services.AddMultipleDbServices(
    ("primary", DatabaseProvider.SQLite, "Data Source=primary.db", null),
    ("reporting", DatabaseProvider.SqlServer, "Server=.;Database=Reports;Trusted_Connection=true;", options => 
    {
        options.CommandTimeout = 120;
        options.EnableQueryCache = true;
    }),
    ("analytics", DatabaseProvider.MySQL, "Server=localhost;Database=analytics;Uid=user;Pwd=pass;", null)
);

// 使用特定資料庫
public class ReportService
{
    private readonly IDbService _primaryDb;
    private readonly IDbService _reportingDb;
    
    public ReportService(
        [FromKeyedServices("primary")] IDbService primaryDb,
        [FromKeyedServices("reporting")] IDbService reportingDb)
    {
        _primaryDb = primaryDb;
        _reportingDb = reportingDb;
    }
}
```

### 5. 非同步操作
```csharp
// 非同步測試連線
var db = await MainService.CreateAndTestAsync(
    "Data Source=test.db", 
    DatabaseProvider.SQLite, 
    cancellationToken);

// 非同步 CRUD 操作
if (db is IDbServiceAsync asyncDb)
{
    var tables = await asyncDb.GetAllTableNamesAsync();
    var record = await asyncDb.GetRecordByIdAsync(1, "Users");
    var newRecord = await asyncDb.InsertRecordAsync(userData, "Users");
    var updated = await asyncDb.UpdateRecordByIdAsync(1, updateData, "Users");
    var deleted = await asyncDb.DeleteRecordByIdAsync(1, "Users");
    
    // 批次操作
    var insertCount = await asyncDb.BulkInsertAsync(bulkData, "Users");
}
```

## 進階功能

### 錯誤處理
```csharp
try
{
    var db = MainService.UseSQLite("Data Source=test.db");
    var result = db.GetRecordById(1, "Users");
}
catch (DbServiceException ex)
{
    // 處理資料庫服務相關錯誤
    Console.WriteLine($"資料庫操作錯誤: {ex.Message}");
    if (ex.TableName != null)
        Console.WriteLine($"資料表: {ex.TableName}");
}
catch (DbConnectionException ex)
{
    // 處理連線錯誤
    Console.WriteLine($"連線錯誤: {ex.Message}");
}
catch (DbQueryException ex)
{
    // 處理查詢錯誤
    Console.WriteLine($"查詢錯誤: {ex.Message}");
    if (ex.SqlQuery != null)
        Console.WriteLine($"SQL: {ex.SqlQuery}");
}
```

### 資源管理
```csharp
// 使用 using 語句確保正確釋放資源
using var db = MainService.UseSQLite("Data Source=test.db");
var tables = db.GetAllTableNames();
// db 會在 using 區塊結束時自動釋放
```

### 輸入驗證
```csharp
// 內建驗證會自動檢查：
// - 資料表名稱格式
// - 欄位名稱格式  
// - SQL 注入防護
// - 參數類型驗證

var db = MainService.UseSQLite("Data Source=test.db");
try
{
    // 這會觸發驗證錯誤
    db.SetCurrentTableName("Invalid-Table-Name!");
}
catch (DbValidationException ex)
{
    Console.WriteLine($"驗證錯誤: {ex.Message}");
}
```

## 最佳實務

1. **使用依賴注入**：在正式專案中建議使用依賴注入模式
2. **錯誤處理**：總是包裝適當的例外處理
3. **資源管理**：使用 using 語句或確保 Dispose
4. **非同步操作**：在可能的情況下使用非同步方法
5. **連線字串安全**：不要在程式碼中硬編碼連線字串
6. **日誌記錄**：啟用日誌以便於除錯和監控

## 設定選項說明

```csharp
var options = new DbServiceOptions
{
    ConnectionString = "...",           // 連線字串
    CommandTimeout = 30,                // 命令逾時（秒）
    EnableQueryCache = false,           // 是否啟用查詢快取
    CacheExpirationMinutes = 5,         // 快取過期時間（分鐘）
    MaxRetryCount = 3,                  // 最大重試次數
    RetryDelaySeconds = 1,              // 重試延遲（秒）
    EnableDetailedLogging = false,      // 是否啟用詳細日誌
    MaxPoolSize = 100,                  // 連線池最大大小
    MinPoolSize = 5                     // 連線池最小大小
};
```
