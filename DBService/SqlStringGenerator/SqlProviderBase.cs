
using DBService.Models.Interface;
using System.ComponentModel.Design;
using System.Data;

namespace DBService.SqlStringGenerator
{
    public abstract class SqlProviderBase : ISqlProviderBase
    {
        protected string? _tableName;
        protected SqlProviderBase() { }
        protected SqlProviderBase(string? tableName)
        {
            _tableName = tableName;
        }
        public void SetTableName(string tableName)
        {
            _tableName = tableName;
        }
        #region DCL

        public string? GetSqlForCheckTableExist()
        {
            if (_tableName == null) return null;
            return $"SELECT name FROM sqlite_master WHERE type='table' AND name='{_tableName}' ;";

        }
        public string? GetSqlForCheckRows()
        {
            if (_tableName == null) return null;
            return $"SELECT Count(*) FROM {_tableName} ;";
        }
        public abstract string? GetSqlLastInsertId();
        public abstract string? GetSqlForTruncate();


        #endregion
        #region DDL
        public string? GetSqlForCreateTable()
        {
            throw new NotImplementedException();
        }

        public string? GetSqlForDropTable()
        {
            if (_tableName == null) return null;
            return $"Drop Table {_tableName} ;";
        }
        #endregion
        #region DQL
        public string? GetSqlById(object id)
        {
            if (_tableName == null) return null;
            if (id is int)
            {
                return $"SELECT * FROM {_tableName} WHERE Id = {id} ;";
            }
            else if (id is string)
            {
                return $"SELECT * FROM {_tableName} WHERE Id = '{id}' ;";
            }
            return null;
        }
        public string? GetSqlForAll(string? whereStr = null)
        {
            if (_tableName == null) return null;
            return whereStr == null ? $"SELECT * FROM {_tableName}" : $"SELECT * FROM {_tableName} WHERE {whereStr} ;";


        }
        public string? GetSqlForValueSet(string fieldName)
        {
            if (_tableName == null) return null;
            return $"SELECT DISTINCT {fieldName} FROM {_tableName}" ;

        }

        public string? GetSqlForFields(string[] fieldNames, string? whereStr = null)
        {
            if (_tableName == null) return null;
            string fieldsStr = string.Join(',', fieldNames);
            return whereStr == null ? $"SELECT {fieldsStr} FROM {_tableName}" : $"SELECT {fieldsStr} FROM {_tableName} WHERE {whereStr};";

        }
        public string? GetSqlByKeyValue(string key, object value)
        {
            if (_tableName == null) return null;
            var queryString = MapForKeyValue(new KeyValuePair<string, object?>(key, value));
            return $"SELECT * FROM {_tableName} WHERE {queryString} ;";
        }
        public string? GetSqlByKeyValues(IDictionary<string, object?> values)
        {
            if (_tableName == null) return null;
            var queryString = MapForKeyValues(values);
            return $"SELECT * FROM {_tableName} WHERE {queryString} ;";
        }
        public string? GetSqlByKeyValues(IEnumerable<KeyValuePair<string, object?>> values)
        {
            if (_tableName == null) return null;
            string queryString = MapForKeyValues(values);
            return $"SELECT * FROM {_tableName} WHERE {queryString} ;";
        }

        #region One-Many
        public string? GetSqlForInnerJoin(string targetTableName, string sourceKeyName, string targetKeyName)
        {
            if (_tableName == null) return null;
            return $"SELECT * FROM {_tableName} INNER JOIN {targetKeyName} ON {_tableName}.{sourceKeyName} = {targetKeyName}.{targetKeyName} ; ";

        }
        public string? GetSqlForLeftJoin(string targetTableName, string sourceKeyName, string targetKeyName)
        {
            if (_tableName == null) return null;
            return $"SELECT * FROM {_tableName} RIGHT OUTER JOIN {targetKeyName} ON {_tableName}.{sourceKeyName} = {targetKeyName}.{targetKeyName} ; ";

        }
        public string? GetSqlForRightJoin(string targetTableName, string sourceKeyName, string targetKeyName)
        {
            if (_tableName == null) return null;
            return $"SELECT * FROM {_tableName} LEFT OUTER JOIN {targetKeyName} ON {_tableName}.{sourceKeyName} = {targetKeyName}.{targetKeyName} ; ";

        }
        #endregion
        #endregion
        #region DML
        public string? GetSqlForInsert(IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (_tableName == null) return null;
            string fieldStr = string.Join(",", source.Select(x => x.Key));
            string valueStr = MapForInsertValue(source.Select(x => x.Value));
            return $"INSERT INTO {_tableName} ({fieldStr}) VALUES ({valueStr}) ;";

        }
        public string? GetSqlForUpdate(int id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (_tableName == null) return null;
            string setClasue = MapForUpdateValue(source);
            return $"UPDATE {_tableName} SET {setClasue} WHERE id = {id} ;";

        }
        public string? GetSqlForUpdateByKey(KeyValuePair<string, object?> criteria, IEnumerable<KeyValuePair<string, object?>> source)
        {
            if (_tableName == null) return null;
            string setClasue = MapForKeyValues(source);
            string whereStr = MapForKeyValue(criteria);
            return $"UPDATE {_tableName} SET {setClasue} WHERE {whereStr} ;";

        }
        public virtual string? GetSqlForDelete(int id)
        {
            if (_tableName == null) return null;
            return $"DELETE FROM {_tableName} WHERE Id = {id} ;";
        }
        public string? GetSqlForDeleteByKey(KeyValuePair<string, object?> criteria)
        {
            if (_tableName == null) return null;
            string whereStr = MapForKeyValue(criteria);
            return $"DELETE FROM {_tableName} WHERE {whereStr} ;";

        }

        #endregion



        #region Private Methed - Utility
        private static string MapForKeyValue(KeyValuePair<string, object?> source)
        {
            var key = source.Key;
            var value = source.Value;
            if (value is DateTime n1)
            {
                return $"{key} = '{n1:yyyy-MM-dd}'";
            }
            else if (value is int n2)
            {
                return $"{key} = {n2}";
            }
            else if (value is bool b)
            {
                int n3 = b ? 1 : 0;
                return $"{key} = {n3}";
            }
            else if (value is string n4)
            {
                return $"{key} = '{n4}'";
            }
            else
            {
                return string.Empty;
            }
        }
        private static string MapForKeyValues(IEnumerable<KeyValuePair<string, object?>> source)
        {
            List<string> setClause = new();
            foreach (var item in source)
            {
                string map = MapForKeyValue(item);
                if (map != null)
                {
                    setClause.Add(map);
                }
            }
            return string.Join(',', setClause);
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
                        else if(item is string ds)
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
                    else if(item.Value is long)
                    {
                        long? n = (long)item.Value;
                        setClause.Add($"{item.Key} = {n}");
                    }
                    else if(item.Value is string sv)
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


        #endregion
        #region Meta
        public abstract string GetSqlFieldsByName(string tableName);
        public abstract string GetSqlTableNameList(bool includeView = true);
        public abstract string GetSqlForeignKeyList();



        #endregion
    }
}
