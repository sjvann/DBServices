# 版本與技術現況審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices 檢視與報表，2026-10-12，文件 `updated: 2026-10-20`）
- 審查日：2026-10-20
- 鏡頭：已承諾的技術是否對過網頁、現有專案或現行 starter，而不是只靠訓練資料斷言
- 範圍：棕地更新。點名版本對 `E:\sjvann\DBServices` 的 csproj。不要求新 starter
- 判定：**revise**
- 未改骨幹

2026-10-20 的版本句只核對了 csproj 裡的 `PackageReference`。Avalonia、`ModelContextProtocol`、`Microsoft.Data.Sqlite`、`Microsoft.Extensions.AI*` 與專案檔一致，那些套件今天仍在 NuGet 上。要改的是三件沒有被這次專案檔核對蓋到的主張：共用框架修補號、登入雜湊的型別，以及已經寫進專案的 PDF 套件。

## 發現

### 1. 共用框架寫成 10.0.12；專案檔沒有這個版本，NuGet 穩定版已是 10.0.13

- 位置：Stack 的「ASP.NET Core Blazor 10.0.12（共用框架）」與「ASP.NET Core Identity PasswordHasher 10.0.12（共用框架）」；Stack 結尾「版本對過 2026-10-20 的專案檔」
- 條件：實作者把 10.0.12 當成工作台共用框架的修補版
- 骨幹寫法：Blazor 與 `PasswordHasher` 都是 ASP.NET Core 10.0.12 共用框架
- 現況：`Workbench.Web.csproj` 只有未帶版本的 `FrameworkReference Include="Microsoft.AspNetCore.App"`。`Workbench.csproj` 使用 `Microsoft.NET.Sdk.Web`，同樣沒有修補號。2026-10-20 的 memlog 只列了 Avalonia、`ModelContextProtocol`、`Microsoft.Data.Sqlite`、`Microsoft.Extensions.AI*`，沒有重查共用框架。NuGet `Microsoft.AspNetCore.App.Ref` 與 `Microsoft.Extensions.Identity.Core` 的最新穩定版都是 10.0.13，發布日 2026-10-13。這台機器的 SDK 10.0.302 帶著 targeting pack 10.0.10，已安裝的 `Microsoft.AspNetCore.App` 也是 10.0.10。10.0.12 是 2026-10-12 那次網頁核對留下的數字。
- 後果：依骨幹安裝 10.0.12 會落後 2026-10-13 的穩定修補。本機建置實際用的是 10.0.10。這兩個 10.0.12 都不在「對過專案檔」的範圍裡。
- 修正：Blazor 列改寫成目標框架 `net10.0`，共用框架修補號跟隨已安裝的 `Microsoft.AspNetCore.App`。若要釘修補版，釘 10.0.13，並寫明本機仍是 10.0.10。`PasswordHasher` 那一列見發現 2，不要再掛 10.0.12。

### 2. 登入雜湊是專案裡的 PBKDF2，不是共用框架的 `PasswordHasher`

- 位置：AD-6；Stack 的「ASP.NET Core Identity PasswordHasher」
- 條件：實作者用 `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` 產生 `passwordVerifier`
- 骨幹寫法：宿主以 ASP.NET Core 10.0.12 共用框架的 `PasswordHasher` 寫入驗證材料
- 現況：`Workbench.Core` 定義自己的 `IPasswordHasher`。`Workbench/Program.cs` 註冊的是 `Workbench.Adapters.Pbkdf2PasswordHasher`。它呼叫 `Rfc2898DeriveBytes.Pbkdf2`，SHA-256、100000 次，驗證字串形狀是 `pbkdf2$100000$` 加鹽與雜湊的 Base64。全庫沒有 `Microsoft.AspNetCore.Identity.PasswordHasher`。該型別今天仍在：ASP.NET Core 10 文件的組件是 `Microsoft.Extensions.Identity.Core.dll`，NuGet 上 `Microsoft.Extensions.Identity.Core` 10.0.13 於 2026-10-13 發布。2026-10-12 memlog 把共用框架雜湊寫成假設；2026-10-20 的專案檔核對沒有打開這個型別。
- 後果：照骨幹會寫入 Identity 的驗證字串，既有 `pbkdf2$...` 紀錄會驗不過。共用框架裡的型別還在，只是這個工作台沒有用它。
- 修正：AD-6 與 Stack 改寫成 `Pbkdf2PasswordHasher`：SHA-256、100000 次、驗證字串 `pbkdf2$100000$<salt>$<hash>`。若日後要改用 Identity 的 `PasswordHasher<TUser>`，另立決定，並使用當時的共用框架修補版。

