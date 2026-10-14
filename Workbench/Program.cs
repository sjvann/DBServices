using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Workbench;
using Workbench.Adapters;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Storage.Sqlite;
using Workbench.Web;
using Workbench.Web.Components;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = StripApiKey(args),
});
InsertModelFile(builder);

var contentRoot = builder.Environment.ContentRootPath;
var sqlitePath = builder.Configuration["Workbench:SqlitePath"];
if (string.IsNullOrWhiteSpace(sqlitePath))
    sqlitePath = Path.Combine(contentRoot, "workbench.db");
else if (!Path.IsPathRooted(sqlitePath))
    sqlitePath = Path.Combine(contentRoot, sqlitePath);

var mcpBase = builder.Configuration["Surfaces:Mcp:BaseUrl"] ?? "";
var webBase = builder.Configuration["Surfaces:Web:BaseUrl"] ?? "";
var webServerType = builder.Configuration["Surfaces:Web:ServerType"] ?? "";
var auditPath = builder.Configuration["Audit:Path"] ?? "";
if (!string.IsNullOrWhiteSpace(auditPath) && !Path.IsPathRooted(auditPath))
    auditPath = Path.Combine(contentRoot, auditPath);
var sidecarPath = builder.Configuration["ModelSidecar:Path"] ?? "";
var modelPath = Path.Combine(contentRoot, "model.json");

builder.Services.AddSingleton(new SqliteDatabase(sqlitePath));
builder.Services.AddSingleton<IDirectory, SqliteDirectory>();
builder.Services.AddSingleton<ITemplateStore, SqliteTemplateStore>();
builder.Services.AddSingleton<IFavoriteStore, SqliteFavoriteStore>();
builder.Services.AddSingleton<IKeywordStore, SqliteKeywordStore>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISessionStore, MemorySessionStore>();
builder.Services.AddSingleton<IPreviewMemory, MemoryPreviewStore>();
builder.Services.AddSingleton<IAuditReader>(new McpAuditReader(auditPath));
builder.Services.AddSingleton<ISnapshotPdfWriter, SnapshotPdfWriter>();
builder.Services.AddSingleton(new McpSurface(new HttpClient(), mcpBase));
builder.Services.AddSingleton(new WebSurface(new HttpClient(), WithSlash(webBase)));
builder.Services.AddSingleton<IDataSurface, RoutingDataSurface>();
builder.Services.AddSingleton(new McpConnectionCatalog(new HttpClient(), mcpBase));
builder.Services.AddSingleton(new WebConnectionCatalog(webServerType));
builder.Services.AddSingleton<IConnectionCatalog, RoutingConnectionCatalog>();
builder.Services.AddSingleton<IModelSidecar>(new SidecarLauncher(sidecarPath, TimeSpan.FromSeconds(30)));
builder.Services.AddSingleton<Profiles>();
builder.Services.AddSingleton<SeedAdminService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<RoleRailService>();
builder.Services.AddSingleton<FillExecutor>();
builder.Services.AddSingleton<LivePreviewService>();
builder.Services.AddSingleton<TemplateLibraryService>();
builder.Services.AddSingleton<SuggestionService>();
builder.Services.AddSingleton(sp => new ModelSettingsService(
    sp.GetRequiredService<IModelSidecar>(),
    sp.GetRequiredService<Profiles>(),
    modelPath,
    ReadModel(builder.Configuration)));
builder.Services.AddSingleton<IntentService>();
builder.Services.AddSingleton<FavoriteService>();
builder.Services.AddSingleton<KeywordService>();
builder.Services.AddSingleton<SnapshotExportService>();
builder.Services.AddScoped<CircuitSession>();
builder.Services.AddScoped<OperatorGate>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers(options => options.Filters.Add(new IgnoreAntiforgeryTokenAttribute()))
    .AddApplicationPart(typeof(CircuitSession).Assembly);

var app = builder.Build();
var directory = app.Services.GetRequiredService<IDirectory>();
var seed = app.Services.GetRequiredService<SeedAdminService>();
if (directory.OperatorCount() == 0)
    seed.Ensure(app.Configuration["Workbench:InitialAdmin:DisplayName"], app.Configuration["Workbench:InitialAdmin:Password"]);
else
    seed.Ensure(null, null);

app.UseStaticFiles();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    if (seed.Unavailable && context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(WorkbenchMessages.Unavailable());
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api")
        && !(context.Request.Path.StartsWithSegments("/api/sessions") && HttpMethods.IsPost(context.Request.Method)))
    {
        var sessionId = ReadSession(context);
        var operatorId = app.Services.GetRequiredService<SessionService>().OperatorId(sessionId);
        if (operatorId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(WorkbenchMessages.Unauthenticated());
            return;
        }

        context.Items["sessionId"] = sessionId;
        context.Items["operatorId"] = operatorId;
    }

    await next();
});
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static string? ReadSession(HttpContext context)
{
    var header = context.Request.Headers.Authorization.ToString();
    if (header.StartsWith("Bearer ", StringComparison.Ordinal) && header.Length > 7)
        return header[7..].Trim();
    if (context.Request.Cookies.TryGetValue("wb_session", out var cookie) && !string.IsNullOrWhiteSpace(cookie))
        return cookie;
    return null;
}

static string WithSlash(string url) =>
    string.IsNullOrWhiteSpace(url) || url.EndsWith('/') ? url : url + "/";

static ModelSettings ReadModel(IConfiguration configuration) => new()
{
    Enabled = bool.TryParse(configuration["Model:enabled"], out var enabled) && enabled,
    Placement = configuration["Model:placement"] ?? "local",
    Endpoint = configuration["Model:endpoint"] ?? "",
    ModelId = configuration["Model:modelId"] ?? "",
    ApiKey = configuration["Model:apiKey"] ?? "",
};

static void InsertModelFile(WebApplicationBuilder builder)
{
    var sources = builder.Configuration.Sources;
    var index = sources.Count;
    for (var i = 0; i < sources.Count; i++)
    {
        var name = sources[i].GetType().Name;
        if (name.Contains("UserSecrets", StringComparison.Ordinal)
            || name.Contains("EnvironmentVariables", StringComparison.Ordinal)
            || name.Contains("CommandLine", StringComparison.Ordinal))
        {
            index = i;
            break;
        }
    }

    sources.Insert(index, new JsonConfigurationSource
    {
        Path = "model.json",
        Optional = true,
        ReloadOnChange = false,
        FileProvider = new PhysicalFileProvider(builder.Environment.ContentRootPath),
    });
}

static string[] StripApiKey(string[] args)
{
    var kept = new List<string>();
    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        var name = arg.TrimStart('-', '/');
        var key = name.Split('=', 2)[0];
        if (key.Contains("apiKey", StringComparison.OrdinalIgnoreCase))
        {
            if (!name.Contains('='))
                index++;
            continue;
        }

        kept.Add(arg);
    }

    return kept.ToArray();
}

public partial class Program;
