# 版本與技術現況審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices 報表工作台，2026-10-12）
- 審查日：2026-10-12
- 鏡頭：已承諾的技術是否對過網頁、現有專案或現行 starter，而不是只靠訓練資料斷言
- 判定：**revise**
- 未改骨幹

Stack 表上的版本號、發布日，以及「`Microsoft.Extensions.AI.OpenAI` 支援 OpenAI 相容端點」這句，這次都對得上 NuGet 與套件說明。要改的是兩件套件契約與綠色場域 starter 預設：本地金鑰可以空白，以及 `model.json` 的覆寫順序。這兩條寫成了規則，memlog 的版本核對沒有覆蓋它們。

## 發現

### 1. 穩定的 OpenAI 用戶端拒絕空白金鑰，而且不設端點時會打到 api.openai.com

- 位置：AD-9、AD-11
- 條件：側車用套件說明裡的穩定建構式組出 `IChatClient`，本地設定的 `apiKey` 為空白，或沒有另外指定端點
- 骨幹寫法：雲端與本地都用 `Microsoft.Extensions.AI.OpenAI`，差別只在 AD-9 的端點。本地金鑰可空白，金鑰只從環境變數讀入。
- 現況：`Microsoft.Extensions.AI.OpenAI` 10.10.1 的說明是「OpenAI 套件與 OpenAI 相容端點的 `IChatClient` 實作」，並依賴 `OpenAI` >= 2.14.0。NuGet 上 `OpenAI` 的最新穩定版就是 2.14.0（2026-09-15），其 net10.0 依賴 `System.ClientModel` 1.15.0。該版 `ApiKeyCredential` 的建構式呼叫 `Update`，而 `Update` 對金鑰做 `Argument.AssertNotNullOrEmpty`：null 丟 `ArgumentNullException`，空字串丟 `ArgumentException`。`ChatClient(string model, string apiKey)` 會把該字串直接送進 `ApiKeyCredential`。`OpenAIClient.GetEndpoint` 在選項沒有 `Endpoint` 時回傳 `https://api.openai.com/v1`。套件 README 的第一個範例就是這個金鑰建構式，沒有設定端點；自訂基底網址的範例才設定 `OpenAIClientOptions.Endpoint`，而且仍然傳入非空白的 `ApiKeyCredential`。不帶金鑰的 `AuthenticationPolicy` 建構式標成 `OPENAI001`。
- 後果：本地端點可連、金鑰留白時，用戶端在送出探測前就建立失敗，回不到 `model_ok`。照 README 第一個範例實作時，AD-9 的端點不會被用到，雲端與本地都會打到 `https://api.openai.com/v1`。相容端點這件事本身成立，但空白金鑰與「不設端點」都不是這個用戶端的活預設。
- 修正：在 AD-11 寫明兩種 placement 都要把 AD-9 的端點放進 `OpenAIClientOptions.Endpoint`，並註明 SDK 預設是 `https://api.openai.com/v1`。儲存的本地金鑰可以空白，但不得把 null 或空字串傳進 `ApiKeyCredential`。本地忽略金鑰時，使用不寫入設定檔的非空白佔位憑證，或明確採用實驗性的 `AuthenticationPolicy` 建構式。

### 2. `model.json` 的覆寫順序不是 Blazor starter 的活預設

