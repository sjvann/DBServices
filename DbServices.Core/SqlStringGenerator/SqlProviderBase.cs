using DbServices.Core.Extensions;
using DbServices.Core.Models;
using DbServices.Core.Models.Enum;
using DbServices.Core.Models.Interface;
using System.Data;
using System.Globalization;
using System.Text;

namespace DbServices.Core.SqlStringGenerator
{
    public abstract class SqlProviderBase : ISqlProviderBase
    {
        protected SqlProviderBase() { }

        #region Abstract 
        public abstract string? GetSqlLastInsertId(string tableName);
        public abstract string? GetSqlForTruncate(string tableName);
        public abstract string? GetSqlForCheckTableExist(string tableName);
        public abstract string? GetSqlTableNameList(bool includeView = true);

        public abstract string? ConvertDataTypeToDb(string dataType);
        public abstract string? GetSqlFieldsByTableName(string tableName);
        public abstract string? GetSqlForeignInfoByTableName(string tableName);
        public abstract IEnumerable<FieldBaseModel> MapToFieldBaseModel(IEnumerable<dynamic> target, IEnumerable<dynamic> foreignness);
        public abstract IEnumerable<ForeignBaseModel> MapToForeignBaseModel(IEnumerable<dynamic> target);
        public abstract IEnumerable<RecordBaseModel> MapToRecordBaseModel(IEnumerable<dynamic> target);

        #endregion

        #region DCL
        public string? GetSqlForCheckRows(string tableName)
        {
            if (tableName == null) return null;
            return $"SELECT Count(*) FROM {tableName};";
        }
        #endregion
        #region DDL
        public abstract string? GetSqlForCreateTable(TableBaseModel dbModel);
        public abstract string? GetSqlForCreateTable(string tableName, IEnumerable<FieldBaseModel> tableDefine);
        public abstract string? GetSqlForAlterTable(TableBaseModel dbModel);
        public abstract string? GetSqlForDropTable(string tableName);

        #endregion
        #region DQL
        /// <summary>
        /// 取得根據 ID 查詢的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <returns>SQL 語句</returns>
        public string? GetSqlById(string tableName, object id)
        {
            if (tableName == null) return null;
            if (id is long || id is int)
            {
                return $"SELECT * FROM {tableName} WHERE Id = {id};";
            }
            else if (id is string)
            {
                return $"SELECT * FROM {tableName} WHERE Id = '{id}';";
            }
            return null;
        }

        /// <summary>
        /// 取得根據 ID 查詢的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlById(string tableName, object id)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            
            var sql = $"SELECT * FROM {tableName} WHERE Id = @Id;";
            var parameters = new { Id = id };
            
            return new ParameterizedSqlResult(sql, parameters);
        }
        public string? GetSqlForAll(string tableName, string? whereStr = null)
        {
            if (tableName == null) return null;
            return string.IsNullOrEmpty(whereStr) ? $"SELECT * FROM {tableName};" : $"SELECT * FROM {tableName} WHERE {whereStr};";


        }
        public string? GetSqlForValueSet(string tableName, string fieldName)
        {
            if (tableName == null) return null;
            return $"SELECT DISTINCT {fieldName} FROM {tableName};";

        }

        public string? GetSqlForFields(string tableName, string[] fieldNames, string? whereStr = null)
        {
            if (tableName == null) return null;
            string fieldsStr = string.Join(',', fieldNames);
            return whereStr == null ? $"SELECT {fieldsStr} FROM {tableName}" : $"SELECT {fieldsStr} FROM {tableName} WHERE {whereStr};";

        }
        /// <summary>
        /// 取得根據單一鍵值查詢的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="key">欄位名稱</param>
        /// <param name="value">欄位值</param>
        /// <param name="operators">查詢運算子</param>
        /// <returns>SQL 語句</returns>
        public string? GetSqlByKeyValue(string tableName, string key, object value, EnumQueryOperator? operators = null)
        {
            if (tableName == null) return null;
            var queryString = MapForKeyValue(new KeyValuePair<string, object?>(key, value), operators);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
        }

