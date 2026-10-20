# Adversary review — Architecture Spine DBServices 檢視與報表 2026-10-20

- **Lens:** adversarial
- **Content class:** docs (architecture spine, behavioral)
- **Content:** `ARCHITECTURE-SPINE.md`（altitude: feature；其下單元是史詩或故事）
- **Parent:** `architecture-DBServices-2026-10-11`。父 AD-12、父 AD-6 整句有效；父 AD-3 自承只描述營運除錯路徑。
- **Method:** 往下一層組兩套實作。每一套都引用現行 Rules，不新增產品範圍，也不把 Deferred 已鎖住的選項當成已採用。兩邊仍對不上的共享資料形狀、同一實體的兩個主人、或兩條狀態變更路徑，才算一個洞。
- **Verdict:** holes

引用位置都指這份主幹的 Rules、Design Paradigm、Structural Seed 與 Consistency Conventions。父脊只在子脊沒有把該句收進來、而兩套又能各自符合子脊時，拿來說明縫在哪裡。

Deferred 已寫明：規劃期表結構通道、轉置是否落地、詞庫新增介面、模型設定與側車啟動、PDF／CSV 套件、稽核保留天數、呼叫者身分證明（目錄寫入只拒絕空白）、資料表級授權、SSO、公開註冊、GraphQL、自由 SQL、區間與跨表。父 AD-12 的目錄媒介（檔案、SQLite 或其他）仍是未決。這些不計入下面的洞。

---

## 兩邊都能逐字符合

### 1. 已發布檢視的工具引數沒有同一層

- **lens:** adversarial
- **location:** AD-3；繼承表 parent AD-3、parent AD-12；父 AD-12「參數是該定義自己宣告的篩選，加上既有的 `connectionId` 規則」；父 AD-3 首句「本決策只描述營運除錯路徑」
- **units:** 史詩「檢視呼叫配接器」對 史詩「公開工具調度」
- **trigger_condition:** 就緒且恰有一筆組態完整連線時要送出一次檢視呼叫。子脊只規定送 `connectionId` 與純量篩選，沒有規定篩選鍵放在哪一層。
- **兩套都符合的讀法:**
  - 配接器引用父 AD-12：宣告過的參數就是篩選，和 `connectionId` 放在同一層。`GET /connections` 恰好一筆時，頂層是 `connectionId` 加上每個參數 `field`。不包 `filters`，因為父 AD-3 的 `filters` 物件只舉 `query`、`count`、`exists`。未宣告的頂層鍵拒絕、不執行。
  - 調度器引用子脊「篩選維持純量」與除錯路徑既有的物件：純量放進恰好一層的 `filters`，`connectionId` 留在外面。`filters` 這個名字是宿主參數，不是檢視欄位。宣告過的 `field` 若出現在頂層，視為未宣告欄位，拒絕、不執行。
- **clash:** 同一句就緒語句，一邊送出 `{ "connectionId", "yearMonth": "2026-10" }`，另一邊送出 `{ "connectionId", "filters": { "yearMonth": "2026-10" } }`。對方的調度都把它當成未宣告引數而拒絕。AD-7 的「恰好呼叫一次」在一邊已執行，在另一邊是 `invalid_arguments` 且不執行。成功形狀 `columns`／`rows` 到不了同一份動態報表。
- **guard_snippet:** 收緊 AD-3，並寫明這是父 AD-12 的呼叫形狀，不是父 AD-3 的除錯路徑：已發布檢視的工具引數頂層恰好是 `connectionId` 與該檢視已宣告的參數 `field`。值是純量。不使用 `filters` 物件，不送 `table`。`filters`、未宣告的 `field`、以及與 `connectionId` 不區分大小寫相同的參數名，都是父 AD-7 的 `invalid_arguments`，不執行。除錯路徑的 `filters` 維持父 AD-3，不套用到 Discovery 裡的檢視。
- **potential_consequence:** 工作台與 `McpService` 各寫一套引數，就緒之後不是零列成功，就是被當成非法引數。稽核裡的 `arguments` 也無法對上畫面所說的參數。

