# 貢獻

歡迎 Pull Request。建議流程：

1. Fork [sjvann/DBServices](https://github.com/sjvann/DBServices)
2. 從 `master` 開功能分支
3. `dotnet test` 通過
4. 公開 API 變更請補 XML 註解與 `docs/guides` 對應頁
5. 開啟 PR，說明動機與測試方式

## 原則

- 維持 `IDbService`／`IDbServiceAsync` 相容；新能力放 `IQuerySession` 或獨立服務。
- 連線用完即還；不要在 Provider 快取 `DbConnection`。
- 使用者輸入走參數與 `IValidationService`。
- 不在此儲存庫內嵌 EZIE SDK。

授權為 MIT。文件寫作見 [編寫文件](documentation.md)。公開發行走 `Release/{版次}` 分支，見 [發行](release.md)。
