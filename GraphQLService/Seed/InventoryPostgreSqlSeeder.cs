using Npgsql;

namespace GraphQLService.Seed;

/// <summary>
/// 在本機 PostgreSQL 建立簡易庫存管理資料庫與種子資料。
/// </summary>
public static class InventoryPostgreSqlSeeder
{
    public const string MarkerTable = "products";
    public const string DefaultDatabaseName = "inventory";

    public static void EnsureDatabase(string adminConnectionString, string databaseName = DefaultDatabaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = "postgres"
        };

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();

        using var existsCmd = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name",
            connection);
        existsCmd.Parameters.AddWithValue("name", databaseName);
        var exists = existsCmd.ExecuteScalar() is not null;
        if (exists)
            return;

        using var createCmd = new NpgsqlCommand(
            $"CREATE DATABASE \"{databaseName.Replace("\"", "\"\"")}\" ENCODING 'UTF8'",
            connection);
        createCmd.ExecuteNonQuery();
    }

    public static bool NeedsSeed(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        if (!TableExists(connection, MarkerTable))
            return true;

        using var command = new NpgsqlCommand($"SELECT COUNT(1) FROM {MarkerTable}", connection);
        return Convert.ToInt64(command.ExecuteScalar()) == 0;
    }

    public static void EnsureSeeded(string connectionString)
    {
        if (!NeedsSeed(connectionString))
            return;

        var sqlPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Seed", "inventory-postgresql.sql");
        if (!File.Exists(sqlPath))
            throw new FileNotFoundException($"找不到庫存管理初始化腳本: {sqlPath}");

        var script = File.ReadAllText(sqlPath);
        ExecuteScript(connectionString, script);

        if (NeedsSeed(connectionString))
            throw new InvalidOperationException("庫存管理資料表建立失敗，請確認 Seed/inventory-postgresql.sql 已複製至輸出目錄。");
    }

    public static void ExecuteScript(string connectionString, string script)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var statement in SplitStatements(script))
            {
                using var command = new NpgsqlCommand(statement, connection, transaction);
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

    private static bool TableExists(NpgsqlConnection connection, string tableName)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT COUNT(1)
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @name
            """,
            connection);
        command.Parameters.AddWithValue("name", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
