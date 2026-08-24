# PostgreSQL JSON

PostgreSQL Provider 將欄位型別 `"Json"`／`"JSON"` 對應為 `JSONB`。C# 端以字串保存，用 `JsonHelper` 序列化。

```csharp
var fields = new[]
{
    new FieldBaseModel { FieldName = "Id", FieldType = "Int64", IsPrimaryKey = true },
    new FieldBaseModel { FieldName = "Name", FieldType = "String", IsNotNull = true },
    new FieldBaseModel { FieldName = "Metadata", FieldType = "Json" }
};
db.CreateNewTable(fields, "Products");

var json = JsonHelper.Serialize(new { Category = "Electronics", Tags = new[] { "new" } });
db.InsertRecord(
    [
        new("Name", "Laptop"),
        new("Metadata", json)
    ],
    "Products");

var row = db.GetRecordById(1, "Products");
var raw = row?.Records?.FirstOrDefault()?.GetFieldValue<string>("Metadata");
var meta = JsonHelper.Deserialize<Dictionary<string, object>>(raw);
```

JSON 路徑查詢（`->`、`@>`）請用 `QuerySql` 寫 PostgreSQL 方言；本庫不翻譯跨引擎 JSON 函數。
