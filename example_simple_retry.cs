// 簡單的重試機制範例（不使用 Polly）
public class SimpleRetryService
{
    private readonly ILogger _logger;

    public SimpleRetryService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation, 
        int maxRetries = 3, 
        TimeSpan delay = default)
    {
        if (delay == default) delay = TimeSpan.FromSeconds(1);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetriableException(ex))
            {
                _logger.LogWarning("操作失敗，將在 {Delay} 後重試 ({Attempt}/{MaxRetries}): {Error}", 
                    delay, attempt + 1, maxRetries, ex.Message);
                
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // 指數退避
            }
        }

        // 如果到這裡，表示所有重試都失敗了
        throw new InvalidOperationException($"操作在 {maxRetries} 次重試後仍然失敗");
    }

    private static bool IsRetriableException(Exception ex)
    {
        return ex is TimeoutException 
            || ex is System.Data.Common.DbException
            || ex is TaskCanceledException;
    }
}
