using DBServices.Models.Enum;


namespace DBServices.Models.Interface
{
    public interface IDataTableService<T> where T : TableDefBaseModel
    {
        /// <summary>
        /// 取回資料表所有欄位名稱
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <returns></returns>
        string[] GetFieldsByTableName();
        /// <summary>
        /// 取得資料表中某欄位的所有資料
        /// </summary>
        /// <param name="fieldName">欄位名稱</param>
        /// <returns></returns>
        string[]? GetValueSetByFieldName(string fieldName);
        /// <summary>
        /// 確認資料表是否有資料
        /// </summary>
        /// <returns></returns>
        bool HasRecord();
        /// <summary>
        /// 取回資料表所所有欄位資料
        /// </summary>
        /// <returns></returns>
        IEnumerable<T>? GetAllRecord();
        /// <summary>
        /// 取回資料表特定ID之所有欄位資料
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        T? GetRecordById(int id);
        /// <summary>
        /// 提供單一查詢條件，取回資料表特定欄位資料
        /// </summary>
        /// <param name="query">查詢條件</param>
        /// <param name="opertor">條件運算子</param>
        /// <returns></returns>
        IEnumerable<T>? GetRecordByKeyValue(KeyValuePair<string, object?> query, EnumQueryOperator? opertor = null);
        /// <summary>
        /// 提供多個查詢條件，取回資料表欄位資料
        /// </summary>

        /// <param name="query">查詢條件</param>
        /// <param name="operators">條件運算子</param>
        /// <returns></returns>
        IEnumerable<T>? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, IEnumerable<EnumQueryOperator>? operators = null);
        /// <summary>
        /// 提供Where條件，取回資料表欄位資料
        /// </summary>
        /// <param name="whereStr">WHEWE子句內容</param>
        /// <returns></returns>
        IEnumerable<T>? GetRecordWithWhere(string whereStr);
        /// <summary>
        /// 取回特定欄位資料，可提供WHERE子句
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="fields">欲取回之欄位名稱清單</param>
        /// <param name="whereStr">WHERE子句</param>
        /// <returns></returns>
        IEnumerable<T>? GetRecordForField(string[] fields, string? whereStr = null);
        /// <summary>
        /// 取回資料表中特定外來鍵之所有欄位資料
        /// </summary>
        /// <param name="fieldName">外來鍵之欄位名稱</param>
        /// <param name="id">外來資料表之ID</param>
        /// <returns></returns>
        IEnumerable<T2>? GetRecordByForeignKey<T2>(string fieldName, int id) where T2 : TableDefBaseModel;
        T? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source);
        T? InsertRecord(T source);
        T? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source);
        bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source);

        bool DeleteRecordById(long id);
        bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria);
        int CreateNewTable();
        int DropTable();
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

    }
}