### 2. `viewId` 沒有指到同一個鍵

- **lens:** adversarial
- **location:** AD-14；AD-15；AD-3；父 AD-2「名稱比對不區分大小寫」；父 AD-12「一個已發布模組視圖是一個公開工具」
- **units:** 故事「樣式綁定」對 故事「目錄主鍵」
- **trigger_condition:** 樣式的 `viewId` 恰好指向一筆已發布檢視，呼叫與「仍被引用」都用這個值。Rules 沒有寫它是工具名稱、穩定識別，或比對是否沿用父脊的不區分大小寫。
- **兩套都符合的讀法:**
  - 樣式綁定引用 AD-14 與父 AD-12：已發布檢視就是公開工具，`viewId` 儲存 Discovery 名稱。呼叫時 `tools/call` 的名稱就是 `viewId`。引用檢查用序數、區分大小寫的字串相等。AD-15 禁止在仍被引用時改名稱，所以這個字串不會漂。
  - 目錄主鍵引用 AD-15「不能改名稱、輸出欄或參數」與 AD-14「指向一筆檢視」：目錄另有不變的 `id`，`name` 才是工具名稱。樣式的 `viewId` 儲存 `id`。配接器先把 `id` 換成 `name` 再呼叫。引用檢查沿父 AD-2，不區分大小寫。
- **clash:** 同一筆樣式的 `viewId` 一邊是「銷貨月檢視」，一邊是「view-sales-month」。直接拿 `viewId` 當工具名稱會得到 `unknown_tool`；先查 `id` 的那套又認不得名稱。大小寫不同的兩筆記錄，一邊算引用、拒絕取消發布，另一邊視為未引用而刪除。父 AD-12 的工具名稱與子脊的綁定鍵不是同一個欄位。
- **guard_snippet:** 收緊 AD-14 與 AD-15：檢視的 `id` 是目錄內不變的非空字串，`name` 是 Discovery 工具名稱。樣式與生成紀錄的 `viewId` 只存 `id`，不存 `name`。呼叫時由目錄把 `id` 換成 `name`，名稱比對沿父 AD-2，序數、不區分大小寫，只做一次。引用檢查只比較 `viewId` 與 `id`，序數、區分大小寫。仍被引用時不能改 `name`、輸出欄或參數；`id` 建立後不能改。
- **potential_consequence:** 樣式庫與目錄對不齊。取消發布在一個故事裡被擋住，在另一個故事裡已發布工具從 Discovery 消失，就緒呼叫變成 `unknown_tool`。

### 3. 檢視文件的欄位與關聯段

- **lens:** adversarial
- **location:** AD-15；父 AD-2「每一筆有名稱、短述與長解說」；父 AD-12「解說為空或仍是草稿時不得發布」「路徑的每一段是目錄外鍵，或定義裡已寫明並由人接受的關聯」
- **units:** 史詩「目錄 HTTP」對 史詩「工作台檢視編輯」
- **trigger_condition:** 兩邊都要保存目的、來源表、關聯、輸出欄與參數，一張來源表時關聯為空。Rules 沒有共同的 JSON 鍵，也沒有把「目的」對上 Discovery 的短述與長解說。
- **兩套都符合的讀法:**
  - 目錄 HTTP 的本體是 `name`、`purpose`、`sources[]`、`joins[]`、`outputs[]`、`parameters[]`。`purpose` 同時寫進 Discovery 的短述與長解說。`joins` 的每一段含 `fromTable`、`fromColumn`、`toTable`、`toColumn`。沒有別的旗標時，有欄位對的段就視為分析師已接受。一張表時 `joins` 為空。
  - 檢視編輯的本體是 `viewId`、`intent`、`tables[]`、`relations[]`、`columns[]`、`filters[]`。`intent` 是目的，可在被引用後修改。Discovery 的解說是另一個 `description`，發布時非空，之後不跟著 `intent` 改。`relations[].accepted === true` 才算已接受；沒有這個旗標就拒絕保存。
