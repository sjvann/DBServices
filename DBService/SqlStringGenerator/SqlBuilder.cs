using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
 * 
 * 仍在測試開發中 * 
 * 
 */
namespace DBServices.SqlStringGenerator
{
    public static class SqlBuilder
    {

        public static DataBaseService SelectTable(this DataBaseService db, string tableName)
        {   
            db.SetCurrentTableName(tableName);
           return db;
        }
        public static DataBaseService SelectField(this DataBaseService db, string[] FieldNameList)
        {
            throw new NotImplementedException();
        }


        public static string GetInsertSql(string tableName, List<string> columns)
        {
            var sql = new StringBuilder();
            sql.Append($"INSERT INTO {tableName} (");
            sql.Append(string.Join(", ", columns));
            sql.Append(") VALUES (");
            sql.Append(string.Join(", ", columns.Select(c => $"@{c}")));
            sql.Append(")");
            return sql.ToString();
        }

        public static string GetUpdateSql(string tableName, List<string> columns, string keyColumn)
        {
            var sql = new StringBuilder();
            sql.Append($"UPDATE {tableName} SET ");
            sql.Append(string.Join(", ", columns.Select(c => $"{c} = @{c}")));
            sql.Append($" WHERE {keyColumn} = @{keyColumn}");
            return sql.ToString();
        }

        public static string GetDeleteSql(string tableName, string keyColumn)
        {
            return $"DELETE FROM {tableName} WHERE {keyColumn} = @{keyColumn}";
        }

        public static string GetSelectSql(string tableName, string keyColumn)
        {
            return $"SELECT * FROM {tableName} WHERE {keyColumn} = @{keyColumn}";
        }

        public static string GetSelectAllSql(string tableName)
        {
            return $"SELECT * FROM {tableName}";
        }   
    }
}
