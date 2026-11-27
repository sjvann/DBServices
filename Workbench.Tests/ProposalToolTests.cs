using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ProposalToolTests
{
    [TestMethod]
    public void List_tables_returns_names_and_notes_inside_scope_only()
    {
        var listed = AnnotationProposalTools.ListTables(Package(), ["orders"]);

        StringAssert.Contains(listed, "orders 訂單");
        Assert.IsFalse(listed.Contains("customers", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(listed.Contains("customerId", StringComparison.Ordinal));
        Assert.IsFalse(listed.Contains("token", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Table_details_include_columns_and_keep_links_until_asked()
    {
        var columns = AnnotationProposalTools.TableDetails(Package(), [], "orders", includeLinks: false);
        StringAssert.Contains(columns, "customerId");
        StringAssert.Contains(columns, "客戶編號");
        Assert.IsFalse(columns.Contains("關聯", StringComparison.Ordinal));
        Assert.IsFalse(columns.Contains("token", StringComparison.Ordinal));

        var linked = AnnotationProposalTools.TableDetails(Package(), [], "orders, customers", includeLinks: true);
        StringAssert.Contains(linked, "連接 orders.customerId -> customers.id");

        var outside = AnnotationProposalTools.TableDetails(Package(), ["orders"], "customers", includeLinks: false);
        StringAssert.Contains(outside, "不在這次範圍");
    }

    [TestMethod]
    public void Canvas_scope_adds_declared_neighbors()
    {
        var scope = AnnotationProposalTools.CanvasScope(Package(), ["orders"]);
        CollectionAssert.AreEquivalent(new[] { "orders", "customers" }, scope);
        Assert.IsFalse(scope.Contains("secrets"));
    }

    [TestMethod]
    public async Task Tool_loop_fetches_names_then_details_before_the_proposal()
    {
        var handler = new ScriptHandler();
        handler.Respond(ToolCall("call-1", "listTables", "{}"));
        handler.Respond(ToolCall("call-2", "getTableDetails", "{\"tableNames\":\"orders\",\"includeLinks\":false}"));
        handler.Respond(Proposal("orders", "qty"));
        var proposer = new ModelViewProposer(new RecordingModel(true), handler);

        var result = await proposer.ProposeAsync(Request(), CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Message);
        Assert.AreEqual("orders", result.Proposal!.Sources[0]);
        Assert.IsFalse(handler.Calls[0].Body.Contains("customerId", StringComparison.Ordinal));
        Assert.IsFalse(handler.Calls[0].Body.Contains("客戶主檔", StringComparison.Ordinal));
        StringAssert.Contains(handler.Calls[0].Body, "listTables");
        StringAssert.Contains(handler.Calls[0].Body, "getTableDetails");
        StringAssert.Contains(handler.Calls[1].Body, "orders 訂單");
        Assert.IsFalse(handler.Calls[1].Body.Contains("customerId", StringComparison.Ordinal));
        StringAssert.Contains(handler.Calls[2].Body, "customerId");
        Assert.IsFalse(handler.Calls[2].Body.Contains("token", StringComparison.Ordinal));
        Assert.IsFalse(handler.Calls[2].Body.Contains("關聯", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Missing_tool_calls_fall_back_to_names_then_named_columns()
    {
        var handler = new ScriptHandler();
        handler.Respond(Proposal("orders", "qty"));
        handler.Respond(Proposal("orders", "qty"));
        handler.Respond(Proposal("orders", "qty"));
        var proposer = new ModelViewProposer(new RecordingModel(true), handler);

        var result = await proposer.ProposeAsync(Request(), CancellationToken.None);

        Assert.IsTrue(result.Ok, result.Message);
        StringAssert.Contains(handler.Calls[1].Body, "orders 訂單");
        Assert.IsFalse(handler.Calls[1].Body.Contains("customerId", StringComparison.Ordinal));
        Assert.IsFalse(handler.Calls[1].Body.Contains("token", StringComparison.Ordinal));
        StringAssert.Contains(handler.Calls[2].Body, "customerId");
        Assert.IsFalse(handler.Calls[2].Body.Contains("token", StringComparison.Ordinal));
    }

    private static ViewProposalRequest Request() => new()
    {
        Ask = "訂單數量",
        Package = Package(),
    };

    private static string Proposal(string source, string column)
    {
        var proposal = System.Text.Json.JsonSerializer.Serialize(new
        {
            sources = new[] { source },
            columns = new[] { new { name = column, source } },
            links = Array.Empty<object>(),
            reasons = new Dictionary<string, string> { [source] = "要看的清單" },
        });
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = proposal } },
            },
        });
    }

    private static string ToolCall(string id, string name, string arguments) =>
        "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"" + id + "\",\"type\":\"function\",\"function\":{\"name\":\"" + name + "\",\"arguments\":" + System.Text.Json.JsonSerializer.Serialize(arguments) + "}}]}}]}";

    private static AnnotationPackageDto Package() => new()
    {
        Id = "active",
        Status = "confirmed",
        Active = true,
        Tables =
        [
            new AnnotationTableDto
            {
                Name = "orders",
                Note = "訂單",
                Columns =
                [
                    new AnnotationColumnDto { Name = "customerId", Type = "integer", Note = "客戶編號" },
                    new AnnotationColumnDto { Name = "qty", Type = "integer", Note = "數量" },
                ],
            },
            new AnnotationTableDto
            {
                Name = "customers",
                Note = "客戶主檔",
                Columns = [new AnnotationColumnDto { Name = "id", Type = "integer", Note = "主鍵" }],
            },
            new AnnotationTableDto
            {
                Name = "secrets",
                Note = "機密",
                Columns = [new AnnotationColumnDto { Name = "token", Type = "text", Note = "權杖" }],
            },
        ],
        Links = [new AnnotationLinkDto { From = "orders", FromColumn = "customerId", To = "customers", ToColumn = "id", Origin = "foreign-key" }],
    };

    private sealed class ScriptHandler : HttpMessageHandler
    {
        private readonly Queue<string> _bodies = new();

        public List<(string Url, string Body)> Calls { get; } = [];

        public void Respond(string body) => _bodies.Enqueue(body);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.RequestUri!.AbsoluteUri, body));
            if (_bodies.Count == 0)
                throw new InvalidOperationException("沒有下一個回應。");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_bodies.Dequeue(), Encoding.UTF8, "application/json"),
            };
        }
    }
}