- **clash:** 同一筆檢視，HTTP 雙方都認為自己帶了目的、來源、關聯、輸出欄與參數，文件卻無法互讀。一邊發布後 Discovery 的解說等於目的，改目的會改解說；另一邊改目的不改解說。關聯段一邊無旗標即保存，另一邊因缺少 `accepted` 而整筆拒絕。父 AD-12 的「解說」與子脊的「目的」沒有被說成同一份文字。
- **guard_snippet:** 收緊 AD-15：目錄 JSON 鍵只有 `id`、`name`、`purpose`、`published`、`sources`（字串陣列）、`links`、`parameters`（`field` 與 `label`）、`columns`（字串陣列）。`purpose` 就是父 AD-2 的短述與長解說，不另存第二份；空白或草稿不能發布；被引用後仍只許改 `purpose`。`links` 的一段是 `from`、`to`、`by`、`kind`。`kind` 只許 `fk` 或 `accepted`。一張來源表時 `links` 為空陣列。兩張以上時每一段都要有 `kind`，否則拒絕保存，既有定義不變。這個文件形狀與媒介選擇無關；媒介仍依父 AD-12 未決，兩套實作不得各選一種媒介。
- **potential_consequence:** `McpService` 與工作台配接器各持有一份檢視。發布在一邊成功、另一邊拒絕；Discovery 上的解說與分析師剛改的目的不一致。

### 4. 被樣式引用的檢視有兩個主人

- **lens:** adversarial
- **location:** AD-15；AD-2；AD-5
- **units:** 故事「刪除前回呼」對 故事「目錄自備引用集合」
- **trigger_condition:** 仍被樣式 `viewId` 引用時，目錄上的刪除、取消發布，以及名稱、輸出欄、參數的修改都要失敗。樣式在工作台 SQLite，目錄在 `McpService`。AD-2 只許 `Workbench` 開啟那份 SQLite。
- **兩套都符合的讀法:**
  - 回呼：`McpService` 的寫入路由在變更前呼叫工作台的查詢，問這個 `viewId` 是否出現在樣式裡。工作台在線且有引用就拒絕。列出已發布檢視不呼叫工作台。`McpService` 不開啟 `Workbench:StorePath`。
  - 引用集合：工作台在樣式保存成功後，把該 `viewId` 的引用計數寫進目錄。`McpService` 只查自己的計數。計數為 0 就允許刪除。工作台停機時，目錄仍可依手邊的計數決定。
- **clash:** 引用這一個事實有兩個主人。樣式已寫入、計數尚未寫入時，回呼那套拒絕刪除，計數那套允許刪除。回呼在工作台停機時整次拒絕，計數那套仍可能刪掉已被樣式指向的檢視。兩條都滿足「路由上有檢查」這句話，失敗順序不同，目錄裡留下的定義就不同。
- **guard_snippet:** 收緊 AD-15：引用集合只有工作台 SQLite 的樣式 `viewId`。`McpService` 不保存第二份計數，也不開啟工作台 SQLite。刪除、取消發布，或修改 `name`、`columns`、`parameters` 之前，`McpService` 向工作台查一次；工作台不可達，或任一樣式的 `viewId` 相等，就拒絕且目錄不變。列出已發布檢視不依賴工作台。
- **potential_consequence:** 樣式仍指向一個已經從 Discovery 消失的檢視，或工作台一停，分析師就刪不了任何檢視。兩個故事會把不同的結果當成 AD-15。

### 5. 動態報表的本體

