# Adversary review — Architecture Spine DBServices 2026-10-11

- **Lens:** adversarial
- **Content class:** docs (architecture spine, behavioral)
- **Content:** `ARCHITECTURE-SPINE.md`
- **Method:** 往下一層組兩套實作。單元是 P0 的 `McpService` 宿主對 `DbServices.Mcp`，以及若分開建造的六個工具處理器。每一套都必須能引用現行 Rules，而且不新增產品範圍。兩邊仍對不上的共享資料形狀、同一實體的兩個主人、或兩條狀態變更路徑，才算一個洞。
- **Verdict:** revise

Deferred 已寫明「稽核追加失敗時結果是否仍回傳」與「Session 丟出且不在 AD-7 對應表內的例外用哪個 code」都尚未採用。這兩項不計入下面的洞：現在就選定其中一種的單元，無法引用「再定 / 不把其中一種當成已採用」。下面的 `QueryResult.Kind=Error` 是另一條路徑。`QuerySession.Execute` 會把例外收成 `QueryResult.FromError`，不會丟出；Deferred 的「Session 丟出」蓋不到這個回傳值。

引用位置都指這份主幹的 Rules。既有 `QuerySession`、`QueryResult`、`FunctionRegistry`、`DbServiceFactory.Create` 只在某一條 AD 已經把行為交進去時才拿來當碰撞的現場，不用來補規則。

---

## 兩邊都能逐字符合

### 1. 參數值的程序內型別

- **lens:** adversarial
- **location:** AD-3；Structural Seed「工具描述在 `DbServices.Mcp`、`MapMcp` 在宿主」
- **units:** `McpService` 對 `DbServices.Mcp`（`query` / `count` / `exists` / `get` / `tables` 若各自綁定，會再裂一次）
- **trigger_condition:** 篩選值與 `includeView`、`id` 跨過宿主／函式庫邊界時，沒有共同的值型別。
- **兩套都符合的讀法:**
  - 宿主引用 AD-3「篩選值原樣放進 Session 字典」，把 System.Text.Json 的 `JsonElement` 原封放進調度器字典。
  - 函式庫同樣引用「原樣」，但把原樣理解成已經還原的 JSON 值：`null`、布林、字串、數字，再放進 `QueryRequest.Arguments`。
- **clash:** 同一筆 `filters.status = 1` 或 `includeView = false`，一邊是 `JsonElement`，一邊是 CLR 純量。AD-1 要求呼叫既有 `QuerySession.Execute`，而既有 `get` 對 `id` 做 `Convert.ToInt64`、`tables` 對 `includeView` 做 `Convert.ToBoolean`。兩種盒子過這兩個轉換的結果不同，一邊查得到、一邊變成 `QueryResult.Error`。AD-3 只規定鍵，沒規定值的 CLR 型別。
- **guard_snippet:** 收緊 AD-3：宿主／函式庫邊界上的值只允許 `null`、布林、字串、Int64、雙精度浮點數；`JsonElement` 停在宿主。省略的 `includeView` 必須是布林 `true`，不是字串。`id` 必須是 Int64 範圍內的整數，拒絕前先規定由調度器而不是各工具轉換。
- **potential_consequence:** 六個工具對同一種 JSON 字面值有的成功、有的落入沒有 code 的 Session 錯誤。

### 2. MCP 結果封套與失敗的判別

- **lens:** adversarial
- **location:** AD-5、AD-7、AD-9
- **units:** `McpService` 對 `DbServices.Mcp`
- **trigger_condition:** `{code, message}` 與成功承載是 JSON 物件、數字或布林，但沒有規定誰組 MCP 結果、失敗用例外還是回傳值、JSON 放在哪個 MCP 欄位。
- **兩套都符合的讀法:**
  - 宿主引用 AD-5（`MapMcp` 在宿主、函式庫不參考 ASP.NET 型別）與 AD-7（完成的 HTTP 回應仍是 MCP 回應）。它把函式庫的任何回傳值都包成文字 content，且不設工具錯誤旗標。`{code, message}` 因此長得像成功結果裡的一段文字。
  - 函式庫引用 AD-7「領域失敗是 MCP 工具錯誤」與 AD-9 的承載形狀，並引用 Stack 裡的 `ModelContextProtocol` 2.2.0（不是 ASP.NET）。它回傳工具錯誤旗標，JSON 只放在 structured content，文字 content 為空。成功的 `count` 同樣只放 structured content 裡的數字。