        /// <summary>
        /// 取得根據單一鍵值查詢的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="key">欄位名稱</param>
        /// <param name="value">欄位值</param>
        /// <param name="operators">查詢運算子（預設為等於）</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlByKeyValue(string tableName, string key, object? value, EnumQueryOperator? operators = null)
        {
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return null;

            var op = CheckOperation(operators);
            var parameterName = $"@{key}";
            var sql = $"SELECT * FROM {tableName} WHERE {key} {op} {parameterName};";
            var parameters = new Dictionary<string, object?> { [key] = value };

            return new ParameterizedSqlResult(sql, parameters);
        }
        public string? GetSqlByKeyValues(string tableName, IDictionary<string, object?> values)
        {
            if (tableName == null) return null;
            var queryString = MapForQueryKeyValues(values);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
        }
        /// <summary>
        /// 取得根據多個鍵值查詢的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="values">查詢條件（多個鍵值對）</param>
        /// <returns>SQL 語句</returns>
        public string? GetSqlByKeyValues(string tableName, IEnumerable<KeyValuePair<string, object?>> values)
        {
            if (tableName == null) return null;
            string queryString = MapForQueryKeyValues(values);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
        }

        /// <summary>
        /// 取得根據多個鍵值查詢的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="values">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlByKeyValues(string tableName, IEnumerable<KeyValuePair<string, object?>> values)
        {
            if (string.IsNullOrEmpty(tableName) || values == null || !values.Any()) return null;

            var conditions = new List<string>();
            var parametersDict = new Dictionary<string, object?>();

            foreach (var kvp in values)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                
                var paramName = $"@{kvp.Key}";
                conditions.Add($"{kvp.Key} = {paramName}");
                parametersDict[kvp.Key] = kvp.Value;
            }

            if (!conditions.Any()) return null;

            var whereClause = string.Join(" AND ", conditions);
            var sql = $"SELECT * FROM {tableName} WHERE {whereClause};";

