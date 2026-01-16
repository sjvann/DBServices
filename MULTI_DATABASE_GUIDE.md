# 多資料庫管理指南

## 📋 概述

本指南說明如何使用 DBServices 同時連接多個不同的資料庫，並進行資料彙整和轉移操作。

## 🎯 使用場景

### 典型場景

1. **資料彙整**：從多個來源資料庫讀取資料，彙整後寫入目標資料庫
2. **資料遷移**：將資料從一個資料庫遷移到另一個資料庫
3. **資料同步**：在多個資料庫之間同步資料
4. **報表生成**：從多個資料庫讀取資料，生成綜合報表

## 🔧 連線池管理

### 每個資料庫都有獨立的連線池

DBServices 為每個資料庫服務實例維護獨立的連線池，這意味著：

- ✅ 每個資料庫的連線池設定是獨立的
- ✅ 不會互相影響
- ✅ 可以針對不同資料庫設定不同的連線池大小

### 連線池設定

```csharp
// 來源資料庫 1 - 設定較小的連線池（只讀）
var source1Options = new DbServiceOptions
{
    ConnectionString = "Server=source1;Database=db1;...",
    MinPoolSize = 2,
    MaxPoolSize = 10  // 只讀操作，不需要太多連線
};

// 來源資料庫 2 - 設定中等連線池
var source2Options = new DbServiceOptions
{
    ConnectionString = "Server=source2;Database=db2;...",
    MinPoolSize = 5,
    MaxPoolSize = 20
};

// 目標資料庫 - 設定較大的連線池（寫入操作）
var targetOptions = new DbServiceOptions
{
    ConnectionString = "Server=target;Database=targetdb;...",
    MinPoolSize = 10,
    MaxPoolSize = 50  // 寫入操作需要更多連線
};
```

## 📚 使用方式

### 方式 1：使用依賴注入（推薦）

```csharp
// Program.cs 或 Startup.cs
services.AddMultipleDbServices(
    // 來源資料庫 1
    ("source1", DatabaseProvider.SqlServer, 
        "Server=source1;Database=db1;...", 
        options => {
            options.MinPoolSize = 2;
            options.MaxPoolSize = 10;
        }),
    
    // 來源資料庫 2
    ("source2", DatabaseProvider.PostgreSQL, 
        "Host=source2;Database=db2;...", 
        options => {
            options.MinPoolSize = 5;
            options.MaxPoolSize = 20;
        }),
    
    // 目標資料庫
    ("target", DatabaseProvider.MySQL, 
        "Server=target;Database=targetdb;...", 
        options => {
            options.MinPoolSize = 10;
            options.MaxPoolSize = 50;
            options.EnableQueryCache = true;
        })
);

// 註冊多資料庫管理服務
services.AddSingleton<IMultiDatabaseService, MultiDatabaseService>();
```

### 方式 2：手動建立和管理

```csharp
// 建立多個資料庫服務
var source1Db = MainService.UseMsSQL(source1Options);
var source2Db = MainService.UsePostgreSQL(source2Options);
var targetDb = MainService.UseMySQL(targetOptions);

// 建立多資料庫管理服務
var multiDbService = new MultiDatabaseService(logger);

// 註冊資料庫服務
multiDbService.RegisterDatabase("source1", source1Db);
multiDbService.RegisterDatabase("source2", source2Db);
multiDbService.RegisterDatabase("target", targetDb);
```

## 🔄 資料彙整範例

### 範例 1：從多個資料庫讀取並彙整

```csharp
public class DataAggregationService
{
    private readonly IMultiDatabaseService _multiDbService;

    public DataAggregationService(IMultiDatabaseService multiDbService)
    {
        _multiDbService = multiDbService;
    }

    public async Task<List<User>> AggregateUsersFromMultipleSourcesAsync()
    {
        // 定義查詢函數
        async Task<IEnumerable<User>> QueryUsers(IDbService db, string dbName)
        {
            // 注意：這裡需要根據實際的 IDbServiceAsync 介面調整
            // 如果使用同步方法，可以這樣：
            var records = db.GetRecordByTableName("Users");
            return records?.Records?.Select(r => new User
            {
                Id = r.GetFieldValue<long>("Id"),
                Name = r.GetFieldValue<string>("Name"),
                Email = r.GetFieldValue<string>("Email"),
                Source = dbName  // 標記來源
            }) ?? Enumerable.Empty<User>();
        }

        // 從多個來源資料庫彙整資料
        var users = await _multiDbService.AggregateDataFromSourcesAsync(
            new[] { "source1", "source2" },
            QueryUsers
        );

        return users;
    }
}
```

### 範例 2：彙整並寫入目標資料庫