- **clash:** 客戶端讀文字 content 時，一邊看到被包進成功結果的錯誤物件，一邊看到空正文；讀 structured content 時剛好相反。`count` 的「JSON 數字」與 `tables` 的「JSON 物件」也不在同一個欄位。
- **guard_snippet:** 收緊 AD-7 與 AD-9：只由宿主組 MCP 結果。函式庫回傳一個中性結果，用回傳值區分成功與失敗，不用例外。成功與失敗的 JSON 都放在同一個已命名的 MCP 欄位，並寫明 `IsError` 只在 AD-7 的失敗時為真。`count` / `exists` 的裸數字與布林不得再包一層物件。
- **potential_consequence:** 宿主與函式庫各自發版後，工具錯誤會被客戶端當成成功，或成功數字被讀成空內容。

### 3. `filters` 撲平深度

- **lens:** adversarial
- **location:** AD-3
- **units:** `query` 處理器對 `exists` 處理器（`count` 可再選第三種）
- **trigger_condition:** 「撲平後的篩選鍵」與「篩選值原樣」沒有定義撲平幾層。
- **兩套都符合的讀法:**
  - `query` 引用這兩句，只撲平一層。`filters: { "meta": { "a": 1 } }` 送進 Session 的鍵是 `meta`，值是那個物件。
  - `exists` 同樣引用這兩句，把撲平理解成展到葉節點。Session 的鍵是 `a` 或 `meta.a`，值是 `1`。葉值仍然原樣。
- **clash:** 保留鍵碰撞清單（`connectionId`、`table`、`id`、`includeView`）兩邊都照做，Session 字典卻不是同一組等值條件。既有 `query` / `exists` 會把每個非 `table` 鍵當成欄位條件。
- **guard_snippet:** 收緊 AD-3：撲平恰好一層。物件或陣列是該鍵的值，不升格葉節點，不產生點號鍵。
- **potential_consequence:** 同一個 `filters` 物件，`query` 與 `exists` 打到不同欄位，存在判斷與列結果對不上。

### 4. 篩選鍵裡的秘密參數名

- **lens:** adversarial
- **location:** AD-3、AD-7
- **units:** `query` 處理器對 `count` 處理器
- **trigger_condition:** `connectionString` / `connectString` 出現在 `filters` 裡，而不是頂層參數。
- **兩套都符合的讀法:**
  - `query` 引用「參數名稱 `connectionString` 或 `connectString`（不區分大小寫）回 `invalid_arguments`」，撲平後這個鍵就是參數名稱，所以不呼叫 Session。
  - `count` 引用同一條 AD 的封閉碰撞清單。清單只有 `connectionId`、`table`、`id`、`includeView`。秘密名稱那一句被讀成頂層工具參數。`filters` 裡的 `connectionString` 原樣撲平並執行。
- **clash:** 一邊拒絕且不建連線，一邊把該值送進 Session 當成欄位條件。AD-7 的 `invalid_arguments` 對應表同時含「秘密參數名」與「碰撞的 filters 鍵」，沒有說秘密名算不算篩選鍵。
- **guard_snippet:** 收緊 AD-3：秘密參數名在頂層與撲平後的篩選鍵都檢查，比對不區分大小寫；命中則 `invalid_arguments`，不撲平、不呼叫 Session、不建立連線。
- **potential_consequence:** `count` 會把連線字串送進既有函式的等值條件，`query` 則拒絕。稽核是否寫下該值又會再裂開（見互斥項）。

### 5. 只差大小寫的篩選鍵

