using DBService.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DBService.TableService
{
    public class TableSqlForSqlite<T> : ITableSqlService<T> where T : TableBased
    {
        private readonly string? _tableName;
        private readonly IEnumerable<FieldBased>? _fields;

        public TableSqlForSqlite()
        {
            _tableName = TableBased.GetTableName<T>();
            _fields = TableBased.GetFields<T>();

        }


        #region DQL

        public string? GetSqlForAll(string? whereStr = null)
        {
            if (_tableName != null)
            {
                return whereStr == null ? $"SELECT * FROM {_tableName}" : $"SELECT * FROM {_tableName} WHERE {whereStr};";
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
                return whereStr == null ? $"SELECT * FROM {GetTableName()}" : $"SELECT {fieldsStr} FROM {GetTableName()} WHERE {whereStr};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKeyValue(string key, object value)
        {
            var queryString = MapForKeyValue(key, value);

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
        #endregion
        #region DML
        public string? GetSqlForInsert(T source, int? parentId = null)
        {
            var target = MapFiledValueFromSource(source);
            string? fieldStr = target != null ? string.Join(",", target.Keys) : null;
            string? valueStr = MapForInsertValue(target);
            if (_tableName != null && !string.IsNullOrEmpty(fieldStr) && !string.IsNullOrEmpty(valueStr))
            {
                return $"INSERT INTO {_tableName} ({fieldStr}) VALUES ({valueStr});";
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
            var target = MapFiledValueFromSource(source);
            string? setClasue = MapForUpdateValue(target);
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
            var target = MapFiledValueFromSource(source);
            string? setClasue = MapForKeyValues(target);
            string? whereStr = MapForKeyValue(key, value);
            if (_tableName != null && !string.IsNullOrEmpty(whereStr) && !string.IsNullOrEmpty(setClasue))
            {
                return $"UPDATE {_tableName} SET {setClasue} WHERE {whereStr};";
            }
            else
            {
                return null;
            }
        }

        public string? GetSqlForBlukAdd()
        {
            string[] fields = (from f in _fields
                               where f.FieldName != null
                               select f.FieldName).ToArray();
            string? queryStr = GetParameters(fields);
            string fieldsStr = string.Join(',', fields);
            if (_tableName != null && queryStr != null && fieldsStr != null)
            {
                return $"INSERT INTO {_tableName} ({fieldsStr}) VALUES ({queryStr})";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlForBlukAdd(IEnumerable<T> sources)
        {
            throw new NotImplementedException();
        }



        #endregion
        #region DDL
        public string? GetSqlForCreateTable()
        {
            throw new NotImplementedException();
        }

        public string? GetSqlForDropTable()
        {
            string dropSql = $"Drop Table {_tableName};";
            return dropSql;

        }

        #endregion
        #region DCL
        public string? GetSqlForCheckTableExist()
        {
            if (_tableName != null)
            {
                return $"SELECT name FROM sqlite_master WHERE type='table' AND name='{_tableName}' ;";
            }
            else
            { return null; }
        }
        public string[]? GetFieldsName()
        {
            return (from f in _fields
                    where !string.IsNullOrEmpty(f.FieldName)
                    select f.FieldName).ToArray();
        }
        IEnumerable<FieldBased>? ITableSqlService<T>.GetFields()
        {
            return _fields;
        }

        public string? GetSqlLastInsertId()
        {
            return "SELECT last_insert_rowid();";
        }

        public string GetSqlForCheckRows()
        {
            return $"SELECT Count(*) FROM {_tableName};";
        }

        public string GetSqlForTruncate()
        {
            throw new NotImplementedException();
        }

        public string? GetTableName()
        {
            return _tableName;
        }
        #endregion
        #region public Service Method
        public static Dictionary<string, object> MapFiledValueFromSource(T? source)
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
                    return $"{key} = '{n}'";
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
        #endregion
    }
}
