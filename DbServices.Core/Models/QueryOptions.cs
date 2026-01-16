namespace DbServices.Core.Models
{
    /// <summary>
    /// 查詢選項，用於進階查詢功能
    /// </summary>
    public class QueryOptions
    {
        /// <summary>
        /// 排序欄位名稱
        /// </summary>
        public string? OrderBy { get; set; }

        /// <summary>
        /// 是否降序排列（預設為 false，即升序）
        /// </summary>
        public bool OrderByDescending { get; set; } = false;

        /// <summary>
        /// 跳過的記錄數（用於分頁）
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// 取得的記錄數（用於分頁）
        /// </summary>
        public int? Take { get; set; }

        /// <summary>
        /// 要選擇的欄位名稱（如果為 null 則選擇所有欄位）
        /// </summary>
        public string[]? SelectFields { get; set; }

        /// <summary>
        /// 是否使用參數化查詢（預設為 true，建議使用）
        /// </summary>
        public bool UseParameterizedQuery { get; set; } = true;
    }
}

