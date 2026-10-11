using DbServices.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

var toolDefinitions = new ToolCatalog([]).Tools;
builder.Services.AddSingleton(sp =>
{
    var connections = sp.GetRequiredService<IConfiguration>()
        .GetSection("Connections")
        .Get<List<ConnectionSetting>>() ?? [];
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
    })
    .WithTools(toolDefinitions.Select(tool => McpServerTool.Create(
        NotExecuted,
        new McpServerToolCreateOptions
        {
            Name = tool.Name,
            Description = tool.Description,
            ReadOnly = tool.ReadOnly,
            Destructive = !tool.NonDestructive,
        })));

var app = builder.Build();
app.Services.GetRequiredService<ToolCatalog>();
app.MapMcp("/mcp");
app.Run();

static string NotExecuted() => throw new InvalidOperationException("工具未執行。");

public partial class Program;
