using System.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Tests;

internal sealed class WorkbenchHost : WebApplicationFactory<Program>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _root;
    public RecordingSurface Surface { get; }
    public RecordingCatalog Catalog { get; }
    public RecordingAudit Audit { get; }
    public RecordingSidecar Sidecar { get; }
    public string SqlitePath { get; }
    public bool PreserveRoot { get; }
    public string AdminName { get; }
    public string AdminPassword { get; }
    public bool SeedAdmin { get; }

    public WorkbenchHost(Action<HostOptions>? configure = null)
    {
        var options = new HostOptions();
        configure?.Invoke(options);
        _root = options.ContentRoot ?? Path.Combine(Path.GetTempPath(), "wb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Surface = options.Surface ?? new RecordingSurface();
        Catalog = options.Catalog ?? new RecordingCatalog();
        Audit = options.Audit ?? new RecordingAudit();
        Sidecar = options.Sidecar ?? new RecordingSidecar();
        SqlitePath = Path.Combine(_root, "workbench.db");
        PreserveRoot = options.PreserveRoot;
        AdminName = options.AdminName;
        AdminPassword = options.AdminPassword;
        SeedAdmin = options.SeedAdmin;
        Options = options;
    }

    public HostOptions Options { get; }
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
                ["Surfaces:Web:ServerType"] = Options.WebServerType,
                ["Surfaces:Mcp:BaseUrl"] = "http://127.0.0.1:9",
                ["Surfaces:Web:BaseUrl"] = "http://127.0.0.1:9",
                ["Model:enabled"] = Options.ModelEnabled ? "true" : "false",
                ["Model:placement"] = "local",
                ["Model:endpoint"] = "http://127.0.0.1:9",
                ["Model:modelId"] = "unit",
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
            if (Options.UseRecordingSurface)
                services.AddSingleton<IDataSurface>(Surface);
            else if (Options.SurfaceOverride is not null)
                services.AddSingleton(Options.SurfaceOverride);
            if (Options.UseRecordingCatalog)
                services.AddSingleton<IConnectionCatalog>(Catalog);
            else if (Options.CatalogOverride is not null)
                services.AddSingleton(Options.CatalogOverride);
            if (Options.UseRecordingAudit)
                services.AddSingleton<IAuditReader>(Audit);
            else if (Options.AuditOverride is not null)
                services.AddSingleton(Options.AuditOverride);
            services.AddSingleton<IModelSidecar>(Sidecar);
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
            throw new InvalidOperationException(((int)response.StatusCode).ToString());
        var sessionId = JsonSerializer.Deserialize<SessionResponse>(body, Json)!.SessionId;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
        var me = await client.GetFromJsonAsync<MeResponse>("api/me", Json);
        return new Session(client, sessionId, me!);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
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
    public string WebServerType { get; set; } = "Sqlite";
    public bool ModelEnabled { get; set; }
    public bool UseRecordingSurface { get; set; } = true;
    public bool UseRecordingCatalog { get; set; } = true;
    public bool UseRecordingAudit { get; set; } = true;
    public RecordingSurface? Surface { get; set; }
    public RecordingCatalog? Catalog { get; set; }
    public RecordingAudit? Audit { get; set; }
    public RecordingSidecar? Sidecar { get; set; }
    public IDataSurface? SurfaceOverride { get; set; }
    public IConnectionCatalog? CatalogOverride { get; set; }
    public IAuditReader? AuditOverride { get; set; }
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

    public async Task<JsonElement> GetAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        return await ReadAsync(response);
    }

    public async Task<JsonElement> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        using var response = await Client.SendAsync(request);
        return await ReadAsync(response);
    }

    public async Task<(int Status, JsonElement Body)> RawAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        using var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
        return ((int)response.StatusCode, document.RootElement.Clone());
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(((int)response.StatusCode).ToString());
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text);
        return document.RootElement.Clone();
    }
}