            return new ParameterizedSqlResult(sql, parametersDict);
        }
        public string? GetSqlByWhereIn(string tableName, string queryField, object[] queryValue)
        {
            if (string.IsNullOrEmpty(tableName)) return null;


            string newQueryInString = string.Empty;

            if (queryValue[0].IsNumber())
            {
                newQueryInString = string.Join(',', queryValue);
            }
            else
            {
                string[]? qArray = (from item in queryValue
                                    select $"'{(string)item}'").ToArray();
                newQueryInString = string.Join(',', qArray);

            }
            return $"SELECT * FROM {tableName} WHERE {queryField} IN ({newQueryInString});";
        }
        public string? GetSqlByWhere(string tableName, string whereString)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            return $"SELECT * FROM {tableName} WHERE {whereString};";
        }

        public string? GetSqlByWhereBetween(string tableName, string queryField, object startQueryValue, object endQueryValue)
        {

            if (string.IsNullOrEmpty(tableName)) return null;

            if (startQueryValue.IsNumber() || endQueryValue.IsNumber())
            {
                return $"SELECT * FROM {tableName} WHERE  {queryField} BETWEEN {startQueryValue} AND {endQueryValue};";
            }
            else
            {
                return $"SELECT * FROM {tableName} WHERE  {queryField} BETWEEN '{startQueryValue}' AND '{endQueryValue}';";
            }

        }

        #region One-Many

        public string? GetSqlForInnerJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName)
        {
            if (string.IsNullOrEmpty(manySideTableName) || string.IsNullOrEmpty(oneSideTableName)) return null;
            return $"SELECT * FROM {manySideTableName} INNER JOIN {oneSideTableName} ON {manySideTableName}.{manySideKeyName} = {oneSideTableName}.{oneSideKeyName}; ";

        }
        public string? GetSqlForLeftJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName)
        {
            if (string.IsNullOrEmpty(manySideTableName) || string.IsNullOrEmpty(oneSideTableName)) return null;
            return $"SELECT * FROM {manySideTableName} RIGHT OUTER JOIN  {oneSideTableName} ON {manySideTableName}.{manySideKeyName} = {oneSideTableName}.{oneSideKeyName}; ";


        }
        public string? GetSqlForRightJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName)
        {
            if (string.IsNullOrEmpty(manySideTableName) || string.IsNullOrEmpty(oneSideTableName)) return null;
            return $"SELECT * FROM {manySideTableName} LEFT OUTER JOIN  {oneSideTableName} ON {manySideTableName}.{manySideKeyName} = {oneSideTableName}.{oneSideKeyName}; ";
        }
        #endregion
        #endregion
        #region DML
        /// <summary>
        /// 取得插入記錄的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="source">要插入的欄位和值</param>
        /// <returns>SQL 語句</returns>
        public string? GetSqlForInsert(string tableName, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;

            string fieldStr = string.Join(",", source.Select(x => x.Key));
            string valueStr = MapForInsertValue(source.Select(x => x.Value));
            return $"INSERT INTO {tableName} ({fieldStr}) VALUES ({valueStr}) ;";

        }

        /// <summary>
        /// 取得插入記錄的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="source">要插入的欄位和值</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlForInsert(string tableName, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (string.IsNullOrEmpty(tableName) || source == null || !source.Any()) return null;

            var fields = source.Select(x => x.Key).ToList();
            var fieldStr = string.Join(", ", fields);
            var parameterNames = fields.Select(f => $"@{f}").ToList();
            var valueStr = string.Join(", ", parameterNames);

            var sql = $"INSERT INTO {tableName} ({fieldStr}) VALUES ({valueStr});";
            
            // 建立參數物件
            var parametersDict = new Dictionary<string, object?>();
            foreach (var item in source)
            {
                parametersDict[item.Key] = item.Value;
            }
            var parameters = parametersDict;

            return new ParameterizedSqlResult(sql, parameters);
        }

        public string? GetSqlForBulkInsert(string tableName, IEnumerable<IEnumerable<KeyValuePair<string, object?>>> source)
        {
            StringBuilder sql = new();
            sql.Append($"INSERT INTO {tableName} (");
            sql.Append(string.Join(',', source.First().Select(x => x.Key)));
            sql.Append(") VALUES ");
            foreach (var item in source)
            {
                sql.Append('(');
                sql.Append(string.Join(',', item.Select(x => x.Value)));
                sql.Append(')');
                if (!source.Last().Equals(item))
                {
                    sql.Append(',');
                }
            }
            return sql.ToString();
        }


        /// <summary>
        /// 取得根據 ID 更新記錄的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <param name="source">要更新的欄位和值</param>
        /// <returns>SQL 語句</returns>
        public string? GetSqlForUpdate(string tableName, long id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;
            string setClause = MapForUpdateValue(source);
            return $"UPDATE {tableName} SET {setClause} WHERE id = {id} ;";

        }

        /// <summary>
        /// 取得根據 ID 更新記錄的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <param name="source">要更新的欄位和值</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlForUpdate(string tableName, long id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (string.IsNullOrEmpty(tableName) || source == null || !source.Any()) return null;

            var setClauses = new List<string>();
            var parametersDict = new Dictionary<string, object?> { ["Id"] = id };

            foreach (var item in source)
            {
                if (string.IsNullOrEmpty(item.Key)) continue;
                var paramName = $"@{item.Key}";
                setClauses.Add($"{item.Key} = {paramName}");
                parametersDict[item.Key] = item.Value;
            }

            if (!setClauses.Any()) return null;

            var setClause = string.Join(", ", setClauses);
            var sql = $"UPDATE {tableName} SET {setClause} WHERE Id = @Id;";

            return new ParameterizedSqlResult(sql, parametersDict);
        }
        public string? GetSqlForUpdateByKey(string tableName, KeyValuePair<string, object?> criteria, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;
            string setClause = MapForKeyValues(source);
            string whereStr = MapForKeyValue(criteria);
            return $"UPDATE {tableName} SET {setClause} WHERE {whereStr} ;";

        }
        /// <summary>
        /// 取得根據 ID 刪除記錄的 SQL 語句（字串拼接方式，不建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <returns>SQL 語句</returns>
        public virtual string? GetSqlForDelete(string tableName, long id)
        {
            if (tableName == null) return null;
            return $"DELETE FROM {tableName} WHERE Id = {id} ;";
        }

        /// <summary>
        /// 取得根據 ID 刪除記錄的參數化 SQL 語句（建議使用）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="id">記錄 ID</param>
        /// <returns>參數化 SQL 結果</returns>
        public virtual ParameterizedSqlResult? GetParameterizedSqlForDelete(string tableName, long id)
        {
            if (string.IsNullOrEmpty(tableName)) return null;

            var sql = $"DELETE FROM {tableName} WHERE Id = @Id;";
            var parameters = new { Id = id };

            return new ParameterizedSqlResult(sql, parameters);
        }
        public string? GetSqlForDeleteByKey(string tableName, KeyValuePair<string, object?> criteria)
        {
            if (tableName == null) return null;
            string whereStr = MapForKeyValue(criteria);
            return $"DELETE FROM {tableName} WHERE {whereStr} ;";

        }

        #endregion

        #region Private Methed - Utility
        private static string MapForKeyValue(KeyValuePair<string, object?> source, EnumQueryOperator? @operator = null)
        {
            var key = source.Key;
            var value = source.Value;
            string op = CheckOperation(@operator);
            if (value is DateTime n1)
            {
                return $"{key} {op} '{n1:yyyy-MM-dd}'";
            }
            else if (value is int n2)
            {
                return $"{key} {op} {n2}";
            }
            else if (value is long n5)
            {
                return $"{key} {op} {n5}";
            }

            else if (value is bool b)
            {
                int n3 = b ? 1 : 0;
                return $"{key} {op} {n3}";
            }
            else if (value is string n4)
            {
                return $"{key} {op} '{n4}'";
            }
            else
            {
                return string.Empty;
            }
        }

        private static string MapForKeyValues(IEnumerable<KeyValuePair<string, object?>> source)
        {
            List<string> setClause = [];
            foreach (var item in source)
            {
                if (item.Value != null)
                {
                    string map = MapForKeyValue(item);
                    if (map != null)
                    {
                        setClause.Add(map);
                    }
                }
            }
            return string.Join(',', setClause);
        }
        private static string MapForQueryKeyValues(IEnumerable<KeyValuePair<string, object?>> source)
        {
            List<string> setClause = [];
            foreach (var item in source)
            {
                if (item.Value != null)
                {
                    string map = MapForKeyValue(item);
                    if (map != null)
                    {
                        setClause.Add(map);
                    }
                }
            }
            return string.Join(" AND ", setClause);
        }
        private static string MapForInsertValue(IEnumerable<object?> source)
        {
            if (source != null && source.Any())
            {
                List<string> sb = new();
                foreach (var item in source)
                {

                    if (item != null)
                    {
                        if (item is DateTime)
                        {
                            DateTime n = (DateTime)item;
                            sb.Add($"'{n:yyyy-MM-dd}'");
                        }
                        else if (item is int)
                        {
                            int n = (int)item;
                            sb.Add($"{n}");
                        }
                        else if (item is bool b)
                        {
                            int n = b ? 1 : 0;
                            sb.Add($"{n}");
                        }
                        else if (item is decimal de)
                        {
                            sb.Add($"{de}");
                        }
                        else if (item is double dd)
                        {
                            sb.Add($"{dd}");
                        }
                        else if (item is string ds)
                        {

                            sb.Add($"'{ds}'");
                        }
                        else
                        {
                            sb.Add($"'{item.ToString()}'");
                        }
                    }
                    else
                    {
                        sb.Add("''");
                    }

                }
                return string.Join(',', sb);
            }
            else
            {
                return string.Empty;
            }

        }
        public static string MapForUpdateValue(IEnumerable<KeyValuePair<string, object?>> source)
        {

            List<string> setClause = new();
            foreach (var item in source)
            {
                if (item.Value != null)
                {
                    if (item.Value is DateTime)
                    {
                        DateTime n = (DateTime)item.Value;
                        setClause.Add($"{item.Key} = '{n:yyyy-MM-dd}'");
                    }
                    else if (item.Value is int)
                    {
                        int n = (int)item.Value;
                        setClause.Add($"{item.Key} = {n}");
                    }
                    else if (item.Value is bool b)
                    {
                        int n = b ? 1 : 0;
                        setClause.Add($"{item.Key} = {n}");
                    }
                    else if (item.Value is long)
                    {
                        long? n = (long)item.Value;
                        setClause.Add($"{item.Key} = {n}");
                    }
                    else if (item.Value is string sv)
                    {
                        setClause.Add($"{item.Key} = '{sv}'");
                    }
                    else
                    {
                        string? n = item.Value.ToString();
                        setClause.Add($"{item.Key} = '{n}'");
                    }
                }

            }
            return string.Join(',', setClause);

        }


        private static string CheckOperation(EnumQueryOperator? @operator)
        {
            return @operator switch
            {
                EnumQueryOperator.Equal => "=",
                EnumQueryOperator.NotEqual => "<>",
                EnumQueryOperator.GreaterThan => ">",
                EnumQueryOperator.GreaterThanOrEqual => ">=",
                EnumQueryOperator.LessThan => "<",
                EnumQueryOperator.LessThanOrEqual => "<=",
                EnumQueryOperator.Like => "LIKE",
                EnumQueryOperator.NotLike => "NOT LIKE",
                _ => "=",
            };
        }






        #endregion
        #region Meta

        #endregion
    }
}
