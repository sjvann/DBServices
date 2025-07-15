# DbServices v2.0.0 發布說明

## 🚀 主要新功能

### .NET 9 升級
- 全面升級到 .NET 9.0
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

## 🆕 新增 API

### 建構器模式
```csharp
var dbService = MainService.CreateBuilder(connectionString)
    .UseSQLite()
    .WithLogging(logger)
    .WithValidation()
    .WithRetryPolicy()
    .Build();
```

### 異步操作
```csharp
// 所有操作都支援異步
var tables = await db.GetAllTableNamesAsync();
var records = await db.GetRecordsByQueryAsync(...);
```

### 依賴注入
```csharp
public class UserService
{
    private readonly IDbService _dbService;
    
    public UserService(IDbService dbService)
    {
        _dbService = dbService;
    }
}
```

## 🐛 已修復問題

- 修復了多執行緒環境下的連線問題
- 改善了記憶體使用效率
- 修正了某些 SQL 語句生成錯誤
- 增強了錯誤處理和異常資訊

## 📖 文件更新

- 全新的 README.md 文件
- 完整的 API 參考
- 詳細的使用範例
- 遷移指南

## 🔗 相關連結

- [GitHub 倉庫](https://github.com/sjvann/DBServices)
- [NuGet 套件](https://www.nuget.org/packages/DbServices)
- [問題回報](https://github.com/sjvann/DBServices/issues)
- [討論區](https://github.com/sjvann/DBServices/discussions)

## 📞 支援

如有任何問題或建議，請透過 GitHub Issues 或 Discussions 與我們聯繫。

---

**DbServices v2.0.0** - 讓資料庫存取更簡單、更現代化 🚀
