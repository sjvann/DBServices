using System.Net;
using System.Text.Json;
using DbServices.Api.Shared;
using DbServices.Mcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
[DoNotParallelize]
public class SelectCompilerTests
{
    [TestMethod]
    public void Compile_emits_one_select_for_join_filter_group_and_limit()
    {
        var single = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
        }, "Sqlite", 100, null, false);
        Assert.IsTrue(single.Ok, single.Message);
        Assert.IsTrue(single.Sql.StartsWith("SELECT ", StringComparison.Ordinal));
        Assert.Contains("LIMIT 100", single.Sql);
        Assert.IsFalse(single.Sql.Contains(';'));

        var capped = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
            Limit = 10,
        }, "Sqlite", 100, null, false);
        Assert.IsTrue(capped.Ok, capped.Message);
        Assert.Contains("LIMIT 10", capped.Sql);
        Assert.IsFalse(capped.Sql.Contains("LIMIT 100", StringComparison.Ordinal));

        var stored = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
            Limit = 10,
        }, "Sqlite", null, null, false);
        Assert.Contains("LIMIT 10", stored.Sql);

        var open = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
        }, "Sqlite", null, null, false);
        Assert.IsFalse(open.Sql.Contains("LIMIT", StringComparison.Ordinal));

        var server = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
            Limit = 10,
        }, "SqlServer", 100, null, false);
        Assert.Contains("TOP (10)", server.Sql);
        Assert.IsFalse(server.Sql.Contains("LIMIT", StringComparison.Ordinal));

        var oracle = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
            Limit = 10,
        }, "Oracle", 100, null, false);
        Assert.Contains("FETCH FIRST 10 ROWS ONLY", oracle.Sql);

        var joined = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["orders", "customers"],
            Links =
            [
                new ViewLink { From = "orders", FromColumn = "CustomerId", To = "customers", ToColumn = "Id", Join = "inner" },
            ],
            Columns = [new ViewColumn { Name = "Title", Source = "orders" }],
            Filters =
            [
                new ViewFilter { Source = "orders", Column = "Title", Operator = "=", Parameter = "標題" },
            ],
        }, "Sqlite", null, null, false);
        Assert.IsTrue(joined.Ok, joined.Message);
        Assert.Contains("INNER JOIN", joined.Sql);
        Assert.Contains("@p0", joined.Sql);
        Assert.AreEqual("標題", joined.Bindings[0].Parameter);

        var left = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["orders", "customers"],
            Links =
            [
                new ViewLink { From = "orders", FromColumn = "CustomerId", To = "customers", ToColumn = "Id", Join = "left" },
            ],
            Columns = [new ViewColumn { Name = "Title", Source = "orders" }],
        }, "Sqlite", null, null, false);
        Assert.IsTrue(left.Ok, left.Message);
        Assert.Contains("LEFT JOIN", left.Sql);

        var grouped = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["orders"],
            Columns = [new ViewColumn { Name = "CustomerId", Source = "orders" }],
            GroupBy = [new ViewGroup { Source = "orders", Column = "CustomerId" }],
            Aggregates = [new ViewAggregate { Function = "sum", Source = "orders", Column = "Qty", Alias = "Total" }],
            Having = [new ViewFilter { Column = "Total", Operator = ">", Value = "4" }],
        }, "Sqlite", null, null, false);
        Assert.IsTrue(grouped.Ok, grouped.Message);
        Assert.Contains("GROUP BY", grouped.Sql);
        Assert.Contains("HAVING", grouped.Sql);
        Assert.Contains("SUM(", grouped.Sql);

        var loose = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["orders", "customers"],
            Columns = [new ViewColumn { Name = "Title", Source = "orders" }],
        }, "Sqlite", 100, null, false);
        Assert.IsFalse(loose.Ok);
        Assert.AreEqual("來源連不起來。", loose.Message);
        Assert.AreEqual("", loose.Sql);

        var aggregate = SelectCompiler.Compile(new ViewDocument
        {
            Sources = ["orders"],
            Aggregates = [new ViewAggregate { Function = "count", Alias = "Total" }],
        }, "Sqlite", null, null, false);
        Assert.IsFalse(aggregate.Ok);
        Assert.AreEqual("沒有 GROUP BY 不能使用彙總或 HAVING。", aggregate.Message);
    }

    [TestMethod]
    public void Preview_stops_at_100_rows_and_marks_truncation()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"preview-cap-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
                for (var index = 1; index <= 101; index++)
                    service.ExecuteSQL("INSERT INTO products (Id, Name) VALUES (" + index + ", 'n" + index + "')");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var catalog = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["warehouse"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var preview = SchemaGraph.Preview(catalog, "warehouse", new ViewDocument
            {
                Sources = ["products"],
                Columns = [new ViewColumn { Name = "Name", Source = "products" }],
                OrderBy = [new ViewOrder { Source = "products", Column = "Id", Direction = "asc" }],
            }, null);
            Assert.IsTrue(preview.Ok, preview.Message);
            Assert.IsTrue(preview.Truncated);
            Assert.AreEqual(100, preview.Rows.Count);
            Assert.AreEqual("n1", preview.Rows[0][0]);
            Assert.AreEqual("n100", preview.Rows[99][0]);
            Assert.IsFalse(preview.Rows.Any(row => row.Contains("n101")));
            Assert.Contains("LIMIT 100", preview.Sql);

            var ten = SchemaGraph.Preview(catalog, "warehouse", new ViewDocument
            {
                Sources = ["products"],
                Columns = [new ViewColumn { Name = "Name", Source = "products" }],
                OrderBy = [new ViewOrder { Source = "products", Column = "Id", Direction = "asc" }],
                Limit = 10,
            }, null);
            Assert.IsTrue(ten.Ok, ten.Message);
            Assert.AreEqual(10, ten.Rows.Count);
            Assert.AreEqual("n10", ten.Rows[9][0]);
            Assert.Contains("LIMIT 10", ten.Sql);
        }
        finally
        {
            Release(dbPath);
        }
    }

    [TestMethod]
    public void Sql_dispatch_runs_the_join_once_and_blank_sql_stays_in_memory()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dispatch-sql-{Guid.NewGuid():N}.db");
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY)");
                service.ExecuteSQL("CREATE TABLE child (Id INTEGER PRIMARY KEY, ParentId INTEGER)");
                service.ExecuteSQL("INSERT INTO parent (Id) VALUES (1)");
                service.ExecuteSQL("INSERT INTO parent (Id) VALUES (2)");
                service.ExecuteSQL("INSERT INTO child (Id, ParentId) VALUES (1, 1)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var catalog = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["warehouse"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var memory = ViewDispatch.Execute(catalog, "warehouse", new ViewDocument
            {
                Sources = ["parent", "child"],
                Links = [new ViewLink { Kind = "accepted", From = "parent", FromColumn = "Id", To = "child", ToColumn = "ParentId", Join = "left" }],
                Columns = [new ViewColumn { Name = "Id", Source = "parent" }],
            }, new Dictionary<string, object?>());
            using var memoryJson = JsonDocument.Parse(memory.Json);
            Assert.AreEqual(1, memoryJson.RootElement.GetProperty("rows").GetArrayLength(), memory.Json);

            var sql = ViewDispatch.Execute(catalog, "warehouse", new ViewDocument
            {
                Sql = "SELECT 1",
                Sources = ["parent", "child"],
                Links = [new ViewLink { Kind = "accepted", From = "parent", FromColumn = "Id", To = "child", ToColumn = "ParentId", Join = "left" }],
                Columns = [new ViewColumn { Name = "Id", Source = "parent" }],
            }, new Dictionary<string, object?>());
            Assert.IsFalse(sql.IsError, sql.Json);
            using var sqlJson = JsonDocument.Parse(sql.Json);
            Assert.AreEqual(2, sqlJson.RootElement.GetProperty("rows").GetArrayLength(), sql.Json);
        }
        finally
        {
            Release(dbPath);
        }
    }

    [TestMethod]
    public void Confirm_blocks_a_view_only_endpoint_outside_the_new_package()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"confirm-link-{Guid.NewGuid():N}.db");
        var root = Path.Combine(Path.GetTempPath(), "confirm-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + dbPath;
        try
        {
            var service = DbServiceFactory.Create(connectionString, "Sqlite");
            try
            {
                service!.ExecuteSQL("CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT)");
                service.ExecuteSQL("CREATE TABLE child (Id INTEGER PRIMARY KEY, ParentId INTEGER)");
            }
            finally
            {
                (service as IDisposable)?.Dispose();
            }

            var path = Path.Combine(root, "annotations.json");
            var draft = new AnnotationPackage
            {
                Id = "draft-1",
                Purpose = "有說明",
                Status = "draft",
                Tables =
                [
                    new AnnotationTable { Name = "parent", Columns = [new AnnotationColumn { Name = "Id", Type = "int" }, new AnnotationColumn { Name = "Name", Type = "text" }] },
                    new AnnotationTable { Name = "child", Columns = [new AnnotationColumn { Name = "Id", Type = "int" }, new AnnotationColumn { Name = "ParentId", Type = "int" }] },
                ],
            };
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { draft }));
            var catalog = new AnnotationCatalog(path);
            var tools = new ToolCatalog(new Dictionary<string, ConnectionSetting>
            {
                ["PlatformDB"] = new() { ServerType = "Sqlite", ConnectionString = connectionString },
            });
            var bytes = File.ReadAllBytes(path);
            var blocked = catalog.Confirm(tools, "PlatformDB",
            [
                new ViewDocument
                {
                    Name = "僅此",
                    Sources = ["parent", "child"],
                    Links =
                    [
                        new ViewLink
                        {
                            Kind = "accepted",
                            From = "parent",
                            FromColumn = "Id",
                            To = "child",
                            ToColumn = "Gone",
                            ViewOnly = true,
                            Reason = "暫時",
                        },
                    ],
                    Columns = [new ViewColumn { Name = "Name", Source = "parent" }],
                },
            ]);
            Assert.AreEqual("confirm_blocked", blocked.Code);
            Assert.Contains("僅此", blocked.Message);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            Release(dbPath);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static void Release(string dbPath)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection")?.GetMethod("ClearAllPools")?.Invoke(null, null);
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }
}
