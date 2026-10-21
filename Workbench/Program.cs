using Microsoft.AspNetCore.Mvc;
using Workbench;
using Workbench.Adapters;
using Workbench.Core;
using Workbench.Core.Ports;
using Workbench.Storage.Sqlite;
using Workbench.Web;
using Workbench.Web.Components;

var builder = WebApplication.CreateBuilder(args);
var contentRoot = builder.Environment.ContentRootPath;
var sqlitePath = builder.Configuration["Workbench:SqlitePath"];
if (string.IsNullOrWhiteSpace(sqlitePath))
    sqlitePath = Path.Combine(contentRoot, "workbench.db");
else if (!Path.IsPathRooted(sqlitePath))
    sqlitePath = Path.Combine(contentRoot, sqlitePath);

var mcpBase = builder.Configuration["Surfaces:Mcp:BaseUrl"] ?? "";
var catalogKey = builder.Configuration["Workbench:CatalogKey"] ?? "";

builder.Services.AddSingleton(new SqliteDatabase(sqlitePath));
builder.Services.AddSingleton<IOperatorStore, SqliteOperatorStore>();
builder.Services.AddSingleton<IStyleStore, SqliteStyleStore>();
builder.Services.AddSingleton<IRecordStore, SqliteRecordStore>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISessionStore, MemorySessionStore>();
builder.Services.AddSingleton<IReportMemory, MemoryReportStore>();
builder.Services.AddSingleton<IReportPdf, SnapshotPdfWriter>();
builder.Services.AddSingleton<IViewCatalog>(new ViewCatalogClient(new HttpClient(), mcpBase, catalogKey));
builder.Services.AddSingleton<IConnectionCount>(new McpConnectionCounter(new HttpClient(), mcpBase));
builder.Services.AddSingleton<IConnectionAdmin>(new McpConnectionAdmin(new HttpClient(), mcpBase, catalogKey));
builder.Services.AddSingleton<IModelProfile>(new FileModelProfile(Path.Combine(contentRoot, "model.json")));
builder.Services.AddSingleton<IViewCaller>(new McpViewCaller(new HttpClient(), mcpBase));
builder.Services.AddSingleton<Profiles>();
builder.Services.AddSingleton<SeedAdminService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<OperatorService>();
builder.Services.AddSingleton<EnvironmentService>();
builder.Services.AddSingleton<ViewService>();
builder.Services.AddSingleton<StyleService>();
builder.Services.AddSingleton<UtteranceService>();
builder.Services.AddSingleton<RecordService>();
builder.Services.AddSingleton<ExportService>();
builder.Services.AddScoped<CircuitSession>();
builder.Services.AddScoped<OperatorGate>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers(options => options.Filters.Add(new IgnoreAntiforgeryTokenAttribute()))
    .AddApplicationPart(typeof(CircuitSession).Assembly);

var app = builder.Build();
var operators = app.Services.GetRequiredService<IOperatorStore>();
var seed = app.Services.GetRequiredService<SeedAdminService>();
if (operators.Count() == 0)
    seed.Ensure(app.Configuration["Workbench:InitialAdmin:DisplayName"], app.Configuration["Workbench:InitialAdmin:Password"]);
else
    seed.Ensure(null, null);

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
        await context.RequestServices.GetRequiredService<EnvironmentService>().RefreshAsync(context.RequestAborted);

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

public partial class Program;