- 位置：AD-9
- 條件：實作者用現行 `dotnet new blazor`，再靠 `WebApplication.CreateBuilder` 的預設組態鏈讀取模型設定
- 骨幹寫法：覆寫順序是內容根的 `model.json`，然後 user secrets，然後環境變數。
- 現況：2026-10-12 在本機 SDK 10.0.302 執行 `dotnet new blazor --framework net10.0`，產生的 `appsettings.json` 只有 Logging 與 `"AllowedHosts": "*"`，沒有 `model.json`，csproj 也沒有 `UserSecretsId`。ASP.NET Core 10 組態文件對 `WebApplicationBuilder` 的應用程式組態，由高到低是：命令列、未加 `ASPNETCORE_` 或 `DOTNET_` 前綴的環境變數、僅在 `Development` 載入的 user secrets、`appsettings.{Environment}.json`、`appsettings.json`。沒有 `UserSecretsId` 時，user secrets 不會成為來源。命令列比環境變數優先。memlog 只核對套件版本，沒有核對這條組態鏈。
- 後果：照 starter 會讀 `appsettings.json`、環境變數與命令列，不會讀 `model.json`，user secrets 也不會蓋過檔案。命令列上的金鑰會贏過環境變數，和 AD-11「金鑰不進程序引數」疊在一起時，預設組態提供者仍會把該引數收進組態。
- 修正：在 AD-9 寫明必須自行把內容根的 `model.json` 加進組態，而且要加在 user secrets 之前；starter 不會載入這個檔。user secrets 要有 `UserSecretsId`，預設宿主只在 `Development` 載入。模型金鑰不得取自命令列提供者，因為該提供者的優先順序高於環境變數。

## 已核對且成立

| 主張 | 核對結果 |
| --- | --- |
| `net10.0` | 方案內現有 csproj 的 `TargetFramework` 都是 `net10.0`，含 `WebService`、`McpService`、`DbServices.Provider.Sqlite`。2026-10-12 產生的 Blazor starter 也是 `net10.0`，Nullable 與 ImplicitUsings 開啟。 |
| ASP.NET Core / Blazor 10.0.12 | NuGet `Microsoft.AspNetCore.OpenApi` 與 `Microsoft.AspNetCore.App.Ref` 的最新穩定版都是 10.0.12，發布日 2026-09-08。`WebService.csproj` 已釘 `Microsoft.AspNetCore.OpenApi` 10.0.12。 |
| `PasswordHasher` 在共用框架 | `Microsoft.Extensions.Identity.Core` 10.0.12 存在（2026-09-08）。ASP.NET Core 10 文件中的 `PasswordHasher<TUser>` 組件是 `Microsoft.Extensions.Identity.Core.dll`。本機 `Microsoft.AspNetCore.App` 10.0.10 目錄內有這個 DLL，型別屬於共用框架，不是 NuGet 上那個停在 2.3.x 的舊 `Microsoft.AspNetCore.Identity` 套件。 |
| Avalonia 12.1.4、Avalonia.Desktop 12.1.4 | 兩者皆為 NuGet 最新穩定版，發布日 2026-10-09T13:28Z。nuspec 含 `net10.0` 與 `net8.0`。本機沒有安裝 Avalonia 專案範本，這次是對套件本身，不是對 `dotnet new avalonia`。 |
| `Microsoft.Data.Sqlite` 10.0.12 | NuGet 最新穩定版，發布日 2026-09-08。`DbServices.Provider.Sqlite.csproj` 已釘同一版。沒有採用 .NET 11 的預覽套件。 |
| `Microsoft.Extensions.AI` 10.10.0 | NuGet 最新穩定版，發布日 2026-09-09。nuspec 含 `net10.0`，並依賴 `Microsoft.Extensions.AI.Abstractions` 10.10.0（最低版本）。10.10.1 沒有再發這個套件。 |
| `Microsoft.Extensions.AI.Abstractions` 10.10.1、`Microsoft.Extensions.AI.OpenAI` 10.10.1 | 兩者皆為 NuGet 最新穩定版，發布日 2026-09-25。OpenAI 套件含 `net10.0`，依賴 Abstractions >= 10.10.1 與 `OpenAI` >= 2.14.0。和 10.10.0 的主套件並存時，直接參考 10.10.1 符合目前穩定線。 |
| OpenAI 相容端點，雲端與本地同一用戶端 | 成立。套件說明寫明 OpenAI 相容端點。`OpenAI` 2.14.0 README 的自訂基底網址範例用於 proxy 或自架的相容 LLM，透過 `OpenAIClientOptions.Endpoint`。預設端點才是 `https://api.openai.com/v1`。見發現 1：端點必須顯式設定，空白金鑰不能進穩定憑證。 |
| `ModelContextProtocol` 2.2.0 | NuGet 最新穩定版，發布日 2026-08-13。nuspec 含 `net10.0`，依賴 `ModelContextProtocol.Core` 2.2.0。AD-3 的用戶端套件名稱與此相符。 |
| `ModelContextProtocol.AspNetCore` 2.2.0 | NuGet 最新穩定版，同日發布。`McpService.csproj` 已釘 2.2.0。net10.0 依賴 `ModelContextProtocol` 2.2.0。父脊椎的宿主套件與此相符。 |
| `AllowedHosts` 出貨值 `*` | 本次產生的 Blazor Web App `appsettings.json` 就是 `"AllowedHosts": "*"`。`WebService\appsettings.json` 也是這個值。AD-2 與現行 starter、現有專案一致。 |
| 側車不另釘 PDF 套件 | Deferred 未指名套件。沒有未核對的 PDF 版本。 |

