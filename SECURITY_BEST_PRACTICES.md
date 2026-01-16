# DBServices 安全性最佳實踐指南

## 📋 概述

本文件提供使用 DBServices 時的安全性最佳實踐建議，幫助開發者建立安全可靠的資料庫應用程式。

## 🔒 核心安全機制

### 1. 輸入驗證

DBServices 內建了多層輸入驗證機制：

#### 表名驗證
```csharp
// ✅ 正確：使用驗證服務
_validationService?.ValidateTableName(tableName);

// ❌ 錯誤：直接使用未驗證的表名
var sql = $"SELECT * FROM {tableName}";
```

**驗證規則**：
- 只能包含字母、數字和底線
- 必須以字母開頭
- 長度不能超過 128 個字元
- 不允許 SQL 注入字元（`'`, `;`, `--`, `*`, `|`, `<`, `>`）

#### 欄位名驗證
```csharp
// ✅ 正確
_validationService?.ValidateFieldName(fieldName);
```

### 2. SQL 注入防護

#### 多層防護策略

1. **驗證層**：使用 `ValidationService` 驗證所有輸入
2. **轉義層**：對 SQL 識別符進行轉義處理
3. **參數化查詢**：建議使用（未來版本將全面支援）

#### 當前實作

```csharp
// PostgreSQL 提供者中的轉義處理
private static string EscapeSqlIdentifier(string identifier)
{
    return identifier.Replace("\"", "\"\"");
}
```

### 3. 連線字串安全

#### ✅ 最佳實踐

```csharp
// ✅ 正確：從設定檔或環境變數讀取
var connectionString = configuration.GetConnectionString("DefaultConnection");

// ❌ 錯誤：硬編碼在程式碼中
var connectionString = "Server=localhost;Database=MyDB;User Id=admin;Password=123456";
```

#### 建議

1. **使用設定檔**：將連線字串存放在 `appsettings.json` 或環境變數中
2. **加密敏感資訊**：對生產環境的連線字串進行加密
3. **使用受控識別**：在雲端環境中使用受控識別而非密碼

### 4. 權限最小化

#### 資料庫使用者權限

建立專用的資料庫使用者，只授予必要的權限：

```sql
-- ✅ 正確：只授予必要的權限
CREATE USER app_user WITH PASSWORD 'secure_password';
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE users TO app_user;
GRANT SELECT ON TABLE products TO app_user;

-- ❌ 錯誤：授予過多權限
GRANT ALL PRIVILEGES ON DATABASE mydb TO app_user;
```

### 5. 錯誤處理

#### ✅ 正確的錯誤處理

```csharp
try
{
    var result = db.GetRecordById(1, "Users");
}
catch (DbValidationException ex)
{
    // 驗證錯誤：記錄但不暴露詳細資訊給使用者
    _logger.LogWarning(ex, "輸入驗證失敗");
    return BadRequest("無效的請求");
}
catch (DbServiceException ex)
{
    // 資料庫錯誤：記錄詳細資訊，但只返回一般性錯誤給使用者
    _logger.LogError(ex, "資料庫操作失敗");
    return StatusCode(500, "伺服器錯誤");
}
```

#### ❌ 錯誤的錯誤處理

```csharp
// ❌ 錯誤：暴露詳細錯誤資訊
catch (Exception ex)
{
    return BadRequest(ex.Message); // 可能洩露敏感資訊
}
```

## 🛡️ 防護措施

### 1. 表名和欄位名驗證

**自動驗證**：
- 所有通過 `SetCurrentTableName` 設定的表名都會自動驗證
- 所有通過 `GetFieldsByTableName` 查詢的表名都會自動驗證

**手動驗證**：
```csharp
var validationService = new ValidationService();
try
{
    validationService.ValidateTableName(userInput);
    // 使用驗證過的表名
}
catch (DbValidationException ex)
{
    // 處理驗證失敗
}
```

### 2. WHERE 子句驗證

**基本檢查**：
```csharp
// ValidationService 會檢查 WHERE 子句中的 SQL 注入字元
validationService.ValidateWhereClause(whereClause);
```

**建議**：
- 盡量使用參數化查詢而非字串拼接
- 避免直接使用使用者輸入構建 WHERE 子句

