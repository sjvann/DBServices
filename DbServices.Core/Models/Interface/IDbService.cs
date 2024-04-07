using DbServices.Core.Models.Enum;

namespace DbServices.Core.Models.Interface
{
    public interface IDbService
    {
        /// <summary>
        /// 取回資料庫所有資料表名稱
        /// </summary>
        /// <param name="includeView">是否包含View。預設是包含。</param>
        /// <returns></returns>
        string[]? GetAllTableNames(bool includeView = true);
        /// <summary>
        /// 取回資料表所有欄位名稱
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns></returns>
        IEnumerable<FieldBaseModel>? GetFieldsByTableName(string tableName);
        /// <summary>
        /// 取得資料表中某欄位的所有資料
        /// </summary>
        /// <param name="tableName">資料表</param>
        /// <param name="fieldName">欄位名稱</param>
        /// <returns></returns>
        IEnumerable<object>? GetValueSetByFieldName(string fieldName, string? tableName = null);
        /// <summary>
        /// 確認資料表是否有資料
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        bool HasRecord(string? tableName = null);
        /// <summary>
        /// 取回資料表所所有欄位資料
        /// </summary>
        /// <param name="tableName">資料表</param>
        /// <returns></returns>
        TableBaseModel? GetRecordByTableName(string tableName);
        /// <summary>
        /// 取回資料表特定ID之所有欄位資料
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        TableBaseModel? GetRecordById(long id, string? tableName = null);
        /// <summary>
        /// 提供單一查詢條件，取回資料表特定欄位資料
        /// </summary>
        /// <param name="tableName">資料表</param>
        /// <param name="query">查詢條件</param>
        /// <param name="opertor">條件運算子</param>
        /// <returns></returns>
        TableBaseModel? GetRecordByKeyValue(KeyValuePair<string, object?> query, EnumQueryOperator? giveOperator = null, string? tableName = null);
        /// <summary>
        /// 提供多個查詢條件，取回資料表欄位資料
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="query">查詢條件</param>
        /// <returns></returns>
        TableBaseModel? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, string? tableName = null);
        /// <summary>
        /// 提供Where條件，取回資料表欄位資料
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="whereStr">WHEWE子句內容</param>
        /// <returns></returns>
        TableBaseModel? GetRecordWithWhere(string whereStr, string? tableName = null);
        /// <summary>
        /// 取回特定欄位資料，可提供WHERE子句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="fields">欲取回之欄位名稱清單</param>
        /// <param name="whereStr">WHERE子句</param>
        /// <returns></returns>
        TableBaseModel? GetRecordForField(string[] fields, string? whereStr = null, string? tableName = null);
        /// <summary>
        /// 取回資料表中特定外來鍵之所有欄位資料
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="fieldName">外來鍵之欄位名稱</param>
        /// <param name="id">外來資料表之ID</param>
        /// <returns></returns>
        TableBaseModel? GetRecordByForeignKeyForOneSide(string fkFieldName, long manySideId, string manySideTableName, string? oneSideTableName = null);
        TableBaseModel? GetRecordByForeignKeyForManySide(int oneSideId, string fkFieldName, string manySideTableName, string? oneSideTableName = null);
        TableBaseModel? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);
        TableBaseModel? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);
        bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null);

        bool DeleteRecordById(long id, string? tableName = null);
        bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria, string? tableName = null);
        int CreateNewTable<T>() where T : TableDefBaseModel;
        int DropTable(string tableName);
        /// <summary>
        /// 執行StoredProcedure
        /// </summary>
        /// <param name="spName">Store Procedure名稱</param>
        /// <param name="parameters">執行參數</param>
        /// <returns></returns>
        int ExecuteStoredProcedure(string spName, IEnumerable<KeyValuePair<string, object?>> parameters);
        /// <summary>
        /// 執行SQL指令
        /// </summary>
        /// <param name="sql">SQL指令</param>
        /// <returns></returns>
        int ExecuteSQL(string sql);
        bool HasTable(string tableName);
        void SetCurrentTableName(string tableName);
        void SetCurrentTable<T>() where T : TableDefBaseModel;
        string? GetCurrentTableName();

    }
}
