using System.ComponentModel.DataAnnotations;

namespace DBService.Models
{
    public class DbModelBased<T> : IDbModel<T>, IBulkInsert where T : class
    {
        [Key]
        public int Id { get; set; }

        public string GetSqlById(int id)
        {
            return $"SELECT * FROM {GetTableName()} WHERE Id = {id}";
        }
        public string GetSqlForAdd(DbModelBased source, int? parentId = null)
        {
            Dictionary<string, object> t = SetupMap(source);
            if (parentId.HasValue)
            {
                t.Add("ParentId", parentId.Value);
            }
            string _fields = string.Join(',', t.Keys);
            string _values = SetupMapForInsertValue(t);


            return $"INSERT INTO {GetTableName()} ({_fields}) VALUES ({_values}); SELECT last_insert_rowid()";
        }
        public string GetSqlForAll(string? query = null)
        {
            return query == null ? $"SELECT DISTINCT * FROM {GetTableName()}" : $"SELECT * FROM {GetTableName()} WHERE {query}";
        }
        public string GetSqlForBlukAdd()
        {
            return $"INSERT INTO {GetTableName()} ({GetSelectFields()}) VALUES ({GetParameters()})";
        }

        public string GetSqlForCheckRows()
        {
            return $"SELECT count(*) FROM {GetTableName()}";
        }
        public string GetSqlForDelete(int id)
        {
            return $"DELETE FROM {GetTableName()} WHERE Id = {id}";
        }
        public string GetSqlForTruncate()
        {
            return $"DELETE FROM {GetTableName()}; VACUUM";
        }
        public string GetSqlForUpdate(DbModelBased source)
        {
            Dictionary<string, object> s = SetupMap(source);
            string setClasue = SetupMapForUpdateValue(s);

            return $"UPDATE {GetTableName()} SET {setClasue} WHERE id = {source.Id}";
        }
        public string GetSqlSpecificFields(string[] fields, string? query = null)
        {
            string _fields = string.Join(',', fields);
            return query == null ? $"SELECT DISTINCT {_fields} FROM {GetTableName()}" : $"SELECT DISTINCT {_fields} FROM {GetTableName()} WHERE {query}";
        }
        protected static string SetupMapForInsertValue(Dictionary<string, object> s)
        {
            List<string> sb = new();
            if (s != null && s.Count > 0)
            {
                foreach (var item in s.Values)
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

            }
            return string.Join(',', sb);
        }

        protected static string SetupMapForUpdateValue(Dictionary<string, object> s)
        {

            List<string> setClause = new();
            foreach (var item in s)
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

        public string GetSelectFields()
        {
            return string.Join(",", GetFields());
        }

        public string GetParameters()
        {
            var p = from a in GetFields()
                    select "@" + a;
            return string.Join(",", p);
        }
    }
}