- **lens:** adversarial
- **location:** AD-3
- **units:** `query` 處理器對 `count` 處理器
- **trigger_condition:** `filters` 同時有 `Name` 與 `name`。AD-3 只對保留鍵寫了不區分大小寫。
- **兩套都符合的讀法:**
  - `query` 用序數、區分大小寫的字典，兩個鍵都原樣送進 Session。碰撞清單沒有禁止它們。
  - `count` 用不區分大小寫的字典（與 AD-3 已寫明的比對方式、以及 `QueryRequest` 預設字典相同），只留下一個值。
- **clash:** 兩次呼叫都不是 `invalid_arguments`，Session 等值條件卻不同。不區分大小寫的那一邊在第二個鍵寫入時還可能丟例外，再掉進沒有 code 的錯誤結果。
- **guard_snippet:** 收緊 AD-3：篩選鍵彼此的比對方式寫死。要嘛序數區分大小寫且兩鍵都保留，要嘛不區分大小寫時視為碰撞並回 `invalid_arguments`，不得留到字典實作。
- **potential_consequence:** `query` 與 `count` 對同一組條件算出不同的列集合與計數。

### 6. `connectionId` 的比對與空值

- **lens:** adversarial
- **location:** AD-3、AD-4、AD-7
- **units:** `get` 處理器對 `query` 處理器
- **trigger_condition:** 目錄鍵是 `main`，呼叫帶 `Main`；以及 `connectionId` 為 JSON `null` 或 `""`。
- **兩套都符合的讀法:**
  - `get` 引用「`connectionId` 不是 `Connections` 的鍵為 `unknown_connection`」。這句沒有寫不區分大小寫（該詞只出現在篩選鍵碰撞與秘密參數名）。`Main` 是 `unknown_connection`。`""` 也不是鍵，同樣是 `unknown_connection`。JSON `null` 同樣不是鍵。
  - `query` 引用「省略 `connectionId`」以及 AD-3 裡唯一寫明的字串比對是不區分大小寫，於是 `Main` 命中 `main`。JSON `null` 與空字串讀成省略：恰有一筆組態完整連線就採用，否則 `connection_required`。
- **clash:** 同一個目錄、同一個引數，`get` 回 `unknown_connection`，`query` 執行或回 `connection_required`。AD-4 只說鍵是 `connectionId`，沒有比較器。
- **guard_snippet:** 收緊 AD-3：目錄查找的比較器寫死（序數或序數忽略大小寫，二擇一）。省略只包含屬性不存在與 JSON `null`；空字串是 `unknown_connection`，不是省略。解析只做一次，放在 `DbServices.Mcp`，宿主不得先填好預設 `connectionId`。
- **potential_consequence:** 六個工具對同一連線名稱給出不同錯誤碼，或有的工具悄悄用上預設連線。

### 7. 組態來源誰蓋過誰

- **lens:** adversarial
- **location:** AD-4
- **units:** `McpService` 對 `DbServices.Mcp`
- **trigger_condition:** 同一個 `connectionId` 在 appsettings、user secrets、環境變數裡各有一個 `ConnectionString`。
- **兩套都符合的讀法:**
  - 宿主引用「appsettings、環境變數與 user secrets 都綁這一個形狀」，用通用主機的預設順序綁定（環境變數蓋過 JSON），把快照交給函式庫。
  - 函式庫引用同一句與「目錄只放在 `DbServices.Mcp`」「啟動時取一次快照」，自己綁定，但讓 JSON 蓋過環境變數。
- **clash:** 兩份快照都含三種來源，選中的連線字串不同。之後的 `Create` 連到不同的資料庫。AD-4 禁止兩份目錄，卻沒有規定綁定這一個動作屬於誰，也沒有規定覆寫順序。
- **guard_snippet:** 收緊 AD-4：只由 `DbServices.Mcp` 綁定並保存那一份快照。宿主交出未解釋的組態來源。覆寫順序寫死為通用主機順序：appsettings，然後 user secrets，然後環境變數。
- **potential_consequence:** 部署用環境變數旋轉密碼之後，一邊已連到新庫，一邊仍快取舊字串。

