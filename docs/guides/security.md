# 安全

## 參數化

`GetRecordById`、`GetRecordByKeyValue`、`GetRecordByKeyValues`、`ExecuteSql`、`QuerySql` 以及 Session 的 Query／Call 都使用參數。

**不要**把使用者字串拼進 `GetRecordByWhere` 或 `ExecuteSQL`。

## 識別字驗證

`ValidationService` 檢查表名／欄位名：字母數字底線、長度上限、拒絕常見 SQL 片段。Provider 仍會依方言引用識別字。

開啟方式：建構器 `WithValidation` 或 DI 註冊的 `IValidationService`。

## 連線字串

```csharp
var cs = builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings:App");
```

禁止寫進原始碼或提交到 git。生產環境用環境變數、Secret Store 或雲端 Managed Identity。

EZIE 元件只填 `connectionId`，連線字串留在 EZIE 連線目錄。

## 權限

應用帳號只給需要的 DML；遷移用另一個較高權限帳號執行。

## 錯誤訊息

`DbServiceException` 可能含 SQL。對外 API 請記錄完整例外、回傳泛化訊息。Session 的 `QueryResult.Kind = Error` 同樣不要原樣吐給匿名用戶。
