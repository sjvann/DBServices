# 新增資料庫提供者指南

本文件說明如何在 DBServices 專案中新增一個新的資料庫提供者。

## 📋 概述

DBServices 採用**提供者模式（Provider Pattern）**，每個資料庫都有獨立的提供者專案。這種設計讓新增資料庫支援變得簡單且模組化。

## 🏗️ 架構設計

```
DbServices.Provider.{DatabaseName}/
├── DbServices.Provider.{DatabaseName}.csproj  # 專案檔案
├── ProviderService.cs                          # 提供者服務（繼承 DataBaseService）
└── SqlStringGenerator/
    └── SqlProviderFor{DatabaseName}.cs         # SQL 產生器（繼承 SqlProviderBase）
```

## 📝 實作步驟

### 步驟 1: 建立專案資料夾和檔案結構

```bash
mkdir DbServices.Provider.{DatabaseName}
mkdir DbServices.Provider.{DatabaseName}\SqlStringGenerator
```

### 步驟 2: 建立專案檔案

建立 `DbServices.Provider.{DatabaseName}.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GeneratePackageOnBuild>True</GeneratePackageOnBuild>
    
    <!-- NuGet Package Metadata -->
    <PackageId>DbServices.Provider.{DatabaseName}</PackageId>
    <Version>5.0.0</Version>
    <Authors>sjvann</Authors>
    <Company>DBServices</Company>
    <Description>DbServices {DatabaseName} 資料庫提供者 - 支援 {DatabaseName} 的現代化 ORM 解決方案</Description>
    <PackageTags>dapper;orm;{database-name};database;async;dotnet10</PackageTags>
    <PackageProjectUrl>https://sjvann.github.io/DBServices/</PackageProjectUrl
    <RepositoryUrl>https://github.com/sjvann/DBServices</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageRequireLicenseAcceptance>false</PackageRequireLicenseAcceptance>
    <Copyright>Copyright © 2025 sjvann</Copyright>
  </PropertyGroup>

  <ItemGroup>
    <!-- 加入資料庫特定的 NuGet 套件 -->
    <PackageReference Include="{DatabaseDriverPackage}" Version="{Version}" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DbServices.Core\DbServices.Core.csproj" />
  </ItemGroup>
</Project>
```

### 步驟 3: 建立 ProviderService.cs

建立 `ProviderService.cs`，繼承 `DataBaseService`：

```csharp
using DbServices.Core;
using DbServices.Core.Configuration;
using DbServices.Core.Services;
using DbServices.Provider.{DatabaseName}.SqlStringGenerator;
using {DatabaseConnectionNamespace};
using Microsoft.Extensions.Logging;

namespace DbServices.Provider.{DatabaseName}
{
    public class ProviderService : DataBaseService
    {
        public ProviderService(string connectionString) : base(connectionString)
        {
            _conn = new {DatabaseConnectionClass}(connectionString);
            _sqlProvider = new SqlProviderFor{DatabaseName}();
            _tableNameList = GetAllTableNames();
        }

        public ProviderService(DbServiceOptions options, ILogger<DataBaseService>? logger = null, 
            IValidationService? validationService = null, IRetryPolicyService? retryPolicyService = null) 
            : base(options, logger, validationService, retryPolicyService)
        {
            _conn = new {DatabaseConnectionClass}(options.ConnectionString);
            _sqlProvider = new SqlProviderFor{DatabaseName}();
            _tableNameList = GetAllTableNames();
        }
    }
}
```

### 步驟 4: 建立 SqlProviderFor{DatabaseName}.cs

建立 `SqlStringGenerator/SqlProviderFor{DatabaseName}.cs`，繼承 `SqlProviderBase`：

