using DbServices.Core.Models;
using DbServices.Core.Models.Enum;

namespace DbServices.Core.Models.Interface
{
    /// <summary>
    /// 非同步資料庫服務介面
    /// </summary>
    public interface IDbServiceAsync
    {
        /// <summary>
        /// 非同步取回資料庫所有資料表名稱
        /// </summary>
        /// <param name="includeView">是否包含View。預設是包含。</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<string[]?> GetAllTableNamesAsync(bool includeView = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步取回資料表所有欄位名稱
        /// </summary>
        /// <param name="tableName">資料表名稱</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<IEnumerable<FieldBaseModel>?> GetFieldsByTableNameAsync(string tableName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步取得資料表中某欄位的所有資料
        /// </summary>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="tableName">資料表</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<IEnumerable<object>?> GetValueSetByFieldNameAsync(string fieldName, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步確認資料表是否有資料
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<bool> HasRecordAsync(string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步取回資料表特定ID之所有欄位資料
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<TableBaseModel?> GetRecordByIdAsync(long id, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步提供單一查詢條件，取回資料表特定欄位資料
        /// </summary>
        /// <param name="query">查詢條件</param>
        /// <param name="giveOperator">條件運算子</param>
        /// <param name="tableName">資料表</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<TableBaseModel?> GetRecordByKeyValueAsync(KeyValuePair<string, object?> query, EnumQueryOperator? giveOperator = null, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步新增紀錄
        /// </summary>
        /// <param name="source"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<TableBaseModel?> InsertRecordAsync(IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步更新紀錄
        /// </summary>
        /// <param name="id"></param>
        /// <param name="source"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<TableBaseModel?> UpdateRecordByIdAsync(long id, IEnumerable<KeyValuePair<string, object?>> source, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步刪除紀錄
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<bool> DeleteRecordByIdAsync(long id, string? tableName = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 非同步批次新增
        /// </summary>
        /// <param name="records"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns></returns>
        Task<int> BulkInsertAsync(IEnumerable<IEnumerable<KeyValuePair<string, object?>>> records, string? tableName = null, CancellationToken cancellationToken = default);
    }
}
