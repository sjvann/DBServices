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
    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    return new ConnectionDirectory(connections, Path.Combine(environment.ContentRootPath, "catalog", "connections.json"));
});
builder.Services.AddSingleton(sp => sp.GetRequiredService<ConnectionDirectory>().Catalog);
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
        var arguments = request.Params?.Arguments;
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var http = services.GetRequiredService<IHttpContextAccessor>();
        var directory = services.GetRequiredService<ConnectionDirectory>();
        var requestedConnection = (ArgumentText(arguments, "connectionId") ?? "").Trim();
        var auditArguments = AuditFromArguments(arguments);
        if (IsClosedTool(name))
        {
            return ValueTask.FromResult(Finish(
                environment,
                configuration,
                http,
                directory.Catalog,
                requestedConnection,
                name,
                auditArguments,
                TableDispatchResult.ToolDisabled()));
        }

        var views = services.GetRequiredService<ViewCatalog>();
        var view = views.FindPublishedByName(name);
        if (view is null)
        {
            return ValueTask.FromResult(Finish(
                environment,
                configuration,
                http,
                directory.Catalog,
                requestedConnection,
                name,
                auditArguments,
                TableDispatchResult.UnknownTool()));
        }

        if (RejectedArguments(view, arguments))
        {
            return ValueTask.FromResult(Finish(
                environment,
                configuration,
                http,
                directory.Catalog,
                requestedConnection,
                view.Name,
                auditArguments,
                TableDispatchResult.UnacceptableArguments()));
        }

        if (ParameterMissing(view, arguments))
        {
            return ValueTask.FromResult(Finish(
                environment,
                configuration,
                http,
                directory.Catalog,
                requestedConnection,
                view.Name,
                auditArguments,
                TableDispatchResult.InvalidArguments()));
        }

        var catalog = directory.Catalog;
        if (!TryResolveConnection(catalog, requestedConnection, out var connectionId, out var failure))
        {
            return ValueTask.FromResult(Finish(
                environment,
                configuration,
                http,
                catalog,
                connectionId,
                view.Name,
                auditArguments,
                failure!));
        }

        var parameters = ReadParameters(view, arguments);
        var outcome = ViewDispatch.Execute(catalog, connectionId, view, parameters);
        return ValueTask.FromResult(Finish(
            environment,
            configuration,
            http,
            catalog,
            connectionId,
            view.Name,
            AuditOf(parameters),
            outcome));
    });

var app = builder.Build();
app.MapGet("/connections", (ConnectionDirectory directory) => Results.Json(ConnectionPayload(directory.Catalog)));
app.MapPost("/connections", async (HttpContext http, ConnectionDirectory directory, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    ConnectionInput? input;
    try
    {
        input = await http.Request.ReadFromJsonAsync<ConnectionInput>();
    }
    catch (JsonException)
    {
        return Results.Json(new { code = "invalid_arguments", message = "連線內容不完整。" }, statusCode: 400);
    }

    var result = directory.Save(input?.Name, input?.ServerType, input?.ConnectionString);
    if (!result.Ok)
        return Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
    return Results.Json(new Dictionary<string, string> { ["name"] = result.Name, ["serverType"] = result.ServerType });
});
app.MapPost("/connections/databases", async (HttpContext http, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    ConnectionInput? input;
    try
    {
        input = await http.Request.ReadFromJsonAsync<ConnectionInput>();
    }
    catch (JsonException)
    {
        return Results.Json(new { code = "invalid_arguments", message = "連線內容不完整。" }, statusCode: 400);
    }

    var listed = await Task.Run(() => DbServices.Api.Shared.ConnectionProbe.ListDatabases(input?.ServerType, input?.ConnectionString), http.RequestAborted);
    return listed.Ok
        ? Results.Json(new { names = listed.Names })
        : Results.Json(new { code = listed.Code, message = listed.Message }, statusCode: 400);
});
app.MapPost("/connections/probe", async (HttpContext http, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    ConnectionInput? input;
    try
    {
        input = await http.Request.ReadFromJsonAsync<ConnectionInput>();
    }
    catch (JsonException)
    {
        return Results.Json(new { code = "invalid_arguments", message = "連線內容不完整。" }, statusCode: 400);
    }

    var probed = await Task.Run(() => DbServices.Api.Shared.ConnectionProbe.Try(input?.ServerType, input?.ConnectionString), http.RequestAborted);
    if (!probed.Ok)
        return Results.Json(new { code = probed.Code, message = probed.Message }, statusCode: 400);
    return Results.Json(new { message = probed.Message });
});
app.MapDelete("/connections/{name}", (string name, HttpContext http, ConnectionDirectory directory, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var result = directory.Remove(name);
    if (!result.Ok)
        return Results.Json(new { code = result.Code, message = result.Message }, statusCode: result.Code == "not_found" ? 404 : 400);
    return Results.Json(new Dictionary<string, string> { ["name"] = result.Name });
});