### 8. 相對稽核路徑的基準目錄

- **lens:** adversarial
- **location:** AD-5、AD-10
- **units:** `McpService` 對 `DbServices.Mcp`
- **trigger_condition:** `Audit:Path` 是相對路徑 `logs/mcp.jsonl`。
- **兩套都符合的讀法:**
  - 宿主引用「預設路徑是內容根目錄下的 `audit/mcp.jsonl`，可用 `Audit:Path` 覆寫」，相對路徑接在內容根後面。
  - 函式庫引用「由 `DbServices.Mcp` 追加」與「覆寫」。覆寫沒有寫基準目錄。AD-5 禁止函式庫參考 ASP.NET 型別，於是相對路徑依行程工作目錄開啟。預設路徑仍可透過非 ASP.NET 的內容根概念對上；覆寫對不上。
- **clash:** 兩個單元各寫各的檔，而且都認為自己就是 AD-10 那一個稽核儲存。宿主並沒有另開「第二個稽核儲存」，它只是把同一條規則的路徑解到別處。
- **guard_snippet:** 收緊 AD-10：相對 `Audit:Path` 一律接在宿主內容根之後。宿主把絕對路徑交進函式庫；只有函式庫開啟該路徑。
- **potential_consequence:** 稽核落在兩份檔案，營運者看的那份缺行，或兩份都被當成完整紀錄。

### 9. 快取連線的開啟與握住

- **lens:** adversarial
- **location:** AD-1、AD-4、AD-5、AD-7
- **units:** `tables` 處理器（或目錄主人）對 `query` 處理器
- **trigger_condition:** AD-7 把「開啟失敗」列成 `connection_failed`，AD-1 又規定工具執行只呼叫 `QuerySession.Execute`，AD-4 在 `Create` 成功後就快取，生命週期等於行程。
- **兩套都符合的讀法:**
  - `tables` 引用 AD-7 的開啟失敗與 AD-4 的行程級快取。開啟是建立步驟，不是工具執行，所以它呼叫會握住連線的 API，再 `Execute`。失敗的 `IDbService` 不快取；`Create` 已成功則快取保留。
  - `query` 引用 AD-1，只呼叫 `Execute`。既有 `Execute` 在沒有握住的連線時，每次開一條短連線並在呼叫結束釋放。它不另外開啟。
- **clash:** 同一筆已快取的 `IDbService`，誰先跑決定之後的連線模式。`tables` 先跑就把連線握到行程結束；之後的 `query` 會走上這條共享連線。`query` 先跑則每次 `Execute` 各開各的。AD-5 的 `Stateless` 只拘束 MCP 工作階段，不禁止並行 HTTP 請求，於是握住的那條連線會被並行請求一起用。開啟失敗之後，一邊下次重試開啟，一邊把失敗記住直到重啟；AD-4 只說 `Create` 失敗不快取，沒有說開啟失敗要不要釘死。
- **guard_snippet:** 收緊 AD-4 與 AD-7：開啟只發生在調度器裡、`Execute` 之前的一次嘗試。開啟失敗是 `connection_failed`，該次建立不快取，下次呼叫可以再 `Create`。快取的 `IDbService` 不得跨呼叫握住連線。禁止各工具自己開啟。
- **potential_consequence:** 工具順序改變連線是否共享；並行請求互相踩同一條連線；資料庫恢復之後有的工具永遠 `connection_failed`，有的已恢復。

### 10. `QueryResult` 錯誤結果與 `fields` 探表

- **lens:** adversarial
- **location:** AD-7、AD-8、AD-9；Deferred 只涵蓋「Session 丟出」
- **units:** `query` 處理器對 `fields` 處理器
- **trigger_condition:** `Execute` 回傳 `Kind=Error`、空的 `columns`/`rows`，並帶 `Error` 字串。內部 `tables` 探表也可能是這種結果。
- **兩套都符合的讀法:**
  - `query` 引用 AD-9：成功承載的鍵恰好是 `columns` 與 `rows`，內容複製自該次 `QueryResult`，空的 `rows` 是成功。它回傳空表成功，不理會 `Kind` 與 `Error`。AD-9 沒有提到這兩個屬性。
  - `fields` 引用 AD-8：先呼叫 `tables`，要求的表名不在回傳名稱中就回 `unknown_table`，且不再呼叫 `fields`。錯誤結果的名稱清單是空的，所以任何表名都是 `unknown_table`。另一套同樣引用 AD-8「回傳名稱」：錯誤結果沒有名稱清單，條件不成立，於是仍呼叫 `fields`。
