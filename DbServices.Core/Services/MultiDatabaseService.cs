using DbServices.Core.Configuration;
using DbServices.Core.Helpers;
using DbServices.Core.Models.Interface;
using Microsoft.Extensions.Logging;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 多資料庫管理服務
    /// 用於管理多個資料庫連線，支援從多個資料庫讀取資料並彙整到目標資料庫
    /// </summary>
    public interface IMultiDatabaseService
    {
        /// <summary>
        /// 註冊資料庫服務
        /// </summary>
        /// <param name="name">資料庫服務名稱</param>
        /// <param name="dbService">資料庫服務實例</param>
        void RegisterDatabase(string name, IDbService dbService);

        /// <summary>
        /// 取得已註冊的資料庫服務
        /// </summary>
        /// <param name="name">資料庫服務名稱</param>
        /// <returns>資料庫服務實例，如果不存在則返回 null</returns>
        IDbService? GetDatabase(string name);

        /// <summary>
        /// 取得所有已註冊的資料庫服務名稱
        /// </summary>
        /// <returns>資料庫服務名稱集合</returns>
        IEnumerable<string> GetRegisteredDatabaseNames();

        /// <summary>
        /// 移除已註冊的資料庫服務
        /// </summary>
        /// <param name="name">資料庫服務名稱</param>
        /// <returns>如果成功移除則返回 true，否則返回 false</returns>
        bool UnregisterDatabase(string name);

        /// <summary>
        /// 從多個來源資料庫讀取資料並彙整
        /// </summary>
        /// <param name="sourceDatabases">來源資料庫名稱陣列</param>
        /// <param name="queryFunc">查詢函數，接收資料庫服務和資料庫名稱，返回查詢結果</param>
        /// <returns>彙整後的資料</returns>
        Task<List<T>> AggregateDataFromSourcesAsync<T>(
            string[] sourceDatabases,
            Func<IDbService, string, Task<IEnumerable<T>>> queryFunc);

        /// <summary>
        /// 從多個來源資料庫讀取資料並寫入目標資料庫
        /// </summary>
        /// <param name="sourceDatabases">來源資料庫名稱陣列</param>
        /// <param name="targetDatabase">目標資料庫名稱</param>
        /// <param name="queryFunc">查詢函數</param>
        /// <param name="insertFunc">插入函數，接收目標資料庫服務和資料，返回插入結果</param>
        /// <returns>寫入的記錄數</returns>
        Task<int> AggregateAndInsertAsync<T>(
            string[] sourceDatabases,
            string targetDatabase,
            Func<IDbService, string, Task<IEnumerable<T>>> queryFunc,
            Func<IDbService, T, Task<bool>> insertFunc);
    }

    /// <summary>
    /// 多資料庫管理服務實作
    /// </summary>
    public class MultiDatabaseService : IMultiDatabaseService
    {
        private readonly Dictionary<string, IDbService> _databases = new();
        private readonly ILogger<MultiDatabaseService>? _logger;
        private readonly object _lock = new();

        public MultiDatabaseService(ILogger<MultiDatabaseService>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 註冊資料庫服務
        /// </summary>
        public void RegisterDatabase(string name, IDbService dbService)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("資料庫名稱不能為空", nameof(name));
            
            if (dbService == null)
                throw new ArgumentNullException(nameof(dbService));

            lock (_lock)
            {
                if (_databases.ContainsKey(name))
                {
                    _logger?.LogWarning("資料庫服務 {DatabaseName} 已存在，將被覆蓋", name);
                }

                _databases[name] = dbService;
                _logger?.LogInformation("已註冊資料庫服務: {DatabaseName}", name);
            }
        }

        /// <summary>
        /// 取得已註冊的資料庫服務
        /// </summary>
        public IDbService? GetDatabase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            lock (_lock)
            {
                return _databases.TryGetValue(name, out var dbService) ? dbService : null;
            }
        }

        /// <summary>
        /// 取得所有已註冊的資料庫服務名稱
        /// </summary>
        public IEnumerable<string> GetRegisteredDatabaseNames()
        {
            lock (_lock)
            {
                return _databases.Keys.ToList();
            }
        }

        /// <summary>
        /// 移除已註冊的資料庫服務
        /// </summary>
        public bool UnregisterDatabase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            lock (_lock)
            {
                var removed = _databases.Remove(name);
                if (removed)
                {
                    _logger?.LogInformation("已移除資料庫服務: {DatabaseName}", name);
                }
                return removed;
            }
        }

        /// <summary>
        /// 從多個來源資料庫讀取資料並彙整
        /// </summary>
        public async Task<List<T>> AggregateDataFromSourcesAsync<T>(
            string[] sourceDatabases,
            Func<IDbService, string, Task<IEnumerable<T>>> queryFunc)
        {
            if (sourceDatabases == null || sourceDatabases.Length == 0)
                throw new ArgumentException("來源資料庫名稱陣列不能為空", nameof(sourceDatabases));

            if (queryFunc == null)
                throw new ArgumentNullException(nameof(queryFunc));

            var results = new List<T>();
            var tasks = new List<Task<IEnumerable<T>>>();

            // 並行查詢所有來源資料庫
            foreach (var dbName in sourceDatabases)
            {
                var dbService = GetDatabase(dbName);
                if (dbService == null)
                {
                    _logger?.LogWarning("資料庫服務 {DatabaseName} 不存在，將跳過", dbName);
                    continue;
                }

                tasks.Add(queryFunc(dbService, dbName));
            }

            // 等待所有查詢完成
            var queryResults = await Task.WhenAll(tasks);

            // 彙整結果
            foreach (var result in queryResults)
            {
                if (result != null)
                {
                    results.AddRange(result);
                }
            }

            _logger?.LogInformation("從 {Count} 個來源資料庫彙整了 {RecordCount} 筆記錄", 
                sourceDatabases.Length, results.Count);

            return results;
        }

        /// <summary>
        /// 從多個來源資料庫讀取資料並寫入目標資料庫
        /// </summary>
        public async Task<int> AggregateAndInsertAsync<T>(
            string[] sourceDatabases,
            string targetDatabase,
            Func<IDbService, string, Task<IEnumerable<T>>> queryFunc,
            Func<IDbService, T, Task<bool>> insertFunc)
        {
            if (string.IsNullOrEmpty(targetDatabase))
                throw new ArgumentException("目標資料庫名稱不能為空", nameof(targetDatabase));

            if (insertFunc == null)
                throw new ArgumentNullException(nameof(insertFunc));

            var targetDb = GetDatabase(targetDatabase);
            if (targetDb == null)
                throw new InvalidOperationException($"目標資料庫服務 {targetDatabase} 不存在");

            // 從來源資料庫彙整資料
            var aggregatedData = await AggregateDataFromSourcesAsync(sourceDatabases, queryFunc);

            // 寫入目標資料庫
            int insertedCount = 0;
            foreach (var item in aggregatedData)
            {
                try
                {
                    var success = await insertFunc(targetDb, item);
                    if (success)
                    {
                        insertedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "寫入資料到目標資料庫時發生錯誤");
                    // 繼續處理下一筆記錄
                }
            }

            _logger?.LogInformation("成功寫入 {InsertedCount}/{TotalCount} 筆記錄到目標資料庫 {TargetDatabase}", 
                insertedCount, aggregatedData.Count, targetDatabase);

            return insertedCount;
        }
    }
}

