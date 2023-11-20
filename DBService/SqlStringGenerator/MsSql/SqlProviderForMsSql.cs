

using DBService.Models;

namespace DBService.SqlStringGenerator.MsSql
{
    public class SqlProviderForMsSql : SqlProviderBase
    {
        public SqlProviderForMsSql() { }

        public override string? ConvertDataTypeToDb(string dataType)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlFieldsByTableName(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForAlterTable(TableBaseModel dbModel)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForCheckTableExist(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForCreateTable(TableBaseModel dbModel)
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
            throw new NotImplementedException();
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
    }
}
