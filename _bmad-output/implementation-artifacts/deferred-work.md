- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-1-啟動宿主-agent-只看見六個唯讀工具.md`
  summary: 客戶端呼叫未出現在 Discovery 的名稱時，四個關閉工具回 tool_disabled，其餘回 unknown_tool。
  evidence: 從 Story 1.1 拆出。2026-10-15 起，公開驗收是 Discovery 等於已發布模組視圖，不是恰好六個唯讀工具。拒絕回應仍是另一個可獨立驗收的行為。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-2-用-connectionid-列出資料表.md`
  summary: 省略 connectionId 時，恰有一筆組態完整的連線就用它，否則回 connection_required。
  evidence: 從 Story 1.2 拆出，讓這一則只交付帶著連線名稱列出資料表；省略名稱是另一個可獨立驗收的選擇規則。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-2-用-connectionid-列出資料表.md`
  summary: 未登記或空白名稱、connectionString 與 connectString、以及建立失敗不快取且恢復後可再成功。
  evidence: 從 Story 1.2 拆出，讓這一則只交付可連線名稱的列出資料表；拒絕與失敗恢復是另一個可獨立驗收的連線規則。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-2-用-connectionid-列出資料表.md`
  summary: 空白連線字串的 tables 呼叫可能回傳空列，看起來像空庫。
  evidence: 審查時無法從 diff 判定空白字串是回列還是丟例外。對一筆 Sqlite 且連線字串為空白的 connectionId 呼叫 tables，才能確定。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-5-用等值條件查列-計數-判斷存在.md`
  summary: 未列出的頂層鍵、篩選保留鍵（含 filters 裡的 connectionId）、只差大小寫的篩選鍵、非物件 filters、篩選值為物件或陣列，以及 connectionString 與 connectString，另做拒絕規則。
  evidence: 從 Story 1.5 拆出，讓這一則只交付可連線名稱加上純量 filters 的查列、計數與存在判斷；參數拒絕是另一個可獨立驗收的規則。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-ai-model-suggest-pull-probe.md`
  summary: 本機 Ollama 可下載建議模型（llama3.2、llama3.1、qwen2.5、mistral），含進度、取消，以及下載位址先去掉 /models 再呼叫 /api/pull。
  evidence: 規格超過 2000 token，使用者選擇拆開。測試連線與整段名稱比對留在原規格；下載可獨立驗收。名稱是否已安裝仍以整段字串為準，不在這一則實作。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-ai-model-suggest-pull-probe.md`
  summary: 測試連線的操作加固：失敗或變更位址時清掉清單、測試中不可再測或儲存、不跟隨重新導向。
  evidence: 使用者再次選擇拆開。預設位址、測試連線與整段名稱比對留在原規格；這些操作可獨立驗收。
