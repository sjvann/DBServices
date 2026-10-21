using System.Net.Http.Json;
using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class SchemaBrowserClient : ISchemaBrowser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _catalogKey;

    public SchemaBrowserClient(HttpClient http, string? baseUrl, string? catalogKey)
    {
        _http = http;
        _catalogKey = catalogKey ?? "";
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _http.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    public async Task<SchemaReadResult> GraphAsync(string connectionId, string callerId, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return SchemaReadResult.Fail("connection_failed", "無法建立連線。");

        try
        {
            var url = "schema/graph?connectionId=" + Uri.EscapeDataString(connectionId);
            using var response = await SendAsync(HttpMethod.Get, url, callerId, null, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ReadFailure(text, "讀取資料表結構失敗。");
            var graph = JsonSerializer.Deserialize<SchemaGraphDto>(text, Json);
            return graph is null
                ? SchemaReadResult.Fail("execution_failed", "讀取資料表結構失敗。")
                : SchemaReadResult.FromGraph(graph);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return SchemaReadResult.Fail("connection_failed", "無法建立連線。");
        }
    }

    public async Task<SchemaReadResult> PreviewAsync(
        string connectionId,
        ViewDocumentDto document,
        IReadOnlyDictionary<string, string> parameters,
        string callerId,
        CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return SchemaReadResult.Fail("connection_failed", "無法建立連線。");

        try
        {
            var body = new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId,
                ["view"] = document,
                ["parameters"] = parameters,
            };
            using var response = await SendAsync(HttpMethod.Post, "schema/preview", callerId, body, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ReadFailure(text, "鍵值查詢失敗。");
            var preview = JsonSerializer.Deserialize<SchemaPreviewDto>(text, Json);
            return preview is null
                ? SchemaReadResult.Fail("execution_failed", "鍵值查詢失敗。")
                : SchemaReadResult.FromPreview(preview);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return SchemaReadResult.Fail("connection_failed", "無法建立連線。");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string callerId, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", _catalogKey);
        request.Headers.TryAddWithoutValidation("X-DBServices-Caller", callerId);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);
        return await _http.SendAsync(request, cancellationToken);
    }

    private static SchemaReadResult ReadFailure(string text, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var code = document.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() ?? "" : "";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "" : "";
            return SchemaReadResult.Fail(code.Length == 0 ? "execution_failed" : code, message.Length == 0 ? fallback : message);
        }
        catch (JsonException)
        {
            return SchemaReadResult.Fail("execution_failed", fallback);
        }
    }
}
