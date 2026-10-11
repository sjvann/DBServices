using Microsoft.Extensions.Configuration;

namespace GraphQLService.Seed;

public static class SeedRunner
{
    public static void Run(IConfiguration configuration)
    {
        var serverType = configuration.GetValue<string>("ServerType:Name") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("default")
            ?? "Data Source=C:\\temp\\Test.db;";

        if (string.Equals(serverType, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            if (!configuration.GetValue("SeedInventory:Enabled", true))
                return;

            var databaseName = configuration.GetValue<string>("InventoryDatabase:Name")
                ?? InventoryPostgreSqlSeeder.DefaultDatabaseName;
            var adminConnection = configuration.GetConnectionString("postgresAdmin")
                ?? BuildAdminConnectionString(connectionString);

            InventoryPostgreSqlSeeder.EnsureDatabase(adminConnection, databaseName);
            InventoryPostgreSqlSeeder.EnsureSeeded(connectionString);
            return;
        }

        if (configuration.GetValue("SeedTestData:Enabled", true))
            TestDbSeeder.EnsureSeeded(connectionString);
    }

    private static string BuildAdminConnectionString(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"
        };
        return builder.ConnectionString;
    }
}
