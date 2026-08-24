# 多資料庫

## 手動註冊

```csharp
var multi = new MultiDatabaseService();
multi.RegisterDatabase("src", MainService.UsePostgreSQL(srcCs));
multi.RegisterDatabase("dst", MainService.UseMsSQL(dstCs));

var names = multi.GetRegisteredDatabaseNames();
IDbService? src = multi.GetDatabase("src");

var users = await multi.AggregateDataFromSourcesAsync(
    ["src"],
    async (db, name) =>
    {
        var table = db.GetRecordByTableName("Users");
        return table?.Records?.Select(r => r) ?? [];
    });
```

每個 `IDbService` 有**獨立**連線池設定，互不搶連線。

## Keyed DI

見 [依賴注入](dependency-injection.md) 的 `AddMultipleDbServices`。讀多寫一、報表彙整、遷移專案都適合。

## 建議

- 來源庫 `MaxPoolSize` 可較小（只讀）；目標庫寫入可較大。
- 不要假設共用事務。
- 欄位型別跨引擎不一致時，在彙整函數裡轉換，不要依賴自動方言翻譯（本庫不做 SQL planner）。
