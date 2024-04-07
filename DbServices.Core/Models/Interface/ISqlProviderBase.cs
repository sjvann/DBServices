using DbServices.Core.Models.Enum;
using System.ComponentModel.DataAnnotations;

namespace DbServices.Core.Models.Interface
{
    public interface ISqlProviderBase
    {
        #region DataCcontrol DCL
        string? ConvertDataTypeToDb(string dataType);

        /// <summary>
        /// 檢查資料表是否有紀錄資料
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlForCheckRows(string tableName);
        /// <summary>
        /// 檢查資料表是否存在
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlForCheckTableExist(string tableName);
        /// <summary>
        /// 取得資料表最近新增的ID,若是流水號,則是取最大的id
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlLastInsertId(string tableName);
        /// <summary>
        /// 清空資料表內容
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlForTruncate(string tableName);

        #endregion
        #region DataDefinition DDL
        /// <summary>
        /// 新增資料表
        /// </summary>
        /// <param name="dbModel"></param>
        /// <returns></returns>
        string? GetSqlForCreateTable(TableBaseModel dbModel);
        /// <summary>
        /// 更新資料表
        /// </summary>
        /// <param name="dbModel"></param>
        /// <returns></returns>
        string? GetSqlForAlterTable(TableBaseModel dbModel);
        /// <summary>
        /// 刪除資料表
        /// </summary>
        /// <returns></returns>
        string? GetSqlForDropTable(string tableName);
        #endregion
        #region DataManipulation DML
        /// <summary>
        /// 刪除某筆特定資料
        /// </summary>
        /// <param name="id">紀錄流水號</param>
        /// <returns></returns>
        string? GetSqlForDelete(string tableName, long id);
        /// <summary>
        /// 刪除資料,依據某條件
        /// </summary>
        /// <param name="criteria"></param>
        /// <returns></returns>
        string? GetSqlForDeleteByKey(string tableName, KeyValuePair<string, object?> criteria);
        /// <summary>
        /// 新增紀錄
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        string? GetSqlForInsert(string tableName, IEnumerable<KeyValuePair<string, object?>> source);
        /// <summary>
        /// 更新紀錄
        /// </summary>
        /// <param name="id"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        string? GetSqlForUpdate(string tableName, long id, IEnumerable<KeyValuePair<string, object?>> source);
        /// <summary>
        /// 更新紀錄,滿足條件者
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        string? GetSqlForUpdateByKey(string tableName, KeyValuePair<string, object?> criteria, IEnumerable<KeyValuePair<string, object?>> source);
        #endregion
        #region DataQuery DQL
        /// <summary>
        /// 取回特定紀錄
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        string? GetSqlById(string tableName, object id);
        /// <summary>
        /// 依據某鍵值取回紀錄
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="operators"></param>
        /// <returns></returns>
        string? GetSqlByKeyValue(string tableName, string key, object value, EnumQueryOperator? @operators = null);
        /// <summary>
        /// 依據條件取回紀錄
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        string? GetSqlByKeyValues(string tableName, IDictionary<string, object?> values);
        /// <summary>
        /// 依據條件取回紀錄
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        string? GetSqlByKeyValues(string tableName, IEnumerable<KeyValuePair<string, object?>> values);
        /// <summary>
        /// 取回所有欄位,可給予where條件字串
        /// </summary>
        /// <param name="whereStr"></param>
        /// <returns></returns>
        string? GetSqlForAll(string tableName, string? whereStr = null);
        /// <summary>
        /// 取回特定欄位,可給予where條件字串
        /// </summary>
        /// <param name="fieldNames"></param>
        /// <param name="whereStr"></param>
        /// <returns></returns>
        string? GetSqlForFields(string tableName, string[] fieldNames, string? whereStr = null);
        /// <summary>
        /// 取回某欄位所有可能值
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        string? GetSqlForValueSet(string tableName, string fieldName);
        /// <summary>
        /// 產生Inner Join
        /// </summary>
        /// <param name="oneSideTableName"></param>
        /// <param name="oneSideKeyName"></param>
        /// <param name="manySideTableName"></param>
        /// <param name="manySideKeyName"></param>
        /// <returns></returns>
        string? GetSqlForInnerJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName);
        /// <summary>
        /// 產生left join
        /// </summary>
        /// <param name="oneSideTableName"></param>
        /// <param name="oneSideKeyName"></param>
        /// <param name="manySideTableName"></param>
        /// <param name="manySideKeyName"></param>
        /// <returns></returns>
        string? GetSqlForLeftJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName);
        /// <summary>
        /// 產生right join
        /// </summary>
        /// <param name="oneSideTableName"></param>
        /// <param name="oneSideKeyName"></param>
        /// <param name="manySideTableName"></param>
        /// <param name="manySideKeyName"></param>
        /// <returns></returns>
        string? GetSqlForRightJoin(string oneSideTableName, string oneSideKeyName, string manySideTableName, string manySideKeyName);
        #endregion
        #region DataMeta
        /// <summary>
        /// 取回資料庫所有資料表
        /// </summary>
        /// <param name="includeView"></param>
        /// <returns></returns>
        string? GetSqlTableNameList(bool includeView = true);

        /// <summary>
        /// 取得所有欄位清單
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlFieldsByTableName(string tableName);
        IEnumerable<FieldBaseModel>? MapToFieldBaseModel(IEnumerable<dynamic> target, IEnumerable<dynamic> foreignness);
        /// <summary>
        /// 取得外來鍵資訊
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        string? GetSqlForeignInfoByTableName(string tableName);
        IEnumerable<ForeignBaseModel>? MapToForeignBaseModel(IEnumerable<dynamic> target);
        IEnumerable<RecordBaseModel>? MapToRecordBaseModel(IEnumerable<dynamic> target);
        #endregion
    }
}
