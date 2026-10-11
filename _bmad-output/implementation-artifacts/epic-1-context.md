# Epic 1 Context: 外部 Agent 安全讀取具名業務庫

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

啟動宿主並登記具名連線後，外部 Agent 用同一組唯讀工具查詢。寫入與自由 SQL 被拒絕，呼叫可在本機追溯。換引擎只改組態；既有簽名與 REST、GraphQL 仍可呼叫。

## Stories

- Story 1.1: 啟動宿主，Agent 只看見六個唯讀工具
- Story 1.2: 用 connectionId 列出資料表
- Story 1.3: 看欄位，不存在的表得到錯誤
- Story 1.4: 依整數 Id 取一列
- Story 1.5: 用等值條件查列、計數、判斷存在
- Story 1.6: 每次呼叫留下可讀的稽核
- Story 1.7: 換 ServerType，同一組工具仍成立

## Requirements & Constraints

- 只廣告 tables、fields、get、query、count、exists：唯讀、非破壞性。insert、update、delete、sql 不執行且不改資料。
- Agent 不傳引擎或連線字串。連線字串與密碼不出現在輸入、錯誤、稽核；傳入則拒絕、不建連線、訊息不回顯。五個引擎共用同一組名稱與參數。
- 每次恰好一個已登記連線。未知名稱不改落他處。省略名稱只在恰一筆可用時採用，否則 connection_required。單筆失敗不快取、不結束程序。tables 無表與 query 無列是空結果；count 為 0，exists 為 false。
- fields 含名稱、型別、主鍵、不可為空；缺表是錯誤。get 只接受整數 Id。篩選值當參數，不接進 SQL。
- 錯誤為 code 加中文 message。閉集：tool_disabled、unknown_tool、unknown_connection、connection_required、connection_failed、invalid_arguments、unknown_table、execution_failed。Session 失敗與稽核寫不進去皆 execution_failed；後者不交還本次結果。失敗不結束程序。
- 語意等於既有 Session Call，不重算列。無登入、無模型、無側車。無身分記 anonymous 仍可呼叫。

## Technical Decisions

- 新專案 McpService（net10.0）與不含 ASP.NET 的 DbServices.Mcp。ModelContextProtocol 與 ModelContextProtocol.AspNetCore 皆 2.2.0。不參考 Provider，不改 Core 或 FunctionRegistry。宿主只參考工具庫；工具庫參考 Core 與 DbServices.Api.Shared。只經 DbServiceFactory.Create 與 QuerySession.Execute（opcode Call，登錄小寫名）。
- MapMcp("/mcp")、Stateless、關舊 SSE。無 OpenAPI、Scalar、CORS。位址來自組態，AllowedHosts 為 *。
- 同一調度器。JSON 先成純量，JsonElement 不過界。includeView 省略為 true。filters 撲平一層、不展開；去掉 connectionId 後只留該函式要的鍵。查找忽略大小寫。空字串是 unknown_connection。未知頂層鍵，或篩選鍵撞上 connectionId、table、id、includeView、connectionString、connectString（忽略大小寫），入調度器後才 invalid_arguments，不呼叫 Session。
- Connections 值僅 ServerType、ConnectionString。引擎 Sqlite、SqlServer、MySQL、Oracle、PostgreSQL。不讀 ConnectionStrings。啟動不 Create；選中才建，成功才快取。零筆仍廣告。未知引擎、空白字串或 Create 為 null 即 connection_failed。
- 僅 fields 先 tables（includeView true，序數忽略大小寫）；未中 unknown_table 且不再呼叫 fields。探表失敗 execution_failed，不另記稽核。其餘不先查表。
- 列結果僅 columns、rows；count 為數字，exists 為布林；空列成功。錯誤鍵僅 code、message。
- 入調度器即追加一行 JSONL。預設內容根 audit/mcp.jsonl，Audit:Path 可改。鍵 timestamp、caller、connectionId、tool、arguments、rowCount、value、error。不寫列與連線字串，並行不寫斷。呼叫者標頭 X-DBServices-Caller。

## Cross-Story Dependencies

- 看欄位、取列與等值三工具沿用列出資料表的連線調度。
- 稽核包住每一次進入調度器的呼叫。
- 換引擎前，查表、看欄位與等值查詢須已對一個可連線的具名連線成功。
