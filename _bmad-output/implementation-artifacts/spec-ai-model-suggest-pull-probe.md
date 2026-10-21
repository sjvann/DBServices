---
title: '本機模型可測試連線並以整段名稱選取'
type: 'feature'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'b0d48c7d9ad9a1ebcc6ec9b36bc43b11b5e076aa'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 環境設定的 AI 區只能手填位址與模型名稱。本機已有 Ollama 時，沒有預設位址，也不能測試連線後點選端點回傳的名稱。

**Approach:** 沿用雲端／本機表單。本機且位址空白時帶入 `http://127.0.0.1:11434/v1`。測試連線列出模型，點選填入整段名稱。`llama3.2:latest` 不是 `llama3.2`。測試不寫 `model.json`。

## Boundaries & Constraints

**Always:**
- 僅管理者。金鑰欄空白時沿用 `model.json`；狀態、錯誤與清單都不含金鑰。
- 測試先 `GET {位址}/models`，8 秒。已以 `/models` 結尾則不重複加。404 或 405 且已填名稱時，改送一次極短 chat。
- 比對用整段字串，區分大小寫。點選填入該整段。`llama3.2:latest` 不是 `llama3.2`。
- 清單排除名稱含 embed、embedding、whisper、tts、dalle、moderation、gpt-image、text-embedding 者。Gemma 保留。
- 本機須為 loopback 的 http(s)；雲端須為 https。不符合，或雲端沒有金鑰（含已存）時不連線。
- 失敗不改 `model.json`。錯誤文案不得含金鑰。

**Never:**
- 不做下載、建議下載鈕、進度或取消下載。
- 不做清清單、測試中鎖按鈕、或不跟隨重新導向。那三項已延後。
- 不加來源範本、多家供應商或記住多組端點。
- 不啟動側車，也不把測試做成側車 `kind=probe`。
- 不改檢視、樣式、語句、MCP 工具與連線目錄，金鑰不進 SQLite。
- 不套用工具呼叫家族黑名單。儲存不需先測試成功。

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 本機空白位址 | 放置為本機，位址空 | 帶入 `http://127.0.0.1:11434/v1`。沒有下載鈕 | 已有位址不覆寫 |
| 測試列出模型 | `/v1/models` 含 `llama3.2` 與嵌入模型 | 可點 `llama3.2`；無嵌入項；檔案不變 | 逾時或拒絕時人話，不寫檔 |
| 名稱不在清單 | 端點有清單，所填名稱不在內 | 仍算連上，並說明清單沒有這個模型 | 不改表單名稱 |
| 標籤名稱 | 清單只有 `llama3.2:latest`，所填 `llama3.2` | 說明清單沒有這個模型。點 `llama3.2:latest` 填入整段 | 不取冒號前的字 |
| 無清單改 chat | `GET /models` 為 404，已填名稱 | 送極短 chat；成功則說明沒有清單 | 未填名稱則要求先填，不送 chat |
| 雲端 | https 且有金鑰 | 可測試；沒有下載鈕 | 非 https 或無金鑰時不連線 |
| 權限與金鑰 | 非管理者；金鑰欄空但檔內已有 | 拒絕非管理者。空白欄仍用檔內金鑰，回應無金鑰字串 | 錯誤不含金鑰 |

</frozen-after-approval>

## Code Map

- `Workbench.Core/Ports/Ports.cs` 218、`Environment.cs` 的 `ProbeConnectionAsync` — 探測結果。先查管理者，不寫設定。
- `Workbench.Adapters/FileModelProfile.cs` 96–119 與新檔 `ModelEndpoint.cs` — 位址規則、空白金鑰、HTTP 探測。對照 `ProjectAskService.ModelsUrl` 與 `ProbeAsync`。
- `Workbench/Program.cs` 33、`Host.cs` 的 `RecordingModel` — 註冊端點與假端點。
- `Environment.razor` 207–230 — 預設位址、測試、點選。別改 202 行資料庫測試，也別改檢視、樣式、語句、側車。沿用 `secondary`、`confirm`、`hint`、`actions`。

## Tasks & Acceptance

**Execution:**
- [x] `Workbench.Core/Ports/Ports.cs` -- 探測結果與 `IModelEndpoint`，以及不回畫面的金鑰解析 -- 金鑰留在設定檔
- [x] `Workbench.Adapters/FileModelProfile.cs` -- 空白金鑰沿用檔內值 -- 不必為了測試重貼金鑰
- [x] `Workbench.Adapters/ModelEndpoint.cs` -- 只做測試：8 秒、整段比對、可注入 `HttpMessageHandler` -- 網路不進 Core，且沒有下載
- [x] `Workbench.Core/Environment.cs` -- 僅管理者可測 -- 與資料庫測試同一權限
- [x] `Workbench/Program.cs` -- 註冊 `IModelEndpoint` -- 執行期能呼叫
- [x] `Workbench.Web/Components/Environment.razor` -- 預設位址、測試、點選整段名稱 -- 沒有下載鈕
- [x] `Workbench.Tests/Host.cs` -- 假端點與金鑰解析 -- 既有宿主仍可啟動
- [x] `Workbench.Tests/ModelEndpointTests.cs` -- 假 HTTP 覆蓋矩陣，含標籤名稱。不連真 Ollama -- 不寫檔且回應不含金鑰