- **clash:** 同一次 Session 失敗，`query` 對客戶端是成功空表；`fields` 要嘛是 `unknown_table`，要嘛繼續呼叫 `fields`。`count` / `exists` 更進一步：成功承載必須是 `count` 那一格的數字或 `exists` 那一格的布林，錯誤結果裡沒有這一格，AD-7 又沒有對應的 code。這一對在 `query` 與 `fields` 上是兩邊都說得通；在 `count` 上則是 AD-7 與 AD-9 不能同時滿足。
- **guard_snippet:** 收緊 AD-7 與 AD-9：`Kind=Error` 或 `Error` 非空不是成功，不得複製成空的 `columns`/`rows`。給這個結果一個 code，或把 Deferred 的那一條改成明確包含「Execute 回傳的錯誤結果」，並規定 P0 在定案前的唯一行為。收緊 AD-8：只有探表結果是列結果時才做表名比對；探表本身是錯誤結果時走同一個執行期 code，不回 `unknown_table`，也不呼叫 `fields`。
- **potential_consequence:** 逾時、權限、方言錯誤在 `query` 看起來像缺列，在 `fields` 看起來像缺表，在 `count` 沒有合法輸出。

### 11. 拒絕名稱可以不進調度器

- **lens:** adversarial
- **location:** AD-2、AD-7、AD-10
- **units:** `McpService` 對 `DbServices.Mcp`
- **trigger_condition:** 客戶端呼叫未廣告的 `sql`（或 `insert` / `update` / `delete`）。
- **兩套都符合的讀法:**
  - 宿主引用 AD-2：這四個名稱不廣告，客戶端仍呼叫時回 `tool_disabled`，不執行 Session Call；公開表面只有六個工具。它在 `MapMcp` 上直接回工具錯誤。AD-10 只要求「進入調度器的呼叫」追加一行，這次沒進入，所以不寫稽核。
  - 函式庫引用 AD-2 的同一句，以及 AD-10「每一次進入調度器的呼叫，含被拒絕者」。呼叫進入調度器，回 `tool_disabled`，並追加一行，`error` 為該 code。
- **clash:** 客戶端都看到 `tool_disabled`，稽核檔一個有行、一個沒有。Capability Map 把 FR-15 放在調度器，但 AD-2 的 Rule 正文沒有說只有調度器可以回這個 code。
- **guard_snippet:** 收緊 AD-2 與 AD-10：這四個名稱與 `unknown_tool` 都必須進入 `DbServices.Mcp` 的調度器，由它回 AD-7 物件並恰好寫一行稽核。宿主不得在進入前完成工具錯誤。
- **potential_consequence:** 關閉寫入與 SQL 的稽核時有時無，視拒絕發生在宿主還是函式庫。

### 12. `fields` 成功時 `rowCount` 算哪一次

- **lens:** adversarial
- **location:** AD-8、AD-10
- **units:** `fields` 處理器對函式庫裡的通用稽核寫入
- **trigger_condition:** `fields` 成功前必定先有一次內部 `tables` 的 `QueryResult`。
- **兩套都符合的讀法:**
  - `fields` 引用 AD-10「`fields` 成功時 `rowCount` 等於該次 `QueryResult` 的列數」，該次指工具自己的 `fields` 結果。
  - 稽核寫入引用同一句「該次 `QueryResult`」，並引用 AD-8「這次內部 `tables` 不另寫稽核」。不另寫一行仍可以用探表那次的列數填進唯一的那一行。
