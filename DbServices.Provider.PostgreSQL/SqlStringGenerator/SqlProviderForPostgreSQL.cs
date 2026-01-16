using DbServices.Core.Models;
using DbServices.Core.SqlStringGenerator;
using System.Text;

namespace DbServices.Provider.PostgreSQL.SqlStringGenerator
{
    public class SqlProviderForPostgreSQL : SqlProviderBase
    {
        public SqlProviderForPostgreSQL() { }

        public override string? GetSqlForCheckTableExist(string tableName)
        {
            if (tableName == null) return null;
            // 使用參數化查詢防止 SQL 注入（表名已通過驗證服務驗證）
            // 注意：在實際執行時，應該使用參數化查詢，這裡只是生成 SQL 字串
            // 表名應該已經通過 ValidationService.ValidateTableName() 驗證
            return $"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{EscapeSqlIdentifier(tableName)}';";
        }

        public override string GetSqlTableNameList(bool includeView = true)
        {
            string whereStr = includeView 
                ? "table_type IN ('BASE TABLE', 'VIEW')" 
                : "table_type = 'BASE TABLE'";
            return $"SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND {whereStr} ORDER BY table_name;";
        }

        public override string? GetSqlLastInsertId(string tableName)
        {
            // PostgreSQL 使用 RETURNING 子句或 CURRVAL
            // 注意：此方法需要序列存在，如果使用 GENERATED ALWAYS AS IDENTITY，則使用此方式
            // 實際使用時建議在 INSERT 語句中直接使用 RETURNING id
            return $"SELECT CURRVAL(pg_get_serial_sequence('{tableName}', 'Id'));";
        }

        public override string? GetSqlForTruncate(string tableName)
        {
            return $"TRUNCATE TABLE {tableName} RESTART IDENTITY CASCADE;";
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

        public override string? GetSqlForCreateTable(string tableName, IEnumerable<FieldBaseModel> tableDefine)
        {
            if (tableDefine == null || !tableDefine.Any()) return null;
            if (CreateColumnDef(tableDefine) is string cd)
            {
                return $"CREATE TABLE IF NOT EXISTS {tableName} ({cd});";
            }
            else
            {
                return null;
            }
        }

        public override string? GetSqlForDropTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            return $"DROP TABLE IF EXISTS {tableName} CASCADE;";
        }

        public override string? GetSqlForAlterTable(TableBaseModel dbModel)
        {
            if (dbModel == null || string.IsNullOrEmpty(dbModel.TableName) || dbModel.Fields == null || !dbModel.Fields.Any())
                return null;

            var sqlStatements = new List<string>();
            var tableName = EscapeSqlIdentifier(dbModel.TableName);

            // 取得現有欄位資訊（需要從資料庫查詢，這裡假設可以比較）
            // 實際使用時，應該先查詢現有欄位，然後比較差異

            foreach (var field in dbModel.Fields)
            {
                var columnName = EscapeSqlIdentifier(field.FieldName);
                var columnType = ConvertDataTypeToDb(field.FieldType);
                var alterStatements = new List<string>();

                // 新增欄位（如果不存在）
                alterStatements.Add($"ADD COLUMN IF NOT EXISTS {columnName} {columnType}");

                // 設定 NOT NULL（如果需要）
                if (field.IsNotNull && !field.IsPrimaryKey)
                {
                    alterStatements.Add($"ALTER COLUMN {columnName} SET NOT NULL");
                }
                else if (!field.IsNotNull)
                {
                    alterStatements.Add($"ALTER COLUMN {columnName} DROP NOT NULL");
                }

                // 注意：PostgreSQL 不支援在 ALTER TABLE 中直接修改欄位類型
                // 需要單獨的 ALTER COLUMN ... TYPE 語句
                // 這裡只處理新增欄位和約束

                if (alterStatements.Any())
                {
                    sqlStatements.Add($"ALTER TABLE {tableName} {string.Join(", ", alterStatements)};");
                }
            }

            // 處理主鍵約束
            var primaryKeyFields = dbModel.Fields.Where(f => f.IsPrimaryKey).ToList();
            if (primaryKeyFields.Any())
            {
                var pkColumns = string.Join(", ", primaryKeyFields.Select(f => EscapeSqlIdentifier(f.FieldName)));
                sqlStatements.Add($"ALTER TABLE {tableName} ADD CONSTRAINT pk_{dbModel.TableName} PRIMARY KEY ({pkColumns});");
            }

            // 處理外鍵約束
            var foreignKeyFields = dbModel.Fields.Where(f => f.IsForeignKey && f.ForeignInfo != null).ToList();
            foreach (var fkField in foreignKeyFields)
            {
                if (fkField.ForeignInfo != null)
                {
                    var constraintName = $"fk_{fkField.FieldName}";
                    var sql = $"ALTER TABLE {tableName} ADD CONSTRAINT {constraintName} " +
                             $"FOREIGN KEY ({EscapeSqlIdentifier(fkField.FieldName)}) " +
                             $"REFERENCES {EscapeSqlIdentifier(fkField.ForeignInfo.ComeFromTableName)} " +
                             $"({EscapeSqlIdentifier(fkField.ForeignInfo.ComeFromFieldName)});";
                    sqlStatements.Add(sql);
                }
            }

            return sqlStatements.Any() ? string.Join("\n", sqlStatements) : null;
        }

        /// <summary>
        /// 取得資料表欄位資訊的 SQL（包含主鍵資訊和 JSON 類型支援）
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>SQL 語句</returns>
        public override string GetSqlFieldsByTableName(string tableName)
        {
            // 表名應該已經通過驗證，但為了安全起見還是進行轉義
            // 同時查詢主鍵資訊和 JSON 類型
            var escapedTableName = EscapeSqlIdentifier(tableName);
            return $@"
                SELECT 
                    c.column_name,
                    c.data_type,
                    c.is_nullable,
                    c.column_default,
                    c.character_maximum_length,
                    c.numeric_precision,
                    c.numeric_scale,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_primary_key,
                    CASE 
                        WHEN c.data_type IN ('json', 'jsonb') THEN true 
                        ELSE false 
                    END AS is_json_type
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT ku.table_name, ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku
                        ON tc.constraint_name = ku.constraint_name
                        AND tc.table_schema = ku.table_schema
                    WHERE tc.constraint_type = 'PRIMARY KEY'
                        AND tc.table_schema = 'public'
                        AND tc.table_name = '{escapedTableName}'
                ) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name
                WHERE c.table_schema = 'public' AND c.table_name = '{escapedTableName}'
                ORDER BY c.ordinal_position;";
        }

        public override string GetSqlForeignInfoByTableName(string tableName)
        {
            return $@"
                SELECT
                    tc.constraint_name,
                    tc.table_name,
                    kcu.column_name,
                    ccu.table_name AS foreign_table_name,
                    ccu.column_name AS foreign_column_name
                FROM information_schema.table_constraints AS tc
                JOIN information_schema.key_column_usage AS kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage AS ccu
                    ON ccu.constraint_name = tc.constraint_name
                    AND ccu.table_schema = tc.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND tc.table_schema = 'public'
                    AND tc.table_name = '{EscapeSqlIdentifier(tableName)}';";
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
                        FieldName = targets["column_name"].ToString() ?? string.Empty,
                        FieldType = MapPostgreSQLTypeToCSharpType(targets["data_type"].ToString() ?? "text"),
                        IsNotNull = targets.ContainsKey("is_nullable") && 
                                   targets["is_nullable"].ToString()?.ToUpper() == "NO",
                        IsPrimaryKey = targets.ContainsKey("is_primary_key") && 
                                      (targets["is_primary_key"] is bool isPk && isPk) ||
                                      (targets["is_primary_key"]?.ToString()?.ToLower() == "true")
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
                        ComeFromFieldName = targets["foreign_column_name"].ToString() ?? string.Empty,
                        ComeFromTableName = targets["foreign_table_name"].ToString() ?? string.Empty,
                        CurrentFieldName = targets["column_name"].ToString() ?? string.Empty
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

        #endregion

        #region Private Methods

        /// <summary>
        /// 轉義 SQL 識別符（表名、欄位名）以防止 SQL 注入
        /// 注意：這只是基本防護，表名應該已經通過 ValidationService 驗證
        /// </summary>
        private static string EscapeSqlIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;

            // PostgreSQL 使用雙引號轉義識別符
            // 但由於我們已經通過 ValidationService 驗證，這裡主要是防禦性編程
            return identifier.Replace("\"", "\"\"");
        }

