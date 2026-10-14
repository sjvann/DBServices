using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class McpConnectionCatalog
{
    private readonly HttpClient _http;
    private readonly Uri? _endpoint;

    public McpConnectionCatalog(HttpClient http, string? baseUrl)
    {
        _http = http;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _endpoint = new Uri(uri, "connections");
    }

    public async Task<CatalogOutcome> ListAsync(CancellationToken cancellationToken)
    {
        if (_endpoint is null)
            return Failed("connection_failed", "無法建立連線。");

        try
        {
            using var response = await _http.GetAsync(_endpoint, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failed("connection_failed", "無法建立連線。");

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Failed("connection_failed", "無法建立連線。");

            var list = new List<ConnectionDto>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return Failed("connection_failed", "無法建立連線。");
                if (!item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                    return Failed("connection_failed", "無法建立連線。");
                if (!item.TryGetProperty("serverType", out var serverType) || serverType.ValueKind != JsonValueKind.String)
                    return Failed("connection_failed", "無法建立連線。");
                list.Add(new ConnectionDto { Name = name.GetString() ?? "", ServerType = serverType.GetString() ?? "" });
            }

            return new CatalogOutcome { Connections = list };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException)
        {
            return Failed("connection_failed", Scrub.Message(exception.Message));
        }
    }

    private static CatalogOutcome Failed(string code, string message) => new()
    {
        ErrorCode = code,
        ErrorMessage = message,
    };
}

public sealed class WebConnectionCatalog
{
    private readonly string _serverType;

    public WebConnectionCatalog(string serverType) => _serverType = serverType;

    public CatalogOutcome List() => new()
    {
        Connections = [new ConnectionDto { Name = "default", ServerType = _serverType }],
    };
}

public sealed class RoutingConnectionCatalog : IConnectionCatalog
{
    private readonly McpConnectionCatalog _mcp;
    private readonly WebConnectionCatalog _web;

    public RoutingConnectionCatalog(McpConnectionCatalog mcp, WebConnectionCatalog web)
    {
        _mcp = mcp;
        _web = web;
    }

    public Task<CatalogOutcome> ListAsync(string surface, CancellationToken cancellationToken)
    {
        if (surface == SurfaceNames.Web)
            return Task.FromResult(_web.List());
        if (surface == SurfaceNames.Mcp)
            return _mcp.ListAsync(cancellationToken);
        return Task.FromResult(new CatalogOutcome { ErrorCode = "forbidden", ErrorMessage = "沒有權限。" });
    }
}
