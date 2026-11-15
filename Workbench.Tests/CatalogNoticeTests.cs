using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench;
using Workbench.Adapters;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Web;

namespace Workbench.Tests;

[TestClass]
public class CatalogNoticeTests
{
    [TestMethod]
    public void Waiting_copy_names_the_address_the_user_typed()
    {
        var down = WorkbenchMessages.CatalogWaiting(WorkbenchMessages.ServiceUnavailableCode, "localhost:5432");
        var missing = WorkbenchMessages.CatalogWaiting(WorkbenchMessages.ServiceUnconfiguredCode, "localhost:5432");

        Assert.AreEqual("資料服務還沒有啟動", down.Title);
        Assert.AreEqual("你填的 localhost:5432 會在資料服務啟動後才送出。", down.Body);
        Assert.AreEqual("資料服務位址還沒有設定", missing.Title);
        Assert.AreEqual("你填的 localhost:5432 會在資料服務位址設定好之後才送出。", missing.Body);
        Assert.IsFalse((down.Title + down.Body + missing.Title + missing.Body).Contains("5291", StringComparison.Ordinal));
        Assert.IsFalse((down.Body + missing.Body).Contains("http", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Stopped_data_service_keeps_its_address_out_of_the_message()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var admin = new McpConnectionAdmin(http, "http://127.0.0.1:9", "local-catalog-key");

        var listed = await admin.DatabasesAsync("PostgreSQL", "Host=localhost;Port=5432", "admin", CancellationToken.None);
        var probed = await admin.ProbeAsync("PostgreSQL", "Host=localhost;Port=5432", "admin", CancellationToken.None);

        Assert.AreEqual(WorkbenchMessages.ServiceUnavailableCode, listed.Code);
        Assert.AreEqual("資料服務沒有回應。", listed.Message);
        Assert.AreEqual(WorkbenchMessages.ServiceUnavailableCode, probed.Code);
        Assert.AreEqual("資料服務沒有回應。", probed.Message);
        Assert.IsFalse((listed.Message + probed.Message).Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Environment_page_shows_the_typed_database_address_when_the_service_is_down()
    {
        await using var page = new Page(DatabaseListResult.Fail(WorkbenchMessages.ServiceUnavailableCode, "資料服務沒有回應（http://127.0.0.1:5291/），無法讀取資料庫清單。"));

        await page.Cut.Find("#connection-engine").ChangeAsync(new ChangeEventArgs { Value = "PostgreSQL" });
        await page.Cut.Find("#connection-port").InputAsync(new ChangeEventArgs { Value = "5433" });
        await page.Cut.Find("#connection-user").InputAsync(new ChangeEventArgs { Value = "postgres" });
        await page.Cut.Find("#connection-password").InputAsync(new ChangeEventArgs { Value = "secret" });
        await page.Cut.Find("#connection-password").BlurAsync(new FocusEventArgs());

        var notice = page.Cut.Find(".notice").TextContent;
        Assert.Contains("資料服務還沒有啟動", notice);
        Assert.Contains("啟動資料服務", notice);
        Assert.Contains("你填的 localhost:5433 會在資料服務啟動後才送出。", notice);
        Assert.IsFalse(notice.Contains("5291", StringComparison.Ordinal));
        Assert.IsFalse(notice.Contains("http", StringComparison.OrdinalIgnoreCase));
        Assert.IsNull(page.Cut.FindAll(".database-choices [role='alert']").SingleOrDefault());
    }

    [TestMethod]
    public async Task Start_button_brings_the_data_service_up_and_lists_databases()
    {
        var admin = new FixedAdmin(DatabaseListResult.Fail(WorkbenchMessages.ServiceUnavailableCode, "資料服務沒有回應。"));
        await using var page = new Page(admin, new ReadyHost(admin));

        await page.Cut.Find("#connection-engine").ChangeAsync(new ChangeEventArgs { Value = "PostgreSQL" });
        await page.Cut.Find("#connection-user").InputAsync(new ChangeEventArgs { Value = "postgres" });
        await page.Cut.Find("#connection-password").InputAsync(new ChangeEventArgs { Value = "secret" });
        await page.Cut.Find("#connection-password").BlurAsync(new FocusEventArgs());
        await page.Cut.Find(".notice .confirm").ClickAsync(new MouseEventArgs());

        Assert.Contains("lamp", page.Cut.Markup);
        Assert.IsFalse(page.Cut.Markup.Contains("資料服務還沒有啟動", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Start_failure_stays_on_the_notice()
    {
        var admin = new FixedAdmin(DatabaseListResult.Fail(WorkbenchMessages.ServiceUnavailableCode, "資料服務沒有回應。"));
        await using var page = new Page(admin, new FailedHost());

        await page.Cut.Find("#connection-engine").ChangeAsync(new ChangeEventArgs { Value = "PostgreSQL" });
        await page.Cut.Find("#connection-user").InputAsync(new ChangeEventArgs { Value = "postgres" });
        await page.Cut.Find("#connection-password").InputAsync(new ChangeEventArgs { Value = "secret" });
        await page.Cut.Find("#connection-password").BlurAsync(new FocusEventArgs());
        await page.Cut.Find(".notice .confirm").ClickAsync(new MouseEventArgs());

        Assert.Contains("找不到資料服務程式，無法啟動。", page.Cut.Find(".notice").TextContent);
    }

    [TestMethod]
    public async Task Test_button_explains_that_the_data_service_must_start_first()
    {
        await using var page = new Page(DatabaseListResult.Fail(WorkbenchMessages.ServiceUnavailableCode, "資料服務沒有回應。"));

        await page.Cut.Find("#connection-engine").ChangeAsync(new ChangeEventArgs { Value = "PostgreSQL" });
        await page.Cut.Find("#connection-user").InputAsync(new ChangeEventArgs { Value = "postgres" });
        await page.Cut.Find("#connection-password").InputAsync(new ChangeEventArgs { Value = "secret" });
        await page.Cut.Find("#connection-password").BlurAsync(new FocusEventArgs());
        var test = page.Cut.Find("form.connection").QuerySelectorAll("button")
            .Single(item => item.TextContent.Contains("測試連線", StringComparison.Ordinal));
        await test.ClickAsync(new MouseEventArgs());

        Assert.Contains("請先啟動資料服務，再測試連線。", page.Cut.Find(".notice").TextContent);
    }

    [TestMethod]
    public async Task Signed_out_page_asks_for_login()
    {
        await using var context = new BunitContext();
        var store = new Operators();
        var sessions = new MemorySessionStore();
        var hasher = new Pbkdf2PasswordHasher();
        var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
        var profiles = new Profiles(store);
        context.Services.AddSingleton(new OperatorGate(new CircuitSession(), sessionService, profiles));
        context.Services.AddSingleton(new EnvironmentService(
            new RecordingConnections(),
            new FixedAdmin(DatabaseListResult.Fail("connection_failed", "無法建立連線。")),
            new RecordingModel(false),
            new RecordingModelEndpoint(),
            profiles));

        var cut = context.Render<Workbench.Web.Components.Environment>();

        Assert.Contains("尚未登入", cut.Markup);
        Assert.IsFalse(cut.Markup.Contains("測試連線", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Database_rejection_stays_on_the_database_field()
    {
        await using var page = new Page(DatabaseListResult.Fail("connection_failed", "密碼不對。"));

        await page.Cut.Find("#connection-engine").ChangeAsync(new ChangeEventArgs { Value = "PostgreSQL" });
        await page.Cut.Find("#connection-user").InputAsync(new ChangeEventArgs { Value = "postgres" });
        await page.Cut.Find("#connection-password").InputAsync(new ChangeEventArgs { Value = "secret" });
        await page.Cut.Find("#connection-password").BlurAsync(new FocusEventArgs());

        Assert.AreEqual("密碼不對。", page.Cut.Find(".database-choices [role='alert']").TextContent);
        Assert.IsFalse(page.Cut.Markup.Contains("資料服務還沒有啟動", StringComparison.Ordinal));
    }

    private sealed class Page : IAsyncDisposable
    {
        private readonly BunitContext _context = new();

        public Page(DatabaseListResult listed)
            : this(new FixedAdmin(listed), null)
        {
        }

        public Page(FixedAdmin admin, IDataServiceHost? host)
        {
            var store = new Operators();
            store.Add(new OperatorRecord { Id = "admin", DisplayName = "管理者", IsAdmin = true });
            var sessions = new MemorySessionStore();
            var hasher = new Pbkdf2PasswordHasher();
            var sessionService = new SessionService(store, hasher, sessions, new SeedAdminService(store, hasher));
            var circuit = new CircuitSession();
            circuit.Adopt(sessions.Create("admin"));
            var profiles = new Profiles(store);
            _context.Services.AddSingleton(new OperatorGate(circuit, sessionService, profiles));
            _context.Services.AddSingleton(new EnvironmentService(
                new RecordingConnections(),
                admin,
                new RecordingModel(false),
                new RecordingModelEndpoint(),
                profiles,
                host));
            Cut = _context.Render<Workbench.Web.Components.Environment>();
        }

        public IRenderedComponent<Workbench.Web.Components.Environment> Cut { get; }

        public ValueTask DisposeAsync() => _context.DisposeAsync();
    }

    private sealed class FixedAdmin(DatabaseListResult listed) : IConnectionAdmin
    {
        public DatabaseListResult Listed { get; set; } = listed;

        public Task<IReadOnlyList<NamedConnection>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<NamedConnection>>([]);

        public Task<CatalogWriteResult> SaveAsync(string name, string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(CatalogWriteResult.Fail(Listed.Code, Listed.Message));

        public Task<CatalogWriteResult> ProbeAsync(string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(CatalogWriteResult.Fail(Listed.Code, Listed.Message));

        public Task<DatabaseListResult> DatabasesAsync(string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(Listed);

        public Task<CatalogWriteResult> RemoveAsync(string name, string callerId, CancellationToken cancellationToken)
            => Task.FromResult(CatalogWriteResult.Fail("not_found", "找不到連線。"));
    }

    private sealed class ReadyHost(FixedAdmin admin) : IDataServiceHost
    {
        public Task<DataServiceStart> StartAsync(CancellationToken cancellationToken)
        {
            admin.Listed = DatabaseListResult.Success(["lamp"]);
            return Task.FromResult(DataServiceStart.Ready("資料服務已啟動。"));
        }
    }

    private sealed class FailedHost : IDataServiceHost
    {
        public Task<DataServiceStart> StartAsync(CancellationToken cancellationToken)
            => Task.FromResult(DataServiceStart.Fail("找不到資料服務程式，無法啟動。"));
    }

    private sealed class Operators : IOperatorStore
    {
        private readonly List<OperatorRecord> _rows = [];

        public int Count() => _rows.Count;
        public void Add(OperatorRecord record) => _rows.Add(record);
        public OperatorRecord? FindByName(string name) => _rows.FirstOrDefault(row => row.DisplayName == name);
        public OperatorRecord? FindById(string id) => _rows.FirstOrDefault(row => row.Id == id);
        public IReadOnlyList<OperatorRecord> List() => _rows.ToList();
        public void ReplaceEntries(string operatorId, IReadOnlyList<string> entries) { }
        public IReadOnlyList<string> EntriesFor(string operatorId) => [];
    }
}
