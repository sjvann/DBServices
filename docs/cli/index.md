# dbsql CLI

`DbServices.Cli` 組件名稱為 `dbsql`，是 SQL 與函數的命令列前端。`\` 指令只在客戶端展開，不會進入 Session 文法。

## 啟動

```bash
dotnet run --project DbServices.Cli -- --provider sqlite --connection "Data Source=app.db"
```

| 選項 | 簡寫 | 說明 |
|------|------|------|
| `--provider` | `-p` | `sqlite`（預設）、`sqlserver`／`mssql`、`mysql`、`oracle`、`postgresql`／`postgres` |
| `--connection` | `-c` | 連線字串（必填） |
| `--query` | `-q` | 單次 SQL 或可被展開的一行（與 `--fn` 互斥） |
| `--fn` | | 單次函數名 |
| `--arg` | | `key=value`，可重複 |
| `--format` | | `table`（預設）、`json`、`csv` |
| `--echo-hidden` | `-E` | 把展開後的 Call／SQL 寫到 stderr |

互動模式：未給 `-q` 也未給 `--fn`。SQL 以分號結束後送出。

## 中介指令

```text
\dt                  列出資料表          → CALL tables
\d [table]           描述欄位            → CALL fields
\call name args      呼叫函數
\fn name --k v       同上
\x                   切換展開列顯示
\h                   說明
\q                   離開
```

```bash
dbsql> \dt
dbsql> \d Users
dbsql> \call get Users 1
dbsql> \fn query --table Users --Name Ada
dbsql> SELECT Id, Name FROM Users WHERE Age > 18;
```

## 單次模式

```bash
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=app.db" -q "SELECT * FROM Users"
dotnet run --project DbServices.Cli -- -p sqlite -c "Data Source=app.db" --fn get --arg table=Users --arg id=1 --format json
```

建置後可直接跑 `dbsql`（輸出在 `DbServices.Cli` 的編譯目錄）。
