using System.Text.Json;

namespace DbServices.Core.Helpers
{
    /// <summary>
    /// JSON 輔助類別
    /// 用於處理 PostgreSQL JSON/JSONB 類型的資料
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// 將物件序列化為 JSON 字串
        /// </summary>
        /// <typeparam name="T">物件類型</typeparam>
        /// <param name="obj">要序列化的物件</param>
        /// <param name="options">JSON 序列化選項（可選）</param>
        /// <returns>JSON 字串</returns>
        public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
        {
            if (obj == null) return "null";
            return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        }

        /// <summary>
        /// 將 JSON 字串反序列化為物件
        /// </summary>
        /// <typeparam name="T">目標物件類型</typeparam>
        /// <param name="json">JSON 字串</param>
        /// <param name="options">JSON 序列化選項（可選）</param>
        /// <returns>反序列化後的物件，如果失敗則返回 default(T)</returns>
        public static T? Deserialize<T>(string? json, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return default(T);

            try
            {
                return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// 將 JSON 字串反序列化為指定類型的物件
        /// </summary>
        /// <param name="targetType">目標物件類型</param>
        /// <param name="json">JSON 字串</param>
        /// <param name="options">JSON 序列化選項（可選）</param>
        /// <returns>反序列化後的物件，如果失敗則返回 null</returns>
        public static object? Deserialize(Type targetType, string? json, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            try
            {
                return JsonSerializer.Deserialize(json, targetType, options ?? DefaultOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 驗證 JSON 字串是否有效
        /// </summary>
        /// <param name="json">要驗證的 JSON 字串</param>
        /// <returns>如果 JSON 有效則返回 true，否則返回 false</returns>
        public static bool IsValidJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 格式化 JSON 字串（美化輸出）
        /// </summary>
        /// <param name="json">JSON 字串</param>
        /// <returns>格式化後的 JSON 字串</returns>
        public static string FormatJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }
    }
}

