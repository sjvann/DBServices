using DbServices.Core.Models.Interface;
using DbServices.Core.Services;
using Microsoft.Extensions.Logging;

namespace DbServices.Core.Migration
{
    /// <summary>
    /// 資料庫遷移服務實作
    /// </summary>
    public class MigrationService : IMigrationService
    {
        private readonly IDbService _dbService;
        private readonly ILogger<MigrationService>? _logger;
        private const string MigrationTableName = "__SchemaMigrations";

        public MigrationService(IDbService dbService, ILogger<MigrationService>? logger = null)
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _logger = logger;
            EnsureMigrationTableExists();
        }

        /// <summary>
        /// 確保遷移記錄表存在
        /// </summary>
        private void EnsureMigrationTableExists()
        {
            try
            {
                if (!_dbService.HasTable(MigrationTableName))
                {
                    // 建立遷移記錄表
                    var createTableSql = GetCreateMigrationTableSql();
                    _dbService.ExecuteSQL(createTableSql);
                    _logger?.LogInformation("已建立遷移記錄表: {TableName}", MigrationTableName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "建立遷移記錄表時發生錯誤");
                throw;
            }
        }

        /// <summary>
        /// 取得建立遷移記錄表的 SQL（需要由具體的資料庫提供者實作）
        /// </summary>
        private string GetCreateMigrationTableSql()
        {
            // 基本實作，可以根據資料庫類型調整
            return $@"
CREATE TABLE IF NOT EXISTS {MigrationTableName} (
    Version BIGINT PRIMARY KEY,
    Description TEXT,
    ExecutedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);";
        }

        /// <summary>
        /// 執行所有待執行的遷移
        /// </summary>
        public int MigrateUp(IEnumerable<MigrationBase> migrations)
        {
            if (migrations == null || !migrations.Any())
                return 0;

            var sortedMigrations = migrations.OrderBy(m => m.Version).ToList();
            var executedMigrations = GetExecutedMigrations().ToHashSet();
            var executedCount = 0;

            foreach (var migration in sortedMigrations)
            {
                if (executedMigrations.Contains(migration.Version))
                {
                    _logger?.LogDebug("遷移 {Version} ({Description}) 已執行，跳過", 
                        migration.Version, migration.Description);
                    continue;
                }

                try
                {
                    _logger?.LogInformation("開始執行遷移 {Version}: {Description}", 
                        migration.Version, migration.Description);

                    migration.Up(_dbService, _logger);

                    // 記錄遷移執行
                    RecordMigration(migration.Version, migration.Description);

                    executedCount++;
                    _logger?.LogInformation("遷移 {Version} 執行成功", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "執行遷移 {Version} 時發生錯誤", migration.Version);
                    throw new InvalidOperationException($"執行遷移 {migration.Version} 失敗: {ex.Message}", ex);
                }
            }

            return executedCount;
        }

        /// <summary>
        /// 執行所有待執行的遷移（非同步）
        /// </summary>
        public async Task<int> MigrateUpAsync(IEnumerable<MigrationBase> migrations, CancellationToken cancellationToken = default)
        {
            if (migrations == null || !migrations.Any())
                return 0;

            var sortedMigrations = migrations.OrderBy(m => m.Version).ToList();
            var executedMigrations = GetExecutedMigrations().ToHashSet();
            var executedCount = 0;

            foreach (var migration in sortedMigrations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (executedMigrations.Contains(migration.Version))
                {
                    _logger?.LogDebug("遷移 {Version} ({Description}) 已執行，跳過", 
                        migration.Version, migration.Description);
                    continue;
                }

                try
                {
                    _logger?.LogInformation("開始執行遷移 {Version}: {Description}", 
                        migration.Version, migration.Description);

                    // 注意：這裡假設 Up 方法支援非同步，實際可能需要調整
                    await Task.Run(() => migration.Up(_dbService, _logger), cancellationToken);

                    // 記錄遷移執行
                    await RecordMigrationAsync(migration.Version, migration.Description, cancellationToken);

                    executedCount++;
                    _logger?.LogInformation("遷移 {Version} 執行成功", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "執行遷移 {Version} 時發生錯誤", migration.Version);
                    throw new InvalidOperationException($"執行遷移 {migration.Version} 失敗: {ex.Message}", ex);
                }
            }

            return executedCount;
        }

        /// <summary>
        /// 回滾最後一個遷移
        /// </summary>
        public bool MigrateDown(IEnumerable<MigrationBase> migrations)
        {
            if (migrations == null || !migrations.Any())
                return false;

            var currentVersion = GetCurrentVersion();
            if (currentVersion == 0)
            {
                _logger?.LogWarning("沒有已執行的遷移可以回滾");
                return false;
            }

            var migration = migrations.FirstOrDefault(m => m.Version == currentVersion);
            if (migration == null)
            {
                _logger?.LogWarning("找不到版本 {Version} 的遷移", currentVersion);
                return false;
            }

            try
            {
                _logger?.LogInformation("開始回滾遷移 {Version}: {Description}", 
                    migration.Version, migration.Description);

                migration.Down(_dbService, _logger);

                // 移除遷移記錄
                RemoveMigrationRecord(currentVersion);

                _logger?.LogInformation("遷移 {Version} 回滾成功", migration.Version);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "回滾遷移 {Version} 時發生錯誤", migration.Version);
                throw new InvalidOperationException($"回滾遷移 {migration.Version} 失敗: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 回滾最後一個遷移（非同步）
        /// </summary>
        public async Task<bool> MigrateDownAsync(IEnumerable<MigrationBase> migrations, CancellationToken cancellationToken = default)
        {
            if (migrations == null || !migrations.Any())
                return false;

            var currentVersion = GetCurrentVersion();
            if (currentVersion == 0)
            {
                _logger?.LogWarning("沒有已執行的遷移可以回滾");
                return false;
            }

            var migration = migrations.FirstOrDefault(m => m.Version == currentVersion);
            if (migration == null)
            {
                _logger?.LogWarning("找不到版本 {Version} 的遷移", currentVersion);
                return false;
            }

            try
            {
                _logger?.LogInformation("開始回滾遷移 {Version}: {Description}", 
                    migration.Version, migration.Description);

                await Task.Run(() => migration.Down(_dbService, _logger), cancellationToken);

                // 移除遷移記錄
                await RemoveMigrationRecordAsync(currentVersion, cancellationToken);

                _logger?.LogInformation("遷移 {Version} 回滾成功", migration.Version);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "回滾遷移 {Version} 時發生錯誤", migration.Version);
                throw new InvalidOperationException($"回滾遷移 {migration.Version} 失敗: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 回滾到指定版本
        /// </summary>
        public int MigrateToVersion(IEnumerable<MigrationBase> migrations, long targetVersion)
        {
            if (migrations == null || !migrations.Any())
                return 0;

            var sortedMigrations = migrations.OrderByDescending(m => m.Version).ToList();
            var executedMigrations = GetExecutedMigrations().OrderByDescending(v => v).ToList();
            var rolledBackCount = 0;

            foreach (var version in executedMigrations)
            {
                if (version <= targetVersion)
                    break;

                var migration = sortedMigrations.FirstOrDefault(m => m.Version == version);
                if (migration != null)
                {
                    try
                    {
                        _logger?.LogInformation("回滾遷移 {Version}: {Description}", 
                            migration.Version, migration.Description);

                        migration.Down(_dbService, _logger);
                        RemoveMigrationRecord(version);
                        rolledBackCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "回滾遷移 {Version} 時發生錯誤", version);
                        throw;
                    }
                }
            }

            return rolledBackCount;
        }

        /// <summary>
        /// 取得當前資料庫版本
        /// </summary>
        public long GetCurrentVersion()
        {
            try
            {
                var records = _dbService.GetRecordByTableName(MigrationTableName);
                if (records?.Records != null && records.Records.Any())
                {
                    var versions = records.Records
                        .Select(r => r.GetFieldValue("Version"))
                        .Where(v => v != null)
                        .Select(v => Convert.ToInt64(v))
                        .ToList();
                    
                    return versions.Any() ? versions.Max() : 0;
                }
                return 0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取得當前版本時發生錯誤");
                return 0;
            }
        }

        /// <summary>
        /// 取得所有已執行的遷移版本
        /// </summary>
        public IEnumerable<long> GetExecutedMigrations()
        {
            try
            {
                var records = _dbService.GetRecordByTableName(MigrationTableName);
                if (records?.Records != null)
                {
                    return records.Records
                        .Select(r => r.GetFieldValue("Version"))
                        .Where(v => v != null)
                        .Select(v => Convert.ToInt64(v));
                }
                return Enumerable.Empty<long>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "取得已執行遷移時發生錯誤");
                return Enumerable.Empty<long>();
            }
        }

        /// <summary>
        /// 記錄遷移執行
        /// </summary>
        private void RecordMigration(long version, string description)
        {
            var data = new[]
            {
                new KeyValuePair<string, object?>("Version", version),
                new KeyValuePair<string, object?>("Description", description),
                new KeyValuePair<string, object?>("ExecutedAt", DateTime.Now)
            };
            _dbService.InsertRecord(data, MigrationTableName);
        }

        /// <summary>
        /// 記錄遷移執行（非同步）
        /// </summary>
        private async Task RecordMigrationAsync(long version, string description, CancellationToken cancellationToken)
        {
            var data = new[]
            {
                new KeyValuePair<string, object?>("Version", version),
                new KeyValuePair<string, object?>("Description", description),
                new KeyValuePair<string, object?>("ExecutedAt", DateTime.Now)
            };
            
            if (_dbService is IDbServiceAsync asyncService)
            {
                await asyncService.InsertRecordAsync(data, MigrationTableName, cancellationToken);
            }
            else
            {
                await Task.Run(() => _dbService.InsertRecord(data, MigrationTableName), cancellationToken);
            }
        }

        /// <summary>
        /// 移除遷移記錄
        /// </summary>
        private void RemoveMigrationRecord(long version)
        {
            var criteria = new KeyValuePair<string, object?>("Version", version);
            _dbService.DeleteRecordByKeyValue(criteria, MigrationTableName);
        }

        /// <summary>
        /// 移除遷移記錄（非同步）
        /// </summary>
        private async Task RemoveMigrationRecordAsync(long version, CancellationToken cancellationToken)
        {
            var criteria = new KeyValuePair<string, object?>("Version", version);
            await Task.Run(() => _dbService.DeleteRecordByKeyValue(criteria, MigrationTableName), cancellationToken);
        }
    }
}

