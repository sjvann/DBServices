- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-1-啟動宿主-agent-只看見六個唯讀工具.md`
  summary: 客戶端呼叫未廣告名稱時，四個關閉工具回 tool_disabled，其餘回 unknown_tool。
  evidence: 從 Story 1.1 拆出，讓第一份規格只交付可啟動的宿主與恰好六個唯讀工具；拒絕回應是另一個可獨立驗收的行為。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-2-用-connectionid-列出資料表.md`
  summary: 省略 connectionId 時，恰有一筆組態完整的連線就用它，否則回 connection_required。
  evidence: 從 Story 1.2 拆出，讓這一則只交付帶著連線名稱列出資料表；省略名稱是另一個可獨立驗收的選擇規則。
- source_spec: `E:\sjvann\DBServices\_bmad-output\implementation-artifacts\spec-1-2-用-connectionid-列出資料表.md`
  summary: 未登記或空白名稱、connectionString 與 connectString、以及建立失敗不快取且恢復後可再成功。
  evidence: 從 Story 1.2 拆出，讓這一則只交付可連線名稱的列出資料表；拒絕與失敗恢復是另一個可獨立驗收的連線規則。