- **clash:** 同一筆成功的 `fields` 稽核，`rowCount` 一個是欄位數，一個是資料表個數。AD-8 只禁止第二行，沒有指定列數來源。
- **guard_snippet:** 收緊 AD-10：`fields` 的 `rowCount` 只等於後續那次 `fields` 的 `QueryResult` 列數。探表結果不進入稽核欄位。
- **potential_consequence:** 稽核上的列數與客戶端 `rows` 長度不一致，且只發生在 `fields`。

### 13. 未列出的頂層參數

- **lens:** adversarial
- **location:** AD-3、AD-7、AD-10
- **units:** `McpService` 對 `query` 處理器
- **trigger_condition:** 呼叫帶額外鍵 `limit`。
- **兩套都符合的讀法:**
  - 宿主引用 AD-3「`query` 的參數是 `connectionId`、`table` 與可省略的 `filters`」，把參數表當成封閉的，schema 拒絕未知屬性。拒絕發生在進入調度器之前，客戶端得到 SDK 的結構錯誤，不是 AD-7 物件，AD-10 也不寫行。宿主認為這不在 AD-7 的對應表裡，所以不是那種領域失敗。
  - `query` 引用 AD-7：`invalid_arguments` 只對應秘密參數名、缺必填、非整數 `id`、碰撞的篩選鍵。`limit` 不在其中，不能用這個 code。Session 字典只放 `table` 與撲平後的篩選鍵，於是忽略 `limit`、執行並寫稽核。
- **clash:** 一邊在協定層拒絕且沒有稽核，一邊成功執行。
- **guard_snippet:** 收緊 AD-3 與 AD-7：未知頂層鍵是 `invalid_arguments`（或明文規定忽略，二擇一）。判定發生在進入調度器之後，所以有稽核行。宿主 schema 與這個判定相同，不得先用 SDK 錯誤擋下。
- **potential_consequence:** 客戶端與測試依所打的層看到完全不同的錯誤，稽核也對不上。

### 14. 工具名稱的大小寫

- **lens:** adversarial
- **location:** AD-1、AD-2、AD-7；Consistency Conventions「與 `FunctionRegistry` 的函式名稱相同，小寫英文」
- **units:** `McpService` 對 `DbServices.Mcp`
- **trigger_condition:** 客戶端呼叫 `Query` 或 `SQL`。
- **兩套都符合的讀法:**
  - 宿主引用 AD-2 的小寫清單與 AD-7「六個名稱以外的其他名稱為 `unknown_tool`」。`Query` 不是 `query`，`SQL` 也不是小寫的 `sql`，兩者都是 `unknown_tool`，不呼叫 Session。
  - 函式庫引用 AD-1「函式名稱為工具名稱」與慣例「與 `FunctionRegistry` 的函式名稱相同」。既有登錄比對不區分大小寫，`Execute` 的 `Text` 為 `Query` 時會跑 `query`。`SQL` 會進入既有的 `sql` 函式；若函式庫先依四個名稱做不區分大小寫的比對，則回 `tool_disabled`。
- **clash:** `Query` 在宿主是 `unknown_tool`，在函式庫是一次真的唯讀查詢。名稱比對的大小寫只有在別的句子裡被寫死，工具名稱這一句沒有。
- **guard_snippet:** 收緊 AD-2 與 AD-7：六個名稱與四個關閉名稱都用序數忽略大小寫比對。先判四個關閉名稱為 `tool_disabled`，再判六個名稱，其餘才是 `unknown_tool`。送進 `Execute` 的 `Text` 一律是小寫登錄名。
- **potential_consequence:** 大小寫變體可以繞過宿主的未知工具判斷，在函式庫裡真的執行 `query`。

### 15. 解析失敗時稽核裡的 `connectionId`

