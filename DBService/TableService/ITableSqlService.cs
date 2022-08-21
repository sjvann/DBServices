namespace DBService.TableService
{
    public interface ITableSqlService<T> where T : class
    {
        #region DQL
        public string? GetSqlForAll(string? whereStr = null);
        public string? GetSqlForFields(string[] fields, string? whereStr = null);
        public string? GetSqlByKey(string key, object value);
        public string? GetSqlByKeyValuePair(Dictionary<string, object> values);
        public string? GetSqlById(int id);

        #endregion
        #region DML
        public string? GetSqlForInsert(T source, int? parentId = null);
        public string? GetSqlForDelete(int id);
        public string? GetSqlForUpdate(int id, T source);
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
        public string[]? GetFields();
        public string? GetParameters();
        public string? GetSqlForCheckTableExist();
        string? GetSqlForDeleteByKey(string key, int id);
        string? GetSqlForUpdateByKey(string key, int id, T source);
        #endregion

    }
}