# Adversary review — Architecture Spine DBServices 報表工作台 2026-10-12

- **Lens:** adversarial
- **Content class:** docs (architecture spine, behavioral)
- **Content:** `ARCHITECTURE-SPINE.md`
- **Method:** 往下一層組兩套實作。單元是史詩或故事。每一套都必須能引用現行 Rules，而且不新增產品範圍。兩邊仍對不上的共享資料形狀、同一實體的兩個主人、或兩條狀態變更路徑，才算一個洞。
- **Verdict:** holes

Deferred 已寫明 PDF／CSV 套件、稽核保留天數、資料表級授權、SSO、公開註冊、GraphQL 填數、自由 SQL、區間與跨表，以及沒有 OpenAI 相容 HTTP 的本地執行庫。這些不計入下面的洞：現在就選定其中一種的單元，無法引用「再定」。

引用位置都指這份主幹的 Rules、Design Paradigm、Structural Seed 與 Consistency Conventions。既有 WebService 的 JSON 只在某一條 AD 已經把「原樣進入預覽」交進去時，才拿來當碰撞的現場。

---

## 兩邊都能逐字符合

### 1. 確認前的呼叫清單沒有主人

- **lens:** adversarial
- **location:** AD-7；AD-5；AD-2；Design Paradigm「核心：確認清單、預覽快照」
- **units:** 史詩「交談確認」對 史詩「活資料預覽確認」
- **trigger_condition:** 就緒後要有一份可憑識別取回的呼叫清單，但 Rules 只規定快照放在宿主記憶體。
- **兩套都符合的讀法:**
  - 交談確認引用 AD-7「由核心產生呼叫清單並給予識別」與 Design Paradigm 的核心擁有確認清單。清單放在 `Workbench.Core` 的靜態字典，鍵只有清單識別。AD-7 把「操作者」寫在快照鍵上，沒有寫在清單鍵上。AD-5 的檔案封閉清單沒有這張表，所以不進 SQLite。AD-7 的離開、下一次確認、閒置 30 分鐘只刪快照，不刪這份清單。項目形狀沿 AD-3 的 MCP：`filters` 物件。
  - 活資料預覽引用同一句「確認請求帶回該識別」，把識別做成簽過名的清單本體，內含操作者 id。不另佔字典，重啟後仍能核對。Web 那一筆不放 `filters`，改放 `resourceName`、`Key`、`Value`，因為 AD-3 規定 Web 的送出形狀就是這兩個查詢參數，而且「實際送出的填數呼叫與清單相同」。
- **clash:** 同一個確認端點無法交換兩種識別。字典那套在重啟後全部失效，簽名那套仍可執行。字典鍵不含操作者，甲可以拿乙的識別完成確認；簽名那套會拒絕。MCP 項目與 Web 項目不是同一種紀錄，執行器無法共用。AD-2 只說宿主持有預覽快照，沒有把未確認清單交給同一處。
- **guard_snippet:** 收緊 AD-7：未確認清單只放宿主記憶體，鍵是操作者加清單識別，程序重啟即失。不進 SQLite、cookie、bearer，也不把呼叫本體簽進識別。項目欄位寫死為 `surface`、`name`、`connectionId`、`table`（Web 為 `resourceName`）、`filters`（純量巢狀；Web 單鍵仍是一個純量，送出時才拆成 `Key` 與 `Value`）、可空的 `componentId`。執行前用當下範本重算清單，與保存的那份逐欄相等；不等就整次拒絕。
- **potential_consequence:** 兩個殼或兩條確認入口各自保存清單，實際送出的呼叫與畫面上列明的那份對不上，或一個操作者執行了另一人的填數呼叫。

### 2. 登入工作階段沒有紀錄本體

- **lens:** adversarial
- **location:** AD-6；AD-5；AD-1；Design Paradigm「登入」在核心，且核心不參考 ASP.NET
- **units:** 故事「Blazor cookie 登入」對 故事「Avalonia bearer 登入」
- **trigger_condition:** 兩邊都要指向宿主上的同一筆工作階段，但這筆紀錄的存放處、欄位與壽命都沒寫。SQLite 封閉清單裡沒有它。
- **兩套都符合的讀法:**
  - Blazor 引用 AD-6 的 cookie，以及 AD-1「核心不參考 ASP.NET」。`PasswordHasher` 因此放在宿主。工作階段是加密 cookie 裡的自包含票證，欄位是操作者 id 與簽發時間。AD-5 不允許工作階段表。Avalonia 的 bearer 複製同一段票證，「同一筆」指內容相同。登出把票證識別放進宿主記憶體的作廢清單。Cookie 沿用 ASP.NET 預設的滑動續期，只更新 cookie。
  - Avalonia 引用 AD-6「同一筆工作階段」與「登出使該工作階段失效」。宿主記憶體只有一筆 `{sessionId, operatorId}`，由核心的登入端口建立。Cookie 與 bearer 都只帶 `sessionId`。登出刪除該筆。重啟後全部失效。`PasswordHasher` 是宿主對該端口的實作，相容模式與迭代次數用套件預設。
- **clash:** 滑動續期之後，cookie 與 bearer 不再是同一筆票證；記憶體那套仍是一筆。程序重啟後，自包含票證的作廢清單一起消失，bearer 又變有效；記憶體那套必須重新登入。兩套寫出的 `passwordVerifier` 不能互換，因為迭代次數沒有寫死。登入狀態的主人一個是 ASP.NET cookie 處理器，一個是核心端口。
- **guard_snippet:** 收緊 AD-6：工作階段只有宿主記憶體一筆，欄位僅 `sessionId` 與操作者 id。Cookie 與 bearer 都只帶 `sessionId`。登出刪除該筆。重啟全部失效。不使用自包含票證，也不做滑動續期。`PasswordHasher` 只在宿主實作核心端口，寫明 `PasswordHasherCompatibilityMode.IdentityV3` 與迭代次數。核心不參考 ASP.NET，也不自己寫第二份工作階段。
- **potential_consequence:** 登出只讓其中一個殼失效；重啟之後已登出的 bearer 恢復；兩次安裝的密碼驗證材料不能互相讀。

### 3. Blazor 與 `DbServices.Mcp` 沒有專案邊界

- **lens:** adversarial
- **location:** AD-1；AD-4；Structural Seed「`Workbench` 參考 `DbServices.Mcp`」與「Blazor 在 `Workbench`」；流程圖 `blazor --> host --> mcpLib`
- **units:** 故事「Blazor 連線清單」對 故事「核心目錄端口」
- **trigger_condition:** 殼不得參考 `DbServices.Mcp`，但 Blazor 沒有自己的專案，編譯進已參考該套件的 `Workbench`。
- **兩套都符合的讀法:**
  - Blazor 連線清單引用 Structural Seed：參考屬於 `Workbench`，Blazor 沒有第二個 `ProjectReference`。AD-1「不參考」因此只約束 `Workbench.Avalonia` 的專案參考。AD-1「只經過核心端口」的封閉清單是填數、結構呼叫、範本讀寫、角色判斷、解析與快照，不含 AD-4 的連線中繼資料。Razor 直接呼叫 `DbServices.Mcp` 的新讀取方法，回傳型別用該套件上沒有連線字串的那個型別。填數仍走核心端口。元件不讀 `ConnectionSnapshot.ConnectionString`。
  - 核心目錄端口引用 AD-1「兩個殼都不參考 `DbServices.Mcp`」，把 Blazor 算成殼，Razor 與 code-behind 都不出現該命名空間。AD-4 的讀取包成 `Workbench.Core` 的端口，回傳 `Workbench.Contracts` 的 `{ name, serverType, available }`。只有宿主的配接器參考 `DbServices.Mcp`。Avalonia 的 HTTP 使用同一個契約。
