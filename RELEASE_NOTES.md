# DbServices v2.0.0 發布說明

## 🚀 主要新功能

### .NET 10 升級
- 全面升級到 .NET 10.0
- 採用最新的 C# 語言特性
- 提升效能和安全性

### 現代化架構
- **Factory Pattern**: MainService 作為統一入口點
- **Builder Pattern**: DbServiceBuilder 提供流暢的 API 介面
- **Dependency Injection**: 完整支援 Microsoft.Extensions.DependencyInjection
- **Options Pattern**: DbServiceOptions 提供靈活的配置選項

### 異步支援
- 所有資料庫操作都支援 async/await
- 非阻塞 I/O 操作
- 更好的擴展性和響應性

### 重試機制
- 內建自動重試功能
- 可配置重試次數和延遲時間
- 智能錯誤處理

### 驗證和日誌
- 輸入參數驗證
- 結構化日誌記錄
- 詳細的錯誤追蹤

## 📋 套件結構

本版本包含以下 NuGet 套件：

| 套件名稱 | 版本 | 描述 |
|----------|------|------|
| `DbServices` | 2.0.0 | 主套件，包含所有功能 |
| `DbServices.Core` | 2.0.0 | 核心庫和抽象介面 |
| `DbServices.Provider.Sqlite` | 2.0.0 | SQLite 資料庫提供者 |
| `DbServices.Provider.SqlServer` | 2.0.0 | SQL Server 資料庫提供者 |
| `DbServices.Provider.MySQL` | 2.0.0 | MySQL 資料庫提供者 |
| `DbServices.Provider.Oracle` | 2.0.0 | Oracle 資料庫提供者 |
| `DbServices.Provider.PostgreSQL` | 2.0.0 | PostgreSQL 資料庫提供者 |

## 💻 安裝方式

### 完整安裝
```bash
dotnet add package DbServices
```

### 按需安裝
```bash
# 核心套件
dotnet add package DbServices.Core

# 選擇需要的資料庫提供者
dotnet add package DbServices.Provider.Sqlite
dotnet add package DbServices.Provider.SqlServer
dotnet add package DbServices.Provider.MySQL
dotnet add package DbServices.Provider.Oracle
dotnet add package DbServices.Provider.PostgreSQL
```

## 🔄 遷移指南

### 從 v1.x 升級

1. **更新套件引用**
   ```xml
   <!-- 舊版本 -->
   <PackageReference Include="DBServices" Version="1.x.x" />
   
   <!-- 新版本 -->
   <PackageReference Include="DbServices" Version="2.0.0" />
   ```

2. **使用新的 API**
   ```csharp
   // v1.x 寫法
   var db = new DataBaseService(connectionString);
   
   // v2.0 寫法
   var db = MainService.UseSQLite(connectionString);
   // 或
   var db = MainService.CreateBuilder(connectionString)
       .UseSQLite()
       .Build();
   ```

3. **依賴注入設定**
   ```csharp
   // 在 Program.cs 中新增
   builder.Services.AddDbServices(options =>
   {
       options.ConnectionString = connectionString;
       options.Provider = DatabaseProvider.SQLite;
   });
   ```

## 🆕 新增功能

### PostgreSQL 支援
- ✅ 完整的 PostgreSQL 18 支援
- ✅ JSON/JSONB 類型支援
- ✅ 自動主鍵識別
- ✅ 完整的資料類型映射

### 多資料庫管理
- ✅ 同時連接多個資料庫
- ✅ 資料彙整功能
- ✅ 獨立的連線池管理

### 事務管理
- ✅ 完整的事務管理服務
- ✅ 自動錯誤處理和回滾
- ✅ 同步和非同步支援

### 資料庫遷移
- ✅ 版本化的資料庫結構管理
- ✅ 執行和回滾遷移
- ✅ 遷移到指定版本

### 進階功能
- ✅ 參數化查詢（防止 SQL 注入）
- ✅ 進階查詢服務（分頁、排序、計數）
- ✅ 資料表結構快取
- ✅ 連線池自動管理

### 建構器模式
```csharp
var dbService = MainService.CreateBuilder(connectionString)
    .UsePostgreSQL()
    .WithConnectionPool(minPoolSize: 5, maxPoolSize: 50)
    .WithQueryCache(enabled: true, expirationMinutes: 10)
    .BuildPostgreSQL();
```

### 異步操作
```csharp
// 所有操作都支援異步
var tables = await db.GetAllTableNamesAsync();
var records = await db.GetRecordByTableNameAsync("Users");
```

### 依賴注入
```csharp
// 註冊多個資料庫
services.AddMultipleDbServices(
    ("primary", DatabaseProvider.PostgreSQL, connectionString1, null),
    ("secondary", DatabaseProvider.SqlServer, connectionString2, null)
);

// 使用
public class UserService
{
    private readonly IDbService _primaryDb;
    
    public UserService([FromKeyedServices("primary")] IDbService primaryDb)
    {
        _primaryDb = primaryDb;
    }
}
```

## 🐛 已修復問題

- 修復了多執行緒環境下的連線問題
- 改善了記憶體使用效率
- 修正了某些 SQL 語句生成錯誤
- 增強了錯誤處理和異常資訊

## 📖 文件更新

- ✅ 全新的 README.md 文件
- ✅ 完整的 API 參考（API_DOCUMENTATION.md）
- ✅ 多資料庫管理指南（MULTI_DATABASE_GUIDE.md）
- ✅ 效能調優指南（PERFORMANCE_TUNING.md）
- ✅ 低優先級功能指南（LOW_PRIORITY_FEATURES_GUIDE.md）
- ✅ 新增資料庫提供者指南（ADD_NEW_DATABASE_GUIDE.md）
- ✅ 安全性最佳實踐（SECURITY_BEST_PRACTICES.md）
- ✅ 詳細的使用範例
- ✅ 遷移指南

## 🔗 相關連結

- [GitHub 倉庫](https://github.com/sjvann/DBServices)
- [NuGet 套件](https://www.nuget.org/packages/DbServices)
- [問題回報](https://github.com/sjvann/DBServices/issues)
- [討論區](https://github.com/sjvann/DBServices/discussions)

## 📞 支援

如有任何問題或建議，請透過 GitHub Issues 或 Discussions 與我們聯繫。

---

**DbServices v2.0.0** - 讓資料庫存取更簡單、更現代化 🚀
