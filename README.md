# DBServices

**現代化多資料庫 ORM 工具包**

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Dapper](https://img.shields.io/badge/Dapper-2.1.35-blue.svg)](https://github.com/DapperLib/Dapper)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.txt)

DBServices 是一個基於 Dapper 的現代化多資料庫 ORM 工具包，提供統一的資料庫存取介面，支援 SQLite、SQL Server、MySQL 和 Oracle 資料庫。本專案採用 .NET 10 開發，具備非同步支援、依賴注入、建構器模式、重試機制、驗證服務等現代化功能。

## 🏗️ 系統架構

```
DBServices/
├── DBService/                    # 主要 SDK 專案
│   ├── MainService.cs           # 工廠類別與入口點
│   ├── DbServiceBuilder.cs     # 建構器模式實作
│   └── Extensions/              # 依賴注入擴充
├── DbServices.Core/             # 核心功能庫
│   ├── DataBaseService.cs       # 抽象基底類別
│   ├── Models/                  # 資料模型與介面
│   ├── Services/                # 核心服務
│   ├── Configuration/           # 設定選項
│   ├── SqlStringGenerator/      # SQL 產生器
│   └── Extensions/              # 擴充方法
├── DbServices.Provider.*/       # 資料庫提供者
│   ├── Sqlite/                  # SQLite 提供者
│   ├── MsSql/                   # SQL Server 提供者
│   ├── MySql/                   # MySQL 提供者
│   └── Oracle/                  # Oracle 提供者
├── WebService/                  # Web API 服務
└── DBServiceTest/               # 單元測試
```

## 🚀 主要功能

### 核心功能
- **多資料庫支援**: SQLite、SQL Server、MySQL、Oracle
- **同步/非同步操作**: 完整的 async/await 支援
- **類型安全**: 強型別操作與泛型支援
- **事務管理**: 自動事務處理與手動控制

### 現代化特性
- **依賴注入**: 原生支援 Microsoft.Extensions.DependencyInjection
- **建構器模式**: 流暢的 API 設計
- **設定選項**: 彈性的配置系統
- **日誌記錄**: 整合 Microsoft.Extensions.Logging
- **重試機制**: 內建錯誤重試策略
- **驗證服務**: 輸入驗證與安全檢查

### 企業級功能
- **連線池管理**: 高效的資源管理
- **批次操作**: 高效能批次新增
- **查詢快取**: 可選的查詢結果快取
- **Web API**: RESTful API 介面

## 📦 安裝

### NuGet 套件
```bash
# 主套件 (包含所有提供者)
dotnet add package DbServices

# 或個別安裝
dotnet add package DbServices.Core
dotnet add package DbServices.Provider.Sqlite
dotnet add package DbServices.Provider.SqlServer
dotnet add package DbServices.Provider.MySQL
dotnet add package DbServices.Provider.Oracle
```

### 原始碼安裝
```bash
git clone https://github.com/sjvann/DBServices.git
cd DBServices
dotnet build
```

## 🔧 快速開始

### 1. 基本使用

```csharp
using DBServices;

// 簡單連線
var db = MainService.UseSQLite("Data Source=test.db");
var tables = await db.GetAllTableNamesAsync();

// 查詢資料
var records = await db.GetRecordsByQueryAsync(
    new[] { new KeyValuePair<string, object>("Name", "John") }, 
    "Users");
```

### 2. 依賴注入模式

```csharp
// 在 Program.cs 或 Startup.cs
builder.Services.AddDbServices(options =>
{
    options.ConnectionString = "Data Source=test.db";
    options.Provider = DatabaseProvider.SQLite;
    options.EnableLogging = true;
    options.EnableValidation = true;
    options.MaxRetryCount = 3;
});

// 在服務中使用
public class UserService
{
    private readonly IDbService _dbService;
    
    public UserService(IDbService dbService)
    {
        _dbService = dbService;
    }
    
    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        return await _dbService.GetRecordsByQueryAsync<User>(
            tableName: "Users");
    }
}
```

### 3. 建構器模式

```csharp
var dbService = MainService.CreateBuilder("Data Source=test.db")
    .UseSQLite()
    .WithLogging(logger)
    .WithValidation()
    .WithRetryPolicy()
    .Build();

// 測試連線
var testResult = await MainService.CreateAndTestAsync(
    "Data Source=test.db", 
    DatabaseProvider.SQLite);
```

### 4. 進階設定

```csharp
var options = new DbServiceOptions
{
    ConnectionString = "Data Source=test.db",
    EnableLogging = true,
    EnableValidation = true,
    EnableCache = true,
    CacheExpirationMinutes = 10,
    MaxRetryCount = 5,
    RetryDelaySeconds = 2,
    CommandTimeoutSeconds = 30
};

var dbService = MainService.UseSQLite(options, serviceProvider);
```

## 🔌 支援的資料庫

| 資料庫 | 提供者 | 狀態 | 連線字串範例 |
|--------|--------|------|--------------|
| SQLite | `UseSQLite()` | ✅ 完整支援 | `Data Source=database.db` |
| SQL Server | `UseMsSQL()` | ✅ 完整支援 | `Server=localhost;Database=TestDB;Trusted_Connection=true` |
| MySQL | `UseMySQL()` | ✅ 完整支援 | `Server=localhost;Database=TestDB;Uid=root;Pwd=password` |
| Oracle | `UseOracle()` | ✅ 完整支援 | `Data Source=localhost:1521/XE;User Id=hr;Password=password` |

## 📚 API 參考

### 查詢操作

```csharp
// 取得所有資料表
string[] tables = await db.GetAllTableNamesAsync();

// 取得欄位資訊
var fields = await db.GetFieldsByTableNameAsync("Users");

// 按 ID 查詢
var user = await db.GetRecordByIdAsync(1, "Users");

// 條件查詢
var users = await db.GetRecordsByQueryAsync(
    new[] { new KeyValuePair<string, object>("Age", 25) }, 
    "Users");
```

### 資料操作

```csharp
// 新增資料
var newUser = await db.InsertRecordAsync(
    new[] { 
        new KeyValuePair<string, object>("Name", "John"),
        new KeyValuePair<string, object>("Age", 30)
    }, 
    "Users");

// 更新資料
var updated = await db.UpdateRecordByIdAsync(1,
    new[] { new KeyValuePair<string, object>("Age", 31) },
    "Users");

// 刪除資料
bool deleted = await db.DeleteRecordByIdAsync(1, "Users");

// 批次新增
var records = new List<IEnumerable<KeyValuePair<string, object>>>();
int inserted = await db.BulkInsertAsync(records, "Users");
```

## 🌐 Web API 服務

DBServices 包含一個完整的 Web API 服務，提供 RESTful 介面：

```bash
# 啟動 Web 服務
cd WebService
dotnet run

# API 端點
GET    /api/tables              # 取得所有資料表
GET    /api/tables/{name}       # 取得資料表詳細資訊
GET    /api/records/{table}     # 查詢記錄
POST   /api/records/{table}     # 新增記錄
PUT    /api/records/{table}/{id} # 更新記錄
DELETE /api/records/{table}/{id} # 刪除記錄
```

## ⚙️ 設定選項

```csharp
public class DbServiceOptions
{
    public string ConnectionString { get; set; }      // 連線字串
    public bool EnableLogging { get; set; }          // 啟用日誌
    public bool EnableValidation { get; set; }       // 啟用驗證
    public bool EnableCache { get; set; }            // 啟用快取
    public int CacheExpirationMinutes { get; set; }  // 快取過期時間
    public int MaxRetryCount { get; set; }           // 最大重試次數
    public int RetryDelaySeconds { get; set; }       // 重試延遲
    public int CommandTimeoutSeconds { get; set; }   // 命令逾時
}
```

## 🧪 測試

```bash
# 執行單元測試
dotnet test

# 執行特定測試
dotnet test --filter "TestCategory=Integration"
```

## 🤝 貢獻

我們歡迎社群貢獻！請參考以下步驟：

1. Fork 此專案
2. 建立功能分支 (`git checkout -b feature/amazing-feature`)
3. 提交變更 (`git commit -m 'Add amazing feature'`)
4. 推送分支 (`git push origin feature/amazing-feature`)
5. 開啟 Pull Request

## 📋 需求

- .NET 10.0 或更高版本
- 支援的資料庫系統之一

## 📄 授權

本專案採用 MIT 授權 - 詳見 [LICENSE.txt](LICENSE.txt) 檔案

## 🔄 版本歷史

- **v2.0.0** (2025-07) - .NET 10 升級，新增現代化功能
- **v1.x** - 初始版本，基本功能實作

## 📞 支援

- 🐛 [回報問題](https://github.com/sjvann/DBServices/issues)
- 💬 [討論區](https://github.com/sjvann/DBServices/discussions)
- 📧 聯絡信箱: [your-email@domain.com]

---

**DBServices** - 讓資料庫存取更簡單、更現代化 🚀