- **lens:** adversarial
- **location:** AD-10
- **units:** `get` 處理器對 `query` 處理器
- **trigger_condition:** 呼叫帶了 `connectionId: "nope"`，結果是 `unknown_connection`。
- **兩套都符合的讀法:**
  - `get` 引用「沒用到的鍵是 null」。解析沒有產生名稱，頂層 `connectionId` 是 null。
  - `query` 引用「`arguments` 是去掉秘密參數名之後的工具參數，且 `connectionId` 已填入解析後的名稱」。呼叫方確實給了名稱，頂層 `connectionId` 與 `arguments.connectionId` 都寫 `nope`。
- **clash:** 同一種拒絕，稽核行一個是 null，一個是呼叫方給的字串。成功路徑兩邊都會寫解析後的名稱，對不上的是失敗路徑。省略且走到 `connection_required` 時，`query` 那套沒有字串可填，才會落到 null。
- **guard_snippet:** 收緊 AD-10：`unknown_connection` 時頂層 `connectionId` 與 `arguments.connectionId` 都是呼叫方給的字串。`connection_required` 時兩者都是 null。
- **potential_consequence:** 依工具不同，失敗稽核無法還原客戶端當時指了哪一個名稱。

### 16. 儲存格的 JSON 編碼

- **lens:** adversarial
- **location:** AD-9
- **units:** `get` 處理器對 `query` 處理器
- **trigger_condition:** `QueryResult` 儲存格是 `DateTime` 或 `byte[]`。AD-9 要求複製進 JSON 物件的 `columns` 與 `rows`。
- **兩套都符合的讀法:**
  - `get` 引用「內容複製」，日期寫成 UTC ISO-8601 字串，位元組寫成 Base64 字串。
  - `query` 同樣引用「內容複製」，日期寫成不含時區的本地格式字串，位元組寫成數字陣列。兩者都是該 CLR 值的 JSON。
- **clash:** 鍵都是 `columns` 與 `rows`，同一儲存格的 JSON 型別與文字不同。空 `rows` 是成功這一句兩邊都遵守。
- **guard_snippet:** 收緊 AD-9：寫明 null、布林、整數、字串、日期、位元組的 JSON 編碼。日期若沒有時區資訊，規定一種表示，不得由各工具選序列化器預設值。
- **potential_consequence:** `get` 與 `query` 回傳同一欄時，客戶端看到的型別不一致。

### 17. 啟動時就 `Create`，或第一次呼叫才 `Create`

- **lens:** adversarial
- **location:** AD-4
- **units:** `McpService` 啟動路徑對 `DbServices.Mcp` 目錄
- **trigger_condition:** 組態裡有多筆組態完整的連線，其中一筆現在連不上。
- **兩套都符合的讀法:**
  - 宿主引用「組態在程序啟動時取一次快照」以及「未知 `ServerType`、空白連線字串，或建立失敗的那一筆不使程序結束」。啟動時對每一筆完整項呼叫 `Create`，失敗不讓程序結束，成功才放進快取。
  - 函式庫引用「`Create` 成功後才快取」與同一條快照句子。快照只複製組態，`Create` 留到該 `connectionId` 第一次被選中。失敗仍然不結束程序、不快取。
- **clash:** 一邊在任何工具呼叫之前就開啟每一條資料庫，並把成功的 `IDbService` 放進行程級快取；另一邊在第一次呼叫前不碰資料庫。AD-4 沒有禁止啟動時建立，也沒有要求那時建立。
- **guard_snippet:** 收緊 AD-4：啟動只做組態快照，不呼叫 `Create`、不開啟連線。`Create` 發生在調度器第一次選中該鍵，而且該次成功才快取。
- **potential_consequence:** 啟動時間、失敗日誌與「何時才有一條活連線被握著」依哪一層實作而定，並和洞 9 的握住行為疊加。

### 18. 稽核 `value` 的 JSON 型別

- **lens:** adversarial
- **location:** AD-9、AD-10
- **units:** `count` 處理器對 `McpService`（若由宿主把稽核物件寫成一行）
- **trigger_condition:** `count` 成功時 AD-10 要寫 `value`，AD-9 要客戶端看到 JSON 數字。稽核鍵的 JSON 型別沒有寫。
- **兩套都符合的讀法:**
  - `count` 引用 AD-9 與「成功時寫 `value`」，`value` 是 JSON 數字。
  - 宿主引用 AD-10「寫 `value`」，把那一格寫成 JSON 字串 `"5"`。鍵仍然存在，也不是 null。
