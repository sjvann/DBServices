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

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
#pragma warning disable MCP9004
        options.EnableLegacySse = false;
#pragma warning restore MCP9004
    });

foreach (var definition in ToolCatalog.Definitions)
{
    var name = definition.Name;
    builder.Services.AddSingleton(sp =>
    {
        var catalog = sp.GetRequiredService<ToolCatalog>();
        var environment = sp.GetRequiredService<IWebHostEnvironment>();
        var configuration = sp.GetRequiredService<IConfiguration>();
        var http = sp.GetRequiredService<IHttpContextAccessor>();
        var tool = catalog.Tools.Single(candidate => candidate.Name == name);
        var options = new McpServerToolCreateOptions
        {
            Name = tool.Name,
            Description = tool.Description,
            ReadOnly = tool.ReadOnly,
            Destructive = !tool.NonDestructive,
        };

        if (name == "tables")
        {
            return McpServerTool.Create(
                (string connectionId, JsonElement? includeView = null) =>
                    CallTables(catalog, environment, configuration, http, connectionId, includeView),
                options);
        }

        if (name == "fields")
        {
            return McpServerTool.Create(
                (string connectionId, string? table = null) =>
                    CallFields(catalog, environment, configuration, http, connectionId, table),
                options);
        }

        if (name == "get")
        {
            return McpServerTool.Create(
                (string connectionId, string? table = null, JsonElement? id = null) =>
                    CallGet(catalog, environment, configuration, http, connectionId, table, id),
                options);
        }

        if (name is "query" or "count" or "exists")
        {
            return McpServerTool.Create(
                (string connectionId, string? table = null, JsonElement? filters = null) =>
                    CallEquality(catalog, environment, configuration, http, name, connectionId, table, filters),
                options);
        }

        return McpServerTool.Create(NotExecuted, options);
    });
}

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
app.MapMcp("/mcp");
app.Run();

static CallToolResult CallTables(
    ToolCatalog catalog,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpContextAccessor http,
    string connectionId,
    JsonElement? includeView)
{
    var outcome = TableDispatch.Execute(catalog, connectionId, IncludeViewArgument(includeView));
    var arguments = new List<KeyValuePair<string, object?>>();
    AddJson(arguments, "includeView", includeView);
    return Finish(environment, configuration, http, catalog, connectionId, "tables", arguments, outcome);
}

static CallToolResult CallFields(
    ToolCatalog catalog,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpContextAccessor http,
    string connectionId,
    string? table)
{
    var outcome = TableDispatch.ExecuteFields(catalog, connectionId, table);
    var arguments = new List<KeyValuePair<string, object?>>();
    AddText(arguments, "table", table);
    return Finish(environment, configuration, http, catalog, connectionId, "fields", arguments, outcome);
}

static CallToolResult CallGet(
    ToolCatalog catalog,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpContextAccessor http,
    string connectionId,
    string? table,
    JsonElement? id)
{
    var outcome = TableDispatch.ExecuteGet(catalog, connectionId, table, id);
    var arguments = new List<KeyValuePair<string, object?>>();
    AddText(arguments, "table", table);
    AddJson(arguments, "id", id);
    return Finish(environment, configuration, http, catalog, connectionId, "get", arguments, outcome);
}

static CallToolResult CallEquality(
    ToolCatalog catalog,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IHttpContextAccessor http,
    string name,
    string connectionId,
    string? table,
    JsonElement? filters)
{
    var outcome = name switch
    {
        "count" => TableDispatch.ExecuteCount(catalog, connectionId, table, filters),
        "exists" => TableDispatch.ExecuteExists(catalog, connectionId, table, filters),
        _ => TableDispatch.ExecuteQuery(catalog, connectionId, table, filters),
    };
    var arguments = new List<KeyValuePair<string, object?>>();
    AddText(arguments, "table", table);
    AddJson(arguments, "filters", filters);
    return Finish(environment, configuration, http, catalog, connectionId, name, arguments, outcome);
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
        return ToolResult(AuditFailed(tool));
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

static TableDispatchResult AuditFailed(string tool) => tool switch
{
    "fields" => TableDispatchResult.FieldsExecutionFailed(),
    "get" => TableDispatchResult.GetExecutionFailed(),
    "query" => TableDispatchResult.QueryExecutionFailed(),
    "count" => TableDispatchResult.CountExecutionFailed(),
    "exists" => TableDispatchResult.ExistsExecutionFailed(),
    _ => TableDispatchResult.ExecutionFailed(),
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

static void AddText(List<KeyValuePair<string, object?>> arguments, string key, string? value)
{
    if (value is not null)
        arguments.Add(new KeyValuePair<string, object?>(key, value));
}

static void AddJson(List<KeyValuePair<string, object?>> arguments, string key, JsonElement? value)
{
    if (value is { } element && element.ValueKind != JsonValueKind.Undefined)
        arguments.Add(new KeyValuePair<string, object?>(key, element));
}

static object? IncludeViewArgument(JsonElement? includeView)
{
    if (includeView is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        return null;

    return value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => value.GetString(),
        _ => value.GetRawText(),
    };
}

static string NotExecuted() => throw new InvalidOperationException("工具未執行。");

public partial class Program;
