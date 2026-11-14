using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace McpService.Tests;

[TestClass]
public class AnnotationDraftTests
{
    private const string Package =
        """
        {"id":"draft-1","title":"Samples","purpose":"","status":"confirmed","active":true,"tables":[],"links":[],"dbml":""}
        """;

    [TestMethod]
    public async Task Annotation_draft_routes_keep_the_first_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "mcp-ann-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = Factory(root, [new("Workbench:CatalogKey", "unit-catalog-key")]);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var missingGet = await SendAsync(client, HttpMethod.Get, "/annotations/draft", null, "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.BadRequest, missingGet.Status);
            Assert.Contains("未提供呼叫者", missingGet.Body);
            var missingPut = await SendAsync(client, HttpMethod.Put, "/annotations/draft", null, "unit-catalog-key", Package);
            Assert.AreEqual(HttpStatusCode.BadRequest, missingPut.Status);
            Assert.Contains("未提供呼叫者", missingPut.Body);

            var wrongGet = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "林可", "wrong-key");
            Assert.AreEqual(HttpStatusCode.Forbidden, wrongGet.Status);
            Assert.Contains("目錄金鑰不符", wrongGet.Body);
            var wrongPut = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "林可", "wrong-key", Package);
            Assert.AreEqual(HttpStatusCode.Forbidden, wrongPut.Status);
            Assert.Contains("目錄金鑰不符", wrongPut.Body);

            var path = Path.Combine(root, "catalog", "annotations.json");
            Assert.IsFalse(File.Exists(path));
            var empty = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.NoContent, empty.Status);

            var saved = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "林可", "unit-catalog-key", Package);
            Assert.AreEqual(HttpStatusCode.OK, saved.Status, saved.Body);
            using (var document = JsonDocument.Parse(saved.Body))
            {
                Assert.AreEqual("draft-1", document.RootElement.GetProperty("id").GetString());
                Assert.AreEqual("draft", document.RootElement.GetProperty("status").GetString());
                Assert.IsFalse(document.RootElement.GetProperty("active").GetBoolean());
            }

            var read = await SendAsync(client, HttpMethod.Get, "/annotations/draft", "林可", "unit-catalog-key");
            Assert.AreEqual(HttpStatusCode.OK, read.Status, read.Body);
            using (var document = JsonDocument.Parse(read.Body))
            {
                Assert.AreEqual("draft-1", document.RootElement.GetProperty("id").GetString());
                Assert.AreEqual("Samples", document.RootElement.GetProperty("title").GetString());
            }

            var bytes = await File.ReadAllBytesAsync(path);
            var again = await SendAsync(client, HttpMethod.Put, "/annotations/draft", "林可", "unit-catalog-key",
                """
                {"id":"draft-2","title":"Other","purpose":"","status":"draft","active":false,"tables":[],"links":[],"dbml":""}
                """);
            Assert.AreEqual(HttpStatusCode.BadRequest, again.Status, again.Body);
            Assert.Contains("draft_exists", again.Body, StringComparison.Ordinal);
            Assert.Contains("已有工作中的草稿，沒有改。", again.Body, StringComparison.Ordinal);
            CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
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
        string? caller,
        string? key,
        string? body = null)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (caller is not null)
            request.Headers.TryAddWithoutValidation("X-DBServices-Caller", caller);
        if (key is not null)
            request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", key);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