- **lens:** adversarial
- **location:** AD-5；AD-7；AD-14；parent AD-9 與 parent AD-12 的成功形狀
- **units:** 故事「保存當時版面」對 故事「再開讀樣式」
- **trigger_condition:** 生成紀錄要能在不呼叫 MCP 的情況下顯示該次動態報表。AD-5 列出樣式識別、檢視識別、純量參數與該次動態報表；AD-14 禁止樣式本身帶資料列。沒有寫「該次動態報表」是否含當時的區塊。
- **兩套都符合的讀法:**
  - 保存當時版面：`report` 含當時區塊複本（`type`、`order`、`text`、`field`、`label`、資料表欄名）以及原樣的 `columns` 與 `rows`。再開只讀這筆紀錄。樣式後來被改，這筆報表不變。
  - 再開讀樣式：`report` 只有 `columns` 與 `rows`。版面從 `templateId` 指向的樣式現況讀出。AD-5 已另存樣式識別，區塊不必複製。儲存格編碼沿父 AD-9，不做第二次彙總。
- **clash:** 同一筆 SQLite 紀錄，一邊的讀取器要有區塊陣列，另一邊只接合樣式表。擁有者改了標題或參數標籤之後，再開看到的版面不同。樣式是私有、紀錄是共享時，第二套還要去讀一份第一套根本不需要的樣式。列本身可以都是 `columns`／`rows`，報表這一個實體仍然是兩種文件。
- **guard_snippet:** 收緊 AD-5 與 AD-7：生成紀錄的 `report` 在寫入時複製當時區塊與該次 `columns`、`rows`。參數值只放在紀錄的純量參數，不回寫樣式區塊。再開與匯出只讀這筆 `report`，不讀樣式表，不呼叫 MCP。樣式之後的修改不改變已寫入的 `report`。
- **potential_consequence:** 共享出去的報表在一個故事裡保持保存當時的版面，在另一個故事裡變成樣式的現況。匯出的 PDF 也跟著分叉。

### 6. 共享範圍的讀者與寫者

- **lens:** adversarial
- **location:** AD-5；AD-8「此人可讀樣式」；Consistency Conventions「入口加共享範圍」
- **units:** 故事「共享即可改」對 故事「擁有者獨寫」
- **trigger_condition:** 私有只有擁有者可讀已經寫明。共享對誰可讀、誰可以改內容或改共享範圍，Rules 沒有寫。AD-8 的候選又依賴「可讀」。
- **兩套都符合的讀法:**
  - 共享即可改：`shared` 表示任何已登入操作者可讀、可改樣式與生成紀錄。`private` 仍只有擁有者可讀。管理員的產品介面不讀他人的私有生成紀錄。語句候選包含這位操作者讀得到的全部樣式。
  - 擁有者獨寫：`shared` 的樣式只對另持 `designer` 或 `enduser` 的人可讀；`shared` 的生成紀錄只對另持 `enduser` 的人可讀。內容、刪除與共享範圍只有 `ownerId` 可改。其他人讀得到也不能寫。
- **clash:** 同一筆 `shared` 樣式，一邊的更新會成功，另一邊拒絕。同一句話的候選集合因此不同：一邊可以就緒並依 AD-7 呼叫檢視，另一邊停在 `incomplete`，讀列次數是 0。生成紀錄的共享讀者也是兩群人。
- **guard_snippet:** 收緊 AD-5：`private` 只有 `ownerId` 可讀可寫。`shared` 樣式對其他持有 `designer` 或 `enduser` 的操作者可讀；`shared` 生成紀錄對其他持有 `enduser` 的操作者可讀。這兩種共享都只有 `ownerId` 可修改、刪除或變更共享範圍。AD-8 的輸入只含這條讀取規則之下的樣式。管理員的產品介面不讀他人的私有樣式與私有生成紀錄。
- **potential_consequence:** 設計師改得到別人的共享樣式，或終端使用者的語句在兩個故事裡一個會打 MCP、一個不會。UJ-4 的「看得到但不能改」沒有落在規則上。

