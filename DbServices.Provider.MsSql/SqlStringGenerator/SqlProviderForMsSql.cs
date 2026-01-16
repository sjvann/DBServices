
using DbServices.Core.Models;
using DbServices.Core.SqlStringGenerator;
using System.Text;

namespace DbServices.Provider.MsSql.SqlStringGenerator
{
    public class SqlProviderForMsSql : SqlProviderBase
    {
        public SqlProviderForMsSql() { }

        public override string? ConvertDataTypeToDb(string? dataType) => dataType switch
        {
            "Boolean" => "bit",
            "Byte" => "tinyint",
            "Byte[]" => "binary",
            "Char" => "nchar",
            "DateOnly" => "ntext",
            "DateTime" => "datetime",
            "DateTimeOffset" => "datetimeoffset",
            "Decimal" => "money",
            "Double" => "float",
            "Guid" => "uniqueidentifier",
            "Int16" => "smallint",
            "Int32" => "int",
            "Int64" => "bigint",
            "SByte" => "tinyint",
            "Single" => "real",
            "String" => "nvarchar",
            _ => "nvarchar"
        };

        /// <summary>
        /// 取得資料表欄位資訊的 SQL（包含主鍵資訊）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string? GetSqlFieldsByTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            
            var escapedTableName = EscapeSqlIdentifier(tableName);
            
            // 使用 JOIN 查詢欄位資訊和主鍵資訊
            return $@"
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN (
    SELECT ku.TABLE_NAME, ku.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
        AND tc.TABLE_NAME = ku.TABLE_NAME
    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
        AND tc.TABLE_NAME = N'{tableName}'
) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
WHERE c.TABLE_NAME = N'{tableName}'
ORDER BY c.ORDINAL_POSITION;";
        }

        public override string? GetSqlForAlterTable(TableBaseModel dbModel)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForCheckTableExist(string tableName)
        {
            return $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES Where TABLE_NAME = N'{tableName}';";
        }

        public override string? GetSqlForCreateTable(TableBaseModel dbModel)
        {
            if (dbModel == null || string.IsNullOrEmpty(dbModel.TableName) || dbModel.Fields == null) return null;
            if (CreateColumnDef(dbModel.Fields) is string cd)
            {
                return $"CREATE TABLE {dbModel.TableName} ({cd});";
            }
            else
            {
                return null;
            }
        }

        public override string? GetSqlForCreateTable(string tableName, IEnumerable<FieldBaseModel> tableDefine)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForDropTable(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForeignInfoByTableName(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForTruncate(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlLastInsertId(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlTableNameList(bool includeView = true)
        {
            string whereStr = includeView ? $"TABLE_TYPE in ('BASE TABLE', 'VIEW')" : $"TABLE_TYPE = 'BASE TABLE'";
            return $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE {whereStr};";

        }

        public override IEnumerable<FieldBaseModel> MapToFieldBaseModel(IEnumerable<dynamic> target, IEnumerable<dynamic> foreignness)
        {
            List<FieldBaseModel> result = [];

            foreach (var item in target)
            {
                if (item is IDictionary<string, object> targets)
                {
                    FieldBaseModel field = new()
                    {
                        FieldName = targets["COLUMN_NAME"].ToString() ?? string.Empty,
                        FieldType = MapSqlServerTypeToCSharpType(targets["DATA_TYPE"].ToString() ?? "nvarchar"),
                        IsNotNull = targets.ContainsKey("IS_NULLABLE") && 
                                   targets["IS_NULLABLE"].ToString()?.ToUpper() == "NO",
                        IsPrimaryKey = targets.ContainsKey("IS_PRIMARY_KEY") && 
                                       Convert.ToInt32(targets["IS_PRIMARY_KEY"]) == 1
                    };

                    // 檢查是否為外鍵
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
                        ComeFromFieldName = targets.ContainsKey("REFERENCED_COLUMN_NAME") 
                            ? targets["REFERENCED_COLUMN_NAME"].ToString() ?? string.Empty 
                            : string.Empty,
                        ComeFromTableName = targets.ContainsKey("REFERENCED_TABLE_NAME") 
                            ? targets["REFERENCED_TABLE_NAME"].ToString() ?? string.Empty 
                            : string.Empty,
                        CurrentFieldName = targets.ContainsKey("COLUMN_NAME") 
                            ? targets["COLUMN_NAME"].ToString() ?? string.Empty 
                            : string.Empty
                    };
                    result.Add(foreign);
                }
            }
            return result;
        }

        public override IEnumerable<RecordBaseModel> MapToRecordBaseModel(IEnumerable<dynamic> target)
        {
            List<RecordBaseModel> result = [];

            foreach (var item in target)
            {
                List<KeyValuePair<string, object>> recordValue = [];
                RecordBaseModel record = new();
                if (item is IDictionary<string, object> recordTarget)
                {
                    if (recordTarget.ContainsKey("Id") || recordTarget.ContainsKey("id"))
                    {
                        var idValue = recordTarget["Id"] ?? recordTarget["id"];
                        if (idValue != null)
                        {
                            record.Id = Convert.ToInt64(idValue);
                        }
                    }

                    recordValue.AddRange(recordTarget);
                    record.FieldValue = recordValue;
                    result.Add(record);
                }
            }

            return result;
        }

        private string MapSqlServerTypeToCSharpType(string? sqlType)
        {
            if (string.IsNullOrEmpty(sqlType)) return "String";

            return sqlType.ToLower() switch
            {
                "bit" => "Boolean",
                "tinyint" => "Byte",
                "smallint" => "Int16",
                "int" => "Int32",
                "bigint" => "Int64",
                "real" => "Single",
                "float" => "Double",
                "decimal" or "numeric" or "money" or "smallmoney" => "Decimal",
                "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" => "String",
                "binary" or "varbinary" or "image" => "Byte[]",
                "date" => "DateOnly",
                "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "datetimeoffset" => "DateTimeOffset",
                "time" => "TimeOnly",
                "uniqueidentifier" => "Guid",
                _ => "String"
            };
        }

        #region Private Methods

        private string CreateColumnDef(IEnumerable<FieldBaseModel> fields)
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
            if (fkParts != null && fkParts.Any())
            {
                var gFK = fkParts.GroupBy(x => x.ComeFromTableName);
                foreach (var gTable in gFK)
                {
                    StringBuilder sb = new();
                    sb.Append(" FOREIGN Key (");
                    sb.Append(string.Join(',', gTable.Select(x => x.CurrentFieldName)));
                    sb.Append(") REFERENCES ").Append(gTable.Key).Append('(');
                    sb.Append(string.Join(',', gTable.Select(x => x.ComeFromFieldName))).Append(')');

                    columns.Add(sb.ToString());
                }
            }
            return string.Join(',', columns);
        }

        #endregion


    }
}
