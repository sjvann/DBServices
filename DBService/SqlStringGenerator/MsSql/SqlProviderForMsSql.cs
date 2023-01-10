

using DBService.Models;

namespace DBService.SqlStringGenerator.MsSql
{
    public class SqlProviderForMsSql : SqlProviderBase
    {
        public SqlProviderForMsSql() { }
        public SqlProviderForMsSql(string? tableName):base(tableName) { }

        public override string GetSqlFieldsByName(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlForTruncate()
        {
            throw new NotImplementedException();
        }

        public override string? GetSqlLastInsertId()
        {
            throw new NotImplementedException();
        }

        public override string GetSqlTableNameList(bool includeView = true)
        {
            throw new NotImplementedException();
        }
    }
}
