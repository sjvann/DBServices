# 資料庫遷移

`MigrationService` 在目標庫維護 `__SchemaMigrations`（`Version`、`Description`、`ExecutedAt`），依版本號升序執行尚未套用的 `MigrationBase`。

```csharp
public sealed class CreateUsers : MigrationBase
{
    public override long Version => 20250101001;
    public override string Description => "建立 Users";

    public override void Up(IDbService db, ILogger? logger = null)
    {
        db.ExecuteSQL("""
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL
            );
            """);
    }

    public override void Down(IDbService db, ILogger? logger = null)
        => db.DropTable("Users");
}

var migrator = new MigrationService(db, logger);
int applied = migrator.MigrateUp([new CreateUsers()]);
```

DDL 的 `CREATE TABLE IF NOT EXISTS` 語法依引擎略有差異，請在遷移裡寫該 Provider 能接受的 SQL，或改用 `CreateNewTable`。

回滾、已執行清單等 API 見 `IMigrationService`（[API 參考](../api/index.md)）。
