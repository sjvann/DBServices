# 發行

公開發行走 GitHub Actions：從 `Release/{版次}` 分支建立 GitHub Release、上傳 NuGet 套件，並把該版次的 DocFX 站台部署到 GitHub Pages。

## 分支慣例

| 項目 | 規則 |
|------|------|
| 分支 | `Release/{版次}`，例如 `Release/2.1.0` |
| 版次 | `major.minor.patch`，可選 `v` 前綴或預發佈後綴（`2.1.0-rc.1`） |
| 標籤 | 工作流程會建立 `v{版次}`（例如 `v2.1.0`） |
| 套件版本 | 必須與 `Directory.Build.props` 的 `<Version>` 相同 |

不要從 `master` 直接切 GitHub Release。`master` 的文件變更仍會更新 Pages；發行分支會再部署一次，讓站台與該版次套件對齊。

## 步驟

1. 在 `master` 把 `Directory.Build.props` 的 `<Version>` 改成目標版次，並視需要更新 `RELEASE_NOTES.md`。
2. 從該提交建立並推送發行分支：

   ```bash
   git checkout master
   git checkout -b Release/2.1.0
   git push -u origin Release/2.1.0
   ```

3. 工作流程 [Release](https://github.com/sjvann/DBServices/actions/workflows/release.yml) 會：
   - 解析版次並核對 `Directory.Build.props`
   - 建置、測試（略過 `TestCategory=Integration`）、打包 NuGet
   - 建立 GitHub Release（附 `.nupkg`）
   - 若已設定 `NUGET_API_KEY`，推送到 NuGet.org
   - 建置 DocFX 並部署 [GitHub Pages](https://sjvann.github.io/DBServices/)

同一分支再次推送會更新同一個 `v{版次}` Release。NuGet 版本不可覆寫；已上架的版次會因 `--skip-duplicate` 被略過。

## GitHub 設定

1. **Settings → Pages → Source** 選 **GitHub Actions**。
2. （選用）**Settings → Secrets and variables → Actions** 新增 `NUGET_API_KEY`，工作流程才會推送 NuGet.org。

也可在 Actions 開啟 **Release** → **Run workflow**。在 `Release/{版次}` 分支上執行會用分支名稱；在 `master` 上執行且未填版次時，會改用 `Directory.Build.props` 的 `<Version>`。版次仍須與該檔一致。

## 文件站

| 事件 | Pages |
|------|--------|
| `Release/{版次}` 發行成功 | 部署該提交的文件（與套件版次同步） |
| 推送到 `master`（文件或 API 變更） | 建置並部署最新文件 |
| PR | 只建置、不部署 |
| `workflow_dispatch`（Docs） | 手動部署目前分支的文件 |
