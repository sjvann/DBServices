namespace DBService.Models
{
    public interface IDbModel<T> where T : TableBased
    {
        #region DQL
        public abstract string GetSqlForAll(string? query = null);
        public abstract string GetSqlSpecificFields(string[] fields, string? query = null);
        public abstract string GetSqlById(int id);
        #endregion
        #region DML
        public abstract string GetSqlForAdd(T source, int? parentId = null);
        public abstract string GetSqlForDelete(int id);
        public abstract string GetSqlForUpdate(T source);
        #endregion
        #region DDL
        #endregion
        #region DCL
        public abstract string GetSqlForTruncate();
        public abstract string GetSqlForCheckRows();
        public string GetTableName();
        public string GetSelectFields();
        public string GetParameters();
        #endregion
    }
}