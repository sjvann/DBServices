using Microsoft.Data.Sqlite;

namespace GraphQLService.Seed;

/// <summary>
/// 建立測試用資料表與範例資料。
/// </summary>
public static class TestDbSeeder
{
    public const string MarkerTable = "Users";

    public static bool NeedsSeed(string connectionString)
    {
        var normalized = NormalizeConnectionString(connectionString);
        if (!File.Exists(ExtractDataSourcePath(normalized)))
            return true;

        using var connection = new SqliteConnection(normalized);
        connection.Open();

        if (!TableExists(connection, MarkerTable))
            return true;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM [{MarkerTable}]";
        return Convert.ToInt64(command.ExecuteScalar()) == 0;
    }

    public static void EnsureSeeded(string connectionString)
    {
        var directory = System.IO.Path.GetDirectoryName(ExtractDataSourcePath(connectionString));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (!NeedsSeed(connectionString))
            return;

        var dbPath = ExtractDataSourcePath(connectionString);
        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            File.Delete(dbPath);

        var sqlPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Seed", "test-tables.sql");
        if (!File.Exists(sqlPath))
            throw new FileNotFoundException($"找不到測試資料表腳本: {sqlPath}");

        var script = File.ReadAllText(sqlPath);
        ExecuteScript(connectionString, script);

        if (NeedsSeed(connectionString))
            throw new InvalidOperationException("測試資料表建立失敗，請確認 Seed/test-tables.sql 已複製至輸出目錄。");
    }

    public static void ExecuteScript(string connectionString, string script)
    {
        using var connection = new SqliteConnection(NormalizeConnectionString(connectionString));
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var statement in SplitStatements(script))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static IEnumerable<string> SplitStatements(string script)
    {
        foreach (var chunk in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var statement = string.Join(
                Environment.NewLine,
                chunk.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal)));

            if (string.IsNullOrWhiteSpace(statement))
                continue;

            yield return statement;
        }
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        return $"Data Source={connectionString}";
    }

    private static string? ExtractDataSourcePath(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
