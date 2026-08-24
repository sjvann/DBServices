# CRUD

`IDbService` 以 `TableBaseModel`／`FieldBaseModel` 描述結果，列資料是鍵值集合，不是強制實體類別。需要強型別時，在應用層自行映射。

## 讀取

```csharp
TableBaseModel? byId = db.GetRecordById(42, "Users");

TableBaseModel? byOne = db.GetRecordByKeyValue(
    new KeyValuePair<string, object?>("Email", "a@b.c"),
    EnumQueryOperator.Equal,
    "Users");

TableBaseModel? byMany = db.GetRecordByKeyValues(
    [
        new("Status", "Active"),
        new("Country", "TW")
    ],
    "Users");

TableBaseModel? all = db.GetRecordByTableName("Users");
```

`EnumQueryOperator`：`Equal`、`NotEqual`、`GreaterThan`、`GreaterThanOrEqual`、`LessThan`、`LessThanOrEqual`、`Like`、`NotLike`。

`GetRecordByWhere` 接受 WHERE 片段；**不要**把未信任輸入直接拼進去，優先鍵值 API 或 `QuerySql`。

```csharp
var names = db.GetValueSetByFieldName("City", "Users");
bool any = db.HasRecord("Users");
bool exists = db.HasTable("Users");
```

## 新增／更新／刪除

```csharp
var inserted = db.InsertRecord(
    [
        new("Name", "Lin"),
        new("Age", 28)
    ],
    "Users");

db.UpdateRecordById(1, [new("Age", 29)], "Users");
db.UpdateRecordByKeyValue(
    new("Email", "a@b.c"),
    [new("Age", 29)],
    "Users");

db.DeleteRecordById(1, "Users");
db.DeleteRecordByKeyValue(new("Email", "a@b.c"), "Users");
```

## 結構

```csharp
string[]? tables = db.GetAllTableNames(includeView: true);
var fields = db.GetFieldsByTableName("Users");

db.CreateNewTable(fields, "UsersArchive");
db.DropTable("UsersArchive");
```

可用 `CreateNewTable<T>()`，`T : TableDefBaseModel`。

## 原始 SQL

```csharp
int n = db.ExecuteSql(
    "UPDATE Users SET Age = @Age WHERE Id = @Id",
    new { Age = 30, Id = 1 });

QueryResult rows = db.QuerySql(
    "SELECT Id, Name FROM Users WHERE Age > @Age",
    new { Age = 18 });
```

`ExecuteSQL(string)` 為無參數版本，僅應用於受控 DDL。

## 非同步

`IDbServiceAsync` 提供對應 `*Async` 方法與 `CancellationToken`。Provider 基底 `DataBaseService` 兩個介面都實作。
