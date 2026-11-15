using System.Reflection;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class DesignCatalogTests
{
    [TestMethod]
    public async Task Design_catalog_lists_only_the_active_package()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog
        {
            Active = ConfirmedPackage(),
            Draft = DraftPackage("草稿表"),
            Others = { ExitedPackage("已退出表") },
        };
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);

        CollectionAssert.AreEqual(
            new[] { "orders", "customers", "accounts", "regions" },
            cut.FindAll(".table-pick").Select(button => button.QuerySelector("span")!.TextContent.Trim()).ToArray());
        var library = cut.Find("aside.library");
        var customer = library.QuerySelectorAll(".field-list > li").Single(row => row.QuerySelector("span")!.TextContent.Trim() == "customerId");
        Assert.IsTrue(customer.TextContent.Contains("int", StringComparison.Ordinal));
        Assert.AreEqual("尚未註記", customer.QuerySelector(".unmarked-badge")!.TextContent.Trim());
        var qty = library.QuerySelectorAll(".field-list > li").Single(row => row.QuerySelector("span")!.TextContent.Trim() == "qty");
        Assert.AreEqual(0, qty.QuerySelectorAll(".unmarked-badge").Length);
        Assert.IsTrue(qty.TextContent.Contains("數量", StringComparison.Ordinal));
        Assert.IsTrue(qty.TextContent.Contains("text", StringComparison.Ordinal));
        var badges = cut.FindAll(".unmarked-badge");
        Assert.AreEqual(3, badges.Count);
        Assert.IsTrue(badges.All(badge => badge.TextContent.Trim() == "尚未註記"));
        Assert.AreEqual(0, cut.FindAll(".empty-catalog").Count);

        var orders = library.QuerySelectorAll(".table-list > li").Single(item => item.QuerySelector(".table-pick span")!.TextContent.Trim() == "orders");
        Assert.AreEqual(0, orders.QuerySelectorAll("p").Length);
        var customers = library.QuerySelectorAll(".table-list > li").Single(item => item.QuerySelector(".table-pick span")!.TextContent.Trim() == "customers");
        Assert.AreEqual("客戶主檔", customers.QuerySelector("p")!.TextContent.Trim());

        var relations = cut.Find("[aria-label='已聲明關聯']");
        CollectionAssert.AreEqual(
            new[]
            {
                "orders.customerId → customers.id",
                "orders.customerId → accounts.id",
                "customers.regionId → regions.id",
            },
            relations.QuerySelectorAll("li").Select(item => item.TextContent.Trim()).ToArray());
        Assert.IsFalse(cut.Markup.Contains("不該出現的理由", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("地區理由", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("外部表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("草稿表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("已退出表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("個資樣本", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("索引樣本", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("DBML樣本", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("差異標記", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("活庫表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("連線字串", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("connectionString", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(1, annotations.ActiveReads);
        Assert.AreEqual(0, annotations.DraftReads);
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
        Assert.AreEqual(1, library.QuerySelectorAll(".field-list > li").Count(row => row.QuerySelector("span")!.TextContent.Trim() == "customerId"));

        await cut.Find("#table-finder").InputAsync(new ChangeEventArgs { Value = "沒有這張" });
        Assert.IsTrue(cut.Markup.Contains("沒有符合的資料表。", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
        Assert.AreEqual(0, cut.FindAll(".empty-catalog").Count);

        await cut.Find("#table-finder").InputAsync(new ChangeEventArgs { Value = "region" });
        CollectionAssert.AreEqual(
            new[] { "regions" },
            cut.FindAll(".table-pick").Select(button => button.QuerySelector("span")!.TextContent.Trim()).ToArray());
    }

    [TestMethod]
    public async Task Design_catalog_extra_edges_remain_one_column_and_stay_reachable()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        var ordersCard = cut.FindAll("article.table-card").Single(card => card.GetAttribute("aria-label") == "orders");
        Assert.AreEqual(1, ordersCard.QuerySelectorAll(".field-list > li").Count(row => row.TextContent.Contains("customerId", StringComparison.Ordinal)));
        var chips = cut.FindAll(".chip").Select(chip => chip.TextContent).ToList();
        Assert.IsTrue(chips.Any(chip => chip.Contains("customers", StringComparison.Ordinal)));
        Assert.IsTrue(chips.Any(chip => chip.Contains("accounts", StringComparison.Ordinal)));
        Assert.IsFalse(chips.Any(chip => chip.Contains("regions", StringComparison.Ordinal)));
        Assert.IsFalse(cut.Markup.Contains("活庫表", StringComparison.Ordinal));

        await cut.FindAll(".chip").Single(chip => chip.TextContent.Contains("accounts", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        Assert.IsTrue(cut.FindAll("article.table-card").Any(card => card.GetAttribute("aria-label") == "accounts"));
        ordersCard = cut.FindAll("article.table-card").Single(card => card.GetAttribute("aria-label") == "orders");
        Assert.AreEqual(1, ordersCard.QuerySelectorAll(".field-list > li").Count(row => row.TextContent.Contains("customerId", StringComparison.Ordinal)));

        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "再往外一層").ClickAsync(new MouseEventArgs());
        Assert.IsTrue(cut.FindAll(".chip").Any(chip => chip.TextContent.Contains("regions", StringComparison.Ordinal)));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
        Assert.AreEqual(1, cut.Find("aside.library").QuerySelectorAll(".field-list > li").Count(row => row.QuerySelector("span")!.TextContent.Trim() == "customerId"));
    }

    [TestMethod]
    public async Task Design_catalog_is_empty_without_an_active_package()
    {
        await AssertEmpty(new Catalog { Draft = DraftPackage("只有草稿") }, "只有草稿");
        await AssertEmpty(new Catalog(), "活庫表");
        await AssertEmpty(new Catalog { Active = ExitedPackage("已退出啟用") }, "已退出啟用");
        var markedDraft = DraftPackage("草稿卻標啟用");
        markedDraft.Active = true;
        await AssertEmpty(new Catalog { Active = markedDraft }, "草稿卻標啟用");
    }

    [TestMethod]
    public async Task Design_catalog_reread_keeps_the_previous_active_package()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog
        {
            Active = new AnnotationPackageDto
            {
                Id = "old",
                Status = "confirmed",
                Active = true,
                Tables = [Table("舊表", "", Column("舊欄", "舊型別", "舊註"))],
            },
            Draft = DraftPackage("新草稿表"),
            Others = { ExitedPackage("已退出表") },
        };
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);
        Assert.IsTrue(cut.Markup.Contains("舊型別", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("新草稿表", StringComparison.Ordinal));

        annotations.Draft = DraftPackage("更新草稿");
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());

        Assert.IsTrue(cut.FindAll(".table-pick").Any(button => button.TextContent.Contains("舊表", StringComparison.Ordinal)));
        Assert.IsTrue(cut.Markup.Contains("舊型別", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("更新草稿", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("新草稿表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("已退出表", StringComparison.Ordinal));
        Assert.AreEqual(2, annotations.ActiveReads);
        Assert.AreEqual(0, annotations.DraftReads);
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);

        await cut.FindAll(".table-pick").Single(button => button.TextContent.Contains("舊表", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.Find("article.table-card").QuerySelector(".field-list input")!.ChangeAsync(new ChangeEventArgs { Value = true });
        await RunPreview(cut);
        var rows = browser.RowReads;
        Assert.IsGreaterThan(0, rows);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());
        await Task.Delay(500);
        Assert.AreEqual(rows, browser.RowReads);
        Assert.AreEqual(0, browser.GraphCalls);

        annotations.Active = new AnnotationPackageDto
        {
            Id = "next",
            Status = "confirmed",
            Active = true,
            Tables = [Table("新啟用表", "", Column("新欄", "新型別", "新註"))],
        };
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());
        CollectionAssert.AreEqual(
            new[] { "新啟用表" },
            cut.FindAll(".table-pick").Select(button => button.QuerySelector("span")!.TextContent.Trim()).ToArray());
        Assert.IsFalse(cut.FindAll(".table-pick").Any(button => button.TextContent.Contains("舊表", StringComparison.Ordinal)));

        annotations.Active = null;
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());
        Assert.AreEqual("資料來源尚未註記", cut.Find(".empty-catalog").TextContent.Trim());
        Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
    }

    [TestMethod]
    public async Task Design_catalog_reread_cancels_a_scheduled_preview()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog
        {
            Active = new AnnotationPackageDto
            {
                Id = "old",
                Status = "confirmed",
                Active = true,
                Tables = [Table("舊表", "", Column("舊欄", "舊型別", "舊註"))],
            },
        };
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);
        await cut.FindAll(".table-pick").Single(button => button.TextContent.Contains("舊表", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.Find("article.table-card").QuerySelector(".field-list input")!.ChangeAsync(new ChangeEventArgs { Value = true });
        var rows = browser.RowReads;
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());
        await Task.Delay(500);
        Assert.AreEqual(rows, browser.RowReads);
        Assert.AreEqual(0, browser.GraphCalls);
    }

    [TestMethod]
    public async Task Design_catalog_saves_one_table_from_the_package()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog
        {
            Active = new AnnotationPackageDto
            {
                Id = "ship",
                Status = "confirmed",
                Active = true,
                Tables = [Table("出貨單", "出貨", Column("單號", "nvarchar", ""))],
            },
        };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.TextContent.Contains("出貨單", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.Find("article.table-card").QuerySelector(".field-list input")!.ChangeAsync(new ChangeEventArgs { Value = true });
        await RunPreview(cut);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        var saved = views.Documents.Single();
        Assert.AreEqual("出貨單", saved.Name);
        Assert.AreEqual("出貨單", saved.Purpose);
        Assert.IsFalse(saved.Confirmed);
        Assert.IsFalse(saved.Published);
        CollectionAssert.AreEqual(new[] { "出貨單" }, saved.Sources.ToArray());
        Assert.AreEqual(0, saved.Links.Count);
        Assert.AreEqual(0, saved.Parameters.Count);
        Assert.AreEqual("單號", saved.Columns.Single().Name);
        Assert.AreEqual("出貨單", saved.Columns.Single().Source);
        CollectionAssert.AreEquivalent(
            new[] { "Columns", "Confirmed", "Id", "Links", "Name", "Parameters", "Published", "Purpose", "Sources" },
            typeof(ViewDocumentDto).GetProperties().Select(property => property.Name).ToArray());
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));

        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "發布").ClickAsync(new MouseEventArgs());
        var published = views.Documents.Single();
        Assert.AreEqual(saved.Id, published.Id);
        Assert.IsTrue(published.Published);
        CollectionAssert.AreEqual(new[] { "出貨單" }, published.Sources.ToArray());
        Assert.AreEqual("單號", published.Columns.Single().Name);
        Assert.AreEqual(0, published.Links.Count);
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.IsGreaterThan(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Design_catalog_read_failure_does_not_fall_back_to_the_live_schema()
    {
        foreach (var failure in new Exception[]
        {
            new HttpRequestException("Data Source=secret;Password=nope"),
            new InvalidOperationException("Data Source=secret;Password=nope"),
            new TaskCanceledException("Data Source=secret;Password=nope"),
            new JsonException("Data Source=secret;Password=nope"),
        })
        {
            await using var context = new BunitContext();
            var annotations = new Catalog { ActiveFailure = failure };
            var browser = new QuietBrowser();
            var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);

            var alert = cut.Find("aside.library .status-message");
            Assert.AreEqual("alert", alert.GetAttribute("role"));
            var lines = alert.QuerySelectorAll("span");
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("沒有保存。", lines[0].TextContent.Trim());
            Assert.AreEqual("execution_failed", lines[1].TextContent.Trim());
            Assert.IsTrue(lines[1].ClassList.Contains("code"));
            Assert.IsTrue(alert.InnerHtml.IndexOf("沒有保存。", StringComparison.Ordinal) < alert.InnerHtml.IndexOf("execution_failed", StringComparison.Ordinal));
            Assert.AreEqual(0, cut.FindAll(".empty-catalog").Count);
            Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
            Assert.IsFalse(cut.Markup.Contains("活庫表", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
            Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
            Assert.AreEqual(1, annotations.ActiveReads);
            Assert.AreEqual(0, browser.GraphCalls);
            Assert.AreEqual(0, browser.RowReads);
        }
    }

    [TestMethod]
    public async Task Design_catalog_read_failure_keeps_execution_failed_on_multi_table_save()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        views.Remember(new ViewDocumentDto
        {
            Id = "kept",
            Name = "訂單",
            Purpose = "訂單",
            Sources = ["orders", "customers"],
            Links = [new ViewLinkDto { Kind = "foreign-key", From = "orders", FromColumn = "customerId", To = "customers", ToColumn = "id" }],
            Columns = [new ViewColumnDto { Name = "qty", Source = "orders" }],
        });
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);
        await cut.FindAll(".index-item").Single(button => button.TextContent.Contains("訂單", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        var before = JsonSerializer.Serialize(views.Documents.Single());
        annotations.ActiveFailure = new HttpRequestException("Data Source=secret;Password=nope");
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "重新讀取").ClickAsync(new MouseEventArgs());
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(before, JsonSerializer.Serialize(views.Documents.Single()));
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
        var alert = cut.Find("section.board .error");
        Assert.AreEqual("execution_failed", alert.QuerySelector(".code")!.TextContent.Trim());
        Assert.AreEqual("沒有保存。", alert.QuerySelectorAll("span")[1].TextContent.Trim());
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Design_catalog_without_analyst_does_not_read_the_catalog()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        annotations.Active!.Tables = [Table("不該讀到", "", Column("欄", "int", ""))];
        var browser = new QuietBrowser();
        var cut = Render(context, "fang", [EntryNames.Governance], annotations, browser);

        Assert.AreEqual("沒有權限。", cut.Find("[role='alert']").TextContent.Trim());
        Assert.AreEqual(0, cut.FindAll("aside.library").Count);
        Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
        Assert.IsFalse(cut.Markup.Contains("不該讀到", StringComparison.Ordinal));
        Assert.AreEqual(0, annotations.ActiveReads);
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Design_catalog_leaves_the_setup_prompt_when_the_environment_is_not_ready()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = new AnnotationPackageDto { Status = "confirmed", Active = true, Tables = [Table("不該讀到", "", Column("欄", "int", ""))] } };
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, ready: false);

        Assert.IsTrue(cut.Markup.Contains("環境還沒設定好", StringComparison.Ordinal));
        Assert.AreEqual(0, cut.FindAll("aside.library").Count);
        Assert.IsFalse(cut.Markup.Contains("不該讀到", StringComparison.Ordinal));
        Assert.AreEqual(0, annotations.ActiveReads);
        Assert.AreEqual(0, browser.GraphCalls);
    }

    [TestMethod]
    public async Task Design_catalog_analyst_can_read_while_writes_stay_with_governance()
    {
        var store = new People();
        store.Add(new OperatorRecord { Id = "lin", DisplayName = "林可", IsAdmin = false });
        store.ReplaceEntries("lin", [EntryNames.Analyst]);
        store.Add(new OperatorRecord { Id = "fang", DisplayName = "方晴", IsAdmin = false });
        store.ReplaceEntries("fang", [EntryNames.Governance]);
        var annotations = new Catalog
        {
            Active = new AnnotationPackageDto
            {
                Id = "on",
                Status = "confirmed",
                Active = true,
                Tables = [Table("啟用表", "", Column("欄", "int", ""))],
            },
        };
        var service = new AnnotationService(new Profiles(store), annotations);

        var read = await service.DesignCatalogAsync("lin", CancellationToken.None);
        Assert.IsTrue(read.Ok);
        Assert.AreEqual("啟用表", read.Value!.Package!.Tables.Single().Name);
        Assert.AreEqual(1, annotations.ActiveReads);

        AssertClosed((await service.ActiveAsync("lin", CancellationToken.None)).Error);
        AssertClosed((await service.DraftAsync("lin", CancellationToken.None)).Error);
        AssertClosed((await service.DraftDbmlAsync("lin", CancellationToken.None)).Error);
        AssertClosed((await service.SaveAsync("lin", new AnnotationPackageDto(), CancellationToken.None)).Error);
        AssertClosed((await service.ImportAsync("lin", "a.xlsx", [], CancellationToken.None)).Error);
        AssertClosed((await service.ConfirmAsync("lin", CancellationToken.None)).Error);
        AssertClosed((await service.AbandonAsync("lin", CancellationToken.None)).Error);
        AssertClosed((await service.ReadStructureAsync("lin", CancellationToken.None)).Error);
        Assert.AreEqual(1, annotations.ActiveReads);
        Assert.AreEqual(0, annotations.DraftReads);
        Assert.AreEqual(0, annotations.SaveAttempts);
        Assert.AreEqual(0, annotations.ImportAttempts);
        Assert.AreEqual(0, annotations.ConfirmAttempts);
        Assert.AreEqual(0, annotations.AbandonAttempts);
        Assert.AreEqual(0, annotations.StructureAttempts);
        Assert.AreEqual(0, annotations.DbmlAttempts);

        AssertClosed((await service.DesignCatalogAsync("fang", CancellationToken.None)).Error);
        Assert.AreEqual(1, annotations.ActiveReads);
        var opened = await service.ActiveAsync("fang", CancellationToken.None);
        Assert.IsTrue(opened.Ok);
        Assert.AreEqual("啟用表", opened.Value!.Package!.Tables.Single().Name);
        Assert.AreEqual(2, annotations.ActiveReads);

        var written = await service.SaveAsync("fang", new AnnotationPackageDto { Id = "draft" }, CancellationToken.None);
        Assert.IsTrue(written.Ok);
        Assert.AreEqual(1, annotations.SaveAttempts);
    }

    [TestMethod]
    public async Task Declared_link_reversed_direction_saves_accepted_kind()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".chip").Single(chip => chip.TextContent.Contains("accounts", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        var link = Field<List<ViewLinkDto>>(cut, "_links").Single();
        Assert.AreEqual("accepted", link.Kind);
        var from = link.From;
        var fromColumn = link.FromColumn;
        link.From = link.To;
        link.FromColumn = link.ToColumn;
        link.To = from;
        link.ToColumn = fromColumn;
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });

        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        var saved = views.Documents.Single();
        Assert.AreEqual("accepted", saved.Links.Single().Kind);
        Assert.AreEqual("accounts", saved.Links.Single().From);
        Assert.AreEqual("id", saved.Links.Single().FromColumn);
        Assert.AreEqual("orders", saved.Links.Single().To);
        Assert.AreEqual("customerId", saved.Links.Single().ToColumn);
        CollectionAssert.AreEquivalent(
            new[] { "Columns", "Confirmed", "Id", "Links", "Name", "Parameters", "Published", "Purpose", "Sources" },
            typeof(ViewDocumentDto).GetProperties().Select(property => property.Name).ToArray());
        Assert.IsFalse(JsonSerializer.Serialize(saved).Contains("不該出現的理由", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Declared_link_foreign_key_keeps_kind()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".chip").Single(chip => chip.TextContent.Contains("customers", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        Assert.AreEqual("foreign-key", Field<List<ViewLinkDto>>(cut, "_links").Single().Kind);
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });

        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        var saved = views.Documents.Single();
        Assert.AreEqual("foreign-key", saved.Links.Single().Kind);
        Assert.AreEqual("orders", saved.Links.Single().From);
        Assert.AreEqual("customerId", saved.Links.Single().FromColumn);
        Assert.AreEqual("customers", saved.Links.Single().To);
        Assert.AreEqual("id", saved.Links.Single().ToColumn);
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Declared_link_one_table_is_not_rejected()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        var saved = views.Documents.Single();
        CollectionAssert.AreEqual(new[] { "orders" }, saved.Sources.ToArray());
        Assert.AreEqual(0, saved.Links.Count);
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Declared_link_one_segment_rejects_without_writing()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        await cut.FindAll(".chip").Single(chip => chip.TextContent.Contains("customers", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.FindAll(".chip").Single(chip => chip.TextContent.Contains("accounts", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        var links = Field<List<ViewLinkDto>>(cut, "_links");
        Assert.AreEqual(2, links.Count);
        links.Single(link => link.To == "accounts").ToColumn = "missing";
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });

        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(0, views.Documents.Count);
        Assert.AreEqual(0, annotations.SaveAttempts);
        Assert.AreEqual(2, Field<List<ViewLinkDto>>(cut, "_links").Count);
        var shown = cut.Find("[aria-label='表怎麼相連']").QuerySelectorAll("li").Select(item => item.TextContent.Trim()).ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "orders.customerId → customers.id", "orders.customerId → accounts.missing" },
            shown);
        var alert = cut.Find("section.board .status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        var lines = alert.QuerySelectorAll("span");
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("這段關聯不在註記包裡。", lines[0].TextContent.Trim());
        Assert.AreEqual("link_undeclared", lines[1].TextContent.Trim());
        Assert.IsTrue(lines[1].ClassList.Contains("code"));
        Assert.IsTrue(alert.InnerHtml.IndexOf("這段關聯不在註記包裡。", StringComparison.Ordinal) < alert.InnerHtml.IndexOf("link_undeclared", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Declared_link_without_active_package_does_not_read_live_schema()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Draft = DraftPackage("草稿表"), Others = { ExitedPackage("已退出表") } };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        views.Remember(new ViewDocumentDto
        {
            Id = "kept",
            Name = "訂單",
            Purpose = "訂單",
            Sources = ["orders", "customers"],
            Links = [new ViewLinkDto { Kind = "foreign-key", From = "orders", FromColumn = "customerId", To = "customers", ToColumn = "id" }],
        });
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);
        var before = JsonSerializer.Serialize(views.Documents.Single());

        await cut.FindAll(".index-item").Single(button => button.TextContent.Contains("訂單", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(before, JsonSerializer.Serialize(views.Documents.Single()));
        Assert.AreEqual(0, annotations.SaveAttempts);
        Assert.IsTrue(cut.Find("[aria-label='表怎麼相連']").TextContent.Contains("orders.customerId → customers.id", StringComparison.Ordinal));
        var alert = cut.Find("section.board .status-message");
        Assert.AreEqual("alert", alert.GetAttribute("role"));
        Assert.AreEqual("這段關聯不在註記包裡。", alert.QuerySelectorAll("span")[0].TextContent.Trim());
        Assert.AreEqual("link_undeclared", alert.QuerySelectorAll("span")[1].TextContent.Trim());
        Assert.IsFalse(cut.Markup.Contains("活庫表", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Data Source=", StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("Password=", StringComparison.Ordinal));
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
    }

    [TestMethod]
    public async Task Declared_link_without_any_link_stays_disconnected()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        Field<List<string>>(cut, "_sources").Add("customers");
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(0, views.Documents.Count);
        var alert = cut.Find("section.board .error");
        Assert.AreEqual("disconnected", alert.QuerySelector(".code")!.TextContent.Trim());
        Assert.AreEqual("來源連不起來。", alert.QuerySelectorAll("span")[1].TextContent.Trim());
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Declared_link_outside_table_saves_accepted_kind()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        views.Remember(new ViewDocumentDto
        {
            Id = "kept",
            Name = "外部",
            Purpose = "外部",
            Sources = ["orders", "外部表"],
            Links = [new ViewLinkDto { Kind = "foreign-key", From = "orders", FromColumn = "customerId", To = "外部表", ToColumn = "id" }],
            Columns = [new ViewColumnDto { Name = "customerId", Source = "orders" }],
        });
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".index-item").Single(button => button.TextContent.Contains("外部", StringComparison.Ordinal)).ClickAsync(new MouseEventArgs());
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        var saved = views.Documents.Single();
        Assert.AreEqual("accepted", saved.Links.Single().Kind);
        Assert.AreEqual("orders", saved.Links.Single().From);
        Assert.AreEqual("customerId", saved.Links.Single().FromColumn);
        Assert.AreEqual("外部表", saved.Links.Single().To);
        Assert.AreEqual("id", saved.Links.Single().ToColumn);
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Declared_link_blank_column_does_not_show_undeclared()
    {
        await using var context = new BunitContext();
        var annotations = new Catalog { Active = ConfirmedPackage() };
        var browser = new QuietBrowser();
        var views = new SavingViews();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser, views);

        await cut.FindAll(".table-pick").Single(button => button.QuerySelector("span")!.TextContent.Trim() == "orders").ClickAsync(new MouseEventArgs());
        Field<List<string>>(cut, "_sources").Add("customers");
        Field<List<ViewLinkDto>>(cut, "_links").Add(new ViewLinkDto
        {
            Kind = "foreign-key",
            From = "orders",
            FromColumn = " ",
            To = "customers",
            ToColumn = "id",
        });
        Field<List<ViewColumnDto>>(cut, "_columns").Add(new ViewColumnDto { Name = "qty", Source = "orders" });
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "儲存").ClickAsync(new MouseEventArgs());

        Assert.AreEqual(0, views.Documents.Count);
        Assert.IsFalse(cut.Markup.Contains("link_undeclared", StringComparison.Ordinal));
        Assert.AreEqual("來源連不起來。", cut.Find("section.board .error").QuerySelectorAll("span")[1].TextContent.Trim());
    }

    private static T Field<T>(IRenderedComponent<Workbench.Web.Components.Views> cut, string name)
        => (T)cut.Instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cut.Instance)!;

    private static async Task AssertEmpty(Catalog annotations, string hidden)
    {
        await using var context = new BunitContext();
        var browser = new QuietBrowser();
        var cut = Render(context, "lin", [EntryNames.Analyst], annotations, browser);
        Assert.AreEqual("資料來源尚未註記", cut.Find(".empty-catalog").TextContent.Trim());
        Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
        Assert.AreEqual(0, cut.FindAll(".unmarked-badge").Count);
        Assert.AreEqual(0, cut.FindAll("aside.library [role='alert']").Count);
        Assert.AreEqual(0, cut.FindAll("[aria-label='已聲明關聯']").Count);
        Assert.IsFalse(cut.Markup.Contains(hidden, StringComparison.Ordinal));
        Assert.IsFalse(cut.Markup.Contains("活庫表", StringComparison.Ordinal));
        await cut.Find("#table-finder").InputAsync(new ChangeEventArgs { Value = hidden });
        Assert.AreEqual(0, cut.FindAll(".table-pick").Count);
        Assert.AreEqual("資料來源尚未註記", cut.Find(".empty-catalog").TextContent.Trim());
        Assert.AreEqual(0, browser.GraphCalls);
        Assert.AreEqual(0, browser.RowReads);
        Assert.AreEqual(0, annotations.DraftReads);
    }

    private static void AssertClosed(ErrorDto? error)
    {
        Assert.IsNotNull(error);
        Assert.AreEqual("forbidden", error.Code);
        Assert.AreEqual("沒有權限。", error.Message);
    }

    private static async Task RunPreview(IRenderedComponent<Workbench.Web.Components.Views> cut)
    {
        var generation = cut.Instance.GetType().GetField("_previewGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cut.Instance);
        var run = cut.Instance.GetType().GetMethod("RunPreview", BindingFlags.Instance | BindingFlags.NonPublic);
        await cut.InvokeAsync(() => (Task)run!.Invoke(cut.Instance, [generation])!);
    }

    private static IRenderedComponent<Workbench.Web.Components.Views> Render(
        BunitContext context,
        string operatorId,
        IReadOnlyList<string> entries,
        Catalog annotations,
        QuietBrowser browser,
        SavingViews? views = null,
        bool ready = true)
    {
        var store = new People();
        store.Add(new OperatorRecord { Id = operatorId, DisplayName = "林可", IsAdmin = false });
        store.ReplaceEntries(operatorId, entries);
        var sessions = new MemorySessionStore();
        var hasher = new Pbkdf2PasswordHasher();
        var profiles = new Profiles(store);
        var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
        var circuit = new CircuitSession();
        circuit.Adopt(sessions.Create(operatorId));
        var connections = new RecordingConnections();
        var environments = new EnvironmentService(
            connections,
            new RecordingConnectionAdmin(),
            new RecordingModel(ready),
            new RecordingModelEndpoint(),
            profiles);
        context.Services.AddSingleton(new OperatorGate(circuit, sessionService, profiles));
        context.Services.AddSingleton(new ViewService(profiles, views ?? new SavingViews(), new EmptyStyles(), environments));
        context.Services.AddSingleton(new SchemaService(profiles, browser, connections, environments));
        context.Services.AddSingleton(environments);
        context.Services.AddSingleton(new AnnotationService(profiles, annotations));
        return context.Render<Workbench.Web.Components.Views>();
    }

    private static AnnotationPackageDto ConfirmedPackage() => new()
    {
        Id = "active",
        Status = "confirmed",
        Active = true,
        Dbml = "DBML樣本",
        Changes = [new AnnotationChangeDto { Kind = "差異標記", Table = "orders", Column = "customerId" }],
        Tables =
        [
            Table("orders", "   ",
                Column("customerId", "int", "   ", "個資樣本"),
                Column("qty", "text", "數量")),
            Table("customers", "客戶主檔",
                Column("id", "int", "主鍵"),
                Column("regionId", "int", "")),
            Table("accounts", "", Column("id", "int", " ")),
            Table("regions", "", Column("id", "int", "地區")),
        ],
        Links =
        [
            Link("orders", "customerId", "customers", "id", "foreign-key", "   "),
            Link("orders", "customerId", "accounts", "id", "declared", "不該出現的理由"),
            Link("orders", "customerId", "外部表", "id", "declared", ""),
            Link("orders", "customerId", "customers", "name", "guessed", ""),
            Link("customers", "regionId", "regions", "id", "foreign-key", "地區理由"),
        ],
    };

    private static AnnotationPackageDto DraftPackage(string name) => new()
    {
        Id = "draft",
        Status = "draft",
        Active = false,
        Tables = [Table(name, "", Column("草稿欄", "int", ""))],
    };

    private static AnnotationPackageDto ExitedPackage(string name) => new()
    {
        Id = "exited",
        Status = "confirmed",
        Active = false,
        Tables = [Table(name, "", Column("退欄", "int", ""))],
    };

    private static AnnotationTableDto Table(string name, string note, params AnnotationColumnDto[] columns) => new()
    {
        Name = name,
        Note = note,
        Columns = columns.ToList(),
        Indexes = [new AnnotationIndexDto { Name = "索引樣本", Columns = columns.Select(column => column.Name).ToList() }],
    };

    private static AnnotationColumnDto Column(string name, string type, string note, string pii = "") => new()
    {
        Name = name,
        Type = type,
        Note = note,
        Pii = pii,
    };

    private static AnnotationLinkDto Link(string from, string fromColumn, string to, string toColumn, string origin, string note) => new()
    {
        From = from,
        FromColumn = fromColumn,
        To = to,
        ToColumn = toColumn,
        Origin = origin,
        Note = note,
    };

    private sealed class People : IOperatorStore
    {
        private readonly List<OperatorRecord> _rows = [];
        private readonly Dictionary<string, List<string>> _entries = [];

        public int Count() => _rows.Count;
        public void Add(OperatorRecord record) => _rows.Add(record);
        public OperatorRecord? FindByName(string name) => _rows.FirstOrDefault(row => row.DisplayName == name);
        public OperatorRecord? FindById(string id) => _rows.FirstOrDefault(row => row.Id == id);
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) => _entries[operatorId] = entries.ToList();
        public IReadOnlyList<string> EntriesFor(string operatorId) => _entries.TryGetValue(operatorId, out var found) ? found : [];
    }

    private sealed class EmptyStyles : IStyleStore
    {
        public void Add(StyleRecord record) { }
        public bool TryUpdate(StyleRecord record) => false;
        public StyleRecord? Find(string id) => null;
        public IReadOnlyList<StyleRecord> ReadableBy(string operatorId) => [];
        public bool References(string viewId) => false;
        public bool Delete(string id, string ownerId) => false;
    }

    private sealed class SavingViews : IViewCatalog
    {
        private readonly List<ViewDocumentDto> _documents = [];

        public IReadOnlyList<ViewDocumentDto> Documents => _documents.Select(Clone).ToList();

        public void Remember(ViewDocumentDto document) => _documents.Add(Clone(document));

        public Task<IReadOnlyList<ViewDocumentDto>> ListAsync(string callerId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ViewDocumentDto>>(_documents.Select(Clone).ToList());

        public Task<ViewDocumentDto?> FindAsync(string id, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(_documents.FirstOrDefault(item => item.Id == id) is { } found ? Clone(found) : null);

        public Task<CatalogWriteResult> SaveAsync(ViewDocumentDto document, string callerId, CancellationToken cancellationToken)
        {
            var copy = Clone(document);
            var index = _documents.FindIndex(item => item.Id == copy.Id);
            if (index < 0)
                _documents.Add(copy);
            else
                _documents[index] = copy;
            return Task.FromResult(CatalogWriteResult.Success(Clone(copy)));
        }

        public Task<CatalogWriteResult> DeleteAsync(string id, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(CatalogWriteResult.Fail("not_found", "找不到檢視。"));

        public Task<CatalogWriteResult> SetPublishedAsync(string id, bool published, string callerId, CancellationToken cancellationToken)
        {
            var found = _documents.FirstOrDefault(item => item.Id == id);
            if (found is null)
                return Task.FromResult(CatalogWriteResult.Fail("not_found", "找不到檢視。"));
            found.Published = published;
            return Task.FromResult(CatalogWriteResult.Success(Clone(found)));
        }

        private static ViewDocumentDto Clone(ViewDocumentDto document)
            => JsonSerializer.Deserialize<ViewDocumentDto>(JsonSerializer.Serialize(document))!;
    }

    private sealed class QuietBrowser : ISchemaBrowser
    {
        public int GraphCalls { get; private set; }
        public int RowReads { get; private set; }

        public Task<SchemaReadResult> GraphAsync(string connectionId, string callerId, CancellationToken cancellationToken)
        {
            GraphCalls++;
            return Task.FromResult(SchemaReadResult.FromGraph(new SchemaGraphDto
            {
                Tables = [new SchemaTableDto { Name = "活庫表", Columns = [new SchemaColumnDto { Name = "活庫欄" }] }],
            }));
        }

        public Task<SchemaReadResult> PreviewAsync(
            string connectionId,
            ViewDocumentDto document,
            IReadOnlyDictionary<string, string> parameters,
            string callerId,
            CancellationToken cancellationToken)
        {
            RowReads++;
            return Task.FromResult(SchemaReadResult.FromPreview(new SchemaPreviewDto()));
        }
    }

    private sealed class Catalog : IAnnotationCatalog
    {
        public AnnotationPackageDto? Active { get; set; }
        public AnnotationPackageDto? Draft { get; set; }
        public List<AnnotationPackageDto> Others { get; } = [];
        public Exception? ActiveFailure { get; set; }
        public int ActiveReads { get; private set; }
        public int DraftReads { get; private set; }
        public int SaveAttempts { get; private set; }
        public int ImportAttempts { get; private set; }
        public int ConfirmAttempts { get; private set; }
        public int AbandonAttempts { get; private set; }
        public int StructureAttempts { get; private set; }
        public int DbmlAttempts { get; private set; }

        public Task<AnnotationPackageDto?> DraftAsync(string callerId, CancellationToken cancellationToken)
        {
            DraftReads++;
            return Task.FromResult(Draft);
        }

        public Task<AnnotationActiveDto> ActiveAsync(string callerId, CancellationToken cancellationToken)
        {
            ActiveReads++;
            if (ActiveFailure is not null)
                throw ActiveFailure;
            return Task.FromResult(new AnnotationActiveDto
            {
                Package = Active,
                Others = Others.ToList(),
            });
        }

        public Task<string?> DraftDbmlAsync(string callerId, CancellationToken cancellationToken)
        {
            DbmlAttempts++;
            return Task.FromResult<string?>(null);
        }

        public Task<AnnotationWriteResult> ImportAsync(AnnotationPackageDto package, string callerId, CancellationToken cancellationToken)
        {
            ImportAttempts++;
            return Task.FromResult(AnnotationWriteResult.Success(package));
        }

        public Task<AnnotationWriteResult> SaveAsync(AnnotationPackageDto package, string callerId, CancellationToken cancellationToken)
        {
            SaveAttempts++;
            return Task.FromResult(AnnotationWriteResult.Success(package));
        }

        public Task<AnnotationWriteResult> ReadStructureAsync(string callerId, CancellationToken cancellationToken)
        {
            StructureAttempts++;
            return Task.FromResult(AnnotationWriteResult.Success(new AnnotationPackageDto()));
        }

        public Task<AnnotationWriteResult> ConfirmAsync(string callerId, CancellationToken cancellationToken)
        {
            ConfirmAttempts++;
            return Task.FromResult(AnnotationWriteResult.Success(new AnnotationPackageDto()));
        }

        public Task<AnnotationWriteResult> AbandonAsync(string callerId, CancellationToken cancellationToken)
        {
            AbandonAttempts++;
            return Task.FromResult(AnnotationWriteResult.Success(new AnnotationPackageDto()));
        }
    }
}