        private string? CreateColumnDef(IEnumerable<FieldBaseModel> fields)
        {
            List<string> columns = [];
            List<ForeignBaseModel> fkParts = [];

            foreach (var field in fields)
            {
                StringBuilder sb = new();
                sb.Append(field.FieldName).Append(' ').Append(ConvertDataTypeToDb(field.FieldType));
                
                if (field.IsPrimaryKey)
                {
                    sb.Append(" PRIMARY KEY");
                }
                
                if (field.IsNotNull && !field.IsPrimaryKey)
                {
                    sb.Append(" NOT NULL");
                }

                if (field.IsPrimaryKey && field.FieldType?.ToLower().Contains("int") == true)
                {
                    sb.Append(" GENERATED ALWAYS AS IDENTITY");
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
                    sb.Append("CONSTRAINT fk_").Append(gTable.First().CurrentFieldName)
                      .Append(" FOREIGN KEY (");
                    sb.Append(string.Join(',', gTable.Select(x => x.CurrentFieldName)));
                    sb.Append(") REFERENCES ").Append(gTable.Key).Append('(');
                    sb.Append(string.Join(',', gTable.Select(x => x.ComeFromFieldName))).Append(')');
                    sb.Append(" ON DELETE SET DEFAULT");
                    sb.Append(" ON UPDATE CASCADE");
                    columns.Add(sb.ToString());
                }
            }

            return string.Join(',', columns);
        }

        /// <summary>
        /// 將 PostgreSQL 資料類型映射到 C# 類型
        /// </summary>
        /// <param name="postgresType">PostgreSQL 資料類型</param>
        /// <returns>C# 類型名稱</returns>
        private string MapPostgreSQLTypeToCSharpType(string? postgresType)
        {
            if (string.IsNullOrEmpty(postgresType)) return "String";

            return postgresType.ToLower() switch
            {
                "boolean" or "bool" => "Boolean",
                "smallint" or "int2" => "Int16",
                "integer" or "int" or "int4" => "Int32",
                "bigint" or "int8" => "Int64",
                "real" or "float4" => "Single",
                "double precision" or "float8" => "Double",
                "numeric" or "decimal" or "money" => "Decimal",
                "character varying" or "varchar" or "text" or "char" or "character" => "String",
                "bytea" => "Byte[]",
                "date" => "DateOnly",
                "timestamp" or "timestamp without time zone" => "DateTime",
                "timestamptz" or "timestamp with time zone" => "DateTimeOffset",
                "time" or "time without time zone" => "TimeOnly",
                "uuid" => "Guid",
                "interval" => "TimeSpan",
                "json" or "jsonb" => "String",  // JSON 類型映射為 String，可以使用 System.Text.Json 處理
                _ => "String"
            };
        }

        /// <summary>
        /// 將 C# 資料類型轉換為 PostgreSQL 資料類型
        /// </summary>
        /// <param name="dataType">C# 資料類型名稱</param>
        /// <returns>PostgreSQL 資料類型</returns>
        public override string? ConvertDataTypeToDb(string? dataType) => dataType switch
        {
            "Boolean" => "BOOLEAN",
            "Byte" => "SMALLINT",
            "Byte[]" => "BYTEA",
            "Char" => "CHAR(1)",
            "DateOnly" => "DATE",
            "DateTime" => "TIMESTAMP",
            "DateTimeOffset" => "TIMESTAMPTZ",
            "Decimal" => "NUMERIC(18,2)",
            "Double" => "DOUBLE PRECISION",
            "Guid" => "UUID",
            "Int" or "Int32" => "INTEGER",
            "Int16" => "SMALLINT",
            "Int64" => "BIGINT",
            "SByte" => "SMALLINT",
            "Single" => "REAL",
            "String" => "TEXT",
            "TimeOnly" => "TIME",
            "TimeSpan" => "INTERVAL",
            "UInt16" => "INTEGER",
            "UInt32" => "BIGINT",
            "UInt64" => "BIGINT",
            "Json" or "JSON" => "JSONB",  // JSON 類型支援
            _ => "TEXT"
        };

        #endregion
    }
}

