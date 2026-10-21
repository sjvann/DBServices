using System.Data.Common;
using System.Reflection;

namespace DbServices.Api.Shared;

/// <summary>
/// 開啟一次連線，並區分缺少驅動與連不上。不保存連線字串。
/// </summary>
public static class ConnectionProbe
{
    private static readonly Dictionary<string, Driver> Drivers = new(StringComparer.Ordinal)
    {
        ["Sqlite"] = new("Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.SqliteConnection", "SQLite"),
        ["SqlServer"] = new("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlConnection", "SQL Server"),
        ["MySQL"] = new("MySql.Data", "MySql.Data.MySqlClient.MySqlConnection", "MySQL"),
        ["Oracle"] = new("Oracle.ManagedDataAccess", "Oracle.ManagedDataAccess.Client.OracleConnection", "Oracle"),
        ["PostgreSQL"] = new("Npgsql", "Npgsql.NpgsqlConnection", "PostgreSQL"),
    };

    public static ProbeResult Try(string? serverType, string? connectionString)
        => Try(serverType, connectionString, Load);

    public static DatabaseList ListDatabases(string? serverType, string? connectionString)
        => ListDatabases(serverType, connectionString, Load);

    public static DatabaseList ListDatabases(string? serverType, string? connectionString, Func<string, Assembly?> load)
    {
        var engine = serverType?.Trim() ?? "";
        var sql = CatalogSql(engine);
        if (sql is null)
            return DatabaseList.Fail("invalid_arguments", "這個種類要直接指定資料庫。");
        if (string.IsNullOrWhiteSpace(connectionString))
            return DatabaseList.Fail("invalid_arguments", "連線內容不完整。");
        if (Missing(engine, load) is { } missing)
            return DatabaseList.Fail(missing.Code, missing.Message);

        var listed = ReadNames(engine, connectionString, sql, load);
        if (listed.Ok || engine != "PostgreSQL" || !MissingCatalog(listed.Message))
            return listed;

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (Value(builder, "Database").Equals("template1", StringComparison.OrdinalIgnoreCase))
            return listed;
        builder["Database"] = "template1";
        var retry = ReadNames(engine, builder.ConnectionString, sql, load);
        return retry.Ok ? retry : listed;
    }

    public static IReadOnlyList<string> Visible(string? serverType, IReadOnlyList<string> names)
    {
        var hidden = serverType?.Trim() switch
        {
            "MySQL" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "information_schema", "performance_schema", "mysql", "sys" },
            "SqlServer" => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tempdb" },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var kept = Clean(names.Where(name => !hidden.Contains(name.Trim())));
        return kept.Count == 0 ? Clean(names) : kept;
    }

    public static ProbeResult Try(string? serverType, string? connectionString, Func<string, Assembly?> load)
    {
        var engine = serverType?.Trim() ?? "";
        if (!Drivers.ContainsKey(engine))
            return ProbeResult.Fail("invalid_arguments", "請選擇資料庫種類。");
        if (string.IsNullOrWhiteSpace(connectionString))
            return ProbeResult.Fail("invalid_arguments", "連線內容不完整。");
        if (Missing(engine, load) is { } missing)
            return missing;
        return engine == "Sqlite"
            ? ProbeSqlite(connectionString, load)
            : Open(engine, connectionString, load);
    }

    private static ProbeResult ProbeSqlite(string connectionString, Func<string, Assembly?> load)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var source = Value(builder, "Data Source");
        var mode = Value(builder, "Mode");
        if (source.Length == 0)
            return ProbeResult.Fail("invalid_arguments", "連線內容不完整。");
        if (source == ":memory:" || mode.Equals("Memory", StringComparison.OrdinalIgnoreCase))
            return Open("Sqlite", connectionString, load);

        string full;
        try
        {
            full = Path.GetFullPath(source);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProbeResult.Fail("connection_failed", "檔案路徑無法使用。");
        }

        var existed = File.Exists(full);
        if (!existed && mode.Equals("ReadOnly", StringComparison.OrdinalIgnoreCase))
            return ProbeResult.Fail("connection_failed", "檔案不存在。");

        var directory = Path.GetDirectoryName(full);
        var directoryExisted = string.IsNullOrEmpty(directory) || Directory.Exists(directory);
        if (!directoryExisted)
        {
            try
            {
                Directory.CreateDirectory(directory!);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ProbeResult.Fail("connection_failed", "無法建立資料庫所在的資料夾。");
            }
        }

