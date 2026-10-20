using System.Text.Json;
using DbServices.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(sp =>
{
    var connections = sp.GetRequiredService<IConfiguration>()
        .GetSection("Connections")
        .Get<Dictionary<string, ConnectionSetting>>()
        ?? new Dictionary<string, ConnectionSetting>();
    return new ToolCatalog(connections);
});
builder.Services.AddSingleton(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    var relative = sp.GetRequiredService<IConfiguration>()["Catalog:Path"];
    if (string.IsNullOrWhiteSpace(relative))
        relative = Path.Combine("catalog", "views.json");
    var path = Path.IsPathRooted(relative)
        ? relative
        : Path.GetFullPath(Path.Combine(environment.ContentRootPath, relative));
    return new ViewCatalog(path);
});

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
#pragma warning disable MCP9004
        options.EnableLegacySse = false;
#pragma warning restore MCP9004
    })
    .WithListToolsHandler((request, _) =>
    {
        var views = request.Server.Services!.GetRequiredService<ViewCatalog>();
        var tools = views.Published().Select(ToTool).ToList();
        return ValueTask.FromResult(new ListToolsResult { Tools = tools });
    })
    .WithCallToolHandler((request, _) =>
    {
        var services = request.Server.Services!;
        var name = request.Params?.Name ?? "";
        var views = services.GetRequiredService<ViewCatalog>();
        var view = views.FindPublishedByName(name);
        if (view is null)
        {
            return ValueTask.FromResult(ToolResult(TableDispatchResult.QueryExecutionFailed()));
        }

        var arguments = request.Params?.Arguments;
        var connectionId = ArgumentText(arguments, "connectionId") ?? "";
        var parameters = ReadParameters(arguments);
        var outcome = ViewDispatch.Execute(
            services.GetRequiredService<ToolCatalog>(),
            connectionId,
            view,
            parameters);
        var auditArguments = new List<KeyValuePair<string, object?>>();
        foreach (var pair in parameters)
            auditArguments.Add(new KeyValuePair<string, object?>(pair.Key, pair.Value));
        return ValueTask.FromResult(Finish(
            services.GetRequiredService<IWebHostEnvironment>(),
            services.GetRequiredService<IConfiguration>(),
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<ToolCatalog>(),
            connectionId,
            view.Name,
            auditArguments,
            outcome));
    });

var app = builder.Build();
app.MapGet("/connections", (ToolCatalog catalog) =>
{
    var payload = new List<Dictionary<string, string>>();
    foreach (var pair in catalog.Connections)
    {
        if (!pair.Value.Available)
            continue;

        payload.Add(new Dictionary<string, string>
        {
            ["name"] = pair.Key,
            ["serverType"] = pair.Value.ServerType,
        });
    }

    return Results.Json(payload);
});

app.MapGet("/views", (ViewCatalog views) => Results.Json(views.List()));
app.MapGet("/views/{id}", (string id, ViewCatalog views) =>
{
    var document = views.Find(id);
    return document is null ? Results.NotFound(new { code = "not_found", message = "找不到檢視。" }) : Results.Json(document);
});
app.MapPut("/views/{id}", async (string id, HttpContext http, ViewCatalog views, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var document = await http.Request.ReadFromJsonAsync<ViewDocument>() ?? new ViewDocument();
    document.Id = id;
    var result = views.Save(document);
    return result.Ok ? Results.Json(result.Document) : Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
});
app.MapDelete("/views/{id}", (string id, HttpContext http, ViewCatalog views, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var result = views.Delete(id);
    return result.Ok ? Results.Json(new { id }) : Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
});
app.MapPost("/views/{id}/publish", (string id, HttpContext http, ViewCatalog views, IConfiguration configuration) =>
    SetPublished(id, true, http, views, configuration));
app.MapPost("/views/{id}/unpublish", (string id, HttpContext http, ViewCatalog views, IConfiguration configuration) =>
    SetPublished(id, false, http, views, configuration));

app.MapMcp("/mcp");
app.Run();

static IResult SetPublished(string id, bool published, HttpContext http, ViewCatalog views, IConfiguration configuration)
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var result = views.SetPublished(id, published);
    return result.Ok ? Results.Json(result.Document) : Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
}