- **clash:** 連線清單這一個實體有兩個主人、兩種 CLR 形狀。Blazor 綁套件型別，Avalonia 綁契約型別。`Workbench` 的專案參考讓 Razor 能編譯 `DbServices.Mcp`（以及它傳遞而來的 `DbServices.Core`）上的其他公開成員；第一套認為這不算殼的參考，第二套認為這已經違反邊界。AD-4 只說「回傳型別沒有連線字串屬性」，沒有說型別放在哪一個專案。
- **guard_snippet:** 收緊 AD-1 與 AD-4：Blazor 畫面放在不參考 `DbServices.Mcp` 的專案，或明文禁止 `Workbench` 的 Razor、code-behind 使用 `DbServices.Mcp`、`DbServices.Core`、Provider 命名空間。連線中繼資料的唯一回傳型別放在 `Workbench.Contracts`，欄位僅 `name`、`serverType`、`available`。只有宿主配接器參考 `DbServices.Mcp`，而且只呼叫 AD-4 那個讀取方法與 AD-3 的四個工具。`Workbench.Core` 不參考 `DbServices.Mcp`。
- **potential_consequence:** 瀏覽器殼直接持有 MCP 型別與基底網址，桌面殼走另一種 JSON。之後任一方都能繞過核心端口呼叫填數。

### 4. 同一句片語可以同時進詞庫與常用項目

- **lens:** adversarial
- **location:** AD-12；AD-5；AD-8；Consistency Conventions「參數值留在該次語句或個人常用項目」
- **units:** 故事「加入關鍵字」對 故事「保存常用項目」
- **trigger_condition:** 關鍵字規則的 `phrase` 與常用項目的「個人叫法」都可以保存同一段文字，沒有跨表唯一性，也沒有誰供解析使用。
- **兩套都符合的讀法:**
  - 加入關鍵字引用 AD-12：`phrase` 寫進共用詞庫，可帶連線名稱、範本識別與參數欄名，沒有參數值。常用項目不寫進這份詞庫。AD-8 的兩層只有看得到的名稱與仍有效的規則，所以語句只命中規則。缺值就停留在未就緒，填數次數是 0。
  - 保存常用項目引用 AD-5「常用項目有 `surface`、連線名稱與參數值」——這句是「有」，不是「僅有」；明文禁止的是版面複本、資料列、密碼與連線字串，沒有禁止 `phrase`。AD-12 稱常用項目為個人叫法，且可含參數值。Consistency Conventions 把參數值放在語句或個人常用項目。這套因此在常用項目另存同一個 `phrase` 與參數值。再次叫用時直接套用該列的連線與參數值，不把值寫進詞庫。
- **clash:** 同一段文字有兩筆記錄、兩條狀態路徑。語句解析得到沒有參數值的規則意圖；從常用清單開啟得到有參數值的意圖。AD-8 不讀常用項目，所以沒有合併規則。若常用項目故事為了讓叫法可被說出來，把 `phrase` 放進 AD-8 的第一層，就和「兩層」的列舉對撞；若關鍵字故事不在常用項目放 `phrase`，兩筆又失去可對齊的鍵。AD-5 也沒有寫常用項目的範本識別，兩套會各自決定有沒有這個欄。
- **guard_snippet:** 收緊 AD-5、AD-8、AD-12：常用項目的欄位寫死為擁有者、名稱、`phrase` 可與名稱相同、範本識別、`surface`、連線名稱、參數值。`phrase` 允許與某條規則的 `phrase` 相同，不做跨表唯一。語句比對只讀 AD-8 的兩層，不讀常用項目，也不套用它的參數值。再次叫用只讀該筆常用項目與範本目前的版面，不把名稱送回解析器。規則仍然沒有參數值。
- **potential_consequence:** 操作者說出剛存好的叫法時，有的入口套用上次的參數值，有的入口停在缺少參數。兩邊都會認為自己符合規則。

### 5. 測到的金鑰與解析時使用的金鑰可以不同

