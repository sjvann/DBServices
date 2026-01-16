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
   - **Expiration**: 選擇適當的過期時間（建議 1 年）
   - **Select Scopes**: 選擇 `Push new packages and package versions`
   - **Select Packages**: 選擇 `All packages` 或指定套件模式 `DbServices*`
4. 點擊 "Create" 並**立即複製**產生的 API Key（只會顯示一次）

### 3. 設定環境變數

#### Windows (PowerShell)
```powershell
# 臨時設定（僅當前會話有效）
$env:NUGET_API_KEY = "your_actual_api_key_here"

# 永久設定（使用者層級）
[Environment]::SetEnvironmentVariable("NUGET_API_KEY", "your_actual_api_key_here", "User")
```

#### Windows (CMD)
```cmd
# 臨時設定
set NUGET_API_KEY=your_actual_api_key_here

# 永久設定（需要重新開啟命令提示字元）
setx NUGET_API_KEY "your_actual_api_key_here"
```

#### Linux/macOS (Bash)
```bash
# 臨時設定
export NUGET_API_KEY="your_actual_api_key_here"

# 永久設定（加入到 ~/.bashrc 或 ~/.zshrc）
echo 'export NUGET_API_KEY="your_actual_api_key_here"' >> ~/.bashrc
source ~/.bashrc
```

## 🎯 發布步驟

### 方法 1: 自動發布（推薦）

#### Windows
```cmd
# 確保已設定 NUGET_API_KEY 環境變數
# 然後執行：
publish-nuget.bat
```

#### Linux/macOS
```bash
# 設定執行權限
chmod +x publish-nuget.sh

# 確保已設定 NUGET_API_KEY 環境變數
# 然後執行：
./publish-nuget.sh
```

### 方法 2: 手動發布

#### 步驟 1: 清理和建置
```bash
# 清理之前的建置
dotnet clean

# 建置 Release 版本
dotnet build --configuration Release
```

#### 步驟 2: 打包套件
```bash
# 打包所有專案（不重新建置）
dotnet pack --configuration Release --no-build
```

#### 步驟 3: 驗證套件
```bash
# 檢查生成的套件
# Windows
dir /s *.nupkg

# Linux/macOS
find . -name "*.nupkg" -type f
```

#### 步驟 4: 發布套件（按順序）

**重要**: 必須先發布依賴套件，再發布主套件。

```bash
# 1. 發布核心套件（必須最先發布）
dotnet nuget push "DbServices.Core\bin\Release\DbServices.Core.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

# 2. 發布資料庫提供者（可以並行發布）
dotnet nuget push "DbServices.Provider.Sqlite\bin\Release\DbServices.Provider.Sqlite.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

dotnet nuget push "DbServices.Provider.MsSql\bin\Release\DbServices.Provider.SqlServer.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

dotnet nuget push "DbServices.Provider.MySql\bin\Release\DbServices.Provider.MySQL.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

dotnet nuget push "DbServices.Provider.Oracle\bin\Release\DbServices.Provider.Oracle.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

dotnet nuget push "DbServices.Provider.PostgreSQL\bin\Release\DbServices.Provider.PostgreSQL.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

# 3. 最後發布主套件（依賴所有其他套件）
dotnet nuget push "DBService\bin\Release\DbServices.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
```

**注意**: 
- Windows 使用 `%NUGET_API_KEY%`
- Linux/macOS 使用 `$NUGET_API_KEY`

## 📦 套件清單

| 套件名稱 | 檔案位置 | 說明 |
|----------|----------|------|
| `DbServices.Core` | `DbServices.Core\bin\Release\DbServices.Core.2.0.0.nupkg` | 核心庫（必須最先發布） |
| `DbServices.Provider.Sqlite` | `DbServices.Provider.Sqlite\bin\Release\DbServices.Provider.Sqlite.2.0.0.nupkg` | SQLite 提供者 |
| `DbServices.Provider.SqlServer` | `DbServices.Provider.MsSql\bin\Release\DbServices.Provider.SqlServer.2.0.0.nupkg` | SQL Server 提供者 |
| `DbServices.Provider.MySQL` | `DbServices.Provider.MySql\bin\Release\DbServices.Provider.MySQL.2.0.0.nupkg` | MySQL 提供者 |
| `DbServices.Provider.Oracle` | `DbServices.Provider.Oracle\bin\Release\DbServices.Provider.Oracle.2.0.0.nupkg` | Oracle 提供者 |
| `DbServices.Provider.PostgreSQL` | `DbServices.Provider.PostgreSQL\bin\Release\DbServices.Provider.PostgreSQL.2.0.0.nupkg` | PostgreSQL 提供者 |
| `DbServices` | `DBService\bin\Release\DbServices.2.0.0.nupkg` | 主套件（包含所有提供者） |

