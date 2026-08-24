# 事務

一般查詢每次 `IDbConnectionScope.Use` 取連線、用完 Dispose（歸還 ADO.NET pool）。**事務必須使用同一條連線**，因此 `TransactionService` 會呼叫 `DataBaseService.AcquireHeldConnection()`，並把 `IDbTransaction` 設成 ambient，供 Dapper 使用。

```csharp
using var tx = new TransactionService(db, logger);

bool ok = tx.ExecuteInTransaction(() =>
{
    db.InsertRecord([new("Name", "A")], "Users");
    db.InsertRecord([new("Title", "t")], "Posts");
});
```

失敗會 Rollback 並回傳 false；成功 Commit。另有：

- `BeginTransaction`／`Commit`／`Rollback` 手動控制
- `ExecuteInTransaction<T>`
- `ExecuteInTransactionAsync`／`ExecuteInTransactionAsync<T>`

```csharp
if (tx.BeginTransaction())
{
    try
    {
        db.UpdateRecordById(1, [new("Stock", 9)], "Items");
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}
```

`IsInTransaction` 可查目前是否持有事務。`TransactionService` 實作 `IDisposable`，dispose 時若仍開啟會 Rollback。

跨資料庫（兩個 `IDbService`）**沒有**分散式事務；請在應用層設計補償。見架構文件「非目標」。
