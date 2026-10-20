using System.Net.Http.Headers;
using Microsoft.Data.Sqlite;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Tests;

internal sealed class WorkbenchHost : WebApplicationFactory<Program>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _root;

    public WorkbenchHost(Action<HostOptions>? configure = null)
    {
        var options = new HostOptions();
        configure?.Invoke(options);
        _root = options.ContentRoot ?? Path.Combine(Path.GetTempPath(), "wb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Catalog = options.Catalog ?? new RecordingCatalog();
        Caller = options.Caller ?? new RecordingCaller();
        Connections = options.Connections ?? new RecordingConnections();
        SqlitePath = Path.Combine(_root, "workbench.db");
        PreserveRoot = options.PreserveRoot;
        AdminName = options.AdminName;
        AdminPassword = options.AdminPassword;
        SeedAdmin = options.SeedAdmin;
        Options = options;
    }

    public HostOptions Options { get; }
    public RecordingCatalog Catalog { get; }
    public RecordingCaller Caller { get; }
    public RecordingConnections Connections { get; }
    public string SqlitePath { get; }
    public bool PreserveRoot { get; }
    public string AdminName { get; }
    public string AdminPassword { get; }
    public bool SeedAdmin { get; }
    public string ContentRoot => _root;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(_root);
        builder.UseEnvironment("Development");
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

            var settings = new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "*",
                ["Workbench:SqlitePath"] = SqlitePath,
                ["Workbench:CatalogKey"] = "unit-catalog-key",
                ["Surfaces:Mcp:BaseUrl"] = "http://127.0.0.1:9",
            };
            if (SeedAdmin)
            {
                settings["Workbench:InitialAdmin:DisplayName"] = AdminName;
                settings["Workbench:InitialAdmin:Password"] = AdminPassword;
            }

            foreach (var pair in Options.Extra)
                settings[pair.Key] = pair.Value;
            config.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IViewCatalog>(Catalog);
            services.AddSingleton<IViewCaller>(Caller);
            services.AddSingleton<IConnectionCount>(Connections);
        });
    }

    public HttpClient Open()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public async Task<Session> LoginAsync(string? name = null, string? secret = null)
    {
        var client = Open();
        var response = await client.PostAsJsonAsync("api/sessions", new LoginRequest
        {
            DisplayName = name ?? AdminName,
            Secret = secret ?? AdminPassword,
        }, Json);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(((int)response.StatusCode) + " " + body);
        var sessionId = JsonSerializer.Deserialize<SessionResponse>(body, Json)!.SessionId;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
        var me = await client.GetFromJsonAsync<MeResponse>("api/me", Json);
        return new Session(client, sessionId, me!);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (!PreserveRoot && Directory.Exists(_root))
        {
            try { Directory.Delete(_root, true); }
            catch (IOException) { }
        }
    }
}

internal sealed class HostOptions
{
    public string AdminName { get; set; } = "管理者";
    public string AdminPassword { get; set; } = "seed-secret";
    public bool SeedAdmin { get; set; } = true;
    public string? ContentRoot { get; set; }
    public bool PreserveRoot { get; set; }
    public RecordingCatalog? Catalog { get; set; }
    public RecordingCaller? Caller { get; set; }
    public RecordingConnections? Connections { get; set; }
    public Dictionary<string, string?> Extra { get; } = new(StringComparer.Ordinal);
}

internal sealed class Session
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Session(HttpClient client, string sessionId, MeResponse me)
    {
        Client = client;
        SessionId = sessionId;
        Me = me;
    }

    public HttpClient Client { get; }
    public string SessionId { get; }
    public MeResponse Me { get; }

    public async Task<(int Status, string Text, JsonElement Body)> RawAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        using var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
        return ((int)response.StatusCode, text, document.RootElement.Clone());
    }
}

internal sealed class RecordingCatalog : IViewCatalog
{
    private readonly List<ViewDocumentDto> _documents = [];

    public IReadOnlyList<string> PublishedNames()
        => _documents.Where(document => document.Published).Select(document => document.Name).ToList();

    public ViewDocumentDto? Stored(string id)
        => _documents.FirstOrDefault(document => document.Id == id);

    public Task<IReadOnlyList<ViewDocumentDto>> ListAsync(string callerId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ViewDocumentDto>>(_documents.Select(Clone).ToList());

    public Task<ViewDocumentDto?> FindAsync(string id, string callerId, CancellationToken cancellationToken)
        => Task.FromResult(Stored(id) is { } found ? Clone(found) : null);

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
    {
        var removed = _documents.RemoveAll(item => item.Id == id);
        return Task.FromResult(removed == 1
            ? CatalogWriteResult.Success(null)
            : CatalogWriteResult.Fail("not_found", "找不到檢視。"));
    }

    public Task<CatalogWriteResult> SetPublishedAsync(string id, bool published, string callerId, CancellationToken cancellationToken)
    {
        var found = Stored(id);
        if (found is null)
            return Task.FromResult(CatalogWriteResult.Fail("not_found", "找不到檢視。"));
        found.Published = published;
        return Task.FromResult(CatalogWriteResult.Success(Clone(found)));
    }

    private static ViewDocumentDto Clone(ViewDocumentDto document)
        => JsonSerializer.Deserialize<ViewDocumentDto>(JsonSerializer.Serialize(document))!;
}

internal sealed class RecordingCaller : IViewCaller
{
    public List<string> ToolNames { get; } = [];
    public List<string> ConnectionIds { get; } = [];
    public List<Dictionary<string, string>> Parameters { get; } = [];
    public int Count => ToolNames.Count;
    public bool Fail { get; set; }
    public string FailCode { get; set; } = "execution_failed";
    public string FailMessage { get; set; } = "鍵值查詢失敗。";
    public List<string> Columns { get; set; } = ["Name"];
    public List<List<string>> Rows { get; set; } = [["ROWMARKER"]];

    public Task<ViewCallOutcome> CallAsync(
        string viewName,
        string connectionId,
        IReadOnlyDictionary<string, string> parameters,
        string callerId,
        CancellationToken cancellationToken)
    {
        ToolNames.Add(viewName);
        ConnectionIds.Add(connectionId);
        Parameters.Add(new Dictionary<string, string>(parameters, StringComparer.Ordinal));
        if (Fail)
            return Task.FromResult(new ViewCallOutcome { Code = FailCode, Message = FailMessage });
        return Task.FromResult(new ViewCallOutcome
        {
            Ok = true,
            Columns = Columns.ToList(),
            Rows = Rows.Select(row => row.ToList()).ToList(),
        });
    }
}

internal sealed class RecordingConnections : IConnectionCount
{
    public int Available { get; set; } = 1;
    public string Name { get; set; } = "warehouse";

    public Task<ConnectionTally> TallyAsync(CancellationToken cancellationToken)
        => Task.FromResult(new ConnectionTally
        {
            Available = Available,
            OnlyName = Available == 1 ? Name : null,
        });
}
