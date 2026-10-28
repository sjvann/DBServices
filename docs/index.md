# DBServices

**現代化多資料庫 ORM 工具包** — 基於 Dapper，統一存取 SQLite、SQL Server、MySQL、Oracle 與 PostgreSQL。

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Dapper](https://img.shields.io/badge/Dapper-2.1.66-blue.svg)](https://github.com/DapperLib/Dapper)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/sjvann/DBServices/blob/master/LICENSE.txt)

<div class="docs-hero">
  <p class="docs-kicker">Session · REST · GraphQL · MCP · 報表</p>
  <p class="docs-lead">一個介面對齊五種資料庫。C# SDK、命令列、Web API、外部 Agent 與報表工作台都走同一條 Session，連線字串留在伺服器。</p>
  <div class="docs-actions">
    <a class="docs-btn" href="getting-started/quickstart.html">三分鐘上手</a>
    <a class="docs-btn docs-btn-ghost" href="workbench/index.html">動態報表</a>
  </div>
</div>

<div class="docs-grid">
  <a class="docs-card" href="getting-started/index.html">
    <i class="bi bi-box-arrow-in-right" aria-hidden="true"></i>
    <h2>入門</h2>
    <p>安裝套件，用工廠、建構器或依賴注入取得第一個查詢。</p>
  </a>
  <a class="docs-card" href="guides/index.html">
    <i class="bi bi-journal-text" aria-hidden="true"></i>
    <h2>指南</h2>
    <p>CRUD、查詢、事務、遷移、安全與效能。</p>
  </a>
  <a class="docs-card" href="cli/index.html">
    <i class="bi bi-terminal" aria-hidden="true"></i>
    <h2>CLI</h2>
    <p><code>dbsql</code> 互動與批次模式，反斜線指令展開成 Session Call。</p>
  </a>
  <a class="docs-card" href="web/index.html">
    <i class="bi bi-hdd-network" aria-hidden="true"></i>
    <h2>Web API</h2>
    <p>把資料表露出成 REST。五種引擎都經 <code>DbServiceFactory</code>。</p>
  </a>
  <a class="docs-card" href="web/graphql.html">
    <i class="bi bi-diagram-3" aria-hidden="true"></i>
    <h2>GraphQL</h2>
    <p>依現有資料表動態產生 Query 與 Mutation，並附操作台。</p>
  </a>
  <a class="docs-card" href="mcp/index.html">
    <i class="bi bi-shield-check" aria-hidden="true"></i>
    <h2>MCP</h2>
    <p>外部 Agent 的 Discovery 只列出已發布檢視。工具名稱就是檢視名稱。</p>
    <span class="docs-chip">最新</span>
  </a>
  <a class="docs-card" href="workbench/index.html">
    <i class="bi bi-file-earmark-bar-graph" aria-hidden="true"></i>
    <h2>動態報表</h2>
    <p>分析師發布檢視，設計師排樣式，終端使用者用一句話產生這一次的報表。</p>
    <span class="docs-chip">最新</span>
  </a>
  <a class="docs-card" href="architecture/index.html">
    <i class="bi bi-layers" aria-hidden="true"></i>
    <h2>架構</h2>
    <p>分層、連線用完即還，以及刻意不做成查詢引擎的界線。</p>
  </a>
  <a class="docs-card" href="api/index.html">
    <i class="bi bi-braces" aria-hidden="true"></i>
    <h2>API 參考</h2>
    <p>由原始碼 XML 註解產生的型別文件。</p>
  </a>
</div>

## 為什麼選擇 DBServices

- **一個介面、五種資料庫**：`IDbService` 對齊 CRUD、結構探索與參數化 SQL。
- **Session 協定**：CLI、Web、GraphQL、MCP 與 C# SDK 都走 `IQuerySession`（Query／Call／Utility）。
- **連線用完即還**：一般操作從 pool 取連線、用完 Dispose；只有事務期間才持有連線。
- **Agent 的 Discovery**：只列出已發布檢視，工具名稱就是檢視名稱。沒有已發布檢視時清單是空的。連線由伺服器組態登記。
- **動態報表**：工作台依樣式與一句話，向該檢視取一次列，並可留下生成紀錄與 PDF。

## 三分鐘上手

```csharp
using DBServices;

var db = MainService.UseSQLite("Data Source=app.db");
string[]? tables = db.GetAllTableNames();
var user = db.GetRecordById(1, "Users");
```

```bash
dotnet run --project DbServices.Cli -- --provider sqlite --connection "Data Source=app.db"
dbsql> \dt
dbsql> SELECT * FROM Users;
```

Agent 要接同一座庫時，改啟動 MCP 宿主，並在組態登記具名連線。見 [MCP 資料平面](mcp/index.md)。要讓人用一句話出報表，見 [動態報表工作台](workbench/index.md)。

## 支援的資料庫

| 資料庫 | 工廠方法 | MCP `ServerType` | 連線字串範例 |
|--------|----------|------------------|--------------|
| SQLite | `UseSQLite` | `Sqlite` | `Data Source=app.db` |
| SQL Server | `UseMsSQL` | `SqlServer` | `Server=localhost;Database=App;Trusted_Connection=true` |
| MySQL | `UseMySQL` | `MySQL` | `Server=localhost;Database=app;Uid=root;Pwd=***` |
| Oracle | `UseOracle` | `Oracle` | `Data Source=localhost:1521/XE;User Id=hr;Password=***` |
| PostgreSQL | `UsePostgreSQL` | `PostgreSQL` | `Host=localhost;Database=app;Username=postgres;Password=***` |

Web API 與 GraphQL 的 `ServerType:Name` 使用與 MCP 相同的五個精確字串。

## 原始碼與授權

- 儲存庫：[github.com/sjvann/DBServices](https://github.com/sjvann/DBServices)
- 授權：MIT
