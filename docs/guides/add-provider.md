# 新增資料庫提供者

DBServices 用 Provider 模式擴充引擎。每個資料庫一個專案。

## 專案結構

```text
DbServices.Provider.{Name}/
  DbServices.Provider.{Name}.csproj
  ProviderService.cs
  SqlStringGenerator/SqlProviderFor{Name}.cs
```

## csproj 要點

- `TargetFramework`：`net10.0`
- 專案參考：`DbServices.Core`
- 套件：該引擎的 ADO.NET 驅動
- NuGet metadata：`PackageId` 建議 `DbServices.Provider.{Name}`

並加入 `DBServices.sln`，以及 `DBService/DBServices.csproj` 的 `ProjectReference`（若要從主套件帶出）。

## ProviderService

繼承 `DataBaseService`，在建構子設定：

1. `Func<DbConnection>`：每次呼叫 `new XxxConnection(connectionString)`，不要快取連線實例。
2. `SqlProviderBase` 實作：識別字引用、型別對應、目錄 SQL（列出資料表／欄位）。

進階建構子接收 `DbServiceOptions`、`ILogger<DataBaseService>`、`IValidationService`、`IRetryPolicyService`，與現有 Provider 對齊。

## SqlProvider

繼承 `SqlProviderBase`，實作抽象方法（limit／offset 語法、引號、布林與日期型別、information_schema 或同等目錄查詢）。參考 `DbServices.Provider.Sqlite` 或 `PostgreSQL`。

## 工廠與 DI

在 `MainService` 增加 `Use{Name}`，在 `IDbServiceBuilder` 增加 `Build{Name}`，在 `DBServices.Extensions.ServiceCollectionExtensions` 增加 `Add{Name}DbService`。

## 測試

在 `DBServiceTest` 增加該引擎的整合測試（可標 `[TestCategory("Integration")]`），至少覆蓋：開連線、建表、Insert／GetById、Session `Call tables`、參數化 `QuerySql`。

## 不要做的事

- 不要在 Provider 裡做跨方言 SQL 改寫。
- 不要持有全域靜態連線。
- 不要把驅動特定例外吞掉而不記錄。