        var opened = Open("Sqlite", connectionString, load);
        CleanupSqlite(full, existed, directory, directoryExisted, load);
        if (!opened.Ok || existed)
            return opened;
        return File.Exists(full)
            ? ProbeResult.Success("已連上資料庫。")
            : ProbeResult.Success("已連上資料庫。這個路徑可以建立檔案，測試沒有留下檔案。");
    }

    private static DatabaseList ReadNames(string serverType, string connectionString, string sql, Func<string, Assembly?> load)
    {
        var driver = Drivers[serverType];
        Assembly? assembly;
        try
        {
            assembly = load(driver.Assembly);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return DatabaseList.Fail(Missing(serverType).Code, Missing(serverType).Message);
        }

        if (assembly is null)
        {
            var missing = Missing(serverType);
            return DatabaseList.Fail(missing.Code, missing.Message);
        }

        try
        {
            var type = assembly.GetType(driver.Type, throwOnError: true)!;
            var connection = (DbConnection)Activator.CreateInstance(type, WithTimeout(serverType, connectionString))!;
            using (connection)
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                using var reader = command.ExecuteReader();
                var names = new List<string>();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0))
                        continue;
                    var name = reader.GetValue(0)?.ToString()?.Trim() ?? "";
                    if (name.Length > 0)
                        names.Add(name);
                }

                return DatabaseList.Success(Visible(serverType, names));
            }
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            var missing = Missing(serverType);
            return DatabaseList.Fail(missing.Code, missing.Message);
        }
        catch (Exception exception)
        {
            return DatabaseList.Fail("connection_failed", Describe(exception, connectionString));
        }
    }

    private static string? CatalogSql(string engine) => engine switch
    {
        "PostgreSQL" => "SELECT datname FROM pg_database WHERE datallowconn AND NOT datistemplate ORDER BY datname",
        "MySQL" => "SHOW DATABASES",
        "SqlServer" => "SELECT name FROM sys.databases WHERE state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name",
        _ => null,
    };

    private static bool MissingCatalog(string message)
        => message.Contains("3D000", StringComparison.OrdinalIgnoreCase)
           || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
           || message.Contains("不存在", StringComparison.Ordinal);

    private static List<string> Clean(IEnumerable<string> names)
        => names
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static ProbeResult Open(string serverType, string connectionString, Func<string, Assembly?> load)
    {
        var driver = Drivers[serverType];
        Assembly? assembly;
        try
        {
            assembly = load(driver.Assembly);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return Missing(serverType);
        }

        if (assembly is null)
            return Missing(serverType);

        try
        {
            var type = assembly.GetType(driver.Type, throwOnError: true)!;
            var connection = (DbConnection)Activator.CreateInstance(type, WithTimeout(serverType, connectionString))!;
            using (connection)
                connection.Open();
            return ProbeResult.Success("已連上資料庫。");
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return Missing(serverType);
        }
        catch (Exception exception)
        {
            return ProbeResult.Fail("connection_failed", Describe(exception, connectionString));
        }
    }

    private static ProbeResult? Missing(string serverType, Func<string, Assembly?> load)
    {
        try
        {
            if (load(Drivers[serverType].Assembly) is null)
                return Missing(serverType);
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return Missing(serverType);
        }

        return null;
    }

    private static ProbeResult Missing(string serverType)
    {
        var driver = Drivers[serverType];
        return ProbeResult.Fail("driver_missing", $"缺少 {driver.Product} 驅動程式（{driver.Assembly}）。");
    }

    private static void CleanupSqlite(string fullPath, bool existed, string? directory, bool directoryExisted, Func<string, Assembly?> load)
    {
        if (existed)
            return;
        try
        {
            load("Microsoft.Data.Sqlite")
                ?.GetType("Microsoft.Data.Sqlite.SqliteConnection")
                ?.GetMethod("ClearAllPools", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            if (!directoryExisted
                && !string.IsNullOrEmpty(directory)
                && Directory.Exists(directory)
                && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TargetInvocationException)
        {
        }
    }

    private static string WithTimeout(string serverType, string connectionString)
    {
        var key = serverType switch
        {
            "SqlServer" => "Connect Timeout",
            "MySQL" or "Oracle" => "Connection Timeout",
            "PostgreSQL" => "Timeout",
            _ => "",
        };
        if (key.Length == 0)
            return connectionString;
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder[key] = 5;
        return builder.ConnectionString;
    }

    private static string Describe(Exception exception, string connectionString)
    {
        var detail = exception.GetBaseException().Message.ReplaceLineEndings(" ").Trim();
        if (detail.Length > 160)
            detail = detail[..160];
        if (detail.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("Pwd=", StringComparison.OrdinalIgnoreCase)
            || (connectionString.Length > 0 && detail.Contains(connectionString, StringComparison.Ordinal)))
        {
            detail = "";
        }

        return detail.Length == 0
            ? "連不上資料庫。請檢查主機、連接埠、帳號與密碼。"
            : "連不上資料庫。" + detail;
    }

    private static bool IsMissing(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is FileNotFoundException or DllNotFoundException or TypeLoadException or FileLoadException or BadImageFormatException or TypeInitializationException)
                return true;
        }

        return false;
    }

    private static Assembly? Load(string assemblyName)
    {
        try
        {
            return Assembly.Load(new AssemblyName(assemblyName));
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return null;
        }
    }

    private static string Value(DbConnectionStringBuilder builder, string key)
        => builder.TryGetValue(key, out var value) ? value?.ToString()?.Trim() ?? "" : "";

    private sealed record Driver(string Assembly, string Type, string Product);
}

public sealed class DatabaseList
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Names { get; init; } = [];

    public static DatabaseList Success(IReadOnlyList<string> names) => new() { Ok = true, Names = names };

    public static DatabaseList Fail(string code, string message) => new() { Code = code, Message = message };
}

public sealed class ProbeResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";

    public static ProbeResult Success(string message) => new() { Ok = true, Message = message };

    public static ProbeResult Fail(string code, string message) => new() { Code = code, Message = message };
}
