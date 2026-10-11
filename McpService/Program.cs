using System.Text.Json;
using DbServices.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

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
                    CallTables(catalog, connectionId, includeView),
                options);
        }

        if (name == "fields")
        {
            return McpServerTool.Create(
                (string connectionId, string? table = null) =>
                    CallFields(catalog, connectionId, table),
                options);
        }

        if (name == "get")
        {
            return McpServerTool.Create(
                (string connectionId, string? table = null, JsonElement? id = null) =>
                    CallGet(catalog, connectionId, table, id),
                options);
        }

        return McpServerTool.Create(NotExecuted, options);
    });
}

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

static CallToolResult CallTables(ToolCatalog catalog, string connectionId, JsonElement? includeView)
{
    var outcome = TableDispatch.Execute(catalog, connectionId, IncludeViewArgument(includeView));
    return new CallToolResult
    {
        IsError = outcome.IsError,
        Content =
        [
            new TextContentBlock { Text = outcome.Json },
        ],
    };
}

static CallToolResult CallFields(ToolCatalog catalog, string connectionId, string? table)
{
    var outcome = TableDispatch.ExecuteFields(catalog, connectionId, table);
    return new CallToolResult
    {
        IsError = outcome.IsError,
        Content =
        [
            new TextContentBlock { Text = outcome.Json },
        ],
    };
}

static CallToolResult CallGet(ToolCatalog catalog, string connectionId, string? table, JsonElement? id)
{
    var outcome = TableDispatch.ExecuteGet(catalog, connectionId, table, id);
    return new CallToolResult
    {
        IsError = outcome.IsError,
        Content =
        [
            new TextContentBlock { Text = outcome.Json },
        ],
    };
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
