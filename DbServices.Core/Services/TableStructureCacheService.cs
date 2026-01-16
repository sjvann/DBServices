using DbServices.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 資料表結構快取服務
    /// </summary>
    public interface ITableStructureCacheService
    {
        /// <summary>
        /// 取得快取的欄位資訊
        /// </summary>
        IEnumerable<FieldBaseModel>? GetCachedFields(string tableName);

        /// <summary>
        /// 設定快取的欄位資訊
        /// </summary>
        void SetCachedFields(string tableName, IEnumerable<FieldBaseModel> fields);

        /// <summary>
        /// 清除指定資料表的快取
        /// </summary>
        void ClearCache(string tableName);

        /// <summary>
        /// 清除所有快取
        /// </summary>
        void ClearAllCache();

        /// <summary>
        /// 檢查快取是否有效
        /// </summary>
        bool IsCacheValid(string tableName);
    }

    /// <summary>
    /// 資料表結構快取服務實作
    /// </summary>
    public class TableStructureCacheService : ITableStructureCacheService
    {
        private readonly ConcurrentDictionary<string, CachedTableStructure> _cache = new();
        private readonly ILogger<TableStructureCacheService>? _logger;
        private readonly int _expirationMinutes;

        public TableStructureCacheService(int expirationMinutes = 5, ILogger<TableStructureCacheService>? logger = null)
        {
            _expirationMinutes = expirationMinutes;
            _logger = logger;
        }

        public IEnumerable<FieldBaseModel>? GetCachedFields(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return null;

            if (_cache.TryGetValue(tableName, out var cached) && IsCacheValid(tableName))
            {
                _logger?.LogDebug("從快取取得資料表結構: {TableName}", tableName);
                return cached.Fields;
            }

            if (cached != null)
            {
                _cache.TryRemove(tableName, out _);
                _logger?.LogDebug("快取已過期，移除: {TableName}", tableName);
            }

            return null;
        }

        public void SetCachedFields(string tableName, IEnumerable<FieldBaseModel> fields)
        {
            if (string.IsNullOrEmpty(tableName) || fields == null)
                return;

            var cached = new CachedTableStructure
            {
                TableName = tableName,
                Fields = fields.ToList(),
                CachedAt = DateTime.UtcNow
            };

            _cache.AddOrUpdate(tableName, cached, (key, oldValue) => cached);
            _logger?.LogDebug("快取資料表結構: {TableName}, 欄位數: {FieldCount}", tableName, fields.Count());
        }

        public void ClearCache(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return;

            if (_cache.TryRemove(tableName, out _))
            {
                _logger?.LogDebug("清除資料表快取: {TableName}", tableName);
            }
        }

        public void ClearAllCache()
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger?.LogInformation("清除所有快取，共 {Count} 個項目", count);
        }

        public bool IsCacheValid(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return false;

            if (!_cache.TryGetValue(tableName, out var cached))
                return false;

            var expirationTime = cached.CachedAt.AddMinutes(_expirationMinutes);
            return DateTime.UtcNow < expirationTime;
        }

        private class CachedTableStructure
        {
            public string TableName { get; set; } = string.Empty;
            public List<FieldBaseModel> Fields { get; set; } = [];
            public DateTime CachedAt { get; set; }
        }
    }
}

