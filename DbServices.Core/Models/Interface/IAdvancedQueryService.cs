using DbServices.Core.Models;

namespace DbServices.Core.Models.Interface
{
    /// <summary>
    /// 進階查詢服務介面
    /// 提供更多元、更方便的查詢功能
    /// </summary>
    public interface IAdvancedQueryService
    {
        /// <summary>
        /// 根據條件查詢記錄（支援分頁和排序）
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="options">查詢選項（排序、分頁等）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>包含符合條件記錄的 TableBaseModel</returns>
        TableBaseModel? GetRecordsWithOptions(
            IEnumerable<KeyValuePair<string, object?>>? query = null,
            QueryOptions? options = null,
            string? tableName = null);

        /// <summary>
        /// 查詢記錄總數
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>記錄總數</returns>
        long GetRecordCount(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null);

        /// <summary>
        /// 查詢是否存在符合條件的記錄
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>如果存在則返回 true，否則返回 false</returns>
        bool Exists(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null);

        /// <summary>
        /// 根據條件查詢單一記錄（如果有多筆則返回第一筆）
        /// </summary>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>第一筆符合條件的記錄，如果找不到則返回 null</returns>
        TableBaseModel? GetFirstRecord(IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null);

        /// <summary>
        /// 根據條件查詢單一欄位的值
        /// </summary>
        /// <typeparam name="T">欄位值的類型</typeparam>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>欄位值，如果找不到則返回 default(T)</returns>
        T? GetFieldValue<T>(string fieldName, IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null);

        /// <summary>
        /// 根據條件查詢多個欄位的值
        /// </summary>
        /// <param name="fieldNames">要查詢的欄位名稱陣列</param>
        /// <param name="query">查詢條件（多個鍵值對，使用 AND 連接）</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>包含欄位值的字典，鍵為欄位名稱，值為欄位值</returns>
        Dictionary<string, object?>? GetFieldValues(string[] fieldNames, IEnumerable<KeyValuePair<string, object?>>? query = null, string? tableName = null);
    }
}

