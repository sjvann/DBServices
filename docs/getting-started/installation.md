# 安裝

## 需求

- .NET 10 SDK 或更新版本
- 至少一種目標資料庫（開發時 SQLite 最省事）

## NuGet

主套件會帶入 Core 與全部提供者：

```bash
dotnet add package DBService
```

只裝需要的元件：

```bash
dotnet add package DbServices.Core
dotnet add package DbServices.Provider.Sqlite
dotnet add package DbServices.Provider.SqlServer
dotnet add package DbServices.Provider.MySQL
dotnet add package DbServices.Provider.Oracle
dotnet add package DbServices.Provider.PostgreSQL
```

套件 ID 與專案對照：

| 套件 | 專案 |
|------|------|
| `DBService` | `DBService/DBServices.csproj` |
| `DbServices.Core` | `DbServices.Core` |
| `DbServices.Provider.Sqlite` | `DbServices.Provider.Sqlite` |
| `DbServices.Provider.SqlServer` | `DbServices.Provider.MsSql` |
| `DbServices.Provider.MySQL` | `DbServices.Provider.MySql` |
| `DbServices.Provider.Oracle` | `DbServices.Provider.Oracle` |
| `DbServices.Provider.PostgreSQL` | `DbServices.Provider.PostgreSQL` |

CLI（`dbsql`）與 Web API 不打包成 NuGet，請從原始碼執行。

## 從原始碼

```bash
git clone https://github.com/sjvann/DBServices.git
cd DBServices
dotnet restore
dotnet build
dotnet test
```

## 本機預覽文件

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

瀏覽器開啟 `http://localhost:8080`。
