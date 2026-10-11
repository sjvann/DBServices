using DbServices.Api.Shared;
using GraphQLService.Configuration;
using GraphQLService.Models;
using GraphQLService.Seed;
using GraphQLService.Services;
using System.Text.Json;

namespace GraphQLService.Endpoints;

public static class ApiEndpoints
{
    private static readonly ProviderInfoDto[] SupportedProviders =
    [
        new()
        {
            Id = "Sqlite",
            DisplayName = "SQLite",
            DefaultConnectionString = "Data Source=C:\\temp\\Test.db;",
            ConnectionHint = "例：Data Source=C:\\data\\my.db;"
        },
        new()
        {
            Id = "SqlServer",
            DisplayName = "SQL Server",
            DefaultConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;",
            ConnectionHint = "Server=主機;Database=資料庫;..."
        },
        new()
        {
            Id = "MySQL",
            DisplayName = "MySQL",
            DefaultConnectionString = "Server=localhost;Database=mysql;User Id=root;Password=;",
            ConnectionHint = "Server=主機;Database=資料庫;User Id=...;Password=..."
        },
        new()
        {
            Id = "Oracle",
            DisplayName = "Oracle",
            DefaultConnectionString = "User Id=system;Password=oracle;Data Source=localhost/XEPDB1;",
            ConnectionHint = "User Id=...;Password=...;Data Source=..."
        },
        new()
        {
            Id = "PostgreSQL",
            DisplayName = "PostgreSQL",
            DefaultConnectionString = "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=;",
            ConnectionHint = "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=..."
        }
    ];

    public static void MapDbApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/providers", () => Results.Ok(SupportedProviders));

        api.MapPost("/seed/inventory", (IConfiguration config) =>
        {
            var connectionString = config.GetConnectionString("default");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Results.BadRequest(new { message = "未設定 ConnectionStrings:default。" });

            try
            {
                var databaseName = config.GetValue<string>("InventoryDatabase:Name")
                    ?? InventoryPostgreSqlSeeder.DefaultDatabaseName;
                var adminConnection = config.GetConnectionString("postgresAdmin")
                    ?? connectionString;

                if (!InventoryPostgreSqlSeeder.NeedsSeed(connectionString))
                {
                    return Results.Ok(new
                    {
                        message = "庫存管理資料表已存在。若要重建請先 DROP DATABASE inventory 後重試。"
                    });
                }

                InventoryPostgreSqlSeeder.EnsureDatabase(adminConnection, databaseName);
                InventoryPostgreSqlSeeder.EnsureSeeded(connectionString);
                return Results.Ok(new
                {
                    message = "PostgreSQL 庫存管理資料庫已建立。請重新啟動服務或按「連線」以載入 Schema。"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        api.MapPost("/seed/test-tables", (IConfiguration config) =>
        {
            var connectionString = config.GetConnectionString("default")
                ?? "Data Source=C:\\temp\\Test.db;";
            try
            {
                if (!TestDbSeeder.NeedsSeed(connectionString))
                {
                    return Results.Ok(new
                    {
                        message = "測試資料表已存在，未重複建立。若要重建請先刪除資料庫檔後重啟服務。"
                    });
                }

                TestDbSeeder.EnsureSeeded(connectionString);
                return Results.Ok(new
                {
                    message = "測試資料表已建立（Users、Products、Orders）。請重新啟動服務或按「連線」以載入 Schema。"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        api.MapGet("/connection/status", (DbConnectionSession session) =>
        {
            return Results.Ok(new ConnectionStatusDto
            {
                Connected = session.IsConnected,
                Provider = session.Provider,
                ConnectionString = session.ConnectionString,
                TableCount = session.Registry?.Tables.Count ?? 0
            });
        });

        api.MapPost("/connection/connect", async (
            ConnectionRequestDto request,
            DbConnectionSession session,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.ConnectionString))
                return Results.BadRequest(new { message = "請提供連線字串。" });

            try
            {
                await session.ConnectAsync(
                    request.Provider,
                    request.ConnectionString.Trim(),
                    cancellationToken);

                var reloader = app.Services.GetRequiredService<GraphQlExecutorReloader>();
                await reloader.EvictAsync(cancellationToken);

                return Results.Ok(new ConnectionStatusDto
                {
                    Connected = true,
                    Provider = session.Provider,
                    ConnectionString = session.ConnectionString,
                    TableCount = session.Registry?.Tables.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        api.MapGet("/schema/tables", (DbConnectionSession session) =>
        {
            if (!session.IsConnected)
                return Results.Ok(Array.Empty<TreeNodeDto>());

            return Results.Ok(SchemaTreeBuilder.BuildTablesTree(session.Registry));
        });

        api.MapGet("/schema/table/{tableName}", (string tableName, DbConnectionSession session) =>
        {
            if (!session.IsConnected)
                return Results.Ok(Array.Empty<TreeNodeDto>());

            return Results.Ok(SchemaTreeBuilder.BuildTableDetailTree(session.Registry, tableName));
        });

        api.MapGet("/schema/tree", (DbConnectionSession session) =>
        {
            if (!session.IsConnected)
                return Results.Ok(Array.Empty<TreeNodeDto>());

            return Results.Ok(SchemaTreeBuilder.BuildSchemaTree(session.Registry));
        });

        api.MapPost("/graphql/result-tree", async (HttpRequest httpRequest, CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(httpRequest.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
                return Results.Ok(Array.Empty<TreeNodeDto>());

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var errorTree = errors.EnumerateArray()
                        .Select((e, i) => new TreeNodeDto
                        {
                            Id = $"error.{i}",
                            Label = e.TryGetProperty("message", out var m) ? m.GetString() ?? "錯誤" : "錯誤",
                            Kind = "error",
                            Expanded = true,
                            Children = SchemaTreeBuilder.JsonToTree(e, $"error.{i}")
                        })
                        .ToList();

                    return Results.Ok(new[]
                    {
                        new TreeNodeDto
                        {
                            Id = "errors",
                            Label = "errors",
                            Kind = "folder",
                            Expanded = true,
                            Children = errorTree
                        }
                    });
                }

                if (root.TryGetProperty("data", out var data))
                    return Results.Ok(SchemaTreeBuilder.JsonToTree(data, "data"));

                return Results.Ok(SchemaTreeBuilder.JsonToTree(root, "response"));
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = $"無法解析 JSON: {ex.Message}" });
            }
        });
    }
}