```csharp
using DbServices.Core.Models;
using DbServices.Core.SqlStringGenerator;
using System.Text;

namespace DbServices.Provider.{DatabaseName}.SqlStringGenerator
{
    public class SqlProviderFor{DatabaseName} : SqlProviderBase
    {
        public SqlProviderFor{DatabaseName}() { }

        // 實作所有抽象方法
        public override string? GetSqlForCheckTableExist(string tableName) { ... }
        public override string GetSqlTableNameList(bool includeView = true) { ... }
        public override string? GetSqlLastInsertId(string tableName) { ... }
        public override string? GetSqlForTruncate(string tableName) { ... }
        public override string? GetSqlForCreateTable(TableBaseModel dbModel) { ... }
        public override string? GetSqlForCreateTable(string tableName, IEnumerable<FieldBaseModel> tableDefine) { ... }
        public override string? GetSqlForDropTable(string tableName) { ... }
        public override string? GetSqlForAlterTable(TableBaseModel dbModel) { ... }
        public override string GetSqlFieldsByTableName(string tableName) { ... }
        public override string GetSqlForeignInfoByTableName(string tableName) { ... }
        public override string? ConvertDataTypeToDb(string? dataType) { ... }
        public override IEnumerable<FieldBaseModel> MapToFieldBaseModel(IEnumerable<dynamic> target, IEnumerable<dynamic> foreignness) { ... }
        public override IEnumerable<ForeignBaseModel> MapToForeignBaseModel(IEnumerable<dynamic> target) { ... }
        public override IEnumerable<RecordBaseModel> MapToRecordBaseModel(IEnumerable<dynamic> target) { ... }
    }
}
```

### 步驟 5: 更新 MainService.cs

在 `DBService/MainService.cs` 中：

1. 在 `DatabaseProvider` enum 中加入新資料庫：
```csharp
public enum DatabaseProvider
{
    SQLite,
    SqlServer,
    MySQL,
    Oracle,
    PostgreSQL,
    {NewDatabase}  // 新增
}
```

2. 加入工廠方法：
```csharp
public static IDbService Use{DatabaseName}(string connectString)
{
    return new DbServices.Provider.{DatabaseName}.ProviderService(connectString);
}

public static IDbService Use{DatabaseName}(DbServiceOptions options, IServiceProvider? serviceProvider = null)
{
    var logger = serviceProvider?.GetService<ILogger<DbServices.Core.DataBaseService>>();
    var validation = serviceProvider?.GetService<IValidationService>();
    var retry = serviceProvider?.GetService<IRetryPolicyService>();
    
    return new DbServices.Provider.{DatabaseName}.ProviderService(options, logger, validation, retry);
}
```

3. 在 `CreateAndTestAsync` 方法中加入 switch case：
```csharp
DatabaseProvider.{NewDatabase} => Use{DatabaseName}(connectionString),
```

### 步驟 6: 更新 DbServiceBuilder.cs

在 `DBService/DbServiceBuilder.cs` 中：

1. 在 `IDbServiceBuilder` 介面中加入方法：
```csharp
IDbService Build{DatabaseName}();
```

2. 實作方法：
```csharp
public IDbService Build{DatabaseName}()
{
    if (_logger is ILogger<DbServices.Core.DataBaseService> typedLogger)
    {
        return new DbServices.Provider.{DatabaseName}.ProviderService(_options, typedLogger, _validationService, _retryPolicyService);
    }
    else
    {
        return new DbServices.Provider.{DatabaseName}.ProviderService(_options.ConnectionString);
    }
}
```

### 步驟 7: 更新 ServiceCollectionExtensions.cs

在 `DBService/Extensions/ServiceCollectionExtensions.cs` 中：

1. 加入擴充方法：
```csharp
public static IServiceCollection Add{DatabaseName}DbService(this IServiceCollection services, 
    string connectionString, 
    Action<DbServiceOptions>? configureOptions = null)
{
    services.AddDbServices(options =>
    {
        options.ConnectionString = connectionString;
        configureOptions?.Invoke(options);
    });

    services.AddScoped<IDbService>(provider =>
    {
        var options = new DbServiceOptions { ConnectionString = connectionString };
        configureOptions?.Invoke(options);
        
        var logger = provider.GetService<ILogger<DataBaseService>>();
        var validation = provider.GetService<IValidationService>();
        var retry = provider.GetService<IRetryPolicyService>();
        
        return new DbServices.Provider.{DatabaseName}.ProviderService(options, logger, validation, retry);
    });

    return services;
}
```

2. 在 `AddMultipleDbServices` 方法中加入 switch case：
```csharp
DatabaseProvider.{NewDatabase} => new DbServices.Provider.{DatabaseName}.ProviderService(options, logger, validation, retry),
```

### 步驟 8: 更新解決方案檔案

在 `DBServices.sln` 中：

1. 加入專案宣告（在 Project 區段）：
```sln
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "DbServices.Provider.{DatabaseName}", "DbServices.Provider.{DatabaseName}\DbServices.Provider.{DatabaseName}.csproj", "{GUID}"
EndProject
```

2. 加入建置設定（在 GlobalSection(ProjectConfigurationPlatforms) 區段）：
```sln
{GUID}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
{GUID}.Debug|Any CPU.Build.0 = Debug|Any CPU
{GUID}.Release|Any CPU.ActiveCfg = Release|Any CPU
{GUID}.Release|Any CPU.Build.0 = Release|Any CPU
```

### 步驟 9: 更新主專案參考

在 `DBService/DBServices.csproj` 中加入專案參考：
```xml
<ProjectReference Include="..\DbServices.Provider.{DatabaseName}\DbServices.Provider.{DatabaseName}.csproj" />
```

## 🎯 關鍵實作要點

### 1. SQL 語法差異處理

不同資料庫的 SQL 語法可能不同，需要在 `SqlProviderFor{DatabaseName}` 中正確處理：

- **資料表查詢**: 使用資料庫特定的資訊架構（如 `INFORMATION_SCHEMA`、`pg_catalog` 等）
- **資料類型映射**: 實作 `ConvertDataTypeToDb` 方法，將 C# 類型映射到資料庫類型
- **主鍵生成**: 處理自動遞增主鍵的方式（IDENTITY、AUTO_INCREMENT、SERIAL 等）
- **外鍵約束**: 處理外鍵語法差異

### 2. 資料類型映射

實作 `ConvertDataTypeToDb` 方法，將 C# 類型映射到資料庫特定類型：

```csharp
public override string? ConvertDataTypeToDb(string? dataType) => dataType switch
{
    "Boolean" => "{DatabaseBooleanType}",
    "Int32" => "{DatabaseIntType}",
    "String" => "{DatabaseStringType}",
    // ... 其他類型
    _ => "{DefaultType}"
};
```

### 3. 資訊架構查詢

每個資料庫的資訊架構查詢方式不同：

- **SQL Server**: `INFORMATION_SCHEMA.TABLES`, `INFORMATION_SCHEMA.COLUMNS`
- **PostgreSQL**: `information_schema.tables`, `pg_catalog.pg_class`
- **MySQL**: `INFORMATION_SCHEMA.TABLES`, `INFORMATION_SCHEMA.COLUMNS`
- **Oracle**: `USER_TABLES`, `USER_TAB_COLUMNS`
- **SQLite**: `sqlite_master`, `PRAGMA table_info()`

### 4. 模型映射

實作三個映射方法：

- `MapToFieldBaseModel`: 將資料庫欄位資訊映射到 `FieldBaseModel`
- `MapToForeignBaseModel`: 將外鍵資訊映射到 `ForeignBaseModel`
- `MapToRecordBaseModel`: 將查詢結果映射到 `RecordBaseModel`

## ✅ 測試檢查清單

完成實作後，請確認：

- [ ] 專案可以成功編譯
- [ ] 可以建立資料庫連線
- [ ] 可以查詢資料表清單
- [ ] 可以查詢欄位資訊
- [ ] 可以執行 CRUD 操作
- [ ] 資料類型映射正確
- [ ] 主鍵自動遞增功能正常
- [ ] 外鍵約束正確處理

## 📚 參考範例

可以參考以下已實作的提供者：

- `DbServices.Provider.PostgreSQL` - PostgreSQL 18 實作範例
- `DbServices.Provider.Sqlite` - SQLite 實作範例
- `DbServices.Provider.MsSql` - SQL Server 實作範例
- `DbServices.Provider.MySql` - MySQL 實作範例
- `DbServices.Provider.Oracle` - Oracle 實作範例

## 🔄 未來改進建議

為了讓新增資料庫更加簡化，可以考慮：

1. **提供者註冊機制**: 使用反射或屬性標記自動註冊提供者
2. **範本專案**: 建立 Visual Studio 專案範本
3. **程式碼產生器**: 使用 Source Generator 自動產生樣板程式碼
4. **單元測試範本**: 提供標準化的測試範本

## 📖 相關文件

- [PostgreSQL 18 官方文件](https://www.postgresql.org/docs/18/index.html)
- [DBServices README](README.md)
- [DbServices.Core 文件](DbServices.Core/README.md)