app.MapGet("/views", (HttpContext http, ViewCatalog views, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    return Results.Json(views.List());
});
app.MapGet("/views/{id}", (string id, HttpContext http, ViewCatalog views, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
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

app.MapGet("/schema/graph", (string? connectionId, HttpContext http, ConnectionDirectory directory, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var graph = SchemaGraph.Read(directory.Catalog, connectionId);
    return graph.Ok
        ? Results.Json(new { tables = graph.Tables })
        : Results.Json(new { code = graph.Code, message = graph.Message }, statusCode: 400);
});
app.MapPost("/schema/preview", async (HttpContext http, ConnectionDirectory directory, IConfiguration configuration) =>
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    SchemaPreviewInput? input;
    try
    {
        input = await http.Request.ReadFromJsonAsync<SchemaPreviewInput>();
    }
    catch (JsonException)
    {
        return Results.Json(new { code = "invalid_arguments", message = "請先選擇資料表。" }, statusCode: 400);
    }

    var preview = SchemaGraph.Preview(directory.Catalog, input?.ConnectionId, input?.View, input?.Parameters);
    return preview.Ok
        ? Results.Json(new { columns = preview.Columns, rows = preview.Rows, truncated = preview.Truncated })
        : Results.Json(new { code = preview.Code, message = preview.Message }, statusCode: 400);
});

app.MapMcp("/mcp");
app.Run();

static IResult SetPublished(string id, bool published, HttpContext http, ViewCatalog views, IConfiguration configuration)
{
    if (RejectWrite(http, configuration) is { } rejected)
        return rejected;
    var result = views.SetPublished(id, published);
    return result.Ok ? Results.Json(result.Document) : Results.Json(new { code = result.Code, message = result.Message }, statusCode: 400);
}

static List<Dictionary<string, string>> ConnectionPayload(ToolCatalog catalog)
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

    return payload;
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
    var required = new List<string>();
    foreach (var parameter in view.Parameters)
    {
        if (string.IsNullOrWhiteSpace(parameter.Name) || properties.ContainsKey(parameter.Name))
            continue;
        properties[parameter.Name] = new Dictionary<string, object?> { ["type"] = "string" };
        if (!parameter.Name.Equals("connectionId", StringComparison.OrdinalIgnoreCase))
            required.Add(parameter.Name);
    }

    return new Tool
    {
        Name = view.Name,
        Description = Describe(view),
        InputSchema = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        }),
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = true,
            DestructiveHint = false,
        },
    };
}

static bool ParameterMissing(ViewDocument view, IDictionary<string, JsonElement>? arguments)
{
    foreach (var parameter in view.Parameters ?? [])
    {
        if (string.IsNullOrWhiteSpace(parameter.Name))
            continue;
        if (!TryArgument(arguments, parameter.Name, out var element) || !TryScalar(element, out _))
            return true;
    }

    return false;
}

static bool TryArgument(IDictionary<string, JsonElement>? arguments, string name, out JsonElement element)
{
    element = default;
    if (arguments is null)
        return false;

    foreach (var pair in arguments)
    {
        if (!pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            continue;
        element = pair.Value;
        return true;
    }

    return false;
}

static string Describe(ViewDocument view)
{
    var parts = new List<string>();
    var purpose = view.Purpose?.Trim() ?? "";
    if (purpose.Length > 0)
        parts.Add(purpose);

    var sources = (view.Sources ?? []).Select(source => source?.Trim() ?? "").Where(source => source.Length > 0).ToList();
    if (sources.Count > 0)
        parts.Add("來源表：" + string.Join("、", sources));

    var links = new List<string>();
    foreach (var link in view.Links ?? [])
    {
        if (string.IsNullOrWhiteSpace(link.From) || string.IsNullOrWhiteSpace(link.To)
            || string.IsNullOrWhiteSpace(link.FromColumn) || string.IsNullOrWhiteSpace(link.ToColumn))
            continue;
        links.Add(link.From.Trim() + "." + link.FromColumn.Trim() + " = " + link.To.Trim() + "." + link.ToColumn.Trim());
    }

    if (links.Count > 0)
        parts.Add("關聯：" + string.Join("；", links));

    var columns = (view.Columns ?? []).Select(column => column.Name?.Trim() ?? "").Where(name => name.Length > 0).ToList();
    if (columns.Count > 0)
        parts.Add("輸出欄：" + string.Join("、", columns));

    var parameters = (view.Parameters ?? []).Select(parameter => parameter.Name?.Trim() ?? "").Where(name => name.Length > 0).ToList();
    if (parameters.Count > 0)
        parts.Add("參數：" + string.Join("、", parameters));

    return string.Join("。", parts);
}

static bool IsClosedTool(string name)
    => name.Equals("insert", StringComparison.OrdinalIgnoreCase)
        || name.Equals("update", StringComparison.OrdinalIgnoreCase)
        || name.Equals("delete", StringComparison.OrdinalIgnoreCase)
        || name.Equals("sql", StringComparison.OrdinalIgnoreCase);

static bool IsSecret(string name)
    => name.Equals("connectionString", StringComparison.OrdinalIgnoreCase)
        || name.Equals("connectString", StringComparison.OrdinalIgnoreCase);

static bool RejectedArguments(ViewDocument view, IDictionary<string, JsonElement>? arguments)
{
    if (arguments is null)
        return false;

    var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var parameter in view.Parameters ?? [])
    {
        if (!string.IsNullOrWhiteSpace(parameter.Name))
            declared.Add(parameter.Name);
    }

    foreach (var pair in arguments)
    {
        if (IsSecret(pair.Key))
            return true;
        if (pair.Key.Equals("connectionId", StringComparison.OrdinalIgnoreCase))
            continue;
        if (!declared.Contains(pair.Key))
            return true;
    }

    return false;
}

static bool TryResolveConnection(ToolCatalog catalog, string requested, out string connectionId, out TableDispatchResult? failure)
{
    failure = null;
    var trimmed = requested.Trim();
    if (trimmed.Length == 0)
    {
        string? only = null;
        var count = 0;
        foreach (var pair in catalog.Connections)
        {
            if (!pair.Value.Available)
                continue;
            count++;
            only = pair.Key;
        }

        if (count != 1 || only is null)
        {
            connectionId = "";
            failure = TableDispatchResult.ConnectionRequired();
            return false;
        }

        connectionId = only;
        return true;
    }

    string? found = null;
    ConnectionSnapshot? snapshot = null;
    foreach (var pair in catalog.Connections)
    {
        if (!pair.Key.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            continue;
        found = pair.Key;
        snapshot = pair.Value;
        break;
    }

    if (snapshot is null || found is null)
    {
        connectionId = trimmed;
        failure = TableDispatchResult.UnknownConnection();
        return false;
    }

    if (!snapshot.Available || string.IsNullOrWhiteSpace(snapshot.ConnectionString))
    {
        connectionId = found;
        failure = TableDispatchResult.ConnectionFailed();
        return false;
    }

    connectionId = found;
    return true;
}

static List<KeyValuePair<string, object?>> AuditFromArguments(IDictionary<string, JsonElement>? arguments)
{
    var list = new List<KeyValuePair<string, object?>>();
    if (arguments is null)
        return list;

    foreach (var pair in arguments)
    {
        if (pair.Key.Equals("connectionId", StringComparison.OrdinalIgnoreCase) || IsSecret(pair.Key))
            continue;
        if (TryScalar(pair.Value, out var scalar))
            list.Add(new KeyValuePair<string, object?>(pair.Key, scalar));
    }

    return list;
}

static List<KeyValuePair<string, object?>> AuditOf(IReadOnlyDictionary<string, object?> parameters)
{
    var list = new List<KeyValuePair<string, object?>>();
    foreach (var pair in parameters)
    {
        if (pair.Key.Equals("connectionId", StringComparison.OrdinalIgnoreCase) || IsSecret(pair.Key))
            continue;
        list.Add(new KeyValuePair<string, object?>(pair.Key, pair.Value));
    }

    return list;
}

static Dictionary<string, object?> ReadParameters(ViewDocument view, IDictionary<string, JsonElement>? arguments)
{
    var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    foreach (var parameter in view.Parameters ?? [])
    {
        if (string.IsNullOrWhiteSpace(parameter.Name))
            continue;
        if (!TryArgument(arguments, parameter.Name, out var element) || !TryScalar(element, out var scalar))
            continue;
        parameters[parameter.Name] = scalar;
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

sealed class SchemaPreviewInput
{
    public string? ConnectionId { get; set; }
    public ViewDocument? View { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public partial class Program;
