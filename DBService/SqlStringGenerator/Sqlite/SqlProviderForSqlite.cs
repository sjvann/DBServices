using DBService.Models;

namespace DBService.SqlStringGenerator.Sqlite
{
    public class SqlProviderForSqlite : SqlProviderBase
    {
        public SqlProviderForSqlite() { }
        public SqlProviderForSqlite(string? tableName) : base(tableName) { }



        public override string GetSqlTableNameList(bool includeView = true)
        {
            string whereStr = includeView ? $"type in ('table', 'view')" : $"type = 'table'";
            return $"SELECT name FROM pragma_table_list WHERE {whereStr} AND name NOT LIKE 'sqlite_%'";
        }
        public override string GetSqlFieldsByName(string tableName)
        {
            return $"SELECT * FROM pragma_table_info('{tableName}')";
        }

        public override string? GetSqlLastInsertId()
        {
            return "SELECT last_insert_rowid();";
        }

        public override string? GetSqlForTruncate()
        {
            return $"DELETE FROM {_tableName}; VACUUM;";
        }

        public override string GetSqlForeignKeyList()
        {
            return $"SELECT * FROM pragma_Foreign_key_list('{_tableName}')";
        }
    }
}
