using DBService.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DBService.TableService
{
    public class TableSqlForMsSql<T> : ITableSqlService<T> where T : TableBased
    {
        private readonly string? _tableName;
        private readonly List<string> _fields = new();
        private readonly List<string> _keyfields = new();

        public TableSqlForMsSql()
        {

            TableAttribute? ta = (TableAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(TableAttribute));
            if (ta != null)
            {
                _tableName = ta.Name;
            }
            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                Attribute[]? cas = Attribute.GetCustomAttributes(prop);
                if (cas != null && cas.Any())
                {
                    foreach (Attribute ca in cas)
                    {

                        if (ca is ColumnAttribute temp && temp.Name != null) _fields.Add(temp.Name);
                        if (ca is KeyAttribute)
                        {
                            _keyfields.Add(prop.Name);
                        }
                    }
                }
            }
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
                return whereStr == null ? $"SELECT {fieldsStr} FROM {GetTableName()};" : $"SELECT {fieldsStr} FROM {GetTableName()} WHERE {whereStr};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKey(string key, object value)
        {
            if (_tableName != null)
            {
                return $"SELECT * FROM {_tableName} WHERE {key} = {value};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKeyValuePair(Dictionary<string, object> values)
        {
            if (_tableName != null && values != null && values.Any())
            {
                string? queryString = SetupMapForKeyValue(values);
                return queryString != null ? $"SELECT * FROM {_tableName} WHERE {queryString};" : null;
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
            if (_tableName != null && _fields != null && _fields.Any())
            {
                var target = MapFiledValue(source);
                string? fieldStr = target != null ? string.Join(",", target.Keys) : null;
                string? valueStr = SetupMapForInsertValue(target);
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
            string? whereStr = SetupMapForKeyValue(key, value);
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
            if (_tableName != null && _fields != null && _fields.Any())
            {
                var target = MapFiledValue(source);
                string? setClasue = SetupMapForKeyValue(target);

                return setClasue != null ? $"UPDATE {_tableName} SET {setClasue} WHERE id = {id};" : null;
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlForUpdateByKey(string key, object value, T source)
        {
            string? whereStr = SetupMapForKeyValue(key, value);
            if (_tableName != null && _fields != null && _fields.Any() && whereStr != null)
            {
                var target = MapFiledValue(source);
                string? setClasue = SetupMapForKeyValue(target);

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

        public string[]? GetFields()
        {
            return _fields.ToArray();
        }
        public string? GetSqlLastInsertId()
        {
            return "SELECT @@IDENTITY";
        }

        public string GetSqlForCheckRows()
        {
            throw new NotImplementedException();
        }

        public string GetSqlForTruncate()
        {
            return $"TRUNCATE TABLE {_tableName};";
        }

        public string? GetTableName()
        {
            return _tableName;
        }
        #endregion
        #region Private Method
        private Dictionary<string, object> MapFiledValue(T? source)
        {
            if (source == null) return new Dictionary<string, object>();
            Type s = typeof(T);
            Dictionary<string, object> map = new();
            if (_fields != null && _fields.Any())
            {
                foreach (var item in _fields)
                {
                    if (item != null && !_keyfields.Contains(item))
                    {
                        PropertyInfo? f = s.GetProperty(item);

                        object? o = f?.GetValue(source) ?? null;
                        if (o != null)
                        {
                            map.Add(item, o);
                        }
                    }

                }
            }
            return map;
        }


        private static string? SetupMapForInsertValue(Dictionary<string, object>? source)
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
                            sb.Add($"N'{newOne}'");
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
        private static string? SetupMapForKeyValue(Dictionary<string, object>? source)
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
                            setClause.Add($"{item.Key} = N'{n}'");
                        }
                    }

                }
                return string.Join(',', setClause);
            }
            else
            { return null; }
        }

        private static string? SetupMapForKeyValue(string key, object value)
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
                else
                {
                    string n = (string)value;
                    return $"{key} = '{n}'";
                }
            }
            return null;

        }



        #endregion

    }
}