- **lens:** adversarial
- **location:** AD-9；AD-10；AD-11
- **units:** 故事「測試連線」對 故事「解析呼叫側車」
- **trigger_condition:** 有效設定的覆寫順序包含 `model.json`、user secrets、環境變數；探測提交的是草稿；側車的金鑰卻「只從環境變數讀入」。沒有寫子程序要注入哪一把、變數叫什麼、空白草稿代表清除還是沿用。
- **兩套都符合的讀法:**
  - 測試連線引用 AD-10：草稿先做 AD-9 的檢查，不通過則 `model_invalid_settings` 且不啟動側車。AD-9 禁止金鑰進入解析 JSON、程序引數、日誌與測試結果，沒有禁止金鑰進入探測 JSON。AD-10 的探測排除清單是語句、範本、連線名稱、資料列與資料庫連線字串，不含 `apiKey`。這套把草稿的 `apiKey`、`endpoint`、`modelId` 放進探測的標準輸入。子程序環境變數保持宿主原樣，不改 `model.json`。雲端草稿的 `apiKey` 空白就判無效，即使 `model.json` 或環境變數裡已有金鑰。
  - 解析呼叫側車引用 AD-9「有效設定只有一份」與覆寫順序：檔案，然後 user secrets，然後環境變數。AD-9 禁止金鑰進入解析 JSON。AD-11 規定金鑰只從環境變數讀入，因此標準輸入裡的金鑰一律忽略。這套在啟動解析前，把合併後的 `apiKey` 寫進子程序環境。變數名稱自定。探測若由同一套側車執行，讀到的是繼承來的環境變數，不是草稿 JSON 裡那一把。`model.json` 的金鑰只有在某一步把它拷進環境時才會被用到；這套只在解析時拷貝合併結果。
- **clash:** 管理員以 `model.json` 的金鑰當草稿送測時，測試故事認為測的是草稿；側車故事只讀環境變數，測到的是另一把，或是空白。解析使用的是覆寫後的合併金鑰。三處可以同時各有一把：檔案、環境變數、未儲存草稿。讀取 API 只回傳金鑰是否已設定，畫面無法知道測的是哪一層。本地草稿把金鑰留白時，第一套當成「沒有金鑰」並允許啟動，第二套仍沿用環境變數裡的雲端金鑰去打 `localhost`。
- **guard_snippet:** 收緊 AD-9、AD-10、AD-11：金鑰不進入探測 JSON，也不進入解析 JSON。宿主為每一次側車程序設定同一個已命名的環境變數，寫明名稱。探測拷貝的是這份草稿的整份設定，空白 `apiKey` 就是空白，不回填檔案或環境變數。解析拷貝的是合併結果，逐欄覆寫：`model.json`，然後 user secrets，然後環境變數。測試結果不得暗示這把金鑰已是解析會用的那把，除非草稿與合併結果的非秘密欄位以及「金鑰是否相同」一致。側車不讀第二個變數名稱。
- **potential_consequence:** 測試成功的是草稿或繼承環境，解析打出去的是另一把金鑰。金鑰只存在檔案裡時，側車可能永遠看不到它。

### 6. 預覽快照的列形狀

- **lens:** adversarial
- **location:** AD-3；AD-4；AD-7
- **units:** 故事「MCP 預覽」對 故事「Web 預覽」
- **trigger_condition:** 結果要原樣進入預覽，快照的值只被寫成「該次畫面的數字與列」。Web 既有 `GET /{resourceName}` 的 JSON 物件含 `ConnectString` 與 `RecordData`；帶 `Key` 與 `Value` 時本體是 JSON 字串。MCP `query` 的成功形狀是 `columns` 與 `rows`。
- **兩套都符合的讀法:**
  - MCP 預覽引用 parent AD-7／AD-9 的成功形狀與 AD-3「不做第二次彙總」。快照的表是 `columns` 與 `rows`。它把 Web 的 `RecordData` 轉成同一形狀，並在進入快照前去掉 `ConnectString`，引用 AD-4 不得寫入 HTTP 或匯出。轉換不算第二次彙總。
  - Web 預覽引用 AD-3「結果列與計數原樣進入預覽」。零鍵回應的物件原樣成為快照，單鍵回應的 JSON 字串也原樣成為快照。`ConnectString` 留在宿主記憶體的快照裡，因為 AD-7 的快照不是 SQLite，AD-4 的禁止清單寫的是日誌、SQLite、HTTP、側車與匯出。匯出前才刪掉該屬性。Blazor 在程序內綁到這份快照，不經過他們所認定的 HTTP API。
