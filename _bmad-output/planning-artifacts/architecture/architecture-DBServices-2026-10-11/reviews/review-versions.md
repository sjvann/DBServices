# 版本與技術現況審查

- 對象：`ARCHITECTURE-SPINE.md`（DBServices Agent 資料平面，2026-10-11）
- 審查日：2026-10-11
- 鏡頭：已承諾的技術是否對過網頁、現有專案或現行 starter，而不是只靠訓練資料斷言
- 判定：**revise**
- 未改骨幹

兩個已承諾的綠色場域設定，和 ModelContextProtocol.AspNetCore 2.2.0 的現行預設不一樣。骨幹把結果寫成規則，沒有寫明必須顯式覆寫 starter。其餘點名的版本、列舉與既有專案參考，這次都對得上。

## 發現

### 1. `MapMcp` 的現行預設路徑是應用程式根，不是 `/mcp`

- 位置：AD-5；Structural Seed 的 `McpService/` 註解
- 條件：實作者照官方入門範例呼叫沒有參數的 `MapMcp()`
- 骨幹寫法：`MapMcp` 路徑為 `/mcp`
- 現況：標籤 `v2.2.0` 的 `McpEndpointRouteBuilderExtensions.MapMcp` 簽章是 `pattern = ""`。已發布的 v2 傳輸文件寫明端點在指定路由上，預設是根路徑，入門範例是 `app.MapMcp()`。`/mcp` 只出現在自訂路由範例，例如 `app.MapMcp("/mcp")`。
- 後果：照 starter 會把 Streamable HTTP 掛在 `/`。客戶端若連 `/mcp` 會打不到這次 P0 的端點。`/mcp` 這個值本身合法，但它不是 starter 的活預設。
- 修正：在 AD-5 寫明呼叫 `MapMcp("/mcp")`，並註明 SDK 預設 pattern 是 `""`。

### 2. 工具註記的現行預設與「唯讀、非破壞性」相反

- 位置：AD-2
- 條件：六個工具用 `[McpServerTool]` 註冊，卻沒有另外設定註記
- 骨幹寫法：六個工具都標成唯讀，不標成破壞性
- 現況：現行 SDK 的 `McpServerToolAttribute`（以及 `McpServerToolCreateOptions`）裡，`ReadOnly` 預設是 `false`，`Destructive` 預設是 `true`。未設定時 getter 回傳這兩個預設。文件說明 `Destructive` 在 `ReadOnly = false` 時才有意義，但預設值本身仍是可破壞。memlog 沒有這兩個屬性的核對紀錄。
- 後果：照屬性預設廣告出去的工具是可寫、可破壞。這和 AD-2 的公開表面相反，客戶端可能把 `tables`、`fields`、`get`、`query`、`count`、`exists` 當成會改資料。
- 修正：點名要設定的屬性，並寫明必須顯式 `ReadOnly = true`、`Destructive = false`，因為 starter 預設相反。這些屬性在 `ModelContextProtocol` 套件，不在 AspNetCore 套件，因此可以留在 `DbServices.Mcp`，不必參考 ASP.NET 型別。

## 已核對且成立

| 主張 | 核對結果 |
| --- | --- |
| `ModelContextProtocol` 2.2.0、`ModelContextProtocol.AspNetCore` 2.2.0 | 2026-10-11 查 NuGet flat-container：兩者最新版都是 `2.2.0`（其前一穩定版為 2.1.0）。Gallery 發布日為 2026-08-13。沒有更新的穩定版或預覽版。AspNetCore 2.2.0 依賴 `ModelContextProtocol`（= 2.2.0），並含 `net10.0` TFM。 |
| `HttpServerSessionMode.Stateless` | v2 API 文件有此列舉，`Stateless = 0`。標籤 `v2.2.0` 原始碼：`SessionMode` 預設就是 `HttpServerSessionMode.Stateless`。設定位置是 `AddMcpServer().WithHttpTransport` 的 `HttpServerTransportOptions`，不是 `MapMcp` 的參數。 |
| 舊版 SSE 關閉 | `EnableLegacySse` 預設為 false（除非 AppContext 開關）。`SessionMode` 為 Stateless 時不映射 `/sse` 與 `/message`；兩者同時打開會在啟動時丟 `InvalidOperationException`。與 AD-5 相符。 |
| `net10.0` | `WebService`、`GraphQLService`、`DbServices.Cli`、`DbServices.Core`、`DBService`、`DbServices.Api.Shared` 與五個 Provider 的 `TargetFramework` 都是 `net10.0`。 |
| WebService 不直接參考 Provider | `WebService.csproj` 只有 `DBService\DBServices.csproj` 與 `DbServices.Api.Shared\DbServices.Api.Shared.csproj`。`DbServices.Api.Shared` 參考 `DBService`。五個 Provider 只出現在 `DBService\DBServices.csproj`。與 Structural Seed 相符。 |
| 五個 `ServerType` | `DbServiceFactory.Create` 的分支是 `Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`，未知值回傳 null。與 AD-3、AD-4、AD-7 相符。 |
| 工具名稱 | `FunctionRegistry.CreateDefault` 含 `tables`、`fields`、`get`、`query`、`count`、`exists`，以及不廣告的 `insert`、`update`、`delete`、`sql`。`QueryOpcode.Call` 存在。 |
| OpenAPI、Scalar、CORS、Kestrel、user secrets | 名稱仍存在。WebService 已使用 `Microsoft.AspNetCore.OpenApi` 10.0.12 與 `Scalar.AspNetCore` 2.17.14。骨幹只說 MCP 宿主不加這三項，沒有另釘版本。 |

## 已核對的偏離（不列為缺陷）

AD-5 的出貨值 `AllowedHosts` 為 `*`，與現行 MCP HTTP 文件相反。v2 傳輸文件要求本機用 `localhost;127.0.0.1;[::1]`，正式環境用精確主機名，並寫明不要用 `"*"`。同一份 `WebService\appsettings.json` 今天就是 `"AllowedHosts": "*"`。memlog 記錄使用者在被告知 SDK 警告後仍採用 `*`，信任邊界放在營運者網路。這是對過網頁與現有專案之後的決定，不是過期斷言。

## 證據

- NuGet：`https://api.nuget.org/v3-flatcontainer/modelcontextprotocol/index.json`、`https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.aspnetcore/index.json`（查詢日 2026-10-11，最後版本皆 `2.2.0`）
- Gallery：`https://www.nuget.org/packages/ModelContextProtocol/`、`https://www.nuget.org/packages/ModelContextProtocol.AspNetCore`
- 列舉：`https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.AspNetCore.HttpServerSessionMode.html`
- `MapMcp`：`https://csharp.sdk.modelcontextprotocol.io/v2/api/Microsoft.AspNetCore.Builder.McpEndpointRouteBuilderExtensions.html`
- 傳輸與 AllowedHosts：`https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html`
- 標籤原始碼：`https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol.AspNetCore/HttpServerTransportOptions.cs`、`.../McpEndpointRouteBuilderExtensions.cs`
- 工具註記：`https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Server.McpServerToolAttribute.html`
- 專案：`WebService\WebService.csproj`、`WebService\appsettings.json`、`DBService\DBServices.csproj`、`DbServices.Api.Shared\DbServices.Api.Shared.csproj`、`DbServices.Api.Shared\DbServiceFactory.cs`、`DbServices.Core\Functions\FunctionRegistry.cs`
