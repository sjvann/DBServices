using DbServices.Core.Models;
using DbServices.Core.Models.Enum;
using DbServices.Core.Models.Interface;
using System.Data;

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
        public abstract string? GetSqlForAlterTable(TableBaseModel dbModel);
        public abstract string? GetSqlForDropTable(string tableName);

        #endregion
        #region DQL
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
        public string? GetSqlByKeyValue(string tableName, string key, object value, EnumQueryOperator? operators = null)
        {
            if (tableName == null) return null;
            var queryString = MapForKeyValue(new KeyValuePair<string, object?>(key, value), operators);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
        }
        public string? GetSqlByKeyValues(string tableName, IDictionary<string, object?> values)
        {
            if (tableName == null) return null;
            var queryString = MapForQueryKeyValues(values);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
        }
        public string? GetSqlByKeyValues(string tableName, IEnumerable<KeyValuePair<string, object?>> values)
        {
            if (tableName == null) return null;
            string queryString = MapForQueryKeyValues(values);
            return $"SELECT * FROM {tableName} WHERE {queryString};";
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
        public string? GetSqlForInsert(string tableName, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;

            string fieldStr = string.Join(",", source.Select(x => x.Key));
            string valueStr = MapForInsertValue(source.Select(x => x.Value));
            return $"INSERT INTO {tableName} ({fieldStr}) VALUES ({valueStr}) ;";

        }
        public string? GetSqlForUpdate(string tableName, long id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;
            string setClause = MapForUpdateValue(source);
            return $"UPDATE {tableName} SET {setClause} WHERE id = {id} ;";

        }
        public string? GetSqlForUpdateByKey(string tableName, KeyValuePair<string, object?> criteria, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (tableName == null) return null;
            string setClause = MapForKeyValues(source);
            string whereStr = MapForKeyValue(criteria);
            return $"UPDATE {tableName} SET {setClause} WHERE {whereStr} ;";

        }
        public virtual string? GetSqlForDelete(string tableName, long id)
        {
            if (tableName == null) return null;
            return $"DELETE FROM {tableName} WHERE Id = {id} ;";
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
