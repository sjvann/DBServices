# GraphQL

`GraphQLService` 用 Hot Chocolate 把資料表露出成動態 Schema。啟動時讀取資料表與欄位，產生 Query 與 Mutation。資料存取與 [Web API](index.md) 相同，都經 `DbServiceFactory` 取得 `IDbService`。

## 啟動

```bash
dotnet run --project GraphQLService
```

預設位址是 `http://localhost:5134`。

| 表面 | 路徑 |
|------|------|
| 操作台 | `/` |
| GraphQL | `/graphql` |

操作台可選擇引擎、查看資料表與欄位，並在編輯器裡送出查詢。資料庫結構變更後需重新啟動，Schema 才會重建。

## 設定

`ServerType:Name` 使用與 Web API 相同的五個精確字串：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。

```json
{
  "ConnectionStrings": {
    "default": "Data Source=app.db"
  },
  "ServerType": {
    "Name": "Sqlite"
  }
}
```

這個宿主只對已存在的資料表提供查詢與 CRUD。建立資料表請用 SDK 或 SQL，不能靠 GraphQL mutation 建表。

## 命名

資料表 `Users` 會變成型別 `Users`。列表查詢是 camelCase 複數 `users`，單筆是單數 `user`，條件查詢是 `usersByField`。變更為 `createUsers`、`updateUsers`、`deleteUsers`。

```graphql
query {
  meta { tables }
  user(id: 1) { id name }
  usersByField(key: "Name", value: "Ada") { id name }
}

mutation {
  createUsers(input: { name: "Ada" }) { id name }
}
```

## 與 REST 的對照

| REST | GraphQL |
|------|---------|
| `GET /meta` | `meta { tables }` |
| `GET /Users/fieldSet` | introspection：`__type(name: "Users")` |
| `GET /Users/valueSet?fieldName=Name` | `fieldValueSet(table: "Users", field: "Name")` |
| `GET /Users` | `users { id name }` |
| `GET /Users?Key=Name&Value=Ada` | `usersByField(key: "Name", value: "Ada")` |
| `GET /Users/1` | `user(id: 1)` |
| `POST /Users` | `createUsers` |
| `PUT /Users/1` | `updateUsers` |
| `DELETE /Users/1` | `deleteUsers` |

## 限制

查詢深度上限是 15。這個版本沒有認證，與 Web API 相同；請只在受信任的網路裡暴露端點。開發環境可能回傳詳細例外，正式環境應關閉例外細節。
