# 低優先級功能使用指南

## 📋 目錄

- [概述](#概述)
- [PostgreSQL JSON 類型支援](#postgresql-json-類型支援)
- [事務管理](#事務管理)
- [資料庫遷移](#資料庫遷移)
- [使用範例](#使用範例)

## 概述

本指南說明如何使用 DBServices 的低優先級功能，包括 PostgreSQL JSON 類型支援、事務管理和資料庫遷移功能。

## PostgreSQL JSON 類型支援

### 功能說明

DBServices 現在支援 PostgreSQL 的 JSON 和 JSONB 類型，可以：

- 自動識別 JSON/JSONB 欄位
- 序列化 C# 物件為 JSON 字串
- 反序列化 JSON 字串為 C# 物件
- 驗證 JSON 格式

### 資料類型映射

PostgreSQL JSON 類型會映射到 C# 的 `String` 類型，可以使用 `JsonHelper` 進行序列化和反序列化。

```csharp
// C# 類型 -> PostgreSQL 類型
"Json" or "JSON" => "JSONB"
```

### 使用範例

#### 1. 建立包含 JSON 欄位的資料表

```csharp
var fields = new[]
{
    new FieldBaseModel
    {
        FieldName = "Id",
        FieldType = "Int64",
        IsPrimaryKey = true
    },
    new FieldBaseModel
    {
        FieldName = "Name",
        FieldType = "String",
        IsNotNull = true
    },
    new FieldBaseModel
    {
        FieldName = "Metadata",  // JSON 欄位
        FieldType = "Json",      // 使用 Json 類型
        IsNotNull = false
    }
};

dbService.CreateNewTable(fields, "Products");
```

#### 2. 插入包含 JSON 資料的記錄

```csharp
// 方式 1：手動序列化
var metadata = new { Category = "Electronics", Tags = new[] { "new", "popular" } };
var jsonString = JsonHelper.Serialize(metadata);

var data = new[]
{
    new KeyValuePair<string, object?>("Name", "Laptop"),
    new KeyValuePair<string, object?>("Metadata", jsonString)
};

var record = dbService.InsertRecord(data, "Products");

// 方式 2：使用擴充方法（需要定義模型類別）
public class Product
{
    public string Name { get; set; } = string.Empty;
    
    [Json]
    public object? Metadata { get; set; }
}

var product = new Product
{
    Name = "Laptop",
    Metadata = new { Category = "Electronics", Tags = new[] { "new", "popular" } }
};

var record = dbService.InsertRecordWithJson(product, "Products");
```

#### 3. 查詢並反序列化 JSON 資料

```csharp
// 方式 1：手動反序列化
var record = dbService.GetRecordById(1, "Products");
if (record?.Records?.FirstOrDefault() is RecordBaseModel rec)
{
    var metadataJson = rec.GetFieldValue<string>("Metadata");
    var metadata = JsonHelper.Deserialize<Dictionary<string, object>>(metadataJson);
}

// 方式 2：使用擴充方法
var product = dbService.GetRecordWithJson<Product>(1, "Products");
if (product != null)
{
    // product.Metadata 已經反序列化為物件
    Console.WriteLine(product.Metadata);
}
```

#### 4. 查詢 JSON 欄位

PostgreSQL 支援強大的 JSON 查詢功能：

```csharp
// 查詢 JSON 欄位中的特定值
var sql = @"SELECT * FROM Products WHERE Metadata->>'Category' = 'Electronics';";
var results = dbService.ExecuteSQL(sql);

// 使用 JSON 運算子
// -> 取得 JSON 物件欄位
// ->> 取得 JSON 物件欄位（文字格式）
// @> 檢查 JSON 是否包含指定值
```

### JsonHelper 工具方法

```csharp
// 序列化物件為 JSON
var json = JsonHelper.Serialize(myObject);

// 反序列化 JSON 為物件
var obj = JsonHelper.Deserialize<MyType>(jsonString);

// 驗證 JSON 格式
bool isValid = JsonHelper.IsValidJson(jsonString);

// 格式化 JSON（美化輸出）
string formatted = JsonHelper.FormatJson(jsonString);
```

## 事務管理

### 功能說明

事務管理服務提供統一的事務管理介面，支援：

- 開始、提交、回滾事務
- 在事務中執行操作
- 自動錯誤處理和回滾
- 非同步操作支援

### 使用範例

#### 1. 基本使用

```csharp
var transactionService = new TransactionService(dbService, logger);

// 開始事務
if (transactionService.BeginTransaction())
{
    try
    {
        // 執行多個操作
        dbService.InsertRecord(data1, "Table1");
        dbService.InsertRecord(data2, "Table2");
        dbService.UpdateRecordById(id, updates, "Table3");
        
        // 提交事務
        transactionService.Commit();
    }
    catch
    {
        // 發生錯誤時自動回滾
        transactionService.Rollback();
        throw;
    }
}
```

#### 2. 使用 ExecuteInTransaction 方法（推薦）

```csharp
var transactionService = new TransactionService(dbService, logger);

// 同步操作
bool success = transactionService.ExecuteInTransaction(() =>
{
    dbService.InsertRecord(data1, "Table1");
    dbService.InsertRecord(data2, "Table2");
    dbService.UpdateRecordById(id, updates, "Table3");
});

// 非同步操作
bool success = await transactionService.ExecuteInTransactionAsync(async () =>
{
    await dbService.InsertRecordAsync(data1, "Table1");
    await dbService.InsertRecordAsync(data2, "Table2");
    await dbService.UpdateRecordByIdAsync(id, updates, "Table3");
});
```

#### 3. 取得返回值

```csharp
var transactionService = new TransactionService(dbService, logger);

// 執行操作並取得返回值
var result = transactionService.ExecuteInTransaction(() =>
{
    var record1 = dbService.InsertRecord(data1, "Table1");
    var record2 = dbService.InsertRecord(data2, "Table2");
    return new { Record1 = record1, Record2 = record2 };
});

// 非同步版本
var result = await transactionService.ExecuteInTransactionAsync(async () =>
{
    var record1 = await dbService.InsertRecordAsync(data1, "Table1");
    var record2 = await dbService.InsertRecordAsync(data2, "Table2");
    return new { Record1 = record1, Record2 = record2 };
});
```

#### 4. 檢查事務狀態

```csharp
var transactionService = new TransactionService(dbService, logger);

if (transactionService.IsInTransaction)
{
    // 當前在事務中
}

// 取得當前事務（用於進階操作）
var transaction = transactionService.GetTransaction();
if (transaction != null)
{
    // 使用事務執行操作
}
```

### 注意事項

1. **自動回滾**：如果 `ExecuteInTransaction` 中發生異常，會自動回滾
2. **資源管理**：`TransactionService` 實作 `IDisposable`，使用完畢後應釋放
3. **連線狀態**：事務開始前會自動確保連線已開啟

## 資料庫遷移

### 功能說明

資料庫遷移服務提供版本化的資料庫結構管理，支援：

- 執行遷移（升級）
- 回滾遷移（降級）
- 遷移到指定版本
- 查詢當前版本和已執行的遷移

### 建立遷移類別

```csharp
public class CreateUsersTable : MigrationBase
{
    public override long Version => 20250116001;  // 使用時間戳記格式：YYYYMMDDHHMM
    public override string Description => "建立 Users 資料表";

    public override void Up(IDbService dbService, ILogger? logger = null)
    {
        var fields = new[]
        {
            new FieldBaseModel
            {
                FieldName = "Id",
                FieldType = "Int64",
                IsPrimaryKey = true
            },
            new FieldBaseModel
            {
                FieldName = "Name",
                FieldType = "String",
                IsNotNull = true
            },
            new FieldBaseModel
            {
                FieldName = "Email",
                FieldType = "String",
                IsNotNull = true
            }
        };

        dbService.CreateNewTable(fields, "Users");
        logger?.LogInformation("已建立 Users 資料表");
    }

    public override void Down(IDbService dbService, ILogger? logger = null)
    {
        dbService.DropTable("Users");
        logger?.LogInformation("已刪除 Users 資料表");
    }
}

public class AddUserStatusColumn : MigrationBase
{
    public override long Version => 20250116002;
    public override string Description => "為 Users 資料表新增 Status 欄位";

    public override void Up(IDbService dbService, ILogger? logger = null)
    {
        var sql = "ALTER TABLE Users ADD COLUMN Status VARCHAR(50) DEFAULT 'Active';";
        dbService.ExecuteSQL(sql);
        logger?.LogInformation("已新增 Status 欄位");
    }

    public override void Down(IDbService dbService, ILogger? logger = null)
    {
        var sql = "ALTER TABLE Users DROP COLUMN Status;";
        dbService.ExecuteSQL(sql);
        logger?.LogInformation("已刪除 Status 欄位");
    }
}
```

### 使用遷移服務

#### 1. 執行所有待執行的遷移

```csharp
var migrationService = new MigrationService(dbService, logger);

var migrations = new MigrationBase[]
{
    new CreateUsersTable(),
    new AddUserStatusColumn(),
    new CreateOrdersTable()
};

// 執行所有待執行的遷移
int executedCount = migrationService.MigrateUp(migrations);
Console.WriteLine($"執行了 {executedCount} 個遷移");

// 非同步版本
int executedCount = await migrationService.MigrateUpAsync(migrations);
```

#### 2. 回滾最後一個遷移

```csharp
var migrationService = new MigrationService(dbService, logger);

var migrations = new MigrationBase[]
{
    new CreateUsersTable(),
    new AddUserStatusColumn()
};

// 回滾最後一個遷移
bool success = migrationService.MigrateDown(migrations);

// 非同步版本
bool success = await migrationService.MigrateDownAsync(migrations);
```

#### 3. 遷移到指定版本

```csharp
var migrationService = new MigrationService(dbService, logger);

// 回滾到版本 20250116001
int rolledBackCount = migrationService.MigrateToVersion(
    migrations,
    targetVersion: 20250116001
);
```

#### 4. 查詢遷移狀態

```csharp
var migrationService = new MigrationService(dbService, logger);

// 取得當前版本
long currentVersion = migrationService.GetCurrentVersion();
Console.WriteLine($"當前資料庫版本: {currentVersion}");

// 取得所有已執行的遷移
var executedMigrations = migrationService.GetExecutedMigrations();
foreach (var version in executedMigrations)
{
    Console.WriteLine($"已執行遷移: {version}");
}
```

### 遷移版本號建議

建議使用時間戳記格式：`YYYYMMDDHHMM`

- `20250116001` - 2025年1月16日的第一個遷移
- `20250116002` - 2025年1月16日的第二個遷移
- `20250117001` - 2025年1月17日的遷移

這樣可以確保遷移按時間順序執行。

### 遷移記錄表

遷移服務會自動建立 `__SchemaMigrations` 表來記錄遷移執行歷史：

```sql
CREATE TABLE __SchemaMigrations (
    Version BIGINT PRIMARY KEY,
    Description TEXT,
    ExecutedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## 使用範例

### 完整範例：使用 JSON 類型、事務和遷移

```csharp
// 1. 建立遷移
public class CreateProductsTable : MigrationBase
{
    public override long Version => 20250116001;
    public override string Description => "建立 Products 資料表（包含 JSON 欄位）";

    public override void Up(IDbService dbService, ILogger? logger = null)
    {
        var fields = new[]
        {
            new FieldBaseModel { FieldName = "Id", FieldType = "Int64", IsPrimaryKey = true },
            new FieldBaseModel { FieldName = "Name", FieldType = "String", IsNotNull = true },
            new FieldBaseModel { FieldName = "Price", FieldType = "Decimal", IsNotNull = true },
            new FieldBaseModel { FieldName = "Metadata", FieldType = "Json", IsNotNull = false }
        };

        dbService.CreateNewTable(fields, "Products");
    }

    public override void Down(IDbService dbService, ILogger? logger = null)
    {
        dbService.DropTable("Products");
    }
}

// 2. 執行遷移
var migrationService = new MigrationService(dbService, logger);
migrationService.MigrateUp(new[] { new CreateProductsTable() });

// 3. 使用事務插入包含 JSON 的資料
var transactionService = new TransactionService(dbService, logger);

transactionService.ExecuteInTransaction(() =>
{
    var product1 = new
    {
        Name = "Laptop",
        Price = 999.99m,
        Metadata = JsonHelper.Serialize(new { Category = "Electronics", Brand = "Dell" })
    };

    var product2 = new
    {
        Name = "Mouse",
        Price = 29.99m,
        Metadata = JsonHelper.Serialize(new { Category = "Accessories", Wireless = true })
    };

    dbService.InsertRecord(ConvertToKeyValuePairs(product1), "Products");
    dbService.InsertRecord(ConvertToKeyValuePairs(product2), "Products");
});

// 4. 查詢並處理 JSON 資料
var record = dbService.GetRecordById(1, "Products");
if (record?.Records?.FirstOrDefault() is RecordBaseModel rec)
{
    var metadataJson = rec.GetFieldValue<string>("Metadata");
    var metadata = JsonHelper.Deserialize<Dictionary<string, object>>(metadataJson);
    Console.WriteLine($"Category: {metadata?["Category"]}");
}
```

## 相關文件

- [API 文件](API_DOCUMENTATION.md)
- [效能調優指南](PERFORMANCE_TUNING.md)
- [多資料庫管理指南](MULTI_DATABASE_GUIDE.md)

