using DBService.Models;
using System.Reflection;

namespace DBService.TableService
{
    public class TableSqlForMsSql<T> : ITableSqlService<T> where T : TableBased
    {
        private readonly string? _tableName;
        private readonly IEnumerable<FieldBased>? _fields;
        public TableSqlForMsSql()
        {
            _tableName = TableBased.GetTableName<T>();
            _fields = TableBased.GetFields<T>();
        }

        #region DQL
        public string? GetSqlForAll(string? whereStr = null)
        {
            if (_tableName != null)
            {

                return whereStr == null ? $"SELECT * FROM {_tableName};" : $"SELECT * FROM {_tableName} WHERE {whereStr};";
            }
            else
            {
                return null;
            }

        }
        public string? GetSqlForFields(string[] fields, string? whereStr = null)
        {
            if (_tableName != null && fields != null && fields.Any())
            {
                string fieldsStr = string.Join(',', fields);
                return whereStr == null ? $"SELECT {fieldsStr} FROM {_tableName};" : $"SELECT {fieldsStr} FROM {_tableName} WHERE {whereStr};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKeyValue(string key, object value)
        {
            string? keyvalue = MapForKeyValue(key, value);
            if (_tableName != null && keyvalue != null)
            {
                return $"SELECT * FROM {_tableName} WHERE {keyvalue};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKeyValues(Dictionary<string, object> values)
        {
            string? queryString = MapForKeyValues(values);
            if (_tableName != null && !string.IsNullOrEmpty(queryString))
            {

                return $"SELECT * FROM {_tableName} WHERE {queryString};";
            }
            else
            {
                return null;
            }

        }


        public string? GetSqlById(int id)
        {
            if (_tableName != null)
            {
                return $"SELECT * FROM {_tableName} WHERE Id = {id};";
            }
            else
            {
                return null;
            }

        }
        #endregion
        #region DML
        public string? GetSqlForInsert(T source, int? parentId = null)
        {
            var target = MapFieldValueFromSource(source);
            if (_tableName != null && _fields != null && _fields.Any())
            {

                string? fieldStr = target != null ? string.Join(",", target.Keys) : null;
                string? valueStr = MapForInsertValue(target);
                return valueStr != null ? $"INSERT INTO {_tableName} ({fieldStr}) VALUES ({valueStr}); " : null;
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlForDelete(int id)
        {
            if (_tableName != null)
            {
                return $"DELETE FROM {_tableName} WHERE Id = {id};";
            }
            else
            {
                return null;
            }

        }
        public string? GetSqlForDeleteByKey(string key, object value)
        {
            string? whereStr = MapForKeyValue(key, value);
            if (_tableName != null && whereStr != null)
            {
                return $"DELETE FROM {_tableName} WHERE {whereStr};";
            }
            else
            {
                return null;
            }

        }
        public string? GetSqlForUpdate(int id, T source)
        {
            var target = MapFieldValueFromSource(source);
            string? setClasue = MapForKeyValues(target);
            if (_tableName != null && !string.IsNullOrEmpty(setClasue))
            {
                return $"UPDATE {_tableName} SET {setClasue} WHERE id = {id};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlForUpdateByKey(string key, object value, T source)
        {
            string? whereStr = MapForKeyValue(key, value);
            if (_tableName != null && _fields != null && _fields.Any() && whereStr != null)
            {
                var target = MapFieldValueFromSource(source);
                string? setClasue = MapForKeyValues(target);

                return setClasue != null ? $"UPDATE {_tableName} SET {setClasue} WHERE {whereStr};" : null;
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlForBlukAdd()
        {
            if (_tableName != null && _fields != null && _fields.Any())
            {
                string fieldsStr = string.Join(',', _fields);
                return $"INSERT INTO {_tableName} ({fieldsStr}) VALUES ({GetParameters()});";
            }
            else
            {
                return null;
            }
        }

        #endregion
        #region DDL
        public string? GetSqlForCreateTable()
        {
            throw new NotImplementedException();
        }

        public string? GetSqlForDropTable()
        {
            string dropSql = $"Drop Table IF Exists {_tableName};";
            return dropSql;

        }

        #endregion
        #region DCL
        public string? GetSqlForCheckTableExist()
        {
            if (_tableName != null)
            {
                return $"SELECT oject_is FROM sys.table WHERE name = '{_tableName}';";
            }
            else
            { return null; }
        }
        public string GetParameters()
        {
            var p = from a in _fields
                    select "@" + a;
            return string.Join(",", p);
        }


        public string? GetSqlLastInsertId()
        {
            return "SELECT @@IDENTITY";
        }

        public string GetSqlForCheckRows()
        {
             return $"SELECT Count(*) FROM {_tableName};";
        }

        public string GetSqlForTruncate()
        {
            return $"TRUNCATE TABLE {_tableName};";
        }

        #endregion
        #region public Service Method
        public static Dictionary<string, object> MapFieldValueFromSource(T? source)
        {
            if (source == null) return new Dictionary<string, object>();
            Type s = typeof(T);
            Dictionary<string, object> map = new();
            var fields = TableBased.GetFields<T>();
            if (fields != null && fields.Any())
            {
                foreach (var item in fields)
                {
                    if (item != null && !string.IsNullOrEmpty(item.FieldName))
                    {
                        PropertyInfo? f = s.GetProperty(item.FieldName);
                        object? o = f?.GetValue(source) ?? null;
                        if (o != null)
                        {
                            map.Add(item.FieldName, o);
                        }
                    }
                }
            }
            return map;
        }
        public static string? MapForInsertValue(Dictionary<string, object>? source)
        {

            if (source != null && source.Any())
            {
                List<string> sb = new();
                foreach (var item in source.Values)
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
                        else
                        {
                            string n = (string)item;
                            string newOne = n.Replace("'", "''");
                            sb.Add($"'{newOne}'");
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
                return null;
            }

        }
        public static string? MapForUpdateValue(Dictionary<string, object>? source)
        {
            if (source != null && source.Any())
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
                        else
                        {
                            string n = (string)item.Value;
                            setClause.Add($"{item.Key} = '{n}'");
                        }
                    }

                }
                return string.Join(',', setClause);
            }
            else
            { return null; }
        }

        public static string? MapForKeyValues(Dictionary<string, object>? source)
        {
            if (source != null && source.Any())
            {
                List<string> setClause = new();
                foreach (var item in source)
                {
                    string? map = MapForKeyValue(item.Key, item.Value);
                    if (map != null)
                    {
                        setClause.Add(map);
                    }
                }
                return string.Join(',', setClause);
            }
            else
            { return null; }
        }
        public static string? MapForKeyValue(string? key, object? value)
        {
            if (value != null)
            {
                if (value is DateTime)
                {
                    DateTime n = (DateTime)value;
                    return $"{key} = '{n:yyyy-MM-dd}'";
                }
                else if (value is int)
                {
                    int n = (int)value;
                    return $"{key} = {n}";
                }
                else if (value is bool b)
                {
                    int n = b ? 1 : 0;
                    return $"{key} = {n}";
                }
                else if (value is string n)
                {
                    return $"{key} = N'{n}'";
                }
            }
            return null;

        }
        public string? GetParameters(string[] fields)
        {
            var p = from a in fields
                    select "@" + a;
            return string.Join(",", p);
        }

        public string? GetSqlForBlukAdd(IEnumerable<T> sources)
        {
            throw new NotImplementedException();
        }

        public string? GetTableName()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<FieldBased>? GetFields()
        {
            throw new NotImplementedException();
        }

        public string[]? GetFieldsName()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
