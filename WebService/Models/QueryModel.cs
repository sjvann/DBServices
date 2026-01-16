using System.Text.Json.Nodes;

namespace WebService.Models
{
    /// <summary>
    /// 查詢參數模型，用於封裝 HTTP 請求中的鍵值查詢參數。
    /// </summary>
    /// <remarks>
    /// 此類別支援 ASP.NET Core 的自訂模型繫結，可直接從 Query String 綁定參數。
    /// </remarks>
    public class QueryModel
    {
        /// <summary>
        /// 取得或設定查詢的鍵名。
        /// </summary>
        /// <value>查詢參數的鍵名字串，可為 <c>null</c>。</value>
        public string? Key { get; set; }

        /// <summary>
        /// 取得或設定查詢的值。
        /// </summary>
        /// <value>查詢參數的值字串，可為 <c>null</c>。</value>
        public string? Value { get; set; }

        /// <summary>
        /// 從 HTTP 請求的 Query String 中綁定並建立 <see cref="QueryModel"/> 實例。
        /// </summary>
        /// <param name="context">目前的 HTTP 上下文。</param>
        /// <returns>
        /// 包含從 Query String 解析出之 <see cref="Key"/> 與 <see cref="Value"/> 的 <see cref="QueryModel"/> 實例。
        /// </returns>
        /// <example>
        /// <code>
        /// // URL: /api/query?Key=name&amp;Value=test
        /// // 會自動綁定為 QueryModel { Key = "name", Value = "test" }
        /// </code>
        /// </example>
        public static ValueTask<QueryModel> BindAsync(HttpContext context)
        {
            string? key = context.Request.Query[nameof(Key)];
            string? value = context.Request.Query[nameof(Value)];
            var result = new QueryModel
            {
                Key = key,
                Value = value
            };

            return ValueTask.FromResult(result);
        }
    }
}
