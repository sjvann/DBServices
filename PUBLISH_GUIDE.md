# 🚀 DbServices NuGet 發布指南

## 📋 準備工作

### 1. 建立 NuGet 帳戶
1. 前往 [NuGet.org](https://www.nuget.org)
2. 點擊右上角 "Sign in"
3. 使用 Microsoft、GitHub 或 AAD 帳戶登入
4. 完成帳戶設定

### 2. 取得 API Key
1. 登入後前往 [API Keys](https://www.nuget.org/account/apikeys)
2. 點擊 "Create" 建立新的 API Key
3. 設定以下項目：
   - **Key Name**: `DbServices Publishing Key`
   - **Expiration**: 選擇適當的過期時間
   - **Select Scopes**: 選擇 `Push new packages and package versions`
   - **Select Packages**: 選擇 `All packages` 或指定套件模式 `DbServices*`
4. 點擊 "Create" 並複製產生的 API Key

### 3. 設定環境變數
#### Windows (PowerShell)
```powershell
# 設定 API Key 環境變數 (請替換為您的實際 API Key)
$env:NUGET_API_KEY = "your_actual_api_key_here"

# 或永久設定
[Environment]::SetEnvironmentVariable("NUGET_API_KEY", "your_actual_api_key_here", "User")
```

#### Linux/macOS (Bash)
```bash
# 設定 API Key 環境變數
export NUGET_API_KEY="your_actual_api_key_here"

# 加入到 ~/.bashrc 或 ~/.zshrc 以永久設定
echo 'export NUGET_API_KEY="your_actual_api_key_here"' >> ~/.bashrc
```

## 🎯 發布步驟

### 自動發布 (推薦)

#### Windows
```cmd
# 執行發布腳本
publish-nuget.bat
```

#### Linux/macOS
```bash
# 設定執行權限
chmod +x publish-nuget.sh

# 執行發布腳本
./publish-nuget.sh
```

### 手動發布

1. **建置專案**
   ```bash
   dotnet clean
   dotnet build --configuration Release
   ```

2. **打包套件**
   ```bash
   dotnet pack --configuration Release --no-build
   ```

3. **發布個別套件**
   ```bash
   # 核心套件
   dotnet nuget push "DbServices.Core\bin\Release\DbServices.Core.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

   # SQLite 提供者
   dotnet nuget push "DbServices.Provider.Sqlite\bin\Release\DbServices.Provider.Sqlite.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

   # SQL Server 提供者
   dotnet nuget push "DbServices.Provider.MsSql\bin\Release\DbServices.Provider.SqlServer.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

   # MySQL 提供者
   dotnet nuget push "DbServices.Provider.MySql\bin\Release\DbServices.Provider.MySQL.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

   # Oracle 提供者
   dotnet nuget push "DbServices.Provider.Oracle\bin\Release\DbServices.Provider.Oracle.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

   # 主套件
   dotnet nuget push "DBService\bin\Release\DbServices.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
   ```

## 📦 已生成的套件

| 套件名稱 | 檔案位置 | 大小 |
|----------|----------|------|
| `DbServices` | `DBService\bin\Release\DbServices.2.0.0.nupkg` | ~12 KB |
| `DbServices.Core` | `DbServices.Core\bin\Release\DbServices.Core.2.0.0.nupkg` | ~40 KB |
| `DbServices.Provider.Sqlite` | `DbServices.Provider.Sqlite\bin\Release\DbServices.Provider.Sqlite.2.0.0.nupkg` | ~8.5 KB |
| `DbServices.Provider.SqlServer` | `DbServices.Provider.MsSql\bin\Release\DbServices.Provider.SqlServer.2.0.0.nupkg` | ~7.5 KB |
| `DbServices.Provider.MySQL` | `DbServices.Provider.MySql\bin\Release\DbServices.Provider.MySQL.2.0.0.nupkg` | ~8.4 KB |
| `DbServices.Provider.Oracle` | `DbServices.Provider.Oracle\bin\Release\DbServices.Provider.Oracle.2.0.0.nupkg` | ~8.4 KB |

## ✅ 發布後驗證

### 1. 檢查 NuGet.org
1. 前往 [您的套件管理頁面](https://www.nuget.org/account/Packages)
2. 確認所有套件都顯示為 "Listed"
3. 檢查套件版本、描述、標籤等資訊

### 2. 測試安裝
```bash
# 建立測試專案
mkdir test-dbservices
cd test-dbservices
dotnet new console

# 安裝套件
dotnet add package DbServices

# 測試程式碼
# 在 Program.cs 中新增測試代碼
```

### 3. 等待索引
- NuGet.org 需要 5-10 分鐘來索引新套件
- 套件可能在搜尋結果中延遲顯示

## 🔄 更新版本

當需要發布新版本時：

1. **更新版本號**
   - 編輯所有 `.csproj` 檔案中的 `<Version>` 標籤
   - 更新 `RELEASE_NOTES.md`

2. **重新建置和發布**
   ```bash
   dotnet clean
   dotnet build --configuration Release
   dotnet pack --configuration Release --no-build
   # 執行發布腳本
   ```

## ⚠️ 注意事項

### 安全性
- **絕不要**將 API Key 提交到版本控制
- 定期輪換 API Key
- 使用最小權限原則設定 API Key

### 套件管理
- 一旦發布到 NuGet.org，版本號不能重複使用
- 刪除套件需要聯繫 NuGet 支援
- 建議在發布前使用本地 NuGet 源測試

### 版本控制
- 遵循 [語義化版本控制](https://semver.org/lang/zh-TW/)
- 主要版本：不相容的 API 變更
- 次要版本：向下相容的功能新增
- 修補版本：向下相容的問題修正

## 📞 支援

如果發布過程中遇到問題：

1. 檢查 [NuGet 文件](https://docs.microsoft.com/nuget/)
2. 查看 [NuGet 狀態頁面](https://status.nuget.org/)
3. 在 [GitHub Issues](https://github.com/sjvann/DBServices/issues) 回報問題

---

**祝您發布成功！** 🎉
