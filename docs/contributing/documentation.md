# 編寫文件

本站用 [DocFX](https://dotnet.github.io/docfx/)：概念頁是 Markdown，API 參考從 csproj 的 XML 註解產生。

## 本機建置

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

瀏覽器開啟 <http://localhost:8080>。輸出在 `docs/_site/`（已 gitignore）。

只產生 API YAML：

```bash
dotnet docfx metadata docs/docfx.json
```

## 目錄慣例

| 路徑 | 用途 |
|------|------|
| `docs/index.md` | 首頁 |
| `docs/toc.yml` | 頂部導覽 |
| `docs/getting-started/` | 安裝與快速開始 |
| `docs/guides/` | 操作指南 |
| `docs/architecture/` | 分層與連線 |
| `docs/cli/`、`docs/web/`、`docs/mcp/`、`docs/workbench/` | CLI、REST、GraphQL、MCP 宿主與報表工作台 |
| `docs/api/` | 產生的 API（`index.md` 可手寫；`*.yml` 勿提交） |
| `docs/template/` | modern 範本覆寫（GitHub 圖示） |

每個資料夾需要 `toc.yml`。新頁請加入該層 TOC。

## API 註解

`Directory.Build.props` 已開啟 `GenerateDocumentationFile`。公開型別與成員請寫 `<summary>`；缺少註解不會讓建置失敗（`CS1591` 已抑制），但 API 頁會較空。

## 發佈

推送到 `master` 會建置並部署 GitHub Pages。推送到 `Release/{版次}` 時，發行工作流程會再部署一次，讓站台與該版次套件同步。PR 只驗證文件能否建置。

站台：<https://sjvann.github.io/DBServices/>

倉庫 **Settings → Pages** 的 Source 須為 **GitHub Actions**。完整步驟見 [發行](release.md)。