### 7. 入口的儲存值與變更權

- **lens:** adversarial
- **location:** AD-1；AD-5；AD-6；Consistency Conventions「三個入口」「一人多個入口時整頁切換」
- **units:** 故事「入口是伺服器謂詞」對 故事「入口只決定頁面」
- **trigger_condition:** 只有管理員能建立操作者與變更入口，未登入不能讀資料。三個站的名稱、存進 SQLite 的字串、以及沒有該入口時 Core 是否拒絕寫入，都沒寫。
- **兩套都符合的讀法:**
  - 伺服器謂詞：入口值是 `analyst`、`designer`、`enduser`。`analyst` 才能寫目錄，`designer` 才能寫樣式，`enduser` 才能比對語句、產生動態報表與寫生成紀錄。沒有該入口時 Core 拒絕，讀列次數是 0。判斷在 Core 端口，控制器不另寫。
  - 頁面切換：入口值是「系統分析師」「報表設計師」「終端使用者」。Core 的入口判斷只決定整頁顯示哪一站。已登入即可呼叫保存樣式、目錄寫入與產生報表的端口。Avalonia 走同一組端口，不因為畫面沒畫出該站而被拒絕。
- **clash:** 指派表的字串彼此認不得。同一位只有終端使用者入口的操作者，一邊的目錄寫入與樣式修改被拒絕，另一邊會落進 SQLite 或目錄。兩個殼都經過 Core，所以都符合 AD-1 的「只經過端口」。
- **guard_snippet:** 收緊 AD-5 與 AD-6：操作者欄位是 `id`（非空字串）、`displayName`、`passwordVerifier`、`admin` 布林、`entries`。`entries` 的元素只許 `analyst`、`designer`、`enduser`，可同時多個。`id` 就是 `X-DBServices-Caller`。目錄寫入只許 `analyst`；樣式寫入只許 `designer`，終端使用者把版面存回樣式庫時只許 `enduser` 且擁有者是自己；語句、動態報表與生成紀錄只許 `enduser`。缺少該入口時 Core 拒絕，不寫儲存，不呼叫 MCP。`PasswordHasher` 使用共用框架的預設選項，不傳入自訂 `PasswordHasherOptions`。
- **potential_consequence:** 入口指派在一個故事裡只是導覽，在另一個故事裡才是授權。非該站的操作者仍能改檢視或樣式。兩次安裝的 `passwordVerifier` 也可能因迭代次數不同而不能互驗。

### 8. 「語句含樣式名稱」沒有邊界

- **lens:** adversarial
- **location:** AD-8
- **units:** 故事「連續字元」對 故事「整段詞」
- **trigger_condition:** 樣式 `name` 被語句含有時進入候選。沒有寫大小寫、連續字元或必須自成一段。
- **兩套都符合的讀法:**
  - 連續字元：序數、區分大小寫。語句裡出現名稱的連續字元即入候選。「請看銷貨月報表」含有「銷貨月報」。
  - 整段詞：名稱必須是一段不被空白或標點切開的詞，且與 `name` 完全相同。「銷貨月報表」是一個詞，不等於「銷貨月報」，不入候選。「銷貨月報，年月」在逗號處切開，名稱入候選。
- **clash:** 同一句話一邊得到恰好一個樣式並在參數齊全時呼叫 MCP，另一邊是 `incomplete`，讀列次數是 0。兩套都使用 AD-8 的三個狀態，沒有多造第四個。
- **guard_snippet:** 收緊 AD-8：樣式 `name`、參數 `field` 與 `label` 都用序數、區分大小寫的連續字元包含。名稱較長的區間先占用；等長時取較左。被占用的字元不再參與下一次包含。沒有命中就不補猜。
- **potential_consequence:** 同一個操作者、同一句話，一個故事送出檢視呼叫，另一個故事停在未就緒。

### 9. 重疊的參數名稱，以及數字要不要變成 JSON 數字