### 3. PDF 套件已經是 QuestPDF 2026.9.1，Deferred 仍寫尚未選定

- 位置：Deferred「PDF 與 CSV 的具體套件」；AD-7 的「PDF 預設 A4，換頁以區塊為界」；Stack「這一輪不新增套件」
- 條件：實作者把 PDF 套件當成還沒選定，或 2026-10-20 的 csproj 掃描當成套件清單已完整
- 骨幹寫法：PDF 與 CSV 的具體套件尚未選定；選定時不得再查資料
- 現況：`Workbench.Adapters.csproj` 已參考 `QuestPDF` 2026.9.1。`SnapshotPdfWriter` 使用 `PageSizes.A4`，區塊之間呼叫 `PageBreak()`。NuGet `questpdf` 索引的最後一版就是 2026.9.1，發布日 2026-09-25。倉儲裡沒有 CSV 套件。2026-10-20 的版本句沒有提到 QuestPDF。
- 後果：PDF 套件、A4 與依區塊換頁都已經在專案裡。Deferred 把 PDF 與 CSV 綁在一起，會讓人再選一次 PDF 套件，或漏記這個已釘的版本。
- 修正：Stack 加入 `QuestPDF` 2026.9.1。Deferred 只留 CSV。AD-7 的 A4 與換頁已經和 `SnapshotPdfWriter` 一致，套件名補上即可。

## 已核對且成立

| 主張 | 核對結果 |
| --- | --- |
| `net10.0` | 工作台各 csproj、`DbServices.ModelSidecar.csproj`、`McpService.csproj` 的 `TargetFramework` 都是 `net10.0`。本機 SDK 10.0.302。 |
| Avalonia 12.1.4、Avalonia.Desktop 12.1.4 | `Workbench.Avalonia.csproj` 與此相同。NuGet 索引最後一版都是 12.1.4，Avalonia 發布日 2026-10-09。12.1.4 nuspec 含 `net10.0` 與 `net8.0`。同一 csproj 還有 `Avalonia.Themes.Fluent` 12.1.4；Stack 沒點名，版本與 Avalonia 相同。 |
| `Microsoft.Data.Sqlite` 10.0.12 | `Workbench.Storage.Sqlite.csproj`、`Workbench.Tests.csproj`、`DbServices.Provider.Sqlite.csproj` 都是 10.0.12。與「對過專案檔」相符。NuGet 另有較新穩定版，見下一節。 |
| `Microsoft.Extensions.AI` 10.10.0、Abstractions 10.10.1、OpenAI 10.10.1 | `DbServices.ModelSidecar.csproj` 與此三版相同。三個套件都仍列在 NuGet。側車目標框架是 `net10.0`，輸出為 `Exe`。 |
| `ModelContextProtocol` 2.2.0 | `Workbench.Adapters.csproj` 是 2.2.0。NuGet 索引最後一版仍是 2.2.0，發布日 2026-08-13。`McpService.csproj` 的 `ModelContextProtocol.AspNetCore` 也是 2.2.0，該套件索引同樣停在 2.2.0。父脊椎的套件號仍然對得上。 |
| `AllowedHosts` 出貨值 `*` | `Workbench\appsettings.json` 是 `"AllowedHosts": "*"`。AD-2 與現有專案一致。這次不另要求 Blazor starter。 |
| `GET /connections` | `McpService\Program.cs` 有 `MapGet("/connections", ...)`。路由仍在。 |
| CSV 套件未選定 | 倉儲 csproj 沒有 CSV 套件。Deferred 的 CSV 半邊仍然成立。PDF 半邊見發現 3。 |

## 已核對的落差（不列為缺陷）

