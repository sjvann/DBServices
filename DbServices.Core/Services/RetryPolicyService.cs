using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 重試政策服務
    /// </summary>
    public interface IRetryPolicyService
    {
        /// <summary>
        /// 執行資料庫操作並自動重試
        /// </summary>
        Task<T> ExecuteDatabaseOperationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// 執行連線操作並自動重試
        /// </summary>
        Task<T> ExecuteConnectionOperationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 重試政策服務實作
    /// </summary>
    public class RetryPolicyService : IRetryPolicyService
    {
        private readonly ILogger<RetryPolicyService> _logger;

        public RetryPolicyService(ILogger<RetryPolicyService> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteDatabaseOperationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(operation, 3, TimeSpan.FromSeconds(1), true, cancellationToken);
        }

        public async Task<T> ExecuteConnectionOperationAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(operation, 5, TimeSpan.FromSeconds(1), false, cancellationToken);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation, 
            int maxRetries, 
            TimeSpan baseDelay, 
            bool useExponentialBackoff,
            CancellationToken cancellationToken = default)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxRetries && IsRetriableException(ex) && !cancellationToken.IsCancellationRequested)
                {
                    lastException = ex;
                    
                    var delay = useExponentialBackoff 
                        ? TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, attempt))
                        : TimeSpan.FromSeconds(baseDelay.TotalSeconds * (attempt + 1));

                    _logger.LogWarning("操作失敗，將在 {Delay}ms 後重試 ({Attempt}/{MaxRetries}): {Error}",
                        delay.TotalMilliseconds, attempt + 1, maxRetries, ex.Message);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception)
                {
                    // 非可重試的例外或已達最大重試次數
                    throw;
                }
            }

            // 如果到這裡，表示所有重試都失敗了
            throw new InvalidOperationException($"操作在 {maxRetries} 次重試後仍然失敗", lastException);
        }

        private static bool IsRetriableException(Exception ex)
        {
            return ex is DbException
                || ex is TimeoutException
                || ex is TaskCanceledException
                || ex is InvalidOperationException;
        }
    }
}
