using System.Net.Http.Json;
using System.Text.Json;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class McpConnectionAdmin : IConnectionAdmin
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _catalogKey;

    public McpConnectionAdmin(HttpClient http, string? baseUrl, string? catalogKey)
    {
        _http = http;
        _catalogKey = catalogKey ?? "";
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _http.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    public async Task<IReadOnlyList<NamedConnection>> ListAsync(CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return [];
        try
        {
            using var response = await _http.GetAsync("connections", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];
            var rows = await response.Content.ReadFromJsonAsync<List<NamedConnection>>(Json, cancellationToken);
            return rows ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            return [];
        }
    }

    public Task<CatalogWriteResult> SaveAsync(string name, string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Post, "connections", callerId, new { name, serverType, connectionString }, cancellationToken);

    public Task<CatalogWriteResult> ProbeAsync(string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Post, "connections/probe", callerId, new { serverType, connectionString }, cancellationToken);

    public async Task<DatabaseListResult> DatabasesAsync(string serverType, string connectionString, string callerId, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return DatabaseListResult.Fail("connection_failed", "尚未設定資料服務位址，無法讀取資料庫清單。");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "connections/databases");
            request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", _catalogKey);
            request.Headers.TryAddWithoutValidation("X-DBServices-Caller", callerId);
            request.Content = JsonContent.Create(new { serverType, connectionString }, options: Json);
            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failed = ReadFailure(text);
                return DatabaseListResult.Fail(failed.Code, failed.Message);
            }

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var names = new List<string>();
            if (document.RootElement.TryGetProperty("names", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } name)
                        names.Add(name);
                }
            }

            return DatabaseListResult.Success(names);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return DatabaseListResult.Fail("connection_failed", "資料服務沒有回應（" + _http.BaseAddress + "），無法讀取資料庫清單。");
        }
    }

    public Task<CatalogWriteResult> RemoveAsync(string name, string callerId, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Delete, "connections/" + Uri.EscapeDataString(name), callerId, null, cancellationToken);

    private async Task<CatalogWriteResult> SendAsync(HttpMethod method, string url, string callerId, object? body, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            return CatalogWriteResult.Fail("connection_failed", url == "connections/probe" ? "尚未設定資料服務位址，無法測試連線。" : "無法建立連線。");
        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("X-DBServices-Workbench", _catalogKey);
            request.Headers.TryAddWithoutValidation("X-DBServices-Caller", callerId);
            if (body is not null)
                request.Content = JsonContent.Create(body, options: Json);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                if (url != "connections/probe")
                    return CatalogWriteResult.Success(null);
                var note = await response.Content.ReadAsStringAsync(cancellationToken);
                return CatalogWriteResult.Noted(ReadMessage(note));
            }
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return ReadFailure(text);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            var message = url == "connections/probe"
                ? "資料服務沒有回應（" + _http.BaseAddress + "），無法測試連線。"
                : "無法建立連線。";
            return CatalogWriteResult.Fail("connection_failed", message);
        }
    }

    private static string ReadMessage(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            if (document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                var value = message.GetString() ?? "";
                if (value.Length > 0
                    && !value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
                    && !value.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }
        catch (JsonException)
        {
        }

        return "已連上資料庫。";
    }

    private static CatalogWriteResult ReadFailure(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var code = document.RootElement.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String
                ? codeValue.GetString() ?? ""
                : "";
            var message = document.RootElement.TryGetProperty("message", out var messageValue) && messageValue.ValueKind == JsonValueKind.String
                ? messageValue.GetString() ?? ""
                : "";
            if (code.Length == 0)
                code = "connection_failed";
            if (message.Length == 0 || message.Contains("Password=", StringComparison.OrdinalIgnoreCase) || message.Contains("Data Source", StringComparison.OrdinalIgnoreCase))
                message = "無法建立連線。";
            return CatalogWriteResult.Fail(code, message);
        }
        catch (JsonException)
        {
            return CatalogWriteResult.Fail("connection_failed", "無法建立連線。");
        }
    }
}