- **lens:** adversarial
- **location:** AD-8「含參數 `field` 或 `label` 時，其後第一個不以空白拆開的詞為值，全由數字組成可作數字」
- **units:** 故事「最長標籤」對 故事「每個子字串都取值」
- **trigger_condition:** 同一句「年月 2026-10」同時含有標籤「年月」與「年」。全數字的詞「可」作數字，沒有規定必須或禁止。
- **兩套都符合的讀法:**
  - 最長標籤：重疊時只保留較長的「年月」，值是其後的 `2026-10`（字串，因為含連字號）。「年」沒有自己的值，狀態是 `incomplete`，不呼叫。全數字的詞維持字串，把「可」讀成允許但這套不轉換。
  - 每個子字串：兩個標籤都命中。「年」的下一個詞取到「月」或同一段裡的 `2026-10`。兩個參數都有值就會 `ready` 並呼叫。全數字的詞改成 JSON 數字再送進純量參數與工具引數。
- **clash:** 一邊不呼叫，另一邊呼叫，而且純量的 JSON 型別一個是字串、一個是數字。生成紀錄裡的參數物件不能互換。父 AD-12 的篩選值因此不是同一組純量。
- **guard_snippet:** 收緊 AD-8：參數命中沿用上一則的最長、不重疊、由左而右。值一律保持 JSON 字串，不因全由數字組成而改成數字。下一個詞以空白切開，標點不另切。這個詞若已被較左的參數用掉，該參數保持缺少。
- **potential_consequence:** 同一個年月參數，一個故事停在缺少參數，另一個故事用錯誤的詞或另一種 JSON 型別打到檢視，列結果不同，寫進生成紀錄的參數也不能互讀。

### 10. 未保存報表與已開啟紀錄共用哪一塊記憶體

- **lens:** adversarial
- **location:** AD-2；AD-7
- **units:** 故事「開啟即占同一槽」對 故事「紀錄另讀 SQLite」
- **trigger_condition:** 未寫入生成紀錄的動態報表在宿主記憶體，鍵是操作者 `id`。匯出接受這塊記憶體或一筆已開啟的生成紀錄。沒有寫「離開」與「開啟紀錄」是否動到同一塊。
- **兩套都符合的讀法:**
  - 同一槽：開啟生成紀錄會把該筆載入這個操作者的記憶體，尚未保存的報表被換掉。離開報表頁、換入口、或這次產生失敗，都刪除這塊記憶體，避免舊列還能被匯出。
  - 另讀：記憶體只放尚未寫入的那一份。開啟紀錄不寫入這塊記憶體，匯出時按紀錄識別讀 SQLite。離開只包含登出、明示關閉與程序重啟；換頁不刪。產生失敗時記憶體保持原樣，回應是錯誤，不用舊列冒充這一次的成功。
- **clash:** 尚未保存的報表在「開啟另一筆紀錄」或「產生失敗」之後，一邊已經消失，另一邊仍可匯出。AD-7 的刪除條件兩邊都引用了，刪除的時點與對象不同。匯出端點無法用同一個識別取回同一份數字。
- **guard_snippet:** 收緊 AD-7：每個操作者至多一塊未保存報表，鍵只有操作者 `id`。開啟生成紀錄不寫入這塊記憶體，也不刪除它；匯出與列印要帶來源：`unsaved` 或紀錄 `id`。離開是登出、明示關閉未保存報表、換成另一個入口，或程序重啟，只刪未保存那塊。呼叫失敗不刪、不替換這塊記憶體，回應是錯誤而不是報表。下一次成功產生才替換它。
- **potential_consequence:** 操作者打開舊紀錄之後，未保存的報表在一個故事裡被清掉，匯出卻在另一個故事裡仍印出舊列。失敗後連續匯出的數字也不一致。

### 11. Avalonia 的 HTTP 與意圖文件

