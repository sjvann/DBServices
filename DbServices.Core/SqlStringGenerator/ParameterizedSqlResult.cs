namespace DbServices.Core.SqlStringGenerator
{
    /// <summary>
    /// 參數化 SQL 查詢結果
    /// 包含 SQL 語句和參數物件，用於安全的參數化查詢
    /// </summary>
    public class ParameterizedSqlResult
    {
        /// <summary>
        /// 參數化 SQL 語句（使用 @parameterName 格式）
        /// </summary>
        public string Sql { get; set; } = string.Empty;

        /// <summary>
        /// 參數物件（用於 Dapper 查詢）
        /// </summary>
        public object Parameters { get; set; } = new { };

        /// <summary>
        /// 建立參數化 SQL 結果
        /// </summary>
        /// <param name="sql">SQL 語句</param>
        /// <param name="parameters">參數物件</param>
        public ParameterizedSqlResult(string sql, object parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }
    }
}

