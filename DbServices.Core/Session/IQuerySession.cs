namespace DbServices.Core.Session
{
    public interface IQuerySession
    {
        Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default);
        QueryResult Execute(QueryRequest request);
    }
}