- **lens:** adversarial
- **location:** AD-1；AD-6；AD-8；Structural Seed「契約在 `Workbench.Contracts`：樣式、意圖、動態報表」
- **units:** 故事「桌面殼」對 故事「宿主控制器」
- **trigger_condition:** Avalonia 的 JSON 型別必須是 `Workbench.Contracts`，bearer 只帶 `sessionId`，控制器不另寫業務判斷。路徑、動詞，以及意圖的欄位沒有表。
- **兩套都符合的讀法:**
  - 桌面殼：`POST /api/sessions` 以 `displayName`、`password` 換 `sessionId`；其後 `Authorization: Bearer`。比對是 `POST /api/match`，回應 `{ "status", "templateIds", "parameters": { "field": "value" } }`。
  - 宿主控制器：`POST /login` 換同一個 `sessionId`，bearer 規則相同。比對是 `POST /intent`，回應 `{ "status", "templates": [{ "id", "name" }], "values": [{ "field", "label", "value" }] }`。兩者都轉進 Core 的同一組端口，狀態只有 `ready`、`ambiguous`、`incomplete`。
- **clash:** 工作階段的攜帶方式相同，資源路徑與意圖 JSON 不同。桌面殼無法取得宿主的樣式與報表，或把參數物件讀成陣列。種子點名的契約沒有涵蓋操作者、檢視、生成紀錄與意圖欄位，所以兩套都算「型別放在 Contracts」。
- **guard_snippet:** 收緊 AD-1，並把路徑寫進 Consistency Conventions 的 HTTP 列：`POST /sessions`、`DELETE /sessions/current`、操作者與入口、檢視代理、樣式、`POST /match`、`POST /reports/current`、生成紀錄、匯出。意圖 JSON 是 `status`、`templateIds`、`parameters`（`field` 與字串 `value`）。上述型別全部放在 `Workbench.Contracts`。控制器只轉送，不改狀態。
- **potential_consequence:** Blazor 在程序內呼叫端口，Avalonia 走另一組 URL 與另一種意圖。兩個殼的就緒結果不能互換。

### 12. 樣式區塊的文件

- **lens:** adversarial
- **location:** AD-14；AD-8 的建議句
- **units:** 故事「欄名全集」對 故事「欄名子集」
- **trigger_condition:** 資料表區塊的欄「是」該檢視的輸出欄。建議的欄屬於該檢視。沒有寫 JSON 鍵名、欄是字串還是物件、可否多個資料表、保存時是否必須等於輸出欄全集。
- **兩套都符合的讀法:**
  - 欄名全集：鍵名 `blocks`。資料表至多一個。`columns` 是 `{ "name", "type" }` 陣列，且必須與檢視輸出欄全集同序。不符合就不能保存。
  - 欄名子集：鍵名 `components`。資料表可以多個。`columns` 是字串陣列，每個名稱都屬於輸出欄，順序由設計師決定，允許少於全集。建議裡的那一個資料表才使用全集。AD-14 的四種 `type` 與從 0 連續的 `order` 兩邊都遵守。
- **clash:** 已保存的樣式一邊無法被另一邊讀入。建議接受之後，一邊得到帶型別的物件陣列，另一邊得到字串陣列。多出來的第二個資料表在一邊是非法文件。
- **guard_snippet:** 收緊 AD-14：樣式區塊的鍵是 `components`。`columns` 是字串陣列，不得重複，每個元素都是所綁檢視的輸出欄名，允許子集與設計師指定的順序。資料表區塊可以有零個以上。建議裡至少一個資料表，其 `columns` 等於檢視輸出欄且同序；接受時把該區塊加到末尾。不使用 `{ name, type }` 物件。
- **potential_consequence:** 報表設計師儲存的版面，Chat 那一側無法還原欄序，或直接拒絕開啟。建議按接受之後兩邊的樣式列不同。

### 13. 多個候選時沒有第二個輸入

