# DBServices

**現代化多資料庫 ORM 工具包**

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Dapper](https://img.shields.io/badge/Dapper-2.1.35-blue.svg)](https://github.com/DapperLib/Dapper)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.txt)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue.svg)](https://sjvann.github.io/DBServices/)
[![GitHub Release](https://img.shields.io/github/v/release/sjvann/DBServices)](https://github.com/sjvann/DBServices/releases)

DBServices 是一個基於 Dapper 的現代化多資料庫 ORM 工具包，提供統一的資料庫存取介面，支援 SQLite、SQL Server、MySQL、Oracle 和 PostgreSQL 資料庫。本專案採用 .NET 10 開發，具備非同步支援、依賴注入、建構器模式、重試機制、驗證服務、事務管理、資料庫遷移等現代化功能。

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
│   ├── Oracle/                  # Oracle 提供者
│   └── PostgreSQL/              # PostgreSQL 提供者
├── WebService/                  # RESTful Web API 服務
├── GraphQLService/              # GraphQL API 服務（動態 Schema）
├── DbServices.Api.Shared/       # REST / GraphQL 共用業務層
├── DbServices.Cli/              # dbsql（SQL 與函數 CLI）
└── DBServiceTest/               # 單元測試
```

底層契約見 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)：所有客戶端走 `IQuerySession`（Query／Call／Utility），連線每次從 pool 取出、用完歸還。

## 🚀 主要功能

### 核心功能
- **多資料庫支援**: SQLite、SQL Server、MySQL、Oracle、PostgreSQL
- **同步/非同步操作**: 完整的 async/await 支援
- **類型安全**: 強型別操作與泛型支援
- **事務管理**: 完整的事務管理服務
- **資料庫遷移**: 版本化的資料庫結構管理
- **多資料庫管理**: 同時連接多個資料庫並進行資料彙整

### 現代化特性
- **依賴注入**: 原生支援 Microsoft.Extensions.DependencyInjection
- **建構器模式**: 流暢的 API 設計
- **設定選項**: 彈性的配置系統
- **日誌記錄**: 整合 Microsoft.Extensions.Logging
- **重試機制**: 內建錯誤重試策略
- **驗證服務**: 輸入驗證與安全檢查
- **參數化查詢**: 防止 SQL 注入攻擊

### 企業級功能
- **連線池管理**: 自動化的連線池設定和管理
- **批次操作**: 高效能批次新增
- **查詢快取**: 資料表結構快取機制
- **進階查詢**: 分頁、排序、計數等進階查詢功能
- **PostgreSQL JSON 支援**: 完整的 JSON/JSONB 類型支援
- **Web API**: RESTful 與 GraphQL API 介面

## 📦 安裝

### NuGet 套件
```bash
# 主套件 (包含所有提供者)
dotnet add package DBService

# 或個別安裝
dotnet add package DbServices.Core
dotnet add package DbServices.Provider.Sqlite
dotnet add package DbServices.Provider.SqlServer
dotnet add package DbServices.Provider.MySQL
dotnet add package DbServices.Provider.Oracle
dotnet add package DbServices.Provider.PostgreSQL
```

### 原始碼安裝
```bash
git clone https://github.com/sjvann/DBServices.git
cd DBServices
dotnet build
```

## 💻 dbsql CLI

```bash
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=test.db"
dbsql> SELECT * FROM Users LIMIT 10;
dbsql> \dt
dbsql> \d Users
dbsql> \call get Users 1
dbsql> \fn query --table Users --Name John
dbsql> \q

# 單次
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=test.db" -q "SELECT * FROM Users"
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=test.db" --fn get --arg table=Users --arg id=1 --format json
```

`\` 指令只在客戶端展開，不進 Session grammar。輸出可用 `--format table|json|csv`。

## 🔌 作為 EZIE 元件

AI_EZIE 元件庫增強包：`ezie.dbservices.catalog`／`query`／`crud`。說明見 [AI_EZIE docs/ezie/dbservices-pack.md](https://github.com/sjvann/AI_EZIE/blob/main/docs/ezie/dbservices-pack.md)。連線只填 EZIE 連線目錄的 `connectionId`，不要在 Config 放明文連線字串。

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
| PostgreSQL | `UsePostgreSQL()` | ✅ 完整支援 | `Host=localhost;Database=TestDB;Username=postgres;Password=password` |

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
var users = await db.GetRecordByKeyValueAsync(
    new KeyValuePair<string, object>("Age", 25), 
    "Users");

// 進階查詢（分頁、排序）
var options = new QueryOptions
{
    Skip = 0,
    Take = 10,
    OrderBy = "Name",
    OrderByDescending = "CreatedAt"
};
var pagedUsers = db.GetRecordsWithOptions(null, options, "Users");
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

### 事務管理

```csharp
using var transactionService = new TransactionService(dbService, logger);

// 在事務中執行多個操作
transactionService.ExecuteInTransaction(() =>
{
    dbService.InsertRecord(data1, "Table1");
    dbService.InsertRecord(data2, "Table2");
    dbService.UpdateRecordById(id, updates, "Table3");
});
```

### 資料庫遷移

```csharp
var migrationService = new MigrationService(dbService, logger);

// 執行所有待執行的遷移
var migrations = new MigrationBase[]
{
    new CreateUsersTable(),
    new AddUserStatusColumn()
};
int executedCount = migrationService.MigrateUp(migrations);
```

### 多資料庫管理

```csharp
// 註冊多個資料庫
var multiDbService = new MultiDatabaseService();
multiDbService.RegisterDatabase("source1", source1Db);
multiDbService.RegisterDatabase("source2", source2Db);
multiDbService.RegisterDatabase("target", targetDb);

// 從多個來源彙整資料
var data = await multiDbService.AggregateDataFromSourcesAsync(
    new[] { "source1", "source2" },
    async (db, name) => await db.GetRecordByTableNameAsync("Users")
);
```

### PostgreSQL JSON 類型

```csharp
// 使用 JSON 欄位
var metadata = new { Category = "Electronics", Tags = new[] { "new", "popular" } };
var jsonString = JsonHelper.Serialize(metadata);

var data = new[]
{
    new KeyValuePair<string, object>("Name", "Laptop"),
    new KeyValuePair<string, object>("Metadata", jsonString)
};
dbService.InsertRecord(data, "Products");

// 查詢並反序列化
var record = dbService.GetRecordById(1, "Products");
var metadataJson = record?.Records?.FirstOrDefault()?.GetFieldValue<string>("Metadata");
var metadataObj = JsonHelper.Deserialize<Dictionary<string, object>>(metadataJson);
```

## 🌐 Web API 服務

DBServices 提供兩種 Web 介面，共用 `DbServices.Api.Shared` 業務層：

### RESTful（WebService）

```bash
cd WebService
dotnet run
# http://localhost:5133
```

| 方法 | 端點 | 說明 |
|------|------|------|
| GET | `/meta` | 所有資料表名稱 |
| GET | `/{table}/fieldSet` | 欄位結構 |
| GET | `/{table}/valueSet?fieldName=` | 欄位值集合 |
| GET | `/{table}` | 查詢記錄（可選 `Key`/`Value`） |
| GET | `/{table}/{id}` | 依 ID 查詢 |
| POST | `/{table}` | 新增記錄 |
| PUT | `/{table}/{id}` | 更新記錄 |
| DELETE | `/{table}/{id}` | 刪除記錄 |

開發環境可使用 Scalar：`http://localhost:5133/scalar`

### GraphQL（GraphQLService）

```bash
cd GraphQLService
dotnet run
# http://localhost:5134/graphql
```

啟動時依資料庫 metadata **動態產生**各資料表的 GraphQL 型別。詳見 [GraphQLService/README.md](GraphQLService/README.md)。

## ⚙️ 設定選項

```csharp
public class DbServiceOptions
{
    public string ConnectionString { get; set; }      // 連線字串
    public bool EnableLogging { get; set; }          // 啟用日誌
    public bool EnableValidation { get; set; }       // 啟用驗證
    public bool EnableQueryCache { get; set; }       // 啟用查詢快取
    public int CacheExpirationMinutes { get; set; }  // 快取過期時間
    public int MaxRetryCount { get; set; }           // 最大重試次數
    public int RetryDelaySeconds { get; set; }       // 重試延遲
    public int CommandTimeout { get; set; }          // 命令逾時（秒）
    public int MinPoolSize { get; set; }             // 最小連線池大小
    public int MaxPoolSize { get; set; }             // 最大連線池大小
}
```

### 連線池設定

```csharp
var options = new DbServiceOptions
{
    ConnectionString = "Server=localhost;Database=TestDB;...",
    MinPoolSize = 5,      // 最小連線數
    MaxPoolSize = 50     // 最大連線數
};

// 連線池設定會自動套用到連線字串
var db = MainService.UseMsSQL(options);
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

公開發行請從 `Release/{版次}` 分支推送（例如 `Release/2.1.0`），工作流程會建立 GitHub Release 並同步 [GitHub Pages](https://sjvann.github.io/DBServices/)。說明見 [docs/contributing/release.md](docs/contributing/release.md)。

## 📋 需求

- .NET 10.0 或更高版本
- 支援的資料庫系統之一

## 📄 授權

本專案採用 MIT 授權 - 詳見 [LICENSE.txt](LICENSE.txt) 檔案

## 📖 文件

完整文件站（DocFX）：<https://sjvann.github.io/DBServices/>

本機預覽：

```bash
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json --serve
```

倉庫內 Markdown：

- [架構契約](docs/ARCHITECTURE.md)
- [入門](docs/getting-started/index.md)
- [指南](docs/guides/index.md)
- [API 文件](API_DOCUMENTATION.md)
- [多資料庫管理指南](MULTI_DATABASE_GUIDE.md)
- [效能調優指南](PERFORMANCE_TUNING.md)
- [低優先級功能指南](LOW_PRIORITY_FEATURES_GUIDE.md)
- [新增資料庫提供者指南](ADD_NEW_DATABASE_GUIDE.md)
- [安全性最佳實踐](SECURITY_BEST_PRACTICES.md)

## 🔄 版本歷史

- **v5.0.0** (2026-08) - 接續 NuGet `DBService` 4.0.0，專案網站改為 GitHub Pages
  - ✅ 套件 ID 對齊 [DBService](https://www.nuget.org/packages/DBService/)
  - ✅ 文件站：https://sjvann.github.io/DBServices/
- **v2.0.0** (2025-01) - 完整功能版本
  - ✅ PostgreSQL 支援
  - ✅ 多資料庫管理服務
  - ✅ 事務管理功能
  - ✅ 資料庫遷移功能
  - ✅ PostgreSQL JSON 類型支援
  - ✅ 連線池自動管理
  - ✅ 進階查詢服務
  - ✅ 參數化查詢（防止 SQL 注入）
  - ✅ 資料表結構快取
- **v1.x** - 初始版本，基本功能實作

## 📞 支援

- 🐛 [回報問題](https://github.com/sjvann/DBServices/issues)
- 💬 [討論區](https://github.com/sjvann/DBServices/discussions)
- 📧 聯絡信箱: [your-email@domain.com]

---

**DBServices** - 讓資料庫存取更簡單、更現代化 🚀
