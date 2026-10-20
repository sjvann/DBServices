using System.Net.Http.Json;
using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class ViewCatalogClient : IViewCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _catalogKey;

    public ViewCatalogClient(HttpClient http, string? baseUrl, string? catalogKey)
    {
        _http = http;
        _catalogKey = catalogKey ?? "";
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _http.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    public async Task<IReadOnlyList<ViewDocumentDto>> ListAsync(string callerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "views", callerId, null, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw Failure(text);
        return JsonSerializer.Deserialize<List<ViewDocumentDto>>(text, Json) ?? throw Failure(text);
    }

    public async Task<ViewDocumentDto?> FindAsync(string id, string callerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "views/" + Uri.EscapeDataString(id), callerId, null, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            throw Failure(text);
        return JsonSerializer.Deserialize<ViewDocumentDto>(text, Json) ?? throw Failure(text);
    }

    public Task<CatalogWriteResult> SaveAsync(ViewDocumentDto document, string callerId, CancellationToken cancellationToken)
        => WriteAsync(HttpMethod.Put, "views/" + Uri.EscapeDataString(document.Id), callerId, document, cancellationToken);

    public Task<CatalogWriteResult> DeleteAsync(string id, string callerId, CancellationToken cancellationToken)
        => WriteAsync(HttpMethod.Delete, "views/" + Uri.EscapeDataString(id), callerId, null, cancellationToken);

    public Task<CatalogWriteResult> SetPublishedAsync(string id, bool published, string callerId, CancellationToken cancellationToken)
        => WriteAsync(HttpMethod.Post, "views/" + Uri.EscapeDataString(id) + (published ? "/publish" : "/unpublish"), callerId, new { }, cancellationToken);

    private async Task<CatalogWriteResult> WriteAsync(HttpMethod method, string url, string callerId, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, url, callerId, body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ReadFailure(text);
        if (method == HttpMethod.Delete)
            return CatalogWriteResult.Success(null);
        try
        {
            var document = JsonSerializer.Deserialize<ViewDocumentDto>(text, Json);
            return CatalogWriteResult.Success(document);
        }
        catch (JsonException)
        {
            return CatalogWriteResult.Fail("execution_failed", "目錄沒有寫入。");
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

    private static InvalidOperationException Failure(string text)
    {
        var result = ReadFailure(text);
        var message = string.IsNullOrWhiteSpace(result.Message) ? "目錄沒有讀取。" : result.Message;
        return new InvalidOperationException(message);
    }

    private static CatalogWriteResult ReadFailure(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var code = document.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() ?? "" : "";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "" : "";
            return CatalogWriteResult.Fail(code, message);
        }
        catch (JsonException)
        {
            return CatalogWriteResult.Fail("execution_failed", "目錄沒有寫入。");
        }
    }
}