- **lens:** adversarial
- **location:** AD-8；AD-7
- **units:** 故事「再講一次名稱」對 故事「點選候選」
- **trigger_condition:** 樣式多於一個是 `ambiguous`，未就緒不呼叫 MCP。比對的輸入只有語句與可讀樣式。沒有寫操作者能否指定其中一筆。
- **兩套都符合的讀法:**
  - 再講一次：只有新的語句能縮小候選。Core 不提供選取端口。操作者沒有把名稱放進下一句之前維持 `ambiguous`，讀列次數是 0。
  - 點選候選：另有一個 Core 端口，只接受上一輪候選裡的樣式 `id`。選中後候選改為這一筆，狀態重算。這個端口的輸入不是語句，所以不違反「比對輸入只有語句」。它自己不呼叫 MCP；若因此變成 `ready`，下一次產生才依 AD-7 呼叫。
- **clash:** 同一個「選第二筆」的動作，一邊狀態不變，另一邊變成 `ready` 並在產生時送出不同的檢視。兩套都沒有確認清單，也都沒有在選取當下讀列。
- **guard_snippet:** 收緊 AD-8：`ambiguous` 時，Core 提供選取端口，引數是上一輪候選中的一個樣式 `id`。成功後候選只留該筆並重算狀態。不在候選內則拒絕，狀態不變。選取與改選都不呼叫 MCP。
- **potential_consequence:** 桌面殼有選取、網站殼只能重講，或相反。同一位操作者在兩個殼上會呼叫到不同的檢視，或一邊永遠停在多個候選。

---

## 不算洞

- 目錄媒介是檔案、SQLite 或其他：父 AD-12 寫明未決。現在選定其中一種的單元不能引用這份子脊的 Rules。AD-15 的「目錄的檔案」只表示工作台不開啟目錄儲存，不是把媒介定成檔案。上面第 3 則要釘的是文件形狀，不是媒介。
- `X-DBServices-Caller` 的身分證明：Deferred 寫明 `McpService` 不加第二套帳號，目錄寫入只拒絕空白。父 AD-6 的 MCP 工具呼叫仍把空白當成 `anonymous` 並執行。不能一邊加驗證中介軟體、一邊聲稱符合。
- 規劃期表結構通道、轉置是否落地、詞庫介面、模型設定、測試連線、側車啟動：Deferred 已排除。三個入口不讀寫模型、不啟動側車，AD-9 至 AD-12 已釘死。
- PDF 與 CSV 的套件、稽核保留天數：Deferred。產生位置仍是 AD-7 的未保存報表或已開啟紀錄，選定套件時不得再呼叫 MCP。
- 確認清單、30 分鐘閒置、預覽快照：AD-7 寫明沒有確認清單，慣例寫明動態報表記憶體沒有閒置逾時。這兩種選法不能再各做一套。
- 工作階段本體：AD-6 已是宿主記憶體的 `sessionId` 與操作者 `id`，cookie 與 bearer 只帶 `sessionId`，登出刪除，重啟清空。這不再讓兩個殼各持有一筆自包含票證。第 7 則留下的是入口字串、變更權，以及 `PasswordHasherOptions`。
- Blazor 與 `DbServices.Mcp`：AD-1 與種子把 Blazor 放在只參考 Core 與 Contracts 的 `Workbench.Web`。`Workbench.Adapters` 不參考 `DbServices.Mcp`。頁面直接持有 MCP 型別的那一套不再符合 Rules。
- `GET /connections`：AD-4 的回應只有組態完整連線的 `name` 與 `serverType`。在這條路由上呼叫 `Create` 會改到父 AD-4 的建立時機，不能算符合父脊的那一套。
- 單一 `Workbench` 進程、不寫第二份 MCP 稽核、不呼叫 `tables`／`fields`／`get`／`query`／`count`／`exists`、不在來源庫執行 `CREATE VIEW`：AD-2、AD-3、AD-13 與父 AD-12 已釘死。