- **clash:** 同一份快照識別，表的 JSON 一個是 `columns`／`rows`，一個是 `RecordData` 或一整段字串。匯出與列印無法共用一個讀取器。第二套的記憶體快照含連線字串，程序內的 Blazor 看得到；第一套沒有這個欄。
- **guard_snippet:** 收緊 AD-3 與 AD-7：進入快照之前，MCP 與 Web 都正規化成 `columns` 與 `rows`，儲存格編碼沿用父脊椎的成功列。Web 的 `ConnectString` 在正規化時丟棄，不進快照、不進 Blazor 電路、不進 HTTP。單鍵回應若是 JSON 字串，先還原成物件再正規化。這一步不是計數，也不是第二次彙總。
- **potential_consequence:** CSV 與 PDF 在 Web 表面匯出的不是畫面上的列，或連線字串進入瀏覽器電路。

### 7. 兩層關鍵字如何合併

- **lens:** adversarial
- **location:** AD-8；AD-12
- **units:** 故事「目錄比對」對 故事「詞庫比對」
- **trigger_condition:** 語句同時含一個連線名稱，以及一條仍有效、但指向另一個連線的 `phrase`。AD-8 只說兩層合併成一個意圖。
- **兩套都符合的讀法:**
  - 目錄比對引用第一層「當時這位操作者看得到的連線名稱、範本名稱、參數 `field` 與 `label`」。名稱命中即進入候選。規則若指向別的連線，兩層都保留，狀態由宿主重算成 `ambiguous`。
  - 詞庫比對引用 AD-12 的規則與 AD-8「合併成一個意圖」。`phrase` 命中的規則覆蓋第一層的名稱，意圖只留規則上的連線與範本。沒有第二個候選，所以可以是 `ready`。
- **clash:** 同一句話，一套停在多個候選、填數次數 0；另一套直接就緒並產生呼叫清單。兩層都沒有被丟掉，只是「合併」沒有優先序。比對是包含還是整句相等，AD-8 也沒寫，兩套還會再裂一次。
- **guard_snippet:** 收緊 AD-8：第一層用序數、區分大小寫的包含比對；規則的 `phrase` 用同一種包含比對。兩層的候選取聯集，不互相覆蓋。聯集後多於一個連線或範本就是 `ambiguous`。參數值只從語句在欄名或標籤之後的第一個詞取得，不從規則取得。
- **potential_consequence:** 同一句話在兩個故事裡一個不能確認、一個會送出不同的連線。

### 8. `design` 是否包含 `view`，以及誰可以確認範本

- **lens:** adversarial
- **location:** AD-5；AD-7；Consistency Conventions「群組角色只約束範本」
- **units:** 故事「角色指派」對 故事「範本確認」
- **trigger_condition:** 一個操作者對一個群組至多一筆 `design` 或 `view`。沒有寫 `design` 是否具備檢視能力，也沒有寫哪一種角色可以確認範本填數。
- **兩套都符合的讀法:**
  - 角色指派引用 AD-5 的兩個儲存值。`design` 只允許改範本，`view` 只允許列出與確認。確認路徑引用 AD-7「核對角色」，`design` 沒有檢視能力，所以不能產生呼叫清單。
  - 範本確認引用同一組儲存值，把 `design` 讀成包含 `view` 的檢查：`design` 或 `view` 都可以確認，只有 `design` 可以改畫布。活資料預覽仍對已登入操作者開放，這句兩套都遵守。
