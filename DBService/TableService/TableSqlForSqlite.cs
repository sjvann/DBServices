﻿using DBService.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.TableService
{
    public class TableSqlForSqlite<T> : ITableSqlService<T> where T : TableBased
    {
        private readonly string? _tableName;
        private readonly string[]? _fields;
        private readonly IEnumerable<FieldBased>? _fieldsWithDataType;
        public TableSqlForSqlite()
        {

            TableAttribute? ta = (TableAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(TableAttribute));
            if (ta != null)
            {
                _tableName = ta.Name;
            }

            ColumnAttribute[] ca = (ColumnAttribute[])Attribute.GetCustomAttributes(typeof(T), typeof(ColumnAttribute));
            if (ca != null && ca.Any())
            {
                _fields = ca.Select(x => x.Name ?? string.Empty).ToArray();
                List<FieldBased> target = new();
                foreach (var item in ca)
                {
                    target.Add(new FieldBased()
                    {
                        FieldName = item.Name,
                        FieldType = item.TypeName
                    });
                }
                _fieldsWithDataType = target;
            }

        }


        #region DQL

        public string? GetSqlForAll(string? whereStr = null)
        {
            if (_tableName != null)
            {
                return whereStr == null ? $"SELECT DISTINCT * FROM {_tableName}" : $"SELECT * FROM {_tableName} WHERE {whereStr};";
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
                return whereStr == null ? $"SELECT DISTINCT {_fields} FROM {GetTableName()}" : $"SELECT DISTINCT {fieldsStr} FROM {GetTableName()} WHERE {whereStr};";
            }
            else
            {
                return null;
            }
        }
        public string? GetSqlByKey(string key, object value)
        {
            if (_tableName != null && _fields != null && _fields.Contains(key))
            {
                return $"SELECT * FROM {_tableName} WHERE {key} = {value};";
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
                return valueStr != null ? $"INSERT INTO {_tableName} ({fieldStr}) VALUES ({valueStr}); SELECT last_insert_rowid();" : null;
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
        public string? GetSqlForUpdate(int id, T source)
        {
            if (_tableName != null && _fields != null && _fields.Any())
            {
                var target = MapFiledValue(source);
                string? setClasue = SetupMapForUpdateValue(target);

                return setClasue != null ? $"UPDATE {_tableName} SET {setClasue} WHERE id = {id};" : null;
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
                return $"INSERT INTO {_tableName} ({fieldsStr}) VALUES ({GetParameters()})";
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
        public string GetParameters()
        {
            var p = from a in _fields
                    select "@" + a;
            return string.Join(",", p);
        }

        public string[]? GetFields()
        {
            return _fields;
        }

        public string GetSqlForCheckRows()
        {
            throw new NotImplementedException();
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
                    object? value = s.GetField(item)?.GetValue(source);
                    if (value != null)
                    {
                        map.Add(item, value);
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
        private static string? SetupMapForUpdateValue(Dictionary<string, object>? source)
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

        public string? GetSqlByKeyValuePair(Dictionary<string, object> values)
        {
            throw new NotImplementedException();
        }

        public string? GetSqlForDeleteByKey(string key, int id)
        {
            throw new NotImplementedException();
        }

        public string? GetSqlForUpdateByKey(string key, int id, T source)
        {
            throw new NotImplementedException();
        }


        #endregion

    }
}