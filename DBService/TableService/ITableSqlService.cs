using DBService.Models;

namespace DBService.TableService
{
    public interface ITableSqlService<T> where T : TableBased
    {
        #region DQL
        public string? GetSqlForAll(string? whereStr = null);
        public string? GetSqlForFields(string[] fields, string? whereStr = null);
        public string? GetSqlByKeyValue(string key, object value);
        public string? GetSqlByKeyValues(Dictionary<string, object> values);
        public string? GetSqlById(int id);

        #endregion
        #region DML
        public string? GetSqlForInsert(T source, int? parentId = null);
        public string? GetSqlForDelete(int id);
        public string? GetSqlForUpdate(int id, T source);
        public string? GetSqlForBlukAdd(IEnumerable<T> sources);
        public string? GetSqlForBlukAdd();
        #endregion
        #region DDL
        public string? GetSqlForCreateTable();
        public string? GetSqlForDropTable();

        #endregion
        #region DCL
        public string? GetSqlForTruncate();
        public string? GetSqlForCheckRows();
        public string? GetTableName();
        public IEnumerable<FieldBased>? GetFields();
        public string[]? GetFieldsName();
        public string? GetParameters(string[] fields);
        public string? GetSqlForCheckTableExist();
        string? GetSqlForDeleteByKey(string key, object value);
        string? GetSqlForUpdateByKey(string key, object value, T source);
        public string? GetSqlLastInsertId();
        #endregion

    }
}