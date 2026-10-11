using DbServices.Mcp;
using ModelContextProtocol.AspNetCore;
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
        var tool = sp.GetRequiredService<ToolCatalog>().Tools.Single(candidate => candidate.Name == name);
        return McpServerTool.Create(
            NotExecuted,
            new McpServerToolCreateOptions
            {
                Name = tool.Name,
                Description = tool.Description,
                ReadOnly = tool.ReadOnly,
                Destructive = !tool.NonDestructive,
            });
    });
}

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();

static string NotExecuted() => throw new InvalidOperationException("工具未執行。");

public partial class Program;