- **clash:** 同一個 `design` 列，一套拒絕範本預覽且填數次數是 0，另一套產生清單並在確認後送出。儲存的列沒有違反「至多一筆」。
- **guard_snippet:** 收緊 AD-5：儲存值仍是 `design` 或 `view`。能力上 `design` 包含 `view`。列出範本、產生呼叫清單與確認填數，兩種角色都可以。寫入範本只有 `design`。沒有該群組角色則整次拒絕。
- **potential_consequence:** 設計者在一個故事裡看不到自己的範本，在另一個故事裡可以確認填數。

### 9. 只綁參數欄名的規則誰能新增

- **lens:** adversarial
- **location:** AD-12
- **units:** 故事「人為新增規則」對 故事「管理員停用規則」
- **trigger_condition:** 連線、範本、參數欄名至少有一項。可見範本的限制寫在「為範本加規則」；「只綁連線名稱」則任何已登入操作者可新增。只填參數欄名陣列時，兩句都不涵蓋。
- **兩套都符合的讀法:**
  - 人為新增規則引用「至少有一項」與「人為新增立即有效」。只含欄名的規則沒有落到範本限制，也不是「只綁連線」，因此任何已登入操作者可寫入，立即有效。
  - 管理員停用規則引用另外兩句，把它們讀成僅有的兩條新增路徑。只含欄名的規則不接受操作者新增；若列上已有這種規則，只有建立者或管理員能改為無效。
- **clash:** 同一張詞庫，一套會多出立即有效的欄名規則，另一套根本不寫入。比對時第一套會用這些欄名從語句取參數值，第二套的意圖少那些參數。
- **guard_snippet:** 收緊 AD-12：只含參數欄名、且沒有連線也沒有範本的規則，任何已登入操作者可新增，立即有效。有範本識別時，仍限該操作者看得到的範本。只綁連線名稱的句子維持不變。
- **potential_consequence:** 欄名規則在一個故事裡會改變解析，在另一個故事裡不存在。

### 10. 快照的「閒置 30 分鐘」從何時起算

- **lens:** adversarial
- **location:** AD-7
- **units:** 故事「匯出 PDF」對 故事「預覽畫面」
- **trigger_condition:** 離開預覽、同一操作者的下一次確認，或閒置 30 分鐘，快照刪除。沒有寫閒置是自產生起算，還是自最後一次讀取起算。
- **兩套都符合的讀法:**
  - 匯出 PDF 引用「閒置 30 分鐘」。時限自快照寫入記憶體起算，中間的匯出與列印不算活動。滿 30 分鐘識別失效。
  - 預覽畫面引用同一個「閒置」。操作者仍開著預覽、或又請求了一次匯出，就重新起算 30 分鐘。離開預覽與下一次確認仍立即刪除。
- **clash:** 產生後第 31 分鐘、操作者剛剛匯出過一次時，一套拒絕舊識別，另一套仍從同一份快照產生 PDF。AD-7 要求連續匯出的業務數字相同，但沒有說第二次是否還在期限內。
- **guard_snippet:** 收緊 AD-7：30 分鐘自快照建立起算，不因匯出、列印或畫面仍開著而延長。離開預覽與同一操作者的下一次確認仍立即刪除。
- **potential_consequence:** 同一識別有時能匯出、有時不能，測試無法判定哪一種才是規則。

---

## 同一條規則裡兩句不能同時成立

這兩項不是「兩邊都滿足每一句」。每一邊都能引用其中一句，但沒有任何一套能同時滿足兩句。洞一樣要收緊規則。

### 11. 側車既要看到合併後的金鑰，又只能讀環境變數