### 3. 連線安全

#### 使用 SSL/TLS

```csharp
// PostgreSQL
var connectionString = "Host=localhost;Database=mydb;Username=user;Password=pass;SSL Mode=Require;";

// SQL Server
var connectionString = "Server=localhost;Database=mydb;User Id=user;Password=pass;Encrypt=True;";
```

#### 連線池設定

```csharp
var options = new DbServiceOptions
{
    ConnectionString = connectionString,
    MaxPoolSize = 100,
    MinPoolSize = 5
};
```

## 📝 程式碼範例

### 安全的查詢操作

```csharp
// ✅ 正確：使用驗證過的表名
public async Task<IEnumerable<User>> GetUsersAsync()
{
    var tableName = "Users"; // 來自受信任的來源
    _validationService?.ValidateTableName(tableName);
    
    return await _dbService.GetRecordsByQueryAsync(
        new[] { new KeyValuePair<string, object>("Status", "Active") },
        tableName
    );
}

// ❌ 錯誤：直接使用使用者輸入
public async Task<IEnumerable<User>> GetUsersAsync(string tableName)
{
    // 危險：未驗證使用者輸入
    return await _dbService.GetRecordsByQueryAsync(
        new[] { new KeyValuePair<string, object>("Status", "Active") },
        tableName // 可能包含 SQL 注入攻擊
    );
}
```

### 安全的插入操作

```csharp
// ✅ 正確：使用強型別模型
public async Task<User> CreateUserAsync(User user)
{
    var keyValuePairs = new[]
    {
        new KeyValuePair<string, object>("Name", user.Name),
        new KeyValuePair<string, object>("Email", user.Email),
        new KeyValuePair<string, object>("Age", user.Age)
    };
    
    var result = await _dbService.InsertRecordAsync(keyValuePairs, "Users");
    return result?.GetObject<User>() ?? throw new InvalidOperationException("建立使用者失敗");
}

// ❌ 錯誤：直接拼接 SQL
public async Task CreateUserAsync(string name, string email)
{
    var sql = $"INSERT INTO Users (Name, Email) VALUES ('{name}', '{email}')";
    // 危險：SQL 注入風險
    await _dbService.ExecuteSQL(sql);
}
```

## 🔍 安全審計

### 啟用日誌記錄

```csharp
var options = new DbServiceOptions
{
    ConnectionString = connectionString,
    EnableDetailedLogging = true
};

// 在生產環境中，記錄所有資料庫操作
_logger.LogInformation("執行查詢: {TableName}, 參數: {Parameters}", tableName, parameters);
```

### 監控異常活動

```csharp
// 監控驗證失敗
if (ex is DbValidationException)
{
    _logger.LogWarning("偵測到可能的 SQL 注入嘗試: {Input}", userInput);
    // 可以加入速率限制或封鎖機制
}
```

## 🚨 常見安全風險

### 1. SQL 注入

**風險**：攻擊者可以執行任意 SQL 語句

**防護**：
- ✅ 使用 `ValidationService` 驗證所有輸入
- ✅ 使用參數化查詢（未來版本）
- ❌ 避免直接拼接 SQL 字串

### 2. 資訊洩露

**風險**：錯誤訊息可能洩露資料庫結構

**防護**：
- ✅ 在生產環境中記錄詳細錯誤，但只返回一般性錯誤給使用者
- ✅ 使用結構化日誌記錄

### 3. 權限提升

**風險**：使用過高權限的資料庫使用者

**防護**：
- ✅ 使用最小必要權限原則
- ✅ 定期審查資料庫使用者權限

## 📚 相關資源

- [OWASP SQL Injection](https://owasp.org/www-community/attacks/SQL_Injection)
- [.NET 安全性最佳實踐](https://learn.microsoft.com/dotnet/standard/security/)
- [PostgreSQL 安全性](https://www.postgresql.org/docs/current/security.html)

## 🔄 更新記錄

- **2025-01-16**: 初始版本
- 包含基本安全機制說明
- 提供最佳實踐範例

---

**重要提醒**：安全性是一個持續的過程，請定期審查和更新您的安全措施。

