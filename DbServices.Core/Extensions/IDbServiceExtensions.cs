using DbServices.Core.Helpers;
using DbServices.Core.Models;
using DbServices.Core.Models.Interface;
using DbServices.Core.Services;
using System.Reflection;
using System.Text.Json;

namespace DbServices.Core.Extensions
{
    /// <summary>
    /// IDbService 擴充方法
    /// 提供 JSON 類型和其他便利功能
    /// </summary>
    public static class IDbServiceExtensions
    {
        /// <summary>
        /// 插入包含 JSON 欄位的記錄（PostgreSQL）
        /// </summary>
        /// <param name="dbService">資料庫服務</param>
        /// <param name="source">要插入的欄位和值</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>插入後的記錄</returns>
        public static TableBaseModel? InsertRecordWithJson<T>(
            this IDbService dbService,
            T obj,
            string? tableName = null) where T : class
        {
            var properties = typeof(T).GetProperties();
            var keyValuePairs = new List<KeyValuePair<string, object?>>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                
                // 檢查是否有 JsonAttribute 標記
                var jsonAttr = prop.GetCustomAttributes(typeof(JsonAttribute), false)
                    .FirstOrDefault() as JsonAttribute;
                
                if (jsonAttr != null && value != null)
                {
                    // 如果是 JSON 欄位，序列化為 JSON 字串
                    keyValuePairs.Add(new KeyValuePair<string, object?>(
                        prop.Name,
                        JsonHelper.Serialize(value)
                    ));
                }
                else
                {
                    keyValuePairs.Add(new KeyValuePair<string, object?>(
                        prop.Name,
                        value
                    ));
                }
            }

            return dbService.InsertRecord(keyValuePairs, tableName);
        }

        /// <summary>
        /// 取得記錄並反序列化 JSON 欄位
        /// </summary>
        /// <typeparam name="T">目標類型</typeparam>
        /// <param name="dbService">資料庫服務</param>
        /// <param name="id">記錄 ID</param>
        /// <param name="tableName">資料表名稱</param>
        /// <returns>反序列化後的物件</returns>
        public static T? GetRecordWithJson<T>(
            this IDbService dbService,
            long id,
            string? tableName = null) where T : class, new()
        {
            var record = dbService.GetRecordById(id, tableName);
            if (record?.Records?.FirstOrDefault() is RecordBaseModel rec)
            {
                var obj = new T();
                var properties = typeof(T).GetProperties();

                foreach (var prop in properties)
                {
                    var fieldValue = rec.GetFieldValue(prop.Name);
                    
                    // 檢查是否有 JsonAttribute 標記
                    var jsonAttr = prop.GetCustomAttributes(typeof(JsonAttribute), false)
                        .FirstOrDefault() as JsonAttribute;
                    
                    if (jsonAttr != null && fieldValue is string jsonString)
                    {
                        // 如果是 JSON 欄位，反序列化
                        try
                        {
                            var deserialized = JsonHelper.Deserialize(prop.PropertyType, jsonString);
                            if (deserialized != null)
                            {
                                prop.SetValue(obj, deserialized);
                            }
                        }
                        catch
                        {
                            // 反序列化失敗，跳過
                        }
                    }
                    else if (fieldValue != null)
                    {
                        try
                        {
                            var converted = Convert.ChangeType(fieldValue, prop.PropertyType);
                            prop.SetValue(obj, converted);
                        }
                        catch
                        {
                            // 轉換失敗，跳過
                        }
                    }
                }

                return obj;
            }

            return default(T);
        }
    }

    /// <summary>
    /// JSON 欄位屬性標記
    /// 用於標記應該序列化為 JSON 的欄位
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class JsonAttribute : Attribute
    {
    }
}