static IResult? RejectWrite(HttpContext http, IConfiguration configuration)
{
    var caller = http.Request.Headers["X-DBServices-Caller"].ToString();
    if (string.IsNullOrWhiteSpace(caller))
        return Results.Json(new { code = "invalid_arguments", message = "未提供呼叫者。" }, statusCode: 400);

    var expected = configuration["Workbench:CatalogKey"] ?? "";
    var actual = http.Request.Headers["X-DBServices-Workbench"].ToString();
    if (string.IsNullOrEmpty(expected) || !string.Equals(actual, expected, StringComparison.Ordinal))
        return Results.Json(new { code = "forbidden", message = "目錄金鑰不符。" }, statusCode: 403);

    return null;
}

static Tool ToTool(ViewDocument view)
{
    var properties = new Dictionary<string, object?>
    {
        ["connectionId"] = new Dictionary<string, object?> { ["type"] = "string" },
    };
    foreach (var parameter in view.Parameters)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || properties.ContainsKey(parameter.Name))
            continue;
        properties[parameter.Name] = new Dictionary<string, object?> { ["type"] = "string" };
    }

    return new Tool
    {
        Name = view.Name,
        Description = string.IsNullOrWhiteSpace(view.Purpose) ? view.Name : view.Purpose,
        InputSchema = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new[] { "connectionId" },
        }),
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
        },
    };
}

static Dictionary<string, object?> ReadParameters(IDictionary<string, JsonElement>? arguments)
{
    var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    if (arguments is null)
        return parameters;

    foreach (var pair in arguments)
    {
        if (pair.Key.Equals("connectionId", StringComparison.OrdinalIgnoreCase)
            || pair.Key.Equals("filters", StringComparison.OrdinalIgnoreCase))
            continue;
        if (TryScalar(pair.Value, out var scalar))
            parameters[pair.Key] = scalar;
    }

    return parameters;
}

static bool TryScalar(JsonElement value, out object? scalar)
{
    switch (value.ValueKind)
    {
        case JsonValueKind.Null:
            scalar = null;
            return true;
        case JsonValueKind.True:
            scalar = true;
            return true;
        case JsonValueKind.False:
            scalar = false;
            return true;
        case JsonValueKind.String:
            scalar = value.GetString();
            return true;
        case JsonValueKind.Number:
            scalar = value.TryGetInt64(out var integer) ? integer : value.GetDouble();
            return true;
        default:
            scalar = null;
            return false;
    }
}

static string? ArgumentText(IDictionary<string, JsonElement>? arguments, string name)
{
    if (arguments is null)
        return null;
    foreach (var pair in arguments)
    {
        if (!pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            continue;
        return pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : pair.Value.ToString();
    }

    return null;
}

static CallToolResult Finish(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpContextAccessor http,
    ToolCatalog catalog,
    string connectionId,
    string tool,
    List<KeyValuePair<string, object?>> arguments,
    TableDispatchResult outcome)
{
    string path;
    try
    {
        path = AuditPath(environment, configuration);
    }
    catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
    {
        return ToolResult(TableDispatchResult.QueryExecutionFailed());
    }

    return ToolResult(CallAudit.Commit(
        path,
        ReadCaller(http),
        ResolvedConnectionId(catalog, connectionId),
        tool,
        arguments,
        outcome));
}

static CallToolResult ToolResult(TableDispatchResult outcome)
    => new()
    {
        IsError = outcome.IsError,
        Content =
        [
            new TextContentBlock { Text = outcome.Json },
        ],
    };

static string AuditPath(IWebHostEnvironment environment, IConfiguration configuration)
{
    var relative = configuration["Audit:Path"];
    if (string.IsNullOrWhiteSpace(relative))
        relative = Path.Combine("audit", "mcp.jsonl");

    return Path.GetFullPath(Path.Combine(environment.ContentRootPath, relative));
}

static string ReadCaller(IHttpContextAccessor http)
{
    var caller = http.HttpContext?.Request.Headers["X-DBServices-Caller"].ToString();
    return string.IsNullOrWhiteSpace(caller) ? "anonymous" : caller;
}

static string ResolvedConnectionId(ToolCatalog catalog, string connectionId)
{
    foreach (var key in catalog.Connections.Keys)
    {
        if (string.Equals(key, connectionId, StringComparison.OrdinalIgnoreCase))
            return key;
    }

    return connectionId;
}

public partial class Program;
