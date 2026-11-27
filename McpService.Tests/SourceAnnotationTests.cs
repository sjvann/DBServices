using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DbServices.Api.Shared;
using DbServices.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
[DoNotParallelize]
public class SourceAnnotationTests
{
    private const string ViewBody =
        """
        {"id":"ship-1","name":"出貨","purpose":"說明出貨","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[{"name":"名稱","column":"Name","source":"products"}],"columns":[{"name":"Name","source":"products"}]}
        """;

    private static readonly JsonSerializerOptions PackageJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    };

    [TestMethod]
    public async Task Source_annotation_saved_view_keeps_nine_fields()
    {
        var root = TempRoot();
        try
        {
            WriteAnnotations(root, ConfirmedLink());
            await using var factory = Factory(root, Settings());
            using var client = Client(factory);
            var saved = await SendAsync(client, HttpMethod.Put, "/views/join-1",
                """
                {"id":"join-1","name":"關聯","purpose":"訂單客戶","confirmed":true,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"CustomerId","to":"customers","toColumn":"Id"}],"parameters":[{"name":"標題","column":"Title","source":"orders"}],"columns":[{"name":"Title","source":"orders"},{"name":"Name","source":"customers"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            var before = await SendAsync(client, HttpMethod.Get, "/views/join-1");
            Assert.AreEqual(HttpStatusCode.OK, before.Status, before.Body);
            using (var document = JsonDocument.Parse(before.Body))
            {
                var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
                CollectionAssert.AreEquivalent(
                    new[] { "id", "name", "purpose", "confirmed", "published", "sources", "links", "parameters", "columns" },
                    names);
                Assert.AreEqual("關聯", document.RootElement.GetProperty("name").GetString());
                Assert.IsTrue(document.RootElement.GetProperty("confirmed").GetBoolean());
                Assert.IsFalse(document.RootElement.GetProperty("published").GetBoolean());
                var link = document.RootElement.GetProperty("links").EnumerateArray().Single();
                Assert.AreEqual("foreign-key", link.GetProperty("kind").GetString());
                Assert.AreEqual("orders", link.GetProperty("from").GetString());
                Assert.AreEqual("CustomerId", link.GetProperty("fromColumn").GetString());
                Assert.AreEqual("customers", link.GetProperty("to").GetString());
                Assert.AreEqual("Id", link.GetProperty("toColumn").GetString());
            }

            Assert.IsFalse(before.Body.Contains("connectionString", StringComparison.OrdinalIgnoreCase), before.Body);
            Assert.IsFalse(before.Body.Contains("\"rows\"", StringComparison.Ordinal), before.Body);
            var stored = File.ReadAllBytes(ViewsPath(root));

            var undeclared = await SendAsync(client, HttpMethod.Put, "/views/join-2",
                """
                {"id":"join-2","name":"未聲明","purpose":"對不上","confirmed":true,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"Title","to":"customers","toColumn":"Name"}],"parameters":[],"columns":[{"name":"Title","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, undeclared.Status, undeclared.Body);
            using (var error = JsonDocument.Parse(undeclared.Body))
            {
                Assert.AreEqual("link_undeclared", error.RootElement.GetProperty("code").GetString());
                Assert.AreEqual("這段關聯不在註記包裡。", error.RootElement.GetProperty("message").GetString());
            }

            CollectionAssert.AreEqual(stored, File.ReadAllBytes(ViewsPath(root)));
            var after = await SendAsync(client, HttpMethod.Get, "/views/join-1");
            Assert.AreEqual(before.Body, after.Body);
            Assert.AreEqual(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/views/join-2")).Status);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Source_annotation_unpublished_view_stays_out_of_discovery()
    {
        await AssertUnpublishedAbsent(package: false);
        await AssertUnpublishedAbsent(package: true);
    }

    [TestMethod]
    public async Task Source_annotation_published_view_remains_when_package_is_absent()
    {
        var dbPath = TempDb();
        var connectionString = "Data Source=" + dbPath;
        var root = TempRoot();
        try
        {
            SeedProducts(connectionString, (1, "one"), (2, "two"));
            await using var factory = Factory(root, Settings(connectionString));
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/ship-1", ViewBody)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/draft-1",
                """
                {"id":"draft-1","name":"未發布","purpose":"還沒公開","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish")).Status);

            var names = (await ListToolsAsync(client)).Select(tool => tool.GetProperty("name").GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "出貨" }, names);

            var call = await CallTool(client, "出貨", """{"connectionId":"warehouse","名稱":"one"}""");
            Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
            var result = call.Rpc.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
            var text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            using (var payload = JsonDocument.Parse(text))
            {
                var rows = payload.RootElement.GetProperty("rows");
                Assert.AreEqual(1, rows.GetArrayLength());
                Assert.AreEqual("one", rows[0][0].GetString());
            }

            Assert.IsFalse(text.Contains("two", StringComparison.Ordinal), text);
            AssertError(await CallTool(client, "source_annotation", """{"connectionId":"warehouse","sql":"select * from products"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "tables", """{"connectionId":"warehouse"}"""), "unknown_tool", "沒有這個工具。");
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Source_annotation_cites_the_active_package_without_reading_rows()
    {
        var dbPath = TempDb();
        var connectionString = "Data Source=" + dbPath;
        var root = TempRoot();
        try
        {
            SeedProducts(connectionString, (1, "business-row-secret"), (2, "other-row"));
            WriteAnnotations(root, CitedPackages());
            await using var factory = Factory(root, Settings(connectionString));
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/draft-1",
                """
                {"id":"draft-1","name":"未發布","purpose":"還沒公開","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/ship-1", ViewBody)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish")).Status);

            var tools = await ListToolsAsync(client);
            var names = tools.Select(tool => tool.GetProperty("name").GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "出貨", "source_annotation" }, names);
            var viewTool = tools[0];
            var viewProperties = viewTool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "connectionId", "名稱" }, viewProperties);
            var annotationTool = tools[1];
            Assert.AreEqual("source_annotation", annotationTool.GetProperty("name").GetString());
            var annotationProperties = annotationTool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject().ToArray();
            Assert.AreEqual(0, annotationProperties.Length);
            Assert.AreEqual(0, annotationTool.GetProperty("inputSchema").GetProperty("required").GetArrayLength());
            Assert.IsFalse(annotationTool.GetRawText().Contains("connectionId", StringComparison.OrdinalIgnoreCase), annotationTool.GetRawText());
            var annotations = annotationTool.GetProperty("annotations");
            Assert.IsTrue(annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.IsFalse(annotations.GetProperty("destructiveHint").GetBoolean());

            var viewCall = await CallTool(client, "出貨", """{"connectionId":"warehouse","名稱":"business-row-secret"}""");
            Assert.IsFalse(viewCall.Rpc.GetProperty("result").GetProperty("isError").GetBoolean(), viewCall.Body);
            Assert.Contains("business-row-secret", viewCall.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "");

            var cited = await CallTool(client, "source_annotation", """{"connectionId":"warehouse","sql":"select * from products"}""");
            var again = await CallTool(client, "Source_Annotation", """{"connectionId":"does-not-exist","sql":"select * from products"}""");
            var upper = await CallTool(client, "SOURCE_ANNOTATION", """{"sql":"select * from products","connectionId":"warehouse"}""");
            var first = CitationText(cited);
            Assert.AreEqual(first, CitationText(again));
            Assert.AreEqual(first, CitationText(upper));
            AssertCitation(first, dbPath);

            AssertError(await CallTool(client, "tables", """{"connectionId":"warehouse","sql":"select * from products"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "fields", """{"connectionId":"warehouse"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "get", """{"connectionId":"warehouse"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "query", """{"connectionId":"warehouse","sql":"select * from products"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "count", """{"connectionId":"warehouse"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "exists", """{"connectionId":"warehouse"}"""), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "insert", """{"sql":"insert into products values (3,'x')"}"""), "tool_disabled", "這個工具已關閉。");
            AssertError(await CallTool(client, "update", "{}"), "tool_disabled", "這個工具已關閉。");
            AssertError(await CallTool(client, "delete", "{}"), "tool_disabled", "這個工具已關閉。");
            AssertError(await CallTool(client, "sql", """{"sql":"select * from products"}"""), "tool_disabled", "這個工具已關閉。");

            var audit = ReadAudit(root);
            Assert.AreEqual(1, audit.Single(row => row.Tool == "出貨").RowCount);
            var citations = audit.Where(row => row.Tool == "source_annotation").ToArray();
            Assert.AreEqual(3, citations.Length);
            Assert.AreEqual(0, citations.Count(row => row.RowCount is not null));
            foreach (var row in citations)
            {
                Assert.IsFalse(row.Line.Contains("business-row-secret", StringComparison.Ordinal), row.Line);
                Assert.IsFalse(row.Line.Contains("Data Source", StringComparison.OrdinalIgnoreCase), row.Line);
                Assert.IsFalse(row.Line.Contains(Path.GetFileName(dbPath), StringComparison.OrdinalIgnoreCase), row.Line);
            }
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Source_annotation_without_active_package_is_unknown_tool()
    {
        await AssertPackageAbsent();
        await AssertPackageAbsent(DraftPackage());
        await AssertPackageAbsent(RetiredPackage());
        await AssertPackageAbsent(DraftPackage(), RetiredPackage());
    }

    [TestMethod]
    public async Task Source_annotation_reserved_name_is_duplicate_on_save_and_publish()
    {
        var fresh = TempRoot();
        try
        {
            await using var factory = Factory(fresh, Settings());
            using var client = Client(factory);
            foreach (var name in new[] { "Source_Annotation", " source_annotation ", "SOURCE_ANNOTATION" })
            {
                var rejected = await SendAsync(client, HttpMethod.Put, "/views/reserved-" + name.Trim().Length, ReservedBody(name));
                Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
                AssertDuplicate(rejected.Body);
            }

            Assert.IsFalse(File.Exists(ViewsPath(fresh)));
        }
        finally
        {
            Delete(fresh);
        }

        var seeded = TempRoot();
        try
        {
            WriteViews(seeded,
                ReservedDocument("reserved-1", "Source_Annotation"),
                new ViewDocument
                {
                    Id = "ship-1",
                    Name = "出貨",
                    Purpose = "說明出貨",
                    Confirmed = true,
                    Published = false,
                    Sources = ["products"],
                    Columns = [new ViewColumn { Name = "Name", Source = "products" }],
                });
            var before = File.ReadAllBytes(ViewsPath(seeded));
            await using var factory = Factory(seeded, Settings());
            using var client = Client(factory);
            var publish = await SendAsync(client, HttpMethod.Post, "/views/reserved-1/publish");
            Assert.AreEqual(HttpStatusCode.BadRequest, publish.Status, publish.Body);
            AssertDuplicate(publish.Body);
            var renamed = await SendAsync(client, HttpMethod.Put, "/views/reserved-2", ReservedBody("  Source_Annotation  "));
            Assert.AreEqual(HttpStatusCode.BadRequest, renamed.Status, renamed.Body);
            AssertDuplicate(renamed.Body);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(ViewsPath(seeded)));

            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish")).Status);
            var reserved = await SendAsync(client, HttpMethod.Get, "/views/reserved-1");
            using var document = JsonDocument.Parse(reserved.Body);
            Assert.IsFalse(document.RootElement.GetProperty("published").GetBoolean());
            Assert.AreEqual("Source_Annotation", document.RootElement.GetProperty("name").GetString());
            var names = (await ListToolsAsync(client)).Select(tool => tool.GetProperty("name").GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "出貨" }, names);
        }
        finally
        {
            Delete(seeded);
        }
    }

    [TestMethod]
    public async Task Source_annotation_unreadable_file_keeps_published_views()
    {
        var dbPath = TempDb();
        var connectionString = "Data Source=" + dbPath;
        var root = TempRoot();
        try
        {
            SeedProducts(connectionString, (1, "one"), (2, "two"));
            Directory.CreateDirectory(Path.Combine(root, "catalog"));
            File.WriteAllText(Path.Combine(root, "catalog", "annotations.json"), "{");
            await using var factory = Factory(root, Settings(connectionString));
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/ship-1", ViewBody)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish")).Status);

            var names = (await ListToolsAsync(client)).Select(tool => tool.GetProperty("name").GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "出貨" }, names);
            AssertError(await CallTool(client, "source_annotation", """{"connectionId":"warehouse","sql":"select * from products"}"""), "unknown_tool", "沒有這個工具。");

            var call = await CallTool(client, "出貨", """{"connectionId":"warehouse","名稱":"one"}""");
            Assert.IsFalse(call.Rpc.GetProperty("result").GetProperty("isError").GetBoolean(), call.Body);
            var text = call.Rpc.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            using var payload = JsonDocument.Parse(text);
            Assert.AreEqual("one", payload.RootElement.GetProperty("rows")[0][0].GetString());
            Assert.IsFalse(text.Contains(connectionString, StringComparison.Ordinal), text);
        }
        finally
        {
            ReleaseSqlite();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
            Delete(root);
        }
    }

    private static async Task AssertUnpublishedAbsent(bool package)
    {
        var root = TempRoot();
        try
        {
            if (package)
                WriteAnnotations(root, ConfirmedLink());
            await using var factory = Factory(root, Settings());
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/draft-1",
                """
                {"id":"draft-1","name":"未發布","purpose":"還沒公開","confirmed":true,"published":false,"sources":["products"],"links":[],"parameters":[],"columns":[{"name":"Name","source":"products"}]}
                """)).Status);
            var names = (await ListToolsAsync(client)).Select(tool => tool.GetProperty("name").GetString()).ToArray();
            Assert.IsFalse(names.Contains("未發布"));
            if (package)
                CollectionAssert.AreEqual(new[] { "source_annotation" }, names);
            else
                Assert.AreEqual(0, names.Length);
        }
        finally
        {
            Delete(root);
        }
    }

    private static async Task AssertPackageAbsent(params AnnotationPackage[] packages)
    {
        var root = TempRoot();
        try
        {
            if (packages.Length > 0)
                WriteAnnotations(root, packages);
            await using var factory = Factory(root, Settings());
            using var client = Client(factory);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put, "/views/ship-1", ViewBody)).Status);
            Assert.AreEqual(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/views/ship-1/publish")).Status);
            var names = (await ListToolsAsync(client)).Select(tool => tool.GetProperty("name").GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { "出貨" }, names);

            AssertError(await CallTool(client, "source_annotation", "{}"), "unknown_tool", "沒有這個工具。");
            AssertError(await CallTool(client, "SOURCE_ANNOTATION", """{"connectionId":"warehouse","sql":"select 1"}"""), "unknown_tool", "沒有這個工具。");
            foreach (var name in new[] { "tables", "fields", "get", "query", "count", "exists" })
                AssertError(await CallTool(client, name, """{"connectionId":"warehouse","sql":"select 1"}"""), "unknown_tool", "沒有這個工具。");
            foreach (var name in new[] { "insert", "update", "delete", "sql" })
                AssertError(await CallTool(client, name, """{"sql":"select 1"}"""), "tool_disabled", "這個工具已關閉。");
        }
        finally
        {
            Delete(root);
        }
    }

    private static void AssertCitation(string text, string dbPath)
    {
        Assert.IsFalse(text.Contains("business-row-secret", StringComparison.Ordinal), text);
        Assert.IsFalse(text.Contains("other-row", StringComparison.Ordinal), text);
        Assert.IsFalse(text.Contains("Data Source", StringComparison.OrdinalIgnoreCase), text);
        Assert.IsFalse(text.Contains(Path.GetFileName(dbPath), StringComparison.OrdinalIgnoreCase), text);
        Assert.IsFalse(text.Contains("\"rows\"", StringComparison.Ordinal), text);
        foreach (var marker in new[]
        {
            "hidden-hname", "secret-pii", "hidden-index", "hidden-index-note", "hidden-dbml", "hidden-change",
            "blank-from", "dropped-end", "guessed-origin", "draft-table", "draft-note", "draft-column", "draft-link",
            "retired-table", "retired-note", "retired-column", "retired-link",
        })
            Assert.IsFalse(text.Contains(marker, StringComparison.Ordinal), marker + " / " + text);

        foreach (var key in new[] { "others", "dbml", "indexes", "hName", "pii", "changes", "active", "status", "connectionString" })
            Assert.IsFalse(text.Contains("\"" + key + "\"", StringComparison.Ordinal), key);

        using var payload = JsonDocument.Parse(text);
        var root = payload.RootElement;
        CollectionAssert.AreEquivalent(new[] { "tables", "links" }, root.EnumerateObject().Select(property => property.Name).ToArray());
        var tables = root.GetProperty("tables").EnumerateArray().ToArray();
        Assert.AreEqual(2, tables.Length);
        Assert.AreEqual("orders", tables[0].GetProperty("name").GetString());
        Assert.AreEqual("訂單", tables[0].GetProperty("note").GetString());
        CollectionAssert.AreEquivalent(new[] { "name", "note", "columns" }, tables[0].EnumerateObject().Select(property => property.Name).ToArray());
        var orderColumns = tables[0].GetProperty("columns").EnumerateArray().ToArray();
        Assert.AreEqual(2, orderColumns.Length);
        Assert.AreEqual("Title", orderColumns[0].GetProperty("name").GetString());
        Assert.AreEqual("annotated-type", orderColumns[0].GetProperty("type").GetString());
        Assert.AreEqual("", orderColumns[0].GetProperty("note").GetString());
        CollectionAssert.AreEquivalent(new[] { "name", "type", "note" }, orderColumns[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("CustomerId", orderColumns[1].GetProperty("name").GetString());
        Assert.AreEqual("客戶", orderColumns[1].GetProperty("note").GetString());
        Assert.AreEqual("customers", tables[1].GetProperty("name").GetString());
        Assert.AreEqual("", tables[1].GetProperty("note").GetString());
        Assert.AreEqual("Id", tables[1].GetProperty("columns")[0].GetProperty("name").GetString());

        var links = root.GetProperty("links").EnumerateArray().ToArray();
        Assert.AreEqual(2, links.Length);
        Assert.AreEqual("orders", links[0].GetProperty("from").GetString());
        Assert.AreEqual("CustomerId", links[0].GetProperty("fromColumn").GetString());
        Assert.AreEqual("customers", links[0].GetProperty("to").GetString());
        Assert.AreEqual("Id", links[0].GetProperty("toColumn").GetString());
        Assert.AreEqual("foreign-key", links[0].GetProperty("origin").GetString());
        Assert.AreEqual("", links[0].GetProperty("note").GetString());
        CollectionAssert.AreEquivalent(
            new[] { "from", "fromColumn", "to", "toColumn", "origin", "note" },
            links[0].EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("RegionId", links[1].GetProperty("fromColumn").GetString());
        Assert.AreEqual("declared", links[1].GetProperty("origin").GetString());
        Assert.AreEqual("地區", links[1].GetProperty("note").GetString());
    }

    private static string CitationText((HttpStatusCode Status, string Body, JsonElement Rpc) call)
    {
        Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
        var result = call.Rpc.GetProperty("result");
        Assert.IsFalse(result.GetProperty("isError").GetBoolean(), call.Body);
        return result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    private static void AssertDuplicate(string body)
    {
        using var error = JsonDocument.Parse(body);
        Assert.AreEqual("duplicate_name", error.RootElement.GetProperty("code").GetString());
        Assert.AreEqual("名稱已存在。", error.RootElement.GetProperty("message").GetString());
    }

    private static string ReservedBody(string name)
        => "{\"id\":\"reserved-1\",\"name\":" + JsonSerializer.Serialize(name) + ",\"purpose\":\"不該留下\",\"confirmed\":true,\"published\":false,\"sources\":[\"products\"],\"links\":[],\"parameters\":[],\"columns\":[{\"name\":\"Name\",\"source\":\"products\"}]}";

    private static ViewDocument ReservedDocument(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Purpose = "不該發布",
            Confirmed = true,
            Published = false,
            Sources = ["products"],
            Columns = [new ViewColumn { Name = "Name", Source = "products" }],
        };

    private static AnnotationPackage ConfirmedLink()
        => new()
        {
            Id = "live",
            Title = "啟用",
            Purpose = "說明",
            Status = "confirmed",
            Active = true,
            Links =
            [
                new AnnotationLink
                {
                    From = "orders",
                    FromColumn = "CustomerId",
                    To = "customers",
                    ToColumn = "Id",
                    Origin = "foreign-key",
                },
            ],
        };

    private static AnnotationPackage DraftPackage()
        => new()
        {
            Id = "draft-1",
            Title = "草稿",
            Purpose = "草稿",
            Status = "draft",
            Active = true,
            Tables = [new AnnotationTable { Name = "draft-table", Note = "draft-note", Columns = [new AnnotationColumn { Name = "Secret", Type = "text", Note = "draft-column" }] }],
        };

    private static AnnotationPackage RetiredPackage()
        => new()
        {
            Id = "retired-1",
            Title = "退出",
            Purpose = "舊",
            Status = "confirmed",
            Active = false,
            Tables = [new AnnotationTable { Name = "retired-table", Note = "retired-note", Columns = [new AnnotationColumn { Name = "Gone", Type = "text", Note = "retired-column" }] }],
        };

    private static AnnotationPackage[] CitedPackages()
        =>
        [
            new AnnotationPackage
            {
                Id = "live",
                Title = "啟用",
                Purpose = "說明",
                Status = "confirmed",
                Active = true,
                Dbml = "hidden-dbml",
                Tables =
                [
                    new AnnotationTable
                    {
                        Name = "orders",
                        Note = "訂單",
                        Columns =
                        [
                            new AnnotationColumn { Name = "Title", Type = "annotated-type", Note = "", HName = "hidden-hname", Pii = "secret-pii" },
                            new AnnotationColumn { Name = "CustomerId", Type = "annotated-type", Note = "客戶" },
                        ],
                        Indexes = [new AnnotationIndex { Name = "hidden-index", Note = "hidden-index-note", Columns = ["Title"] }],
                    },
                    new AnnotationTable
                    {
                        Name = "customers",
                        Note = "",
                        Columns = [new AnnotationColumn { Name = "Id", Type = "annotated-type", Note = "" }],
                    },
                ],
                Links =
                [
                    new AnnotationLink { From = "orders", FromColumn = " ", To = "customers", ToColumn = "Id", Origin = "declared", Note = "blank-from" },
                    new AnnotationLink { From = "orders", FromColumn = "CustomerId", To = "customers", ToColumn = "Id", Origin = "foreign-key", Note = "" },
                    new AnnotationLink { From = "orders", FromColumn = "", To = "customers", ToColumn = "Id", Origin = "foreign-key", Note = "dropped-end" },
                    new AnnotationLink { From = "orders", FromColumn = "Title", To = "customers", ToColumn = "Name", Origin = "invented", Note = "guessed-origin" },
                    new AnnotationLink { From = "orders", FromColumn = "RegionId", To = "customers", ToColumn = "RegionId", Origin = "declared", Note = "地區" },
                ],
                Changes = [new AnnotationChange { Kind = "hidden-change", Table = "orders", Column = "Title" }],
            },
            new AnnotationPackage
            {
                Id = "draft-1",
                Title = "草稿",
                Purpose = "草稿說明",
                Status = "draft",
                Active = false,
                Tables = [new AnnotationTable { Name = "draft-table", Note = "draft-note", Columns = [new AnnotationColumn { Name = "Secret", Type = "text", Note = "draft-column" }] }],
                Links = [new AnnotationLink { From = "draft-table", FromColumn = "Secret", To = "customers", ToColumn = "Id", Origin = "declared", Note = "draft-link" }],
            },
            new AnnotationPackage
            {
                Id = "retired-1",
                Title = "退出",
                Purpose = "舊說明",
                Status = "confirmed",
                Active = false,
                Tables = [new AnnotationTable { Name = "retired-table", Note = "retired-note", Columns = [new AnnotationColumn { Name = "Gone", Type = "text", Note = "retired-column" }] }],
                Links = [new AnnotationLink { From = "retired-table", FromColumn = "Gone", To = "customers", ToColumn = "Id", Origin = "foreign-key", Note = "retired-link" }],
            },
        ];

    private static void WriteAnnotations(string root, params AnnotationPackage[] packages)
    {
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        File.WriteAllText(Path.Combine(root, "catalog", "annotations.json"), JsonSerializer.Serialize(packages, PackageJson));
    }

    private static void WriteViews(string root, params ViewDocument[] documents)
    {
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        File.WriteAllText(ViewsPath(root), JsonSerializer.Serialize(documents, PackageJson));
    }

    private static string ViewsPath(string root) => Path.Combine(root, "catalog", "views.json");

    private static void SeedProducts(string connectionString, params (int Id, string Name)[] rows)
    {
        var service = DbServiceFactory.Create(connectionString, "Sqlite");
        try
        {
            service!.ExecuteSQL("CREATE TABLE products (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
            foreach (var (id, name) in rows)
                service.ExecuteSQL("INSERT INTO products (Id, Name) VALUES (" + id + ", '" + name + "')");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    private readonly record struct AuditRow(string Tool, int? RowCount, string Line);

    private static List<AuditRow> ReadAudit(string root)
    {
        var rows = new List<AuditRow>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "audit", "mcp.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var rowCount = document.RootElement.GetProperty("rowCount");
            rows.Add(new AuditRow(
                document.RootElement.GetProperty("tool").GetString() ?? "",
                rowCount.ValueKind == JsonValueKind.Number ? rowCount.GetInt32() : null,
                line));
        }

        return rows;
    }

    private static KeyValuePair<string, string?>[] Settings(string? connectionString = null)
    {
        if (connectionString is null)
            return [new("Workbench:CatalogKey", "unit-catalog-key")];

        return
        [
            new("Workbench:CatalogKey", "unit-catalog-key"),
            new("Connections:warehouse:ServerType", "Sqlite"),
            new("Connections:warehouse:ConnectionString", connectionString),
        ];
    }

    private static string TempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-source-annotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string TempDb() => Path.Combine(Path.GetTempPath(), "source-annotation-" + Guid.NewGuid().ToString("N") + ".db");

    private static void Delete(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static void ReleaseSqlite()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            assembly.GetType("Microsoft.Data.Sqlite.SqliteConnection")?.GetMethod("ClearAllPools")?.Invoke(null, null);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        return client;
    }

    private static WebApplicationFactory<Program> Factory(string root, IEnumerable<KeyValuePair<string, string?>> settings)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(root);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                for (var index = config.Sources.Count - 1; index >= 0; index--)
                {
                    var source = config.Sources[index];
                    var name = source.GetType().Name;
                    if (source is EnvironmentVariablesConfigurationSource
                        || name.Contains("UserSecrets", StringComparison.Ordinal)
                        || name.Contains("CommandLine", StringComparison.Ordinal))
                    {
                        config.Sources.RemoveAt(index);
                    }
                }

                config.AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "*" });
                config.AddInMemoryCollection(settings);
            });
        });

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? body = null)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", "林可");
        request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", "unit-catalog-key");
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallTool(HttpClient client, string name, string arguments)
        => CallPayloadAsync(client,
            "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tools/call\",\"params\":{\"name\":" + JsonSerializer.Serialize(name) + ",\"arguments\":" + arguments + "}}");

    private static async Task<(HttpStatusCode Status, string Body, JsonElement Rpc)> CallPayloadAsync(HttpClient client, string payload)
    {
        await InitializeAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", "林可");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body, ParseRpc(body));
    }

    private static async Task<JsonElement[]> ListToolsAsync(HttpClient client)
    {
        await InitializeAsync(client);
        using var listRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""",
                Encoding.UTF8,
                "application/json"),
        };
        listRequest.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        using var listResponse = await client.SendAsync(listRequest);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode, body);
        return ParseRpc(body).GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.Clone()).ToArray();
    }

    private static async Task InitializeAsync(HttpClient client)
    {
        using var initRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"mcp-tests","version":"1.0"}}}
                """,
                Encoding.UTF8,
                "application/json"),
        };
        using var initResponse = await client.SendAsync(initRequest);
        Assert.AreEqual(HttpStatusCode.OK, initResponse.StatusCode, await initResponse.Content.ReadAsStringAsync());
    }

    private static void AssertError((HttpStatusCode Status, string Body, JsonElement Rpc) call, string code, string message)
    {
        Assert.AreEqual(HttpStatusCode.OK, call.Status, call.Body);
        var result = call.Rpc.GetProperty("result");
        Assert.IsTrue(result.GetProperty("isError").GetBoolean(), call.Body);
        using var payload = JsonDocument.Parse(result.GetProperty("content")[0].GetProperty("text").GetString() ?? "{}");
        Assert.AreEqual(code, payload.RootElement.GetProperty("code").GetString());
        Assert.AreEqual(message, payload.RootElement.GetProperty("message").GetString());
    }

    private static JsonElement ParseRpc(string body)
    {
        if (!body.Contains("data:", StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        JsonElement? last = null;
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var data = trimmed["data:".Length..].Trim();
            if (data.Length == 0)
                continue;
            using var document = JsonDocument.Parse(data);
            var clone = document.RootElement.Clone();
            if (clone.TryGetProperty("result", out _))
                return clone;
            last = clone;
        }

        return last ?? throw new AssertFailedException(body);
    }
}
