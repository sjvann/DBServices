# DBServices 效能調優指南

## 📋 目錄

- [概述](#概述)
- [連線池管理](#連線池管理)
- [查詢快取](#查詢快取)
- [查詢優化](#查詢優化)
- [批次操作](#批次操作)
- [監控和診斷](#監控和診斷)
- [最佳實踐](#最佳實踐)

## 概述

本指南提供 DBServices 的效能調優建議，幫助您優化應用程式的資料庫操作效能。

## 連線池管理

### 設定連線池大小

連線池是提升資料庫效能的重要機制。適當的連線池設定可以減少連線建立和銷毀的開銷。

```csharp
var options = new DbServiceOptions
{
    ConnectionString = connectionString,
    MinPoolSize = 5,      // 最小連線數
    MaxPoolSize = 100    // 最大連線數
};
```

### 連線池大小建議

根據應用程式特性選擇適當的連線池大小：

| 應用程式類型 | MinPoolSize | MaxPoolSize | 說明 |
|------------|------------|------------|------|
| 小型應用 | 2-5 | 10-20 | 低並發，簡單查詢 |
| 中型應用 | 5-10 | 50-100 | 中等並發，混合操作 |
| 大型應用 | 10-20 | 100-200 | 高並發，複雜查詢 |
| 高負載應用 | 20-50 | 200-500 | 極高並發，需要仔細調優 |

### 連線池監控

監控連線池使用情況，避免連線耗盡：

```csharp
// 在應用程式中監控連線池狀態
// 注意：這需要資料庫特定的監控工具
```

**警告訊號**:
- 頻繁的連線超時
- 應用程式回應變慢
- 資料庫連線數接近上限

### 資料庫特定的連線池設定

不同資料庫的連線池參數名稱可能不同，DBServices 會自動處理：

- **SQL Server**: `Min Pool Size`, `Max Pool Size`
- **PostgreSQL**: `Minimum Pool Size`, `Maximum Pool Size`
- **MySQL**: `MinimumPoolSize`, `MaximumPoolSize`
- **Oracle**: `Min Pool Size`, `Max Pool Size`
- **SQLite**: 不支援傳統連線池，使用快取模式

## 查詢快取

### 啟用查詢快取

查詢快取可以快取資料表結構資訊，減少重複的資料庫查詢。

```csharp
var options = new DbServiceOptions
{
    ConnectionString = connectionString,
    EnableQueryCache = true,
    CacheExpirationMinutes = 10  // 快取過期時間（分鐘）
};
```

### 快取適用場景

✅ **適合使用快取**:
- 頻繁查詢相同資料表的結構資訊
- 資料表結構不經常變更
- 需要快速回應的應用程式

❌ **不適合使用快取**:
- 資料表結構經常變更
- 記憶體資源有限
- 需要即時取得最新的結構資訊

### 快取過期時間設定

根據資料表變更頻率設定快取過期時間：

- **靜態資料表**（很少變更）: 30-60 分鐘
- **一般資料表**（偶爾變更）: 5-15 分鐘
- **動態資料表**（經常變更）: 1-5 分鐘或不使用快取

### 手動清除快取

如果需要立即清除快取：

```csharp
// 透過 ITableStructureCacheService 清除快取
cacheService.RemoveFields(tableName);
```

## 查詢優化

### 使用參數化查詢

所有 DBServices 的查詢方法都使用參數化查詢，這不僅安全，也能提升效能：

```csharp
// ✅ 推薦：使用參數化查詢（自動）
var record = dbService.GetRecordById(123, "Users");

// ❌ 不推薦：直接使用字串拼接（不安全且效能較差）
var record = dbService.GetRecordWithWhere("Id = 123", "Users");
```

### 選擇特定欄位

只查詢需要的欄位，減少資料傳輸量：

```csharp
var options = new QueryOptions
{
    SelectFields = new[] { "Id", "Name", "Email" }  // 只查詢需要的欄位
};
var records = dbService.GetRecordsWithOptions(null, options, "Users");
```

### 使用分頁查詢

對於大量資料，使用分頁查詢避免一次載入過多資料：

```csharp
var options = new QueryOptions
{
    Skip = 0,
    Take = 20  // 每頁 20 筆記錄
};
var page1 = dbService.GetRecordsWithOptions(null, options, "Users");
```

### 使用索引

確保查詢欄位有適當的索引：

```sql
-- 為常用查詢欄位建立索引
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_users_status ON Users(Status);
```

### 避免 N+1 查詢問題

使用批次查詢或 JOIN 避免多次查詢：

```csharp
// ❌ 不推薦：N+1 查詢問題
foreach (var userId in userIds)
{
    var user = dbService.GetRecordById(userId, "Users");
    // 處理邏輯
}

// ✅ 推薦：批次查詢
var query = new[]
{
    new KeyValuePair<string, object?>("Id", userIds)
};
var users = dbService.GetRecordByKeyValues(query, "Users");
```

## 批次操作

### 批次插入

對於大量資料插入，考慮使用批次操作：

```csharp
// 注意：目前版本需要手動實作批次插入
// 未來版本可能會加入批次插入支援
```

### 使用交易

對於多個相關操作，使用交易可以提升效能：

```csharp
// 注意：目前版本需要手動管理交易
// 未來版本可能會加入交易管理支援
```

## 監控和診斷

### 啟用詳細日誌

在開發和測試環境中啟用詳細日誌：

```csharp
var options = new DbServiceOptions
{
    EnableDetailedLogging = true
};
```

### 監控查詢效能

使用日誌記錄查詢執行時間：

```csharp
// 日誌會自動記錄查詢資訊
// 檢查日誌中的查詢時間和執行計劃
```

### 常見效能問題

1. **連線池耗盡**
   - 症狀：查詢超時，應用程式變慢
   - 解決：增加 `MaxPoolSize` 或檢查連線是否正確釋放

2. **慢查詢**
   - 症狀：某些查詢執行時間過長
   - 解決：檢查索引、優化查詢、使用分頁

3. **記憶體使用過高**
   - 症狀：應用程式記憶體持續增長
   - 解決：檢查快取設定、減少快取過期時間

4. **資料庫連線過多**
   - 症狀：資料庫連線數接近上限
   - 解決：減少 `MaxPoolSize`、檢查連線洩漏

## 最佳實踐

### 1. 適當設定連線池

```csharp
// 根據應用程式特性設定
var options = new DbServiceOptions
{
    MinPoolSize = 5,
    MaxPoolSize = 50  // 不要設定過大，避免資源浪費
};
```

### 2. 啟用查詢快取（適用場景）

```csharp
var options = new DbServiceOptions
{
    EnableQueryCache = true,
    CacheExpirationMinutes = 10
};
```

### 3. 使用參數化查詢

```csharp
// ✅ 所有查詢方法都自動使用參數化查詢
var record = dbService.GetRecordById(123, "Users");
```

### 4. 選擇需要的欄位

```csharp
var options = new QueryOptions
{
    SelectFields = new[] { "Id", "Name" }  // 只查詢需要的欄位
};
```

### 5. 使用分頁查詢

```csharp
var options = new QueryOptions
{
    Skip = 0,
    Take = 20  // 避免一次載入過多資料
};
```

### 6. 適當設定命令逾時

```csharp
var options = new DbServiceOptions
{
    CommandTimeout = 30  // 根據查詢複雜度設定
};
```

### 7. 監控和調整

- 定期檢查連線池使用情況
- 監控查詢執行時間
- 根據實際情況調整設定

## 效能測試

### 基準測試

建立基準測試來評估效能改進：

```csharp
[TestMethod]
public void PerformanceTest_GetRecordById()
{
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < 1000; i++)
    {
        dbService.GetRecordById(i, "Users");
    }
    
    stopwatch.Stop();
    Console.WriteLine($"1000 queries took: {stopwatch.ElapsedMilliseconds}ms");
}
```

### 壓力測試

使用壓力測試工具（如 Apache JMeter）測試應用程式在高負載下的表現。

## 相關文件

- [API 文件](API_DOCUMENTATION.md)
- [安全性最佳實踐](SECURITY_BEST_PRACTICES.md)
- [新增資料庫提供者指南](ADD_NEW_DATABASE_GUIDE.md)