骨幹把 `Microsoft.Data.Sqlite` 與 `Microsoft.Extensions.AI*` 寫成 2026-10-20 的專案檔版本。這句對 csproj 成立，沒有把它們寫成 NuGet 最新版。

- `Microsoft.Data.Sqlite` 最新穩定版是 10.0.13，發布日 2026-10-13。專案與骨幹維持 10.0.12。索引裡的 11.0.0 各版都是預覽，專案留在 `net10.0`。
- `Microsoft.Extensions.AI`、`Microsoft.Extensions.AI.Abstractions`、`Microsoft.Extensions.AI.OpenAI` 的最新穩定版都是 10.11.0，發布日 2026-10-14。側車與骨幹維持 10.10.0 / 10.10.1 / 10.10.1。
- `Directory.Build.props` 在 `Workbench.Storage.Sqlite` 額外釘 `SQLitePCLRaw.lib.e_sqlite3` 3.53.3。骨幹沒有為這個傳遞依賴另寫版本；`Microsoft.Data.Sqlite` 本身的 10.0.12 仍然對得上 csproj。

這一輪寫明不新增套件。上述較新的 NuGet 穩定版是後續可升級的修補，不是這份骨幹對專案檔的錯釘。

## 證據

- 查詢日：2026-10-20
- 專案：`Workbench.Avalonia\Workbench.Avalonia.csproj`、`Workbench.Adapters\Workbench.Adapters.csproj`、`Workbench.Storage.Sqlite\Workbench.Storage.Sqlite.csproj`、`Workbench.Tests\Workbench.Tests.csproj`、`DbServices.Provider.Sqlite\DbServices.Provider.Sqlite.csproj`、`DbServices.ModelSidecar\DbServices.ModelSidecar.csproj`、`McpService\McpService.csproj`、`Workbench.Web\Workbench.Web.csproj`、`Workbench\Workbench.csproj`、`Workbench\Program.cs`、`Workbench\appsettings.json`、`Workbench.Adapters\Pbkdf2PasswordHasher.cs`、`Workbench.Adapters\SnapshotPdfWriter.cs`、`Workbench.Core\Ports\Ports.cs`、`McpService\Program.cs`、`Directory.Build.props`
- memlog：2026-10-12 的 NuGet 核對；2026-10-20 只寫「Stack pins match the csproj files」，列出的是 Avalonia 12.1.4、`ModelContextProtocol` 2.2.0、`Microsoft.Data.Sqlite` 10.0.12、`Microsoft.Extensions.AI` 10.10.0、Abstractions 10.10.1、OpenAI 10.10.1
- NuGet 版本索引：`https://api.nuget.org/v3-flatcontainer/avalonia/index.json`、`avalonia.desktop/index.json`、`microsoft.data.sqlite/index.json`、`microsoft.extensions.ai/index.json`、`microsoft.extensions.ai.abstractions/index.json`、`microsoft.extensions.ai.openai/index.json`、`modelcontextprotocol/index.json`、`modelcontextprotocol.aspnetcore/index.json`、`microsoft.aspnetcore.app.ref/index.json`、`microsoft.extensions.identity.core/index.json`、`questpdf/index.json`
- 發布日：`microsoft.aspnetcore.app.ref/10.0.13` 與 `microsoft.extensions.identity.core/10.0.13` 為 2026-10-13；`microsoft.data.sqlite/10.0.13` 為 2026-10-13；三個 `microsoft.extensions.ai*` 的 10.11.0 為 2026-10-14；`avalonia/12.1.4` 為 2026-10-09；`modelcontextprotocol/2.2.0` 為 2026-08-13；`questpdf/2026.9.1` 為 2026-09-25
- Avalonia 12.1.4 nuspec：`https://api.nuget.org/v3-flatcontainer/avalonia/12.1.4/avalonia.nuspec`，依賴群組含 `net10.0`
- `PasswordHasher<TUser>`：`https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1?view=aspnetcore-10.0`，組件 `Microsoft.Extensions.Identity.Core.dll`
- 本機：`dotnet --list-runtimes` 的 `Microsoft.AspNetCore.App` 為 10.0.10；targeting pack `Microsoft.AspNetCore.App.Ref` 與 `Microsoft.NETCore.App.Ref` 皆為 10.0.10