## ✅ 發布後驗證

### 1. 檢查 NuGet.org
1. 前往 [您的套件管理頁面](https://www.nuget.org/account/Packages)
2. 確認所有套件都顯示為 "Listed"
3. 檢查套件版本、描述、標籤等資訊是否正確
4. 確認 README 文件已正確顯示

### 2. 測試安裝
```bash
# 建立測試專案
mkdir test-dbservices
cd test-dbservices
dotnet new console

# 安裝主套件
dotnet add package DbServices --version 2.0.0

# 或安裝個別套件
dotnet add package DbServices.Core --version 2.0.0
dotnet add package DbServices.Provider.PostgreSQL --version 2.0.0

# 測試程式碼
# 在 Program.cs 中新增測試代碼
```

### 3. 等待索引
- NuGet.org 需要 **5-10 分鐘**來索引新套件
- 套件可能在搜尋結果中延遲顯示
- 可以透過直接 URL 訪問：`https://www.nuget.org/packages/DbServices/2.0.0`

## 🔄 更新版本

當需要發布新版本時：

1. **更新版本號**
   - 編輯所有 `.csproj` 檔案中的 `<Version>` 標籤
   - 更新 `RELEASE_NOTES.md`
   - 更新 `README.md` 中的版本資訊

2. **更新發布說明**
   - 編輯 `.csproj` 檔案中的 `<PackageReleaseNotes>` 標籤

3. **重新建置和發布**
   ```bash
   dotnet clean
   dotnet build --configuration Release
   dotnet pack --configuration Release --no-build
   # 執行發布腳本或手動發布
   ```

## ⚠️ 重要注意事項

### 安全性
- **絕不要**將 API Key 提交到版本控制系統（Git）
- 定期輪換 API Key（建議每 6-12 個月）
- 使用最小權限原則設定 API Key
- 如果 API Key 洩露，立即在 NuGet.org 上撤銷

### 套件管理
- **版本號不能重複使用**：一旦發布到 NuGet.org，版本號就無法再次使用
- **刪除套件**：需要聯繫 NuGet 支援，且可能影響依賴該套件的專案
- **建議在發布前測試**：使用本地 NuGet 源或 NuGet.org 的預覽功能

### 版本控制
- 遵循 [語義化版本控制](https://semver.org/lang/zh-TW/) (Semantic Versioning)
  - **主要版本** (2.0.0 → 3.0.0): 不相容的 API 變更
  - **次要版本** (2.0.0 → 2.1.0): 向下相容的功能新增
  - **修補版本** (2.0.0 → 2.0.1): 向下相容的問題修正

### 發布順序
1. **必須先發布** `DbServices.Core`（其他套件都依賴它）
2. **然後發布** 所有提供者套件（可以並行）
3. **最後發布** `DbServices` 主套件（依賴所有其他套件）

## 🐛 常見問題

### 問題 1: API Key 無效
**錯誤訊息**: `Response status code does not indicate success: 401 (Unauthorized)`

**解決方法**:
- 確認 API Key 是否正確設定
- 確認 API Key 是否已過期
- 確認 API Key 是否有發布權限

### 問題 2: 套件已存在
**錯誤訊息**: `Response status code does not indicate success: 409 (Conflict)`

**解決方法**:
- 版本號已存在，需要更新版本號
- 檢查是否已經發布過該版本

### 問題 3: 依賴套件不存在
**錯誤訊息**: `Unable to find package DbServices.Core`

**解決方法**:
- 確認已先發布 `DbServices.Core`
- 等待 5-10 分鐘讓 NuGet.org 索引完成
- 檢查套件名稱是否正確

## 📞 支援

如果發布過程中遇到問題：

1. 檢查 [NuGet 文件](https://docs.microsoft.com/nuget/)
2. 查看 [NuGet 狀態頁面](https://status.nuget.org/)
3. 在 [GitHub Issues](https://github.com/sjvann/DBServices/issues) 回報問題
4. 查看 [NuGet 常見問題](https://docs.microsoft.com/nuget/nuget-org/nuget-org-faq)

## 🎉 發布檢查清單

發布前請確認：

- [ ] 所有專案已正確建置（無錯誤）
- [ ] 所有套件的版本號一致
- [ ] 所有套件的描述和標籤已更新
- [ ] README 文件已包含在套件中
- [ ] API Key 已正確設定
- [ ] 已測試本地安裝套件
- [ ] 發布順序正確（Core → Providers → Main）

---

**祝您發布成功！** 🚀

