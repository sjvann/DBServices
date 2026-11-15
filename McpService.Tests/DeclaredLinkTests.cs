using System.Net;
using System.Text;
using System.Text.Json;
using DbServices.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
public class DeclaredLinkTests
{
    [TestMethod]
    public async Task Declared_link_reversed_direction_saves_accepted_kind()
    {
        var root = Root();
        try
        {
            WritePackages(
                root,
                Package(true, "draft", Link("orders", "nope", "customers", "id", "declared", "草稿理由")),
                Package(false, "confirmed", Link("orders", "other", "customers", "id", "declared", "退出理由")),
                Package(true, "confirmed", Link(" customers ", " id ", " orders ", " customerId ", "declared", "別的理由")));
            var before = await File.ReadAllBytesAsync(Annotations(root));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Put, "/views/v-declared",
                """
                {"id":"v-declared","name":"對得上","purpose":"對得上","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "id", "name", "purpose", "confirmed", "published", "sources", "links", "parameters", "columns" },
                names);
            var link = document.RootElement.GetProperty("links")[0];
            Assert.AreEqual("accepted", link.GetProperty("kind").GetString());
            Assert.AreEqual("orders", link.GetProperty("from").GetString());
            Assert.AreEqual("customerId", link.GetProperty("fromColumn").GetString());
            Assert.AreEqual("customers", link.GetProperty("to").GetString());
            Assert.AreEqual("id", link.GetProperty("toColumn").GetString());
            Assert.AreEqual("qty", document.RootElement.GetProperty("columns")[0].GetProperty("name").GetString());
            Assert.AreEqual("orders", document.RootElement.GetProperty("columns")[0].GetProperty("source").GetString());
            Assert.IsFalse(saved.Body.Contains("別的理由", StringComparison.Ordinal), saved.Body);
            Assert.IsFalse(saved.Body.Contains("link_undeclared", StringComparison.Ordinal), saved.Body);
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_foreign_key_keeps_kind()
    {
        var root = Root();
        try
        {
            WritePackages(root, Package(true, "confirmed", Link("orders", "customerId", "customers", "id", "foreign-key", "不要比較理由")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Put, "/views/v-fk",
                """
                {"id":"v-fk","name":"外鍵","purpose":"外鍵","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"accepted","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual("foreign-key", document.RootElement.GetProperty("links")[0].GetProperty("kind").GetString());
            Assert.IsFalse(saved.Body.Contains("不要比較理由", StringComparison.Ordinal), saved.Body);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_one_table_is_not_rejected()
    {
        var root = Root();
        try
        {
            WritePackages(root, Package(true, "confirmed", Link("orders", "customerId", "customers", "id", "declared", "用不到")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var saved = await SendAsync(client, HttpMethod.Put, "/views/v-one",
                """
                {"id":"v-one","name":"一張表","purpose":"一張表","confirmed":false,"published":false,"sources":["orders"],"links":[],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            Assert.IsFalse(saved.Body.Contains("link_undeclared", StringComparison.Ordinal), saved.Body);
            using var document = JsonDocument.Parse(saved.Body);
            Assert.AreEqual(1, document.RootElement.GetProperty("sources").GetArrayLength());
            Assert.AreEqual(0, document.RootElement.GetProperty("links").GetArrayLength());
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_one_segment_rejects_without_writing()
    {
        var root = Root();
        try
        {
            WritePackages(
                root,
                Package(
                    true,
                    "confirmed",
                    Link("orders", "customerId", "customers", "id", "foreign-key", ""),
                    Link("orders", "customerId", "accounts", "id", "declared", "")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var kept = await SendAsync(client, HttpMethod.Put, "/views/v-kept",
                """
                {"id":"v-kept","name":"原檢視","purpose":"原檢視","confirmed":false,"published":false,"sources":["orders"],"links":[],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, kept.Status, kept.Body);
            var viewsBefore = await File.ReadAllBytesAsync(Views(root));
            var notesBefore = await File.ReadAllBytesAsync(Annotations(root));

            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-kept",
                """
                {"id":"v-kept","name":"原檢視","purpose":"原檢視","confirmed":false,"published":false,"sources":["orders","customers","accounts"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"},{"kind":"accepted","from":"orders","fromColumn":"customerId","to":"accounts","toColumn":"ID"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("link_undeclared", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("這段關聯不在註記包裡。", error.RootElement.GetProperty("message").GetString());
            Assert.AreEqual(2, error.RootElement.EnumerateObject().Count());
            Assert.IsFalse(rejected.Body.Contains("Data Source=", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(rejected.Body.Contains("Password=", StringComparison.Ordinal), rejected.Body);
            CollectionAssert.AreEqual(viewsBefore, await File.ReadAllBytesAsync(Views(root)));
            CollectionAssert.AreEqual(notesBefore, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_without_active_package_does_not_read_live_schema()
    {
        var root = Root();
        try
        {
            WritePackages(
                root,
                Package(true, "draft", Link("orders", "customerId", "customers", "id", "foreign-key", "")),
                Package(false, "confirmed", Link("orders", "customerId", "customers", "id", "declared", "")));
            var notesBefore = await File.ReadAllBytesAsync(Annotations(root));
            await using var factory = Factory(root,
            [
                new("Connections:warehouse:ServerType", "Sqlite"),
                new("Connections:warehouse:ConnectionString", "Data Source=secret;Password=nope"),
            ]);
            using var client = factory.CreateClient();
            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-live",
                """
                {"id":"v-live","name":"沒有啟用包","purpose":"沒有啟用包","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("link_undeclared", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("這段關聯不在註記包裡。", error.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(rejected.Body.Contains("Data Source=", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(rejected.Body.Contains("Password=", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(rejected.Body.Contains("活庫", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(File.Exists(Views(root)));
            CollectionAssert.AreEqual(notesBefore, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_without_any_link_stays_disconnected()
    {
        var root = Root();
        try
        {
            WritePackages(root, Package(true, "confirmed", Link("orders", "customerId", "customers", "id", "foreign-key", "")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-open",
                """
                {"id":"v-open","name":"接不起來","purpose":"接不起來","confirmed":false,"published":false,"sources":["orders","customers"],"links":[],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("disconnected", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("來源連不起來。", error.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(rejected.Body.Contains("link_undeclared", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(File.Exists(Views(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_unreadable_package_is_execution_failed()
    {
        var root = Root();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "catalog"));
            await File.WriteAllTextAsync(Annotations(root), "{");
            var notesBefore = await File.ReadAllBytesAsync(Annotations(root));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-unread",
                """
                {"id":"v-unread","name":"讀不到","purpose":"讀不到","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("execution_failed", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("沒有保存。", error.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(rejected.Body.Contains("link_undeclared", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(rejected.Body.Contains("Data Source", StringComparison.OrdinalIgnoreCase), rejected.Body);
            Assert.IsFalse(File.Exists(Views(root)));
            CollectionAssert.AreEqual(notesBefore, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_blank_column_stays_disconnected()
    {
        var root = Root();
        try
        {
            WritePackages(root, Package(true, "confirmed", Link("orders", "customerId", "customers", "id", "foreign-key", "")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-blank",
                """
                {"id":"v-blank","name":"空白欄","purpose":"空白欄","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":" ","to":"customers","toColumn":"id"}],"parameters":[],"columns":[]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("disconnected", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("來源連不起來。", error.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(rejected.Body.Contains("link_undeclared", StringComparison.Ordinal), rejected.Body);
            Assert.IsFalse(File.Exists(Views(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_guessed_origin_is_undeclared()
    {
        var root = Root();
        try
        {
            WritePackages(root, Package(true, "confirmed", Link("orders", "customerId", "customers", "id", "guessed", "")));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var kept = await SendAsync(client, HttpMethod.Put, "/views/v-kept",
                """
                {"id":"v-kept","name":"原檢視","purpose":"原檢視","confirmed":false,"published":false,"sources":["orders"],"links":[],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.OK, kept.Status, kept.Body);
            var viewsBefore = await File.ReadAllBytesAsync(Views(root));
            var notesBefore = await File.ReadAllBytesAsync(Annotations(root));

            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-kept",
                """
                {"id":"v-kept","name":"原檢視","purpose":"原檢視","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"foreign-key","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("link_undeclared", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("這段關聯不在註記包裡。", error.RootElement.GetProperty("message").GetString());
            CollectionAssert.AreEqual(viewsBefore, await File.ReadAllBytesAsync(Views(root)));
            CollectionAssert.AreEqual(notesBefore, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    public async Task Declared_link_draft_only_match_is_undeclared()
    {
        var root = Root();
        try
        {
            WritePackages(
                root,
                Package(true, "draft", Link("orders", "customerId", "customers", "id", "declared", "")),
                Package(true, "confirmed", Link("orders", "regionId", "customers", "id", "declared", "")));
            var notesBefore = await File.ReadAllBytesAsync(Annotations(root));
            await using var factory = Factory(root, []);
            using var client = factory.CreateClient();
            var rejected = await SendAsync(client, HttpMethod.Put, "/views/v-draft",
                """
                {"id":"v-draft","name":"草稿關聯","purpose":"草稿關聯","confirmed":false,"published":false,"sources":["orders","customers"],"links":[{"kind":"accepted","from":"orders","fromColumn":"customerId","to":"customers","toColumn":"id"}],"parameters":[],"columns":[{"name":"qty","source":"orders"}]}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, rejected.Status, rejected.Body);
            using var error = JsonDocument.Parse(rejected.Body);
            Assert.AreEqual("link_undeclared", error.RootElement.GetProperty("code").GetString());
            Assert.AreEqual("這段關聯不在註記包裡。", error.RootElement.GetProperty("message").GetString());
            Assert.IsFalse(File.Exists(Views(root)));
            CollectionAssert.AreEqual(notesBefore, await File.ReadAllBytesAsync(Annotations(root)));
        }
        finally
        {
            Delete(root);
        }
    }

    private static string Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-declared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Delete(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static string Annotations(string root) => Path.Combine(root, "catalog", "annotations.json");

    private static string Views(string root) => Path.Combine(root, "catalog", "views.json");

    private static void WritePackages(string root, params AnnotationPackage[] packages)
    {
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        File.WriteAllText(Annotations(root), JsonSerializer.Serialize(packages, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
    }

    private static AnnotationPackage Package(bool active, string status, params AnnotationLink[] links) => new()
    {
        Id = status + (active ? "-on" : "-off"),
        Title = "註記",
        Purpose = "說明",
        Status = status,
        Active = active,
        Links = links.ToList(),
    };

    private static AnnotationLink Link(string from, string fromColumn, string to, string toColumn, string origin, string note) => new()
    {
        From = from,
        FromColumn = fromColumn,
        To = to,
        ToColumn = toColumn,
        Origin = origin,
        Note = note,
    };

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
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Workbench:CatalogKey"] = "unit-catalog-key" });
                config.AddInMemoryCollection(settings);
            });
        });

    private static async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpClient client, HttpMethod method, string url, string body)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", "林可");
        request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", "unit-catalog-key");
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