- **lens:** adversarial
- **location:** AD-9、AD-11
- **units:** 故事「合併 model.json」對 故事「側車讀環境」
- **trigger_condition:** 金鑰只寫在 `model.json` 或 user secrets，程序環境裡沒有同名變數。
- **兩套引用:**
  - 合併那套引用 AD-9：有效金鑰來自檔案，然後 user secrets，然後環境變數。檔案裡的非空白金鑰就是有效金鑰。
  - 側車那套引用 AD-11：金鑰只從環境變數讀入。標準輸入不能帶金鑰（AD-9 禁止進入解析 JSON；探測 JSON 即使能帶，側車這一句也不讀）。環境裡沒有變數，金鑰就是空白。
- **clash:** 不增加一句「宿主把有效金鑰拷進子程序環境」，檔案與 user secrets 永遠到不了側車；拷貝則表示金鑰的讀取來源不只是原本的環境變數。兩句不能同時成立。這和洞 5 是同一條縫：測到的那把與解析使用的那把會分開。
- **guard_snippet:** 收緊 AD-11：側車只讀宿主為該子程序設定的那一個變數。宿主在 `ProcessStartInfo` 裡寫入 AD-9 的有效金鑰（解析）或 AD-10 的草稿金鑰（探測），不繼承宿主程序裡同名的其他金鑰。變數名稱寫死。
- **potential_consequence:** 只把金鑰放在 `model.json` 的部署，檢查通過，側車卻以空白金鑰呼叫，或誤用機器上另一個環境變數。

### 12. 核心擁有登入，又不得參考做出工作階段的框架

- **lens:** adversarial
- **location:** Design Paradigm、AD-1、AD-6
- **units:** 故事「核心登入端口」對 故事「宿主 cookie 登入」
- **trigger_condition:** 第一個管理員與之後的登入都要使用 ASP.NET Core 的 `PasswordHasher`，工作階段要讓 cookie 與 bearer 指向同一筆。
- **兩套引用:**
  - 核心登入引用 Design Paradigm：登入在 `Workbench.Core`。端口要能驗證密碼並建立 AD-6 的那一筆工作階段。
  - 宿主 cookie 引用 AD-1：核心不參考 ASP.NET。`PasswordHasher` 與 cookie 處理器都在宿主。工作階段由宿主建立，核心沒有這筆紀錄。
- **clash:** 登入與工作階段的主人只能有一個。放在核心就參考了 ASP.NET，或自己再做一份驗證；放在宿主就不是「核心的登入」。洞 2 的兩種紀錄就是這兩句各自落地的結果。
- **guard_snippet:** 收緊 AD-1 與 AD-6：核心擁有登入端口與工作階段的記憶體紀錄。`PasswordHasher` 與 cookie／bearer 的寫入格式只在宿主，經端口注入。核心的紀錄格式以洞 2 的 `sessionId` 為準。
- **potential_consequence:** 兩個故事各寫一套登入，cookie 與 bearer 指向不同的紀錄，登出無法同時失效。

---

## 不算洞

- PDF、CSV 的具體套件，以及稽核保留天數：Deferred 已禁止把其中一種當成已採用。產生位置仍是宿主快照。
- 資料表級授權、SSO、公開註冊、GraphQL、自由 SQL、區間與跨表：Deferred 已排除。
- 沒有 OpenAI 相容 HTTP 的本地執行庫：Deferred 要求先改 AD-11，不在殼或核心另引 SDK。
- 快照不寫入 SQLite、Avalonia 不開啟該檔、不另寫第二份 MCP 稽核：AD-2、AD-7、parent AD-10 已釘死。
- 工作台不呼叫 `get`、`exists`、`insert`、`update`、`delete`、`sql`，也不把篩選拼進 SQL：parent AD-2、parent AD-3 與 AD-3 已釘死。
- `enabled` 為 false 時只走關鍵字、側車不寫 SQLite、解析失敗不改範本與規則：AD-8、AD-10、AD-11 已釘死。
- 模型金鑰不進 SQLite、範本沒有連線名稱與資料列、常用清單只回擁有者：AD-5 與 AD-9 已釘死。這些句子沒有解決洞 4 的雙份片語，也沒有解決洞 5 的哪一把金鑰被送進側車。
