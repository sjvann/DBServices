# DBServices API 文件

## 📚 目錄

- [概述](#概述)
- [核心介面](#核心介面)
- [查詢方法](#查詢方法)
- [操作方法](#操作方法)
- [進階查詢](#進階查詢)
- [設定選項](#設定選項)
- [使用範例](#使用範例)

## 概述

DBServices 是一個基於 Dapper 的多資料庫 ORM 工具包，支援 SQLite、SQL Server、MySQL、Oracle 和 PostgreSQL。

### 主要特性

- ✅ 多資料庫支援（SQLite, SQL Server, MySQL, Oracle, PostgreSQL）
- ✅ 參數化查詢（防止 SQL 注入）
- ✅ 非同步操作支援
- ✅ 資料表結構快取
- ✅ 連線池自動管理
- ✅ 自動重試機制
- ✅ 完整的 XML 文件註解
- ✅ 事務管理服務
- ✅ 資料庫遷移功能
- ✅ 多資料庫管理服務
- ✅ PostgreSQL JSON 類型支援
- ✅ 進階查詢服務（分頁、排序、計數）

## 核心介面

### IDbService

主要的資料庫服務介面，提供所有基本的資料庫操作。

```csharp
public interface IDbService
{
    // 資料表操作
    string[]? GetAllTableNames(bool includeView = true);
    IEnumerable<FieldBaseModel>? GetFieldsByTableName(string tableName);
    bool HasTable(string tableName);
    bool HasRecord(string? tableName = null);
    
    // 查詢操作
    TableBaseModel? GetRecordById(long id, string? tableName = null);
    TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, 
        EnumQueryOperator? giveOperator = null, string? tableName = null);
    TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, 
        string? tableName = null);
    
    // 操作操作
    TableBaseModel? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source, 
        string? tableName = null);
    TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, 
        string? tableName = null);
    bool DeleteRecordById(long id, string? tableName = null);
}
```

### IDbServiceAsync

非同步版本的資料庫服務介面。

```csharp
public interface IDbServiceAsync
{
    Task<string[]?> GetAllTableNamesAsync(bool includeView = true, 
        CancellationToken cancellationToken = default);
    Task<TableBaseModel?> GetRecordByIdAsync(long id, string? tableName = null, 
        CancellationToken cancellationToken = default);
    Task<TableBaseModel?> InsertRecordAsync(IEnumerable<KeyValuePair<string, object?>> source, 
        string? tableName = null, CancellationToken cancellationToken = default);
    // ... 其他非同步方法
}
```

### IAdvancedQueryService

進階查詢服務介面，提供分頁、排序等進階功能。

```csharp
public interface IAdvancedQueryService
{
    TableBaseModel? GetRecordsWithOptions(
        IEnumerable<KeyValuePair<string, object?>>? query = null,
        QueryOptions? options = null,
        string? tableName = null);
    
    long GetRecordCount(IEnumerable<KeyValuePair<string, object?>>? query = null, 
        string? tableName = null);
    
    bool Exists(IEnumerable<KeyValuePair<string, object?>>? query = null, 
        string? tableName = null);
    
    TableBaseModel? GetFirstRecord(IEnumerable<KeyValuePair<string, object?>>? query = null, 
        string? tableName = null);
    
    T? GetFieldValue<T>(string fieldName, 
        IEnumerable<KeyValuePair<string, object?>>? query = null, 
        string? tableName = null);
    
    Dictionary<string, object?>? GetFieldValues(string[] fieldNames, 
        IEnumerable<KeyValuePair<string, object?>>? query = null, 
        string? tableName = null);
}
```

## 查詢方法

### GetRecordById

根據 ID 取得單筆記錄。

```csharp
var record = dbService.GetRecordById(123, "Users");
```

**參數**:
- `id` (long): 記錄 ID
- `tableName` (string?): 資料表名稱（可選，預設使用當前設定的資料表）

**返回值**: `TableBaseModel?` - 包含記錄的資料表模型，如果找不到則返回 null

**特性**:
- ✅ 使用參數化查詢（安全）
- ✅ 自動驗證表名和欄位名
- ✅ 完整的錯誤處理和日誌記錄

### GetRecordByKeyValue

根據單一鍵值對查詢記錄。

```csharp
var record = dbService.GetRecordByKeyValue(
    new KeyValuePair<string, object?>("Email", "user@example.com"),
    EnumQueryOperator.Equals,
    "Users"
);
```

**參數**:
- `query` (KeyValuePair<string, object?>): 查詢條件（鍵值對）
- `giveOperator` (EnumQueryOperator?): 查詢運算子（預設為等於）
- `tableName` (string?): 資料表名稱（可選）

**支援的運算子**:
- `Equals` - 等於
- `NotEquals` - 不等於
- `GreaterThan` - 大於
- `LessThan` - 小於
- `GreaterThanOrEqual` - 大於等於
- `LessThanOrEqual` - 小於等於
- `Like` - 模糊查詢
- `NotLike` - 不模糊查詢

### GetRecordByKeyValues

根據多個鍵值對查詢記錄（使用 AND 連接）。

```csharp
var query = new[]
{
    new KeyValuePair<string, object?>("Status", "Active"),
    new KeyValuePair<string, object?>("Age", 18)
};
var records = dbService.GetRecordByKeyValues(query, "Users");
```

## 操作方法

### InsertRecord

插入新記錄。

```csharp
var data = new[]
{
    new KeyValuePair<string, object?>("Name", "John Doe"),
    new KeyValuePair<string, object?>("Email", "john@example.com"),
    new KeyValuePair<string, object?>("Age", 30)
};
var insertedRecord = dbService.InsertRecord(data, "Users");
```

**返回值**: 插入後的完整記錄（包含自動產生的 ID）

### UpdateRecordById

根據 ID 更新記錄。

```csharp
var updates = new[]
{
    new KeyValuePair<string, object?>("Name", "Jane Doe"),
    new KeyValuePair<string, object?>("Age", 31)
};
var updatedRecord = dbService.UpdateRecordById(123, updates, "Users");
```

### DeleteRecordById

根據 ID 刪除記錄。

```csharp
bool deleted = dbService.DeleteRecordById(123, "Users");
```

## 進階查詢

### GetRecordsWithOptions

支援分頁、排序的進階查詢。

```csharp
var options = new QueryOptions
{
    OrderBy = "CreatedDate",
    OrderByDescending = true,
    Skip = 10,
    Take = 20,
    SelectFields = new[] { "Id", "Name", "Email" }
};

var query = new[]
{
    new KeyValuePair<string, object?>("Status", "Active")
};

var records = dbService.GetRecordsWithOptions(query, options, "Users");
```

**QueryOptions 屬性**:
- `OrderBy` (string?): 排序欄位名稱
- `OrderByDescending` (bool): 是否降序排列（預設 false）
- `Skip` (int?): 跳過的記錄數（用於分頁）
- `Take` (int?): 取得的記錄數（用於分頁）
- `SelectFields` (string[]?): 要選擇的欄位名稱（如果為 null 則選擇所有欄位）

### GetRecordCount

查詢記錄總數。

```csharp
var count = dbService.GetRecordCount(
    new[] { new KeyValuePair<string, object?>("Status", "Active") },
    "Users"
);
```

### Exists

檢查記錄是否存在。

```csharp
bool exists = dbService.Exists(
    new[] { new KeyValuePair<string, object?>("Email", "user@example.com") },
    "Users"
);
```

### GetFieldValue<T>

取得單一欄位的值（強型別）。

```csharp
string email = dbService.GetFieldValue<string>(
    "Email",
    new[] { new KeyValuePair<string, object?>("Id", 123) },
    "Users"
);
```

## 設定選項

### DbServiceOptions

資料庫服務設定選項。

```csharp
var options = new DbServiceOptions
{
    ConnectionString = "Server=localhost;Database=mydb;...",
    CommandTimeout = 60,                    // 命令逾時秒數（預設 30）
    EnableQueryCache = true,                // 啟用查詢快取
    CacheExpirationMinutes = 10,            // 快取過期時間（分鐘）
    MaxRetryCount = 3,                      // 最大重試次數
    RetryDelaySeconds = 1,                  // 重試延遲秒數
    EnableDetailedLogging = false,          // 啟用詳細日誌
    MaxPoolSize = 100,                     // 連線池最大大小
    MinPoolSize = 5                        // 連線池最小大小
};
```

## 使用範例

### 基本使用

```csharp
// 建立資料庫服務
var db = MainService.UsePostgreSQL("Host=localhost;Database=mydb;...");

// 查詢記錄
var user = db.GetRecordById(1, "Users");

// 插入記錄
var newUser = db.InsertRecord(new[]
{
    new KeyValuePair<string, object?>("Name", "John"),
    new KeyValuePair<string, object?>("Email", "john@example.com")
}, "Users");

// 更新記錄
var updated = db.UpdateRecordById(1, new[]
{
    new KeyValuePair<string, object?>("Name", "Jane")
}, "Users");

// 刪除記錄
db.DeleteRecordById(1, "Users");
```

### 使用建構器模式

```csharp
var db = MainService.CreateBuilder("Host=localhost;Database=mydb;...")
    .WithTimeout(60)
    .WithQueryCache(enabled: true, expirationMinutes: 10)
    .WithConnectionPool(minPoolSize: 5, maxPoolSize: 100)
    .BuildPostgreSQL();
```

### 使用依賴注入

```csharp
// Program.cs
services.AddPostgreSQLDbService("Host=localhost;Database=mydb;...", options =>
{
    options.EnableQueryCache = true;
    options.MaxPoolSize = 100;
});

// 控制器
public class UserController : ControllerBase
{
    private readonly IDbService _dbService;
    
    public UserController(IDbService dbService)
    {
        _dbService = dbService;
    }
    
    [HttpGet("{id}")]
    public IActionResult GetUser(long id)
    {
        var user = _dbService.GetRecordById(id, "Users");
        return user != null ? Ok(user) : NotFound();
    }
}
```

### 進階查詢範例

```csharp
// 分頁查詢
var page1 = dbService.GetRecordsWithOptions(
    query: new[] { new KeyValuePair<string, object?>("Status", "Active") },
    options: new QueryOptions
    {
        OrderBy = "CreatedDate",
        OrderByDescending = true,
        Skip = 0,
        Take = 10
    },
    tableName: "Users"
);

// 查詢總數
var totalCount = dbService.GetRecordCount(
    new[] { new KeyValuePair<string, object?>("Status", "Active") },
    "Users"
);

// 檢查存在性
if (dbService.Exists(
    new[] { new KeyValuePair<string, object?>("Email", "user@example.com") },
    "Users"
))
{
    // 處理邏輯
}
```

## 安全性

所有查詢方法都使用參數化查詢，有效防止 SQL 注入攻擊。表名和欄位名都會經過驗證和轉義處理。

## 效能優化

- **查詢快取**: 啟用 `EnableQueryCache` 可以快取資料表結構資訊
- **連線池**: 設定適當的 `MinPoolSize` 和 `MaxPoolSize` 可以優化連線管理
- **參數化查詢**: 所有查詢都使用參數化查詢，提升效能和安全性

## 相關文件

- [README.md](README.md) - 專案概述和快速開始
- [多資料庫管理指南](MULTI_DATABASE_GUIDE.md) - 多資料庫使用指南
- [效能調優指南](PERFORMANCE_TUNING.md) - 效能優化建議
- [低優先級功能指南](LOW_PRIORITY_FEATURES_GUIDE.md) - JSON、事務、遷移功能
- [新增資料庫提供者指南](ADD_NEW_DATABASE_GUIDE.md) - 如何新增資料庫支援
- [安全性最佳實踐](SECURITY_BEST_PRACTICES.md) - 安全使用建議
- [發布說明](RELEASE_NOTES.md) - 版本更新記錄

- [效能調優指南](PERFORMANCE_TUNING.md)
- [安全性最佳實踐](SECURITY_BEST_PRACTICES.md)
- [新增資料庫提供者指南](ADD_NEW_DATABASE_GUIDE.md)