internal sealed class RecordingSurface : IDataSurface
{
    public List<string> Calls { get; } = [];
    public List<string> ParameterKeys { get; } = [];
    public List<string> Tables { get; set; } = ["products"];
    public Dictionary<string, List<FieldDto>> Fields { get; } = new(StringComparer.Ordinal)
    {
        ["products"] = [new FieldDto { Name = "Id", Type = "INTEGER", PrimaryKey = true, NotNull = true }, new FieldDto { Name = "Name", Type = "TEXT", NotNull = true }],
        ["salary"] = [new FieldDto { Name = "Amount", Type = "INTEGER", NotNull = true }],
    };
    public List<List<string>> Rows { get; set; } = [["1", "one"]];
    public decimal CountValue { get; set; } = 1;
    public string? FailQueryConnection { get; set; }
    public bool FailCount { get; set; }
    public string? UnknownTable { get; set; }
    public bool IncludeViewSeen { get; set; }

    public Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        Note("tables", request);
        IncludeViewSeen = request.IncludeViewOmitted;
        return Task.FromResult(new SurfaceOutcome { Ok = true, Tool = "tables", Tables = Tables.ToList() });
    }

    public Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        Note("fields", request);
        if (request.Table == UnknownTable)
            return Task.FromResult(Fail("fields", "unknown_table", "找不到資料表。"));
        if (request.Table is null || !Fields.TryGetValue(request.Table, out var fields))
            return Task.FromResult(Fail("fields", "unknown_table", "找不到資料表。"));
        return Task.FromResult(new SurfaceOutcome { Ok = true, Tool = "fields", Fields = fields.ToList() });
    }

    public Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        Note("query", request);
        if (request.Connection == FailQueryConnection)
            return Task.FromResult(Fail("query", "connection_failed", "無法建立連線。"));
        return Task.FromResult(new SurfaceOutcome
        {
            Ok = true,
            Tool = "query",
            Columns = ["Id", "Name"],
            Rows = Rows.Select(row => row.ToList()).ToList(),
            ParameterKeys = request.Filters.Select(filter => filter.Key).ToList(),
        });
    }

    public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        Note("count", request);
        if (FailCount || request.Connection == FailQueryConnection)
            return Task.FromResult(Fail("count", FailCount ? "execution_failed" : "connection_failed", "無法完成。"));
        return Task.FromResult(new SurfaceOutcome
        {
            Ok = true,
            Tool = "count",
            Count = CountValue,
            ParameterKeys = request.Filters.Select(filter => filter.Key).ToList(),
        });
    }

    public void AssertAllowed()
    {
        foreach (var call in Calls)
            AssertTool(call);
    }

    public static void AssertTool(string tool) =>
        Assert.IsTrue(tool is "tables" or "fields" or "query" or "count");

    private void Note(string tool, SurfaceRequest request)
    {
        Calls.Add(tool);
        foreach (var filter in request.Filters)
            ParameterKeys.Add(filter.Key);
    }

    private static SurfaceOutcome Fail(string tool, string code, string message) => new()
    {
        Ok = false,
        Tool = tool,
        ErrorCode = code,
        ErrorMessage = message,
    };
}

internal sealed class RecordingCatalog : IConnectionCatalog
{
    public List<ConnectionDto> Mcp { get; set; } =
    [
        new ConnectionDto { Name = "inventory", ServerType = "SqlServer" },
        new ConnectionDto { Name = "inventory_pg", ServerType = "PostgreSQL" },
    ];
    public bool Fail { get; set; }
    public string WebServerType { get; set; } = "Sqlite";

    public Task<CatalogOutcome> ListAsync(string surface, CancellationToken cancellationToken)
    {
        if (Fail)
            return Task.FromResult(new CatalogOutcome { ErrorCode = "connection_failed", ErrorMessage = "無法建立連線。" });
        if (surface == SurfaceNames.Web)
            return Task.FromResult(new CatalogOutcome { Connections = [new ConnectionDto { Name = "default", ServerType = WebServerType }] });
        return Task.FromResult(new CatalogOutcome { Connections = Mcp.ToList() });
    }
}

internal sealed class RecordingAudit : IAuditReader
{
    public List<AuditEntry> Entries { get; } = [];
    public bool ThrowOnRead { get; set; }

    public AuditRead ReadAll()
    {
        if (ThrowOnRead)
            throw new IOException("locked");
        return new AuditRead { Entries = Entries.ToList() };
    }
}

internal sealed class RecordingSidecar : IModelSidecar
{
    public List<SidecarRequest> Requests { get; } = [];
    public SidecarResponse Next { get; set; } = new() { Code = ModelCodes.Ok, Message = "ok", Started = true, Stdout = "" };

    public Task<SidecarResponse> InvokeAsync(SidecarRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Next);
    }
}
