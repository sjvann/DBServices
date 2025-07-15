namespace DbServices.Core.Configuration
{
    /// <summary>
    /// 資料庫服務設定選項
    /// </summary>
    public class DbServiceOptions
    {
        /// <summary>
        /// 連線字串
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// 命令逾時秒數（預設30秒）
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// 是否啟用查詢快取
        /// </summary>
        public bool EnableQueryCache { get; set; } = false;

        /// <summary>
        /// 快取過期時間（分鐘）
        /// </summary>
        public int CacheExpirationMinutes { get; set; } = 5;

        /// <summary>
        /// 最大重試次數
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 重試延遲秒數
        /// </summary>
        public int RetryDelaySeconds { get; set; } = 1;

        /// <summary>
        /// 是否啟用詳細日誌
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// 連線池最大大小
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// 連線池最小大小
        /// </summary>
        public int MinPoolSize { get; set; } = 5;
    }
}
