using Microsoft.Extensions.Logging;

namespace DbServices.Core.Migration
{
    /// <summary>
    /// 資料庫遷移服務介面
    /// </summary>
    public interface IMigrationService
    {
        /// <summary>
        /// 執行所有待執行的遷移
        /// </summary>
        /// <param name="migrations">遷移類別集合（應該按版本號排序）</param>
        /// <returns>執行的遷移數量</returns>
        int MigrateUp(IEnumerable<MigrationBase> migrations);

        /// <summary>
        /// 執行所有待執行的遷移（非同步）
        /// </summary>
        /// <param name="migrations">遷移類別集合（應該按版本號排序）</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>執行的遷移數量</returns>
        Task<int> MigrateUpAsync(IEnumerable<MigrationBase> migrations, CancellationToken cancellationToken = default);

        /// <summary>
        /// 回滾最後一個遷移
        /// </summary>
        /// <param name="migrations">遷移類別集合</param>
        /// <returns>如果成功回滾則返回 true，否則返回 false</returns>
        bool MigrateDown(IEnumerable<MigrationBase> migrations);

        /// <summary>
        /// 回滾最後一個遷移（非同步）
        /// </summary>
        /// <param name="migrations">遷移類別集合</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>如果成功回滾則返回 true，否則返回 false</returns>
        Task<bool> MigrateDownAsync(IEnumerable<MigrationBase> migrations, CancellationToken cancellationToken = default);

        /// <summary>
        /// 回滾到指定版本
        /// </summary>
        /// <param name="migrations">遷移類別集合</param>
        /// <param name="targetVersion">目標版本號</param>
        /// <returns>回滾的遷移數量</returns>
        int MigrateToVersion(IEnumerable<MigrationBase> migrations, long targetVersion);

        /// <summary>
        /// 取得當前資料庫版本
        /// </summary>
        /// <returns>當前版本號，如果沒有遷移記錄則返回 0</returns>
        long GetCurrentVersion();

        /// <summary>
        /// 取得所有已執行的遷移版本
        /// </summary>
        /// <returns>已執行的遷移版本號集合</returns>
        IEnumerable<long> GetExecutedMigrations();
    }
}

