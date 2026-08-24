# 查詢

## 鍵值條件

`GetRecordByKeyValue`／`GetRecordByKeyValues` 以 AND 連接條件，值一律走參數，識別名稱由各 Provider 引用。

## 進階查詢

`DataBaseService` 實作 `IAdvancedQueryService`：

```csharp
if (db is IAdvancedQueryService q)
{
    var page = q.GetRecordsWithOptions(
        query: [new("Status", "Active")],
        options: new QueryOptions
        {
            OrderBy = "CreatedAt",
            OrderByDescending = true,
            Skip = 20,
            Take = 10,
            SelectFields = ["Id", "Name", "CreatedAt"],
            UseParameterizedQuery = true
        },
        tableName: "Users");

    long total = q.GetRecordCount([new("Status", "Active")], "Users");
    bool exists = q.Exists([new("Email", "a@b.c")], "Users");
    var first = q.GetFirstRecord([new("Status", "Active")], "Users");
    int? age = q.GetFieldValue<int>("Age", [new("Id", 1L)], "Users");
}
```

`QueryOptions`：`OrderBy`、`OrderByDescending`、`Skip`、`Take`、`SelectFields`、`UseParameterizedQuery`（預設 true）。

## Session SQL

```csharp
var session = db.GetSession();
var result = session.Execute(new QueryRequest
{
    Text = "SELECT Id, Name FROM Users WHERE Age > @Age",
    Opcode = QueryOpcode.Query,
    Arguments = new Dictionary<string, object?> { ["Age"] = 21 },
    Format = ResultFormat.Json
});
```

`QueryResult.Kind`：`Rows`、`NonQuery`、`Catalog`、`Error`。錯誤不會丟例外，而是 `Kind = Error` 且 `Error` 有訊息（Session 層）。直接呼叫 `IDbService` 仍可能丟 `DbServiceException`。
