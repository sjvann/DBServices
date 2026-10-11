# GraphQLService

以 [Hot Chocolate](https://chillicream.com/docs/hotchocolate) 提供 DBServices 的 GraphQL 介面。啟動時會讀取資料庫中所有資料表與欄位，**動態產生**對應的 Object / Input 型別與 Query / Mutation 欄位。

與 [WebService](../WebService) 共用 [DbServices.Api.Shared](../DbServices.Api.Shared) 業務層（`DbTableOperations`）。

## 啟動

```bash
cd GraphQLService
dotnet run
```

- **操作台（自訂 UI）**：`http://localhost:5134/`
  - 選擇資料庫系統 → 輸入連線字串 → 連線
  - 最左欄：**目前資料庫所有資料表**
  - 中欄：選定資料表的欄位 / 查詢範本 + GraphQL 編輯器
  - 右欄：查詢結果 **樹狀檢視**（可切換原始 JSON）
- GraphQL 端點：`http://localhost:5134/graphql`
- Banana Cake Pop IDE：`http://localhost:5134/graphql`

## 設定

與 WebService 相同，編輯 `appsettings.json`：

```json
{
  "ConnectionStrings": {
    "default": "Data Source=C:\\temp\\Test.db;"
  },
  "ServerType": {
    "Name": "Sqlite"
  },
  "SeedTestData": {
    "Enabled": true
  }
}
```

`ServerType:Name` 支援：`Sqlite`、`SqlServer`、`MySQL`、`Oracle`、`PostgreSQL`。

> 資料庫結構變更後需**重新啟動**服務以重建 GraphQL Schema。

## 能否用 GraphQL 新增資料表？

**目前不行。** GraphQLService 僅對**已存在**的資料表提供查詢與 CRUD（動態 Schema）。  
建立資料表請使用 SDK 的 `CreateNewTable<T>()` / `CreateNewTable(...)`，或執行 SQL 腳本（見下方測試資料表）。

## PostgreSQL 庫存管理（本機）

`appsettings.Development.json` 預設連線本機 PostgreSQL，並自動建立資料庫 `inventory`：

```json
{
  "ConnectionStrings": {
    "default": "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=您的密碼",
    "postgresAdmin": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=您的密碼"
  },
  "ServerType": { "Name": "PostgreSQL" },
  "SeedInventory": { "Enabled": true }
}
```

| 資料表 | 說明 |
|--------|------|
| categories | 商品分類 |
| products | 商品與庫存 (stock_qty) |
| suppliers / customers | 供應商、客戶 |
| purchase_orders / purchase_order_items | 進貨單 |
| sales_orders / sales_order_items | 銷貨單 |

手動觸發：`POST /api/seed/inventory`

```graphql
query {
  products { id name sku stockQty unitPrice }
  salesOrders { id customerId orderDate totalAmount }
}
```

> 密碼請勿提交至公開版本庫；本機開發可放在 `appsettings.Development.json`（已加入 `.gitignore` 建議）或使用 User Secrets。

## 測試資料表（C:\temp\Test.db）

首次啟動且 `SeedTestData:Enabled` 為 `true` 時，若資料庫尚無 `Users` 表，會自動建立：

| 資料表 | 說明 |
|--------|------|
| Users | 使用者（Name、Email） |
| Products | 商品（Name、Price、Qty） |
| Orders | 訂單（ProductName、Quantity、OrderDate） |

手動觸發：`POST /api/seed/test-tables`

### 範例查詢

```graphql
query {
  users { id name email }
  products { id name price qty }
  orders { id productName quantity orderDate }
}
```

## REST 對照表

假設資料表名稱為 `Users`（GraphQL 型別名為 `Users`）：

| REST | GraphQL |
|------|---------|
| `GET /meta` | `query { meta { tables } }` |
| `GET /Users/fieldSet` | Schema introspection：`__type(name: "Users")` |
| `GET /Users/valueSet?fieldName=Name` | `query { fieldValueSet(table: "Users", field: "Name") }` |
| `GET /Users` | `query { users { id name } }` |
| `GET /Users?Key=Name&Value=Alice` | `query { usersByField(key: "Name", value: "Alice") { id name } }` |
| `GET /Users/1` | `query { user(id: 1) { id name } }` |
| `POST /Users` | `mutation { createUsers(input: { name: "Alice" }) { id name } }` |
| `PUT /Users/1` | `mutation { updateUsers(id: 1, input: { name: "Bob" }) { id name } }` |
| `DELETE /Users/1` | `mutation { deleteUsers(id: 1) }` |

命名規則：

- 型別：PascalCase 表名（`Users`）
- 列表查詢：camelCase 複數（`users`）
- 單筆查詢：camelCase 單數（`user`）
- 條件查詢：`usersByField`
- Mutation：`createUsers`、`updateUsers`、`deleteUsers`

## 範例

### 列出所有資料表

```graphql
query {
  meta {
    tables
  }
}
```

### 查詢全表

```graphql
query {
  users {
    id
    name
  }
}
```

### 依 ID 查詢

```graphql
query {
  user(id: 1) {
    id
    name
  }
}
```

### 新增

```graphql
mutation {
  createUsers(input: { name: "Alice" }) {
    id
    name
  }
}
```

### 更新

```graphql
mutation {
  updateUsers(id: 1, input: { name: "Bob" }) {
    id
    name
  }
}
```

### 刪除

```graphql
mutation {
  deleteUsers(id: 1)
}
```

## 架構

```
GraphQLService/
├── Program.cs
├── Configuration/GraphQlServiceExtensions.cs
├── Schema/
│   ├── DbSchemaRegistry.cs       # 啟動載入 tables / fields
│   ├── DbGraphQlSchemaConfigurator.cs
│   ├── GraphQlTypeMapper.cs
│   └── TableDescriptor.cs
└── Resolvers/DynamicTableResolver.cs
```

## 安全與限制

- 查詢深度上限：15（`AddMaxExecutionDepthRule`）
- 開發環境會回傳詳細例外訊息；正式環境請關閉 `IncludeExceptionDetails`
- 第一版未實作認證，與 WebService 相同
