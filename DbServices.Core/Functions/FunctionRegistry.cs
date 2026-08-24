using DbServices.Core.Models.Interface;
using DbServices.Core.Session;

namespace DbServices.Core.Functions
{
    public sealed class FunctionRegistry : IFunctionRegistry
    {
        private readonly Dictionary<string, DbFunctionDescriptor> _functions =
            new(StringComparer.OrdinalIgnoreCase);

        public void Register(DbFunctionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            _functions[descriptor.Name] = descriptor;
        }

        public bool TryGet(string name, out DbFunctionDescriptor? descriptor)
            => _functions.TryGetValue(name, out descriptor);

        public IReadOnlyList<DbFunctionDescriptor> List() => _functions.Values.ToList();

        public static FunctionRegistry CreateDefault(IDbService db)
        {
            var registry = new FunctionRegistry();
            registry.Register(new DbFunctionDescriptor
            {
                Name = "tables",
                Description = "列出資料表",
                Handler = args =>
                {
                    var includeView = ReadBool(args, "includeView", true);
                    var names = db.GetAllTableNames(includeView) ?? [];
                    return QueryResult.FromRows("tables", ["name"], names.Select(n => (IReadOnlyList<object?>)[n]).ToList());
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "fields",
                Description = "列出欄位",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var fields = db.GetFieldsByTableName(table)?.ToList() ?? [];
                    return QueryResult.FromRows("fields",
                        ["name", "type", "pk", "notNull"],
                        fields.Select(f => (IReadOnlyList<object?>)[f.FieldName, f.FieldType, f.IsPrimaryKey, f.IsNotNull]).ToList());
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "get",
                Description = "依 Id 取一列",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var id = Convert.ToInt64(Require(args, "id"));
                    return QueryResultMapper.FromTable(db.GetRecordById(id, table), "get");
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "query",
                Description = "鍵值查詢",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var pairs = args
                        .Where(kv => !kv.Key.Equals("table", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    return QueryResultMapper.FromTable(db.GetRecordByKeyValues(pairs, table), "query");
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "insert",
                Description = "新增一列",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var pairs = args.Where(kv => !kv.Key.Equals("table", StringComparison.OrdinalIgnoreCase));
                    return QueryResultMapper.FromTable(db.InsertRecord(pairs, table), "insert");
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "update",
                Description = "依 Id 更新",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var id = Convert.ToInt64(Require(args, "id"));
                    var pairs = args.Where(kv =>
                        !kv.Key.Equals("table", StringComparison.OrdinalIgnoreCase) &&
                        !kv.Key.Equals("id", StringComparison.OrdinalIgnoreCase));
                    return QueryResultMapper.FromTable(db.UpdateRecordById(id, pairs, table), "update");
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "delete",
                Description = "依 Id 刪除",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var id = Convert.ToInt64(Require(args, "id"));
                    var ok = db.DeleteRecordById(id, table);
                    return QueryResult.FromNonQuery("delete", ok ? 1 : 0);
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "count",
                Description = "計數",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var pairs = args
                        .Where(kv => !kv.Key.Equals("table", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var count = db is IAdvancedQueryService advanced
                        ? advanced.GetRecordCount(pairs.Count == 0 ? null : pairs, table)
                        : (db.GetRecordByTableName(table)?.Records?.Count() ?? 0);
                    return QueryResult.FromRows("count", ["count"], [[count]]);
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "exists",
                Description = "是否存在",
                Handler = args =>
                {
                    var table = RequireString(args, "table");
                    var pairs = args
                        .Where(kv => !kv.Key.Equals("table", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    var exists = db is IAdvancedQueryService advanced
                        ? advanced.Exists(pairs.Count == 0 ? null : pairs, table)
                        : db.HasRecord(table);
                    return QueryResult.FromRows("exists", ["exists"], [[exists]]);
                }
            });
            registry.Register(new DbFunctionDescriptor
            {
                Name = "sql",
                Description = "參數化 SQL",
                Handler = args =>
                {
                    var sql = RequireString(args, "sql");
                    if (db is DataBaseService data)
                        return data.QuerySql(sql, args);
                    db.ExecuteSQL(sql);
                    return QueryResult.FromNonQuery("sql", 0);
                }
            });
            return registry;
        }

        private static object? Require(IReadOnlyDictionary<string, object?> args, string key)
        {
            if (!args.TryGetValue(key, out var value) || value is null)
                throw new ArgumentException($"缺少參數: {key}");
            return value;
        }

        private static string RequireString(IReadOnlyDictionary<string, object?> args, string key)
            => Convert.ToString(Require(args, key)) ?? throw new ArgumentException($"缺少參數: {key}");

        private static bool ReadBool(IReadOnlyDictionary<string, object?> args, string key, bool fallback)
        {
            if (!args.TryGetValue(key, out var value) || value is null) return fallback;
            return Convert.ToBoolean(value);
        }
    }
}
