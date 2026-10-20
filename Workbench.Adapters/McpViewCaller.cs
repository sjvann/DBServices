using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class McpConnectionCounter : IConnectionCount
{
    private readonly HttpClient _http;
    private readonly Uri? _base;

    public McpConnectionCounter(HttpClient http, string? baseUrl)
    {
        _http = http;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _base = uri;
    }

    public async Task<ConnectionTally> TallyAsync(CancellationToken cancellationToken)
    {
        if (_base is null)
            return new ConnectionTally();

        try
        {
            using var response = await _http.GetAsync(new Uri(_base, "connections"), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new ConnectionTally();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new ConnectionTally();
            var names = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() is { Length: > 0 } text)
                    names.Add(text);
            }

            return new ConnectionTally
            {
                Available = names.Count,
                OnlyName = names.Count == 1 ? names[0] : null,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            return new ConnectionTally();
        }
    }
}

public sealed class McpViewCaller : IViewCaller
{
    private readonly HttpClient _http;
    private readonly Uri? _endpoint;

    public McpViewCaller(HttpClient http, string? baseUrl)
    {
        _http = http;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _endpoint = new Uri(uri, "mcp");
    }

    public async Task<ViewCallOutcome> CallAsync(
        string viewName,
        string connectionId,
        IReadOnlyDictionary<string, string> parameters,
        string callerId,
        CancellationToken cancellationToken)
    {
        if (_endpoint is null || string.IsNullOrWhiteSpace(viewName))
            return Failed("connection_failed", "無法建立連線。");

        var arguments = new Dictionary<string, object?> { ["connectionId"] = connectionId };
        foreach (var pair in parameters)
        {
            if (pair.Key.Equals("filters", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("connectionId", StringComparison.OrdinalIgnoreCase))
                continue;
            arguments[pair.Key] = pair.Value;
        }

        try
        {
            var options = new HttpClientTransportOptions
            {
                Endpoint = _endpoint,
                Name = "workbench",
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-DBServices-Caller"] = string.IsNullOrWhiteSpace(callerId) ? "anonymous" : callerId,
                },
            };
            await using var transport = new HttpClientTransport(options, _http, loggerFactory: null, ownsHttpClient: false);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var result = await client.CallToolAsync(viewName, arguments, cancellationToken: cancellationToken);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
            if (result.IsError == true)
                return ReadError(text);
            return ReadRows(text);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or IOException)
        {
            return Failed("connection_failed", "無法建立連線。");
        }
    }

    private static ViewCallOutcome ReadError(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var code = document.RootElement.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String
                ? codeElement.GetString() ?? ""
                : "";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? ""
                : "";
            return Failed(code, message);
        }
        catch (JsonException)
        {
            return Failed("connection_failed", "無法建立連線。");
        }
    }

    private static ViewCallOutcome ReadRows(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var columns = document.RootElement.GetProperty("columns").EnumerateArray().Select(item => item.GetString() ?? "").ToList();
            var rows = new List<List<string>>();
            foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
                rows.Add(row.EnumerateArray().Select(Cell).ToList());
            return new ViewCallOutcome { Ok = true, Columns = columns, Rows = rows };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Failed("execution_failed", "鍵值查詢失敗。");
        }
    }

    private static string Cell(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null => "",
        _ => element.GetRawText(),
    };

    private static ViewCallOutcome Failed(string code, string message) => new()
    {
        Code = code,
        Message = message,
    };
}
