using DbServices.Core.Catalog;
using DbServices.Core.Functions;
using DbServices.Core.Models.Interface;

namespace DbServices.Core.Session
{
    public sealed class QuerySession(IDbService db, IFunctionRegistry functions, ICatalogService catalog) : IQuerySession
    {
        public QueryResult Execute(QueryRequest request)
            => ExecuteAsync(request).GetAwaiter().GetResult();

        public Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                var opcode = request.Opcode == QueryOpcode.Auto
                    ? Classify(request.Text, functions)
                    : request.Opcode;

                var result = opcode switch
                {
                    QueryOpcode.Call => ExecuteCall(request),
                    QueryOpcode.Utility => ExecuteUtility(request),
                    _ => ExecuteSql(request)
                };
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(QueryResult.FromError(ex.Message));
            }
        }

        public static QueryOpcode Classify(string text, IFunctionRegistry functions)
        {
            var token = FirstToken(text);
            if (string.IsNullOrEmpty(token))
                return QueryOpcode.Query;
            if (IsUtility(token))
                return QueryOpcode.Utility;
            if (functions.TryGet(token, out _))
                return QueryOpcode.Call;
            return QueryOpcode.Query;
        }

        private QueryResult ExecuteCall(QueryRequest request)
        {
            var name = FirstToken(request.Text);
            if (!functions.TryGet(name, out var descriptor) || descriptor == null)
                return QueryResult.FromError($"未知函數: {name}");
            return descriptor.Handler(request.Arguments);
        }

        private QueryResult ExecuteUtility(QueryRequest request)
        {
            var token = FirstToken(request.Text).ToLowerInvariant();
            switch (token)
            {
                case "ping":
                case "test":
                    _ = catalog.ListTables();
                    return QueryResult.FromRows("ping", ["ok"], [[true]]);
                case "reload":
                    catalog.Reload();
                    return QueryResult.FromNonQuery("reload", 0);
                default:
                    return QueryResult.FromError($"未知 utility: {token}");
            }
        }

        private QueryResult ExecuteSql(QueryRequest request)
        {
            var sql = request.Text.Trim();
            if (db is DataBaseService data)
            {
                if (LooksLikeQuery(sql))
                    return data.QuerySql(sql, request.Arguments.Count == 0 ? null : request.Arguments);
                var affected = data.ExecuteSql(sql, request.Arguments.Count == 0 ? null : request.Arguments);
                return QueryResult.FromNonQuery(FirstToken(sql).ToUpperInvariant(), affected);
            }

            if (LooksLikeQuery(sql))
                return QueryResult.FromError("目前服務不支援 QuerySql");
            var rows = db.ExecuteSQL(sql);
            return QueryResult.FromNonQuery(FirstToken(sql).ToUpperInvariant(), rows);
        }

        public static bool LooksLikeQuery(string sql)
        {
            var token = FirstToken(sql);
            return token.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
                   || token.Equals("WITH", StringComparison.OrdinalIgnoreCase)
                   || token.Equals("PRAGMA", StringComparison.OrdinalIgnoreCase)
                   || token.Equals("SHOW", StringComparison.OrdinalIgnoreCase)
                   || token.Equals("EXPLAIN", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUtility(string token)
            => token.Equals("ping", StringComparison.OrdinalIgnoreCase)
               || token.Equals("test", StringComparison.OrdinalIgnoreCase)
               || token.Equals("reload", StringComparison.OrdinalIgnoreCase);

        public static string FirstToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var span = text.AsSpan().Trim();
            var i = 0;
            while (i < span.Length && !char.IsWhiteSpace(span[i]))
                i++;
            return span[..i].ToString();
        }
    }
}
