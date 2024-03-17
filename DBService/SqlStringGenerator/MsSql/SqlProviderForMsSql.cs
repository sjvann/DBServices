using DBServices.Models;
using System.Text;

namespace DBServices.SqlStringGenerator.MsSql
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

        public override string? GetSqlFieldsByTableName(string tableName)
        {
            return $"SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'{tableName}';";
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
            throw new NotImplementedException();
        }

        public override IEnumerable<ForeignBaseModel> MapToForeignBaseModel(IEnumerable<dynamic> target)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RecordBaseModel> MapToRecordBaseModel(IEnumerable<dynamic> target)
        {
            throw new NotImplementedException();
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
