using System.Data.Common;

namespace DbServices.Core.Connections
{
    public interface IDbConnectionScope
    {
        T Use<T>(Func<DbConnection, T> action);
        Task<T> UseAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken cancellationToken = default);
    }
}