- **clash:** 同一筆成功，稽核行的 `value` 一個是數字、一個是字串。`exists` 會再分成布林與字串 `"true"`。
- **guard_snippet:** 收緊 AD-10：`value` 的 JSON 型別與回給客戶端的承載相同，`count` 是數字，`exists` 是布林，不是字串。寫檔的那一層不得再 `ToString`。
- **potential_consequence:** 稽核消費端依工具實作者不同，有時解析失敗，有時把數字讀成字串。

---

## 同一條 AD 裡兩句不能同時成立

這兩項不是「兩邊都滿足每一句」。每一邊都能引用其中一句，但沒有任何一套能同時滿足該 AD 的兩句。洞一樣要收緊規則。

### 19. 稽核引數要忠於參數，又不得寫入目錄裡的秘密

- **lens:** adversarial
- **location:** AD-10
- **units:** `query` 處理器對 `count` 處理器
- **trigger_condition:** 篩選值等於目錄裡的 `ConnectionString` 或其中的密碼，鍵名不是 `connectionString` / `connectString`。
- **兩套引用:**
  - `query` 引用「`arguments` 是去掉秘密參數名之後的工具參數」，值原樣寫入稽核。
  - `count` 引用「目錄裡的連線字串與其密碼不寫入此檔，也不寫入宿主日誌」，把相等的值塗掉。
- **clash:** 寫下原值就違反後句；塗掉就不再是前句的工具參數。AD-3 只保證錯誤訊息不含該值，沒有規定稽核值比對。
- **guard_snippet:** 收緊 AD-10：秘密只按參數名移除（頂層與篩選鍵），不按值掃描；或相反，規定值與任一目錄秘密相等時如何替換。兩句只能留一種。
- **potential_consequence:** 有的工具把密碼寫進 `audit/mcp.jsonl` 與宿主日誌，有的工具改寫了引數，回放時對不上客戶端輸入。

### 20. `sql` 同時對上兩個 code

- **lens:** adversarial
- **location:** AD-7
- **units:** 關閉名稱守衛對「六個名稱以外」守衛（可放在宿主或函式庫）
- **trigger_condition:** 客戶端呼叫小寫的 `sql`、`insert`、`update` 或 `delete`。
- **兩套引用:**
  - 一邊引用「`insert`、`update`、`delete`、`sql` 為 `tool_disabled`」。
  - 另一邊引用「六個名稱以外的其他名稱為 `unknown_tool`」。這四個名稱不在六個裡面。
- **clash:** 同一個名稱，兩個 code 都是 AD-7 的字面對應。沒有一套能同時滿足兩句。
- **guard_snippet:** 收緊 AD-7：先比對四個關閉名稱，得到 `tool_disabled`；`unknown_tool` 只用於既不是這四個、也不是六個工具的名稱。
- **potential_consequence:** 關閉寫入與自由 SQL 的呼叫有時是 `tool_disabled`，有時是 `unknown_tool`，客戶端與稽核的 `error` 欄分裂。

---

## 不算洞

- 稽核追加失敗要不要仍回傳工具結果：Deferred 已禁止把其中一種當成已採用。
- Session 丟出的逾時、權限、方言例外用哪個 code：Deferred 已延後。與洞 10 不同，那一項是 `Execute` 已吞掉例外之後的 `QueryResult`。
- `connectionId` 省略且組態完整筆數不是一：AD-7 已對到 `connection_required`，不是 `invalid_arguments`。
- 成功時空 `rows`、`count`/`exists` 不重算、內部探表不另寫一行、不另廣告：AD-8、AD-9、AD-10 已經把這些釘死。
- `ServerType` 的大小寫：AD-3 列出的五個字串與 Consistency Conventions「與 `DbServiceFactory` 的分支相同」是序數相同。忽略大小寫的那一套不能引用現行規則。
