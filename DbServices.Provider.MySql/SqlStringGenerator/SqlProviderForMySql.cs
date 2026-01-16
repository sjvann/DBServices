
using DbServices.Core.Models;
using DbServices.Core.SqlStringGenerator;
using System.Text;

namespace DbServices.Provider.MySql.SqlStringGenerator
{
    public class SqlProviderForMySql : SqlProviderBase
    {
        public SqlProviderForMySql() { }


        /// <summary>
        /// 檢查資料表是否存在
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string? GetSqlForCheckTableExist(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            var escapedTableName = EscapeSqlIdentifier(tableName);
            return $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{escapedTableName}';";
        }

        /// <summary>
        /// 取得資料表名稱清單
        /// </summary>
        /// <param name="includeView">是否包含視圖</param>
        /// <returns>SQL 語句</returns>
        public override string GetSqlTableNameList(bool includeView = true)
        {
            string whereStr = includeView 
                ? "TABLE_TYPE IN ('BASE TABLE', 'VIEW')" 
                : "TABLE_TYPE = 'BASE TABLE'";
            return $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE {whereStr};";
        }

        /// <summary>
        /// 取得最後插入的 ID
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string? GetSqlLastInsertId(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            var escapedTableName = EscapeSqlIdentifier(tableName);
            return $"SELECT LAST_INSERT_ID() AS LastId FROM {escapedTableName} LIMIT 1;";
        }

        /// <summary>
        /// 清空資料表
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string? GetSqlForTruncate(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            var escapedTableName = EscapeSqlIdentifier(tableName);
            return $"TRUNCATE TABLE {escapedTableName};";
        }

        /// <summary>
        /// 取得資料表欄位資訊的 SQL（包含主鍵資訊）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string GetSqlFieldsByTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return string.Empty;
            
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
    c.ORDINAL_POSITION,
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
        AND tc.TABLE_NAME = '{escapedTableName}'
) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
WHERE c.TABLE_NAME = '{escapedTableName}'
ORDER BY c.ORDINAL_POSITION;";
        }

        /// <summary>
        /// 取得外鍵資訊
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string GetSqlForeignInfoByTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return string.Empty;
            
            var escapedTableName = EscapeSqlIdentifier(tableName);
            
            return $@"
SELECT 
    kcu.COLUMN_NAME AS CurrentFieldName,
    kcu.REFERENCED_TABLE_NAME AS ComeFromTableName,
    kcu.REFERENCED_COLUMN_NAME AS ComeFromFieldName
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
INNER JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
    ON kcu.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
    AND kcu.TABLE_SCHEMA = rc.CONSTRAINT_SCHEMA
WHERE kcu.TABLE_NAME = '{escapedTableName}'
    AND kcu.REFERENCED_TABLE_NAME IS NOT NULL;";
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
                        FieldName = targets.ContainsKey("COLUMN_NAME") 
                            ? targets["COLUMN_NAME"].ToString() ?? string.Empty
                            : string.Empty,
                        FieldType = targets.ContainsKey("DATA_TYPE") 
                            ? targets["DATA_TYPE"].ToString() ?? "varchar"
                            : "varchar",
                        IsNotNull = targets.ContainsKey("IS_NULLABLE") && 
                                   targets["IS_NULLABLE"].ToString()?.ToUpper() == "NO",
                        IsPrimaryKey = targets.ContainsKey("IS_PRIMARY_KEY") && 
                                      Convert.ToInt32(targets["IS_PRIMARY_KEY"]) == 1,
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
                        ComeFromFieldName = targets.ContainsKey("ComeFromFieldName") 
                            ? targets["ComeFromFieldName"].ToString() ?? string.Empty
                            : (targets.ContainsKey("REFERENCED_COLUMN_NAME") 
                                ? targets["REFERENCED_COLUMN_NAME"].ToString() ?? string.Empty
                                : string.Empty),
                        ComeFromTableName = targets.ContainsKey("ComeFromTableName") 
                            ? targets["ComeFromTableName"].ToString() ?? string.Empty
                            : (targets.ContainsKey("REFERENCED_TABLE_NAME") 
                                ? targets["REFERENCED_TABLE_NAME"].ToString() ?? string.Empty
                                : string.Empty),
                        CurrentFieldName = targets.ContainsKey("CurrentFieldName") 
                            ? targets["CurrentFieldName"].ToString() ?? string.Empty
                            : (targets.ContainsKey("COLUMN_NAME") 
                                ? targets["COLUMN_NAME"].ToString() ?? string.Empty
                                : string.Empty)
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
                        record.Id = (long)(recordTarget["Id"] ?? recordTarget["id"]);
                    }

                    recordValue.AddRange(recordTarget);
                    record.FieldValue = recordValue;
                    result.Add(record);
                }
            }

            return result;
        }
        #endregion
        #region Private Method

        /// <summary>
        /// 轉義 SQL 識別符（表名、欄位名）以防止 SQL 注入
        /// MySQL 使用反引號 ` 轉義識別符
        /// </summary>
        private static string EscapeSqlIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            // MySQL 使用反引號轉義識別符
            // 將反引號加倍以進行轉義
            return identifier.Replace("`", "``");
        }

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

        public override string? GetSqlForAlterTable(TableBaseModel dbModel)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForDropTable(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForCreateTable(string tableName, IEnumerable<FieldBaseModel> tableDefine)
        {
            throw new NotImplementedException();
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

        #endregion


    }
}