## 已核對的環境差（不列為缺陷）

骨幹把 Blazor 寫成共用框架 10.0.12，與 OpenApi 套件同一修補帶。NuGet 上 `Microsoft.AspNetCore.App.Ref` 10.0.12 確實是目前最新穩定版。這台機器的 SDK 10.0.302 仍帶著 targeting pack 10.0.10，已安裝的 `Microsoft.AspNetCore.App` 也是 10.0.10。OpenApi 的 PackageReference 不會把共用框架執行階段升到 10.0.12。這是工作站落後已發布的修補版，骨幹釘的版本本身沒有過期。

## 證據

- 查詢日：2026-10-12
- NuGet 註冊葉：`https://api.nuget.org/v3/registration5-semver1/avalonia/12.1.4.json`、`avalonia.desktop/12.1.4.json`、`microsoft.data.sqlite/10.0.12.json`、`microsoft.extensions.ai/10.10.0.json`、`microsoft.extensions.ai.abstractions/10.10.1.json`、`microsoft.extensions.ai.openai/10.10.1.json`、`modelcontextprotocol/2.2.0.json`、`modelcontextprotocol.aspnetcore/2.2.0.json`、`microsoft.aspnetcore.openapi/10.0.12.json`、`microsoft.aspnetcore.app.ref/10.0.12.json`、`microsoft.extensions.identity.core/10.0.12.json`、`openai/2.14.0.json`
- 套件說明與 TFM：上述套件的 flat-container nuspec；`Microsoft.Extensions.AI.OpenAI` 10.10.1 頁面寫明 OpenAI 相容端點
- OpenAI 2.14.0：`https://www.nuget.org/packages/openai` 的自訂基底網址範例；`OpenAIClient.GetEndpoint` 預設 `https://api.openai.com/v1`（openai-dotnet `25152ff0` 的 `src/Custom/OpenAIClient.cs`）；`ChatClient(string, string)` 轉入 `ApiKeyCredential`（同提交的 `src/Custom/Chat/ChatClient.cs`）
- 空白金鑰：`https://github.com/Azure/azure-sdk-for-net/blob/System.ClientModel_1.15.0/sdk/core/System.ClientModel/src/Convenience/ApiKeyCredential.cs` 的 `Update` 呼叫 `Argument.AssertNotNullOrEmpty`
- 組態順序：`https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0`
- `PasswordHasher<TUser>`：`https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1?view=aspnetcore-10.0`
- 本機 starter：SDK 10.0.302 的 `dotnet new blazor --framework net10.0`，`appsettings.json` 含 `"AllowedHosts": "*"`，csproj 無 `UserSecretsId`
- 本機執行階段：`Microsoft.AspNetCore.App` 10.0.10，其目錄含 `Microsoft.Extensions.Identity.Core.dll`；targeting pack 目錄只有 10.0.10
- 專案：`WebService\WebService.csproj`、`WebService\appsettings.json`、`McpService\McpService.csproj`、`DbServices.Provider.Sqlite\DbServices.Provider.Sqlite.csproj`
