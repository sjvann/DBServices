# 連線範圍

## 規則

- Provider 只提供 `Func<DbConnection>`，不長活持有連線。
- 一般操作：`IDbConnectionScope.Use`／`UseAsync` → 建立 → 開啟 → 執行 → Dispose（歸還 pool）。
- 事務：`AcquireHeldConnection` 持有同一連線，Dapper 呼叫帶 ambient `IDbTransaction`。結束事務後釋放持有。

## 為何重要

長活 `DbConnection` 會耗盡 pool、與事務語意糾纏。把「借還」收斂到 scope，CLI 與 Web 就不會各寫一套 Open／Close。

## 實作提示

應用程式不應自行 `GetConnection()` 後忘記釋放。需要多陳述式原子性時用 `TransactionService`，不要手動快取連線。