```csharp
public async Task<int> AggregateAndTransferAsync()
{
    // 定義查詢函數
    async Task<IEnumerable<Dictionary<string, object?>>> QueryData(
        IDbService db, string dbName)
    {
        // 注意：這裡需要根據實際的 IDbServiceAsync 介面調整
        // 如果使用同步方法，可以這樣：
        var records = db.GetRecordByTableName("Orders");
        return records?.Records?.Select(r => new Dictionary<string, object?>
        {
            ["OrderId"] = r.GetFieldValue<long>("Id"),
            ["CustomerName"] = r.GetFieldValue<string>("CustomerName"),
            ["Amount"] = r.GetFieldValue<decimal>("Amount"),
            ["Source"] = dbName
        }) ?? Enumerable.Empty<Dictionary<string, object?>>();
    }

    // 定義插入函數
    async Task<bool> InsertData(IDbService targetDb, Dictionary<string, object?> data)
    {
        var result = await targetDb.InsertRecordAsync(
            data.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)),
            "AggregatedOrders"
        );
        return result != null;
    }

    // 彙整並寫入
    var insertedCount = await _multiDbService.AggregateAndInsertAsync(
        new[] { "source1", "source2" },  // 來源資料庫
        "target",                          // 目標資料庫
        QueryData,
        InsertData
    );

    return insertedCount;
}
```

### 範例 3：使用建構器模式

```csharp
// 建立來源資料庫服務
var source1Db = MainService.CreateBuilder("Server=source1;Database=db1;...")
    .WithConnectionPool(minPoolSize: 2, maxPoolSize: 10)
    .BuildSqlServer();

var source2Db = MainService.CreateBuilder("Host=source2;Database=db2;...")
    .WithConnectionPool(minPoolSize: 5, maxPoolSize: 20)
    .BuildPostgreSQL();

// 建立目標資料庫服務
var targetDb = MainService.CreateBuilder("Server=target;Database=targetdb;...")
    .WithConnectionPool(minPoolSize: 10, maxPoolSize: 50)
    .WithQueryCache(enabled: true, expirationMinutes: 10)
    .BuildMySQL();

// 使用多資料庫管理服務
var multiDbService = new MultiDatabaseService();
multiDbService.RegisterDatabase("source1", source1Db);
multiDbService.RegisterDatabase("source2", source2Db);
multiDbService.RegisterDatabase("target", targetDb);
```

## ⚡ 效能優化建議

### 1. 並行查詢

`AggregateDataFromSourcesAsync` 方法會自動並行查詢所有來源資料庫，無需手動處理：

```csharp
// 自動並行查詢，無需手動建立 Task
var data = await _multiDbService.AggregateDataFromSourcesAsync(
    new[] { "source1", "source2", "source3" },
    QueryFunction
);
```

### 2. 批次寫入

對於大量資料，考慮批次寫入：

```csharp
// 批次寫入（需要手動實作）
var batchSize = 1000;
var batches = aggregatedData
    .Select((item, index) => new { item, index })
    .GroupBy(x => x.index / batchSize)
    .Select(g => g.Select(x => x.item).ToList());

foreach (var batch in batches)
{
    // 批次插入邏輯
    await BatchInsertAsync(targetDb, batch);
}
```

### 3. 連線池大小建議

根據操作類型設定連線池大小：

| 操作類型 | MinPoolSize | MaxPoolSize | 說明 |
|---------|------------|------------|------|
| 只讀查詢 | 2-5 | 10-20 | 來源資料庫通常只需要較小的連線池 |
| 寫入操作 | 10-20 | 50-100 | 目標資料庫需要較大的連線池 |
| 混合操作 | 5-10 | 30-50 | 平衡讀寫操作 |

## 🔒 安全性考量

### 1. 連線字串安全

確保連線字串安全儲存：

```csharp
// 使用 Configuration 或 Key Vault
var connectionString = configuration.GetConnectionString("Source1");
```

### 2. 權限最小化

為不同資料庫使用不同的資料庫帳號，遵循權限最小化原則：

- **來源資料庫**：只讀權限
- **目標資料庫**：讀寫權限

### 3. 錯誤處理

妥善處理錯誤，避免影響其他資料庫操作：

```csharp
try
{
    var data = await _multiDbService.AggregateDataFromSourcesAsync(
        sourceDatabases,
        queryFunc
    );
}
catch (Exception ex)
{
    _logger.LogError(ex, "資料彙整失敗");
    // 處理錯誤，可能重試或記錄
}
```

## 📊 監控和診斷

### 監控連線池使用情況

```csharp
// 檢查已註冊的資料庫
var registeredDbs = _multiDbService.GetRegisteredDatabaseNames();
foreach (var dbName in registeredDbs)
{
    var db = _multiDbService.GetDatabase(dbName);
    // 檢查連線狀態等
}
```

### 日誌記錄

多資料庫管理服務會自動記錄操作日誌：

- 資料庫註冊/移除
- 資料彙整進度
- 錯誤資訊

## 🎯 最佳實踐

1. **使用依賴注入**：透過 DI 容器管理資料庫服務生命週期
2. **適當設定連線池**：根據操作類型設定連線池大小
3. **錯誤處理**：妥善處理錯誤，避免影響其他操作
4. **監控和日誌**：監控連線池使用情況和操作日誌
5. **資源清理**：適時移除不需要的資料庫服務

## 📚 相關文件

- [API 文件](API_DOCUMENTATION.md)
- [效能調優指南](PERFORMANCE_TUNING.md)
- [安全性最佳實踐](SECURITY_BEST_PRACTICES.md)