**Acceptance Criteria:**
- Given 管理者打開環境設定且位址空白, when 放置是本機, then 位址為 `http://127.0.0.1:11434/v1`，且畫面上沒有下載鈕。
- Given 端點有回應, when 測試連線, then 可點選模型名稱，且 `model.json` 不變。
- Given 清單只有 `llama3.2:latest`, when 測試所填的是 `llama3.2`, then 說明清單沒有這個模型。點選後名稱欄是 `llama3.2:latest`。

## Implementation Notes

- 含下載的程式已撤回。下載，以及清清單、測試中鎖按鈕、不跟隨重新導向，都已延後。
- 測試連線已接上。`ModelEndpoint` 只做探測。`FullyQualifiedName~ModelEndpoint|FullyQualifiedName~Model_file_keeps_the_key_out_of_the_status` 16 項通過。矩陣各列都有對應測試且已執行。沒有在瀏覽器打開網站。

## Spec Change Log

- 整段名稱相同：`llama3.2:latest` 不是 `llama3.2`。
- 下載，以及清清單、測試中鎖按鈕、不跟隨重新導向，已改到 deferred-work。

## Review Triage Log

- low — `ModelsUrl` 用字串接 `/models`，查詢字串或片段會接錯。日常位址沒有查詢字串，改成 Uri 要另寫組裝。拒絕。
- low — chat 未設 `stream: false` 時，串流回應會拖到逾時。兩次請求各 8 秒不是缺陷。patch：JSON 加上 `stream: false`。
- false — 不跟隨重新導向是這次意圖明確不做的。代理送出金鑰要另做處理，不在這次。
- false — 401 與連線失敗都用「連不上這個位址。」已是人話。Ollama 的清單欄位是小寫 `data`／`id`。空清單仍算連上。
- low — 只含空白的金鑰不會沿用檔內值。patch：空白視同沒填。金鑰很短就遮掉模型名稱要另定長度，拒絕那一部分。
- false — `ResolveKey` 只給探測使用，畫面經管理者檢查才呼叫。測試替身不必複製檔案金鑰。
- low — 預設位址寫了兩份；從本機改回雲端會留下 http 位址。要共享常數與切換邏輯。拒絕。
- medium — 點選 `llama3.2:latest` 後，狀態仍寫清單沒有這個模型。patch：點選時清掉這句。失敗或改位址後留下舊清單是這次明確不做的。
- false — 頁面沒有取消權杖，離開頁面不會取消。兩顆「測試連線」是資料庫與 AI 各自的按鈕，文案是規格要的。
- false — Code Map 行號過時。修正方式是改規格。拒絕。
- low — 金鑰含換行可能讓標頭丟出例外。要另加檢查。拒絕。
- low — 清單本文沒有長度上限。要另加上限。拒絕。
- low — 手動清空位址後按儲存不會先填預設。開啟頁面時會填。拒絕。
- false — 測試中再按一次會互相覆蓋。測試中鎖按鈕是這次明確不做的。
- low — 未登入時 `Gate.Current` 為空會中斷。表單只在管理者時出現。拒絕。
- false — 比對前去掉前後空白，不會把 `:latest` 切掉，也不改大小寫。
- medium — 頁面測試失敗沒有斷言 alert。已核對：拿掉 `_error = probed.Error` 時現有頁面測試仍過。patch：補頁面斷言。
- medium — 畫面上新打的金鑰沒有進入頁面測試。已核對：草稿改送空白時，直接呼叫的測試仍過。patch：補頁面斷言。
- medium — `ftp://127.0.0.1` 沒有測試。已核對：刪掉 scheme 判斷時現有 http 測試仍過。patch：補一筆不斷線的拒絕。
- medium — `HttpClient.Timeout` 的指派沒有被測試鎖住。要把 client 露出來才觀察得到。defer。

## Design Notes

測試打 `.../v1/models`。`localhost` 視同 `127.0.0.1`。不去掉 `:latest`。

## Verification

**Commands:**
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~ModelEndpoint` -- expected: 矩陣列通過
- `dotnet test Workbench.Tests/Workbench.Tests.csproj --filter FullyQualifiedName~Model_file_keeps_the_key_out_of_the_status` -- expected: 通過

**Manual checks (if no CLI):**
- 管理者打開環境設定：本機有預設位址與測試連線，沒有下載。失敗不改設定檔。
