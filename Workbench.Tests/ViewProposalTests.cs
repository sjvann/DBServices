using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Contracts;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class ViewProposalTests
{
    [TestMethod]
    public void Fixed_proposer_accepts_a_declared_link_without_changing_the_package()
    {
        var package = Package();
        var before = package.Links.Count;
        var proposer = new FixedViewProposer
        {
            Script = new ViewProposalDto
            {
                Sources = ["orders", "customers"],
                Columns = [new ViewColumnDto { Name = "qty", Source = "orders" }],
                Links =
                [
                    new ViewLinkDto
                    {
                        From = "orders",
                        FromColumn = "customerId",
                        To = "customers",
                        ToColumn = "id",
                        Reason = "訂單要帶出客戶",
                    },
                ],
                Reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["orders"] = "訂單是要看的清單",
                    ["customers"] = "客戶主檔說明客戶是誰",
                },
            },
        };

        var result = proposer.ProposeAsync(new ViewProposalRequest { Package = package, Ask = "訂單客戶" }, CancellationToken.None).Result;
        Assert.IsTrue(result.Ok, result.Message);
        Assert.IsFalse(result.Proposal!.Links[0].ViewOnly);
        Assert.AreEqual("foreign-key", result.Proposal.Links[0].Kind);
        Assert.AreEqual(before, package.Links.Count);
    }

    [TestMethod]
    public void Fixed_proposer_keeps_a_reasoned_view_only_link()
    {
        var result = ViewProposal.Gate(new ViewProposalDto
        {
            Sources = ["orders", "customers"],
            Columns = [new ViewColumnDto { Name = "qty", Source = "orders" }],
            Links =
            [
                new ViewLinkDto
                {
                    From = "orders",
                    FromColumn = "qty",
                    To = "customers",
                    ToColumn = "id",
                    ViewOnly = true,
                    Reason = "這次用數量對客戶編號",
                },
            ],
            Reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["orders"] = "訂單",
                ["customers"] = "客戶",
            },
        }, Package());
        Assert.IsTrue(result.Ok, result.Message);
        Assert.IsTrue(result.Proposal!.Links[0].ViewOnly);
        Assert.AreEqual("accepted", result.Proposal.Links[0].Kind);
    }

    [TestMethod]
    public void Fixed_proposer_rejects_a_table_outside_the_package_and_a_blank_reason()
    {
        var outside = ViewProposal.Gate(new ViewProposalDto
        {
            Sources = ["external"],
            Columns = [new ViewColumnDto { Name = "id", Source = "external" }],
            Reasons = new Dictionary<string, string> { ["external"] = "不在包內" },
        }, Package());
        Assert.IsFalse(outside.Ok);
        Assert.AreEqual("proposal_outside", outside.Code);

        var blank = ViewProposal.Gate(new ViewProposalDto
        {
            Sources = ["orders", "customers"],
            Links =
            [
                new ViewLinkDto
                {
                    From = "orders",
                    FromColumn = "qty",
                    To = "customers",
                    ToColumn = "id",
                    ViewOnly = true,
                },
            ],
            Reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["orders"] = "訂單",
                ["customers"] = "客戶",
            },
        }, Package());
        Assert.IsFalse(blank.Ok);
        Assert.AreEqual("link_reason_required", blank.Code);
    }

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
                Columns = [new AnnotationColumnDto { Name = "customerId", Note = "客戶" }, new AnnotationColumnDto { Name = "qty", Note = "數量" }],
            },
            new AnnotationTableDto
            {
                Name = "customers",
                Note = "客戶主檔",
                Columns = [new AnnotationColumnDto { Name = "id", Note = "主鍵" }],
            },
        ],
        Links = [new AnnotationLinkDto { From = "orders", FromColumn = "customerId", To = "customers", ToColumn = "id", Origin = "foreign-key" }],
    };
}
