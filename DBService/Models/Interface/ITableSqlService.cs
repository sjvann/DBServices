using DBService.Models;

namespace DBService.Models.Interface
{
    public interface ITableSqlService<T> : IDataControl, IDataQuery, IDataDefinition where T : TableBased
    {

        #region DML
        public string? GetSqlForInsert(T source, int? parentId = null);
        public string? GetSqlForDelete(int id);
        public string? GetSqlForUpdate(int id, T source);
        public string? GetSqlForBlukAdd(IEnumerable<T> sources);
        public string? GetSqlForBlukAdd();
        string? GetSqlForUpdateByKey(string key, object value, T source);


        #endregion

    }
}