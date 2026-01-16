using DbServices.Core.Models;

namespace DbServices.Core.Services
{
    /// <summary>
    /// 事務管理服務介面
    /// 提供資料庫事務的統一管理
    /// </summary>
    public interface ITransactionService : IDisposable
    {
        /// <summary>
        /// 開始一個新的事務
        /// </summary>
        /// <returns>如果成功開始事務則返回 true，否則返回 false</returns>
        bool BeginTransaction();

        /// <summary>
        /// 提交當前事務
        /// </summary>
        /// <returns>如果成功提交則返回 true，否則返回 false</returns>
        bool Commit();

        /// <summary>
        /// 回滾當前事務
        /// </summary>
        /// <returns>如果成功回滾則返回 true，否則返回 false</returns>
        bool Rollback();

        /// <summary>
        /// 檢查是否在事務中
        /// </summary>
        /// <returns>如果當前在事務中則返回 true，否則返回 false</returns>
        bool IsInTransaction { get; }

        /// <summary>
        /// 在事務中執行操作
        /// </summary>
        /// <param name="action">要執行的操作</param>
        /// <returns>如果操作成功則返回 true，否則返回 false</returns>
        bool ExecuteInTransaction(Action action);

        /// <summary>
        /// 在事務中執行非同步操作
        /// </summary>
        /// <param name="action">要執行的非同步操作</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>如果操作成功則返回 true，否則返回 false</returns>
        Task<bool> ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);

        /// <summary>
        /// 在事務中執行操作並返回結果
        /// </summary>
        /// <typeparam name="T">返回值的類型</typeparam>
        /// <param name="func">要執行的操作</param>
        /// <returns>操作的結果</returns>
        T? ExecuteInTransaction<T>(Func<T> func);

        /// <summary>
        /// 在事務中執行非同步操作並返回結果
        /// </summary>
        /// <typeparam name="T">返回值的類型</typeparam>
        /// <param name="func">要執行的非同步操作</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>操作的結果</returns>
        Task<T?> ExecuteInTransactionAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default);
    }
}

