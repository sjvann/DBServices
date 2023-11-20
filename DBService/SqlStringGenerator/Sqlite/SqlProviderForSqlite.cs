using DBService.Models;
using System.Text;

namespace DBService.SqlStringGenerator.Sqlite
{
    public class SqlProviderForSqlite : SqlProviderBase
    {
        public SqlProviderForSqlite() { }


        public override string? GetSqlForCheckTableExist(string tableName)
        {
            if (tableName == null) return null;
            return $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}' ;";

        }
        public override string GetSqlTableNameList(bool includeView = true)
        {
            string whereStr = includeView ? $"type in ('table', 'view')" : $"type = 'table'";
            return $"SELECT name FROM pragma_table_list WHERE {whereStr} AND name NOT LIKE 'sqlite_%'";
        }


        public override string? GetSqlLastInsertId(string tableName)
        {
            return $"SELECT MAX(Id) FROM {tableName};";
        }

        public override string? GetSqlForTruncate(string tableName)
        {
            return $"DELETE FROM {tableName}; VACUUM;";
        }



        public override string? GetSqlForCreateTable(TableBaseModel dbModel)
        {
            if (dbModel == null || string.IsNullOrEmpty(dbModel.TableName) || dbModel.Fields == null) return null;
            if (CreateColumnDef(dbModel.Fields) is string cd)
            {
                return $"CREATE TABLE IF NOT EXISTS {dbModel.TableName} ({cd});";
            }
            else
            {
                return null;
            }

        }

        public override string? GetSqlForAlterTable(TableBaseModel dbModel)
        {
            throw new NotImplementedException();
        }
        public override string GetSqlFieldsByTableName(string tableName)
        {
            return $"SELECT * FROM pragma_table_info('{tableName}')";
        }
       
        public override string GetSqlForeignInfoByTableName(string tableName)
        {
            return $"SELECT * FROM pragma_Foreign_key_list('{tableName}')";
        }
        #region Mapping to BaseModel
        public override IEnumerable<FieldBaseModel> MapToFieldBaseModel(IEnumerable<dynamic> target, IEnumerable<dynamic> foreignness)
        {
            List<FieldBaseModel> result = [];
           
            foreach (var item in target)
            {
                if (item is IDictionary<string, object> targets)
                {
                    FieldBaseModel field = new()
                    {
                        FieldName = targets["name"].ToString(),
                        FieldType = targets["type"].ToString(),
                        IsNotNull =  (long)targets["notnull"] == 1,
                        IsPrimaryKey = (long)targets["pk"] == 1,
                    };
                    if (foreignness != null && foreignness.Any())
                    {
                        var foreignInfos = MapToForeignBaseModel(foreignness);
                        var fk = foreignInfos.FirstOrDefault(x => x.CurrentFieldName == field.FieldName);
                        if (fk != null)
                        {
                            field.IsForeignKey = true;
                            field.ForeignInfo = fk;
                        }
                    }   
                    result.Add(field);
                }
            }

            return result;
        }
        public override IEnumerable<ForeignBaseModel> MapToForeignBaseModel(IEnumerable<dynamic> target)
        {
            List<ForeignBaseModel> result = [];
            foreach (var item in target)
            {
                if (item is IDictionary<string, object> targets)
                {
                    ForeignBaseModel foreign = new()
                    {
                        ComeFromFieldName = targets["to"].ToString(),
                        ComeFromTableName = targets["table"].ToString(),
                        CurrentFieldName = targets["from"].ToString()
                    };
                    result.Add(foreign);
                }
            }
            return result;
        }

        public override IEnumerable<RecordBaseModel> MapToRecordBaseModel(IEnumerable<dynamic> target)
        {

            List<RecordBaseModel> result = new();

            foreach (var item in target)
            {
                List<KeyValuePair<string, object>> recordValue = new();
                RecordBaseModel record = new();
                if (item is IDictionary<string, object> recordTarget)
                { 
                    recordValue.AddRange(recordTarget);
                    record.FieldValue = recordValue;
                    result.Add(record);
                }
            }

            return result;
        }
        #endregion
        #region Private Method

        private string? CreateColumnDef(IEnumerable<FieldBaseModel> fields)
        {
            List<string> columns = [];

            List<ForeignBaseModel> fkParts = [];


            foreach (var field in fields)
            {
                StringBuilder sb = new();
                sb.Append(field.FieldName).Append(' ').Append(ConvertDataTypeToDb(field.FieldType));
                if (field.IsNotNull)
                {
                    sb.Append(" NOT NULL");
                }
                if (field.IsPrimaryKey)
                {
                    sb.Append(" PRIMARY KEY");
                }
                if (field.IsForeignKey && field.ForeignInfo != null)
                {
                    fkParts.Add(field.ForeignInfo);
                }
                columns.Add(sb.ToString());
            }
            if(fkParts != null && fkParts.Any())
            {
                var gFK = fkParts.GroupBy(x => x.ComeFromTableName);
                foreach(var gTable in gFK)
                {
                    StringBuilder sb = new();
                    sb.Append(" FOREIGN Key (");
                    sb.Append(string.Join(',', gTable.Select(x => x.CurrentFieldName)));
                    sb.Append(") REFERENCES ").Append(gTable.Key).Append('(');
                    sb.Append(string.Join(',', gTable.Select(x=>x.ComeFromFieldName))).Append(')');

                    columns.Add(sb.ToString());
                }
            }
            return string.Join(',', columns);

        }

        public override string? ConvertDataTypeToDb(string? dataType) => dataType switch
        {
            "Boolean" => "INTEGER",
            "Byte" => "INTEGER",
            "Byte[]" => "BLOB",
            "Char" => "TEXT",
            "DateOnly" => "TEXT",
            "DateTime" => "TEXT",
            "DateTimeOffset" => "TEXT",
            "Decimal" => "TEXT",
            "Double" => "REAL",
            "Guid" => "TEXT",
            "Int16" => "INTEGER",
            "Int32" => "INTEGER",
            "Int64" => "INTEGER",
            "SByte" => "INTEGER",
            "Single" => "REAL",
            "String" => "TEXT",
            "TimeOnly" => "TEXT",
            "TimeSpan" => "TEXT",
            "UInt16" => "INTEGER",
            "UInt32" => "INTEGER",
            "UInt64" => "INTEGER",
            _ => "TEXT"

        };

        #endregion


    }
}
