using DbServices.Core.Models.Interface;
using Microsoft.Extensions.Logging;
using System.Data;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 事務管理服務實作
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly IDbService _dbService;
        private readonly ILogger<TransactionService>? _logger;
        private IDbTransaction? _transaction;
        private bool _disposed = false;

        /// <summary>
        /// 檢查是否在事務中
        /// </summary>
        public bool IsInTransaction => _transaction != null;

        public TransactionService(IDbService dbService, ILogger<TransactionService>? logger = null)
        {
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _logger = logger;
        }

        /// <summary>
        /// 開始一個新的事務
        /// </summary>
        public bool BeginTransaction()
        {
            if (IsInTransaction)
            {
                _logger?.LogWarning("事務已經開始，無法重複開始");
                return false;
            }

            try
            {
                if (_dbService is DataBaseService baseService)
                {
                    var connection = baseService.GetConnection();
                    if (connection != null && connection.State == ConnectionState.Open)
                    {
                        _transaction = connection.BeginTransaction();
                        _logger?.LogInformation("事務已開始");
                        return true;
                    }
                    else
                    {
                        _logger?.LogError("資料庫連線未開啟，無法開始事務");
                        return false;
                    }
                }
                else
                {
                    _logger?.LogError("資料庫服務不支援事務管理");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "開始事務時發生錯誤");
                return false;
            }
        }

        /// <summary>
        /// 提交當前事務
        /// </summary>
        public bool Commit()
        {
            if (!IsInTransaction)
            {
                _logger?.LogWarning("沒有活動的事務可以提交");
                return false;
            }

            try
            {
                _transaction?.Commit();
                _logger?.LogInformation("事務已提交");
                _transaction?.Dispose();
                _transaction = null;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "提交事務時發生錯誤");
                try
                {
                    _transaction?.Rollback();
                    _logger?.LogInformation("已回滾事務");
                }
                catch (Exception rollbackEx)
                {
                    _logger?.LogError(rollbackEx, "回滾事務時發生錯誤");
                }
                finally
                {
                    _transaction?.Dispose();
                    _transaction = null;
                }
                return false;
            }
        }

        /// <summary>
        /// 回滾當前事務
        /// </summary>
        public bool Rollback()
        {
            if (!IsInTransaction)
            {
                _logger?.LogWarning("沒有活動的事務可以回滾");
                return false;
            }

            try
            {
                _transaction?.Rollback();
                _logger?.LogInformation("事務已回滾");
                _transaction?.Dispose();
                _transaction = null;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "回滾事務時發生錯誤");
                _transaction?.Dispose();
                _transaction = null;
                return false;
            }
        }

        /// <summary>
        /// 在事務中執行操作
        /// </summary>
        public bool ExecuteInTransaction(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!BeginTransaction())
                return false;

            try
            {
                action();
                return Commit();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "在事務中執行操作時發生錯誤");
                Rollback();
                throw;
            }
        }

        /// <summary>
        /// 在事務中執行非同步操作
        /// </summary>
        public async Task<bool> ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!BeginTransaction())
                return false;

            try
            {
                await action();
                return Commit();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "在事務中執行非同步操作時發生錯誤");
                Rollback();
                throw;
            }
        }

        /// <summary>
        /// 在事務中執行操作並返回結果
        /// </summary>
        public T? ExecuteInTransaction<T>(Func<T> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            if (!BeginTransaction())
                return default(T);

            try
            {
                var result = func();
                if (Commit())
                {
                    return result;
                }
                return default(T);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "在事務中執行操作時發生錯誤");
                Rollback();
                throw;
            }
        }

        /// <summary>
        /// 在事務中執行非同步操作並返回結果
        /// </summary>
        public async Task<T?> ExecuteInTransactionAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            if (!BeginTransaction())
                return default(T);

            try
            {
                var result = await func();
                if (Commit())
                {
                    return result;
                }
                return default(T);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "在事務中執行非同步操作時發生錯誤");
                Rollback();
                throw;
            }
        }

        /// <summary>
        /// 取得當前事務（用於在操作中使用）
        /// </summary>
        /// <returns>當前事務，如果沒有活動的事務則返回 null</returns>
        public IDbTransaction? GetTransaction() => _transaction;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (IsInTransaction)
                    {
                        try
                        {
                            _transaction?.Rollback();
                            _logger?.LogWarning("事務在 Dispose 時被回滾");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "在 Dispose 時回滾事務發生錯誤");
                        }
                    }
                    _transaction?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}

