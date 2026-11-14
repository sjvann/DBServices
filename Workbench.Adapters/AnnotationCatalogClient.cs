using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class AnnotationCatalogClient : IAnnotationCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _catalogKey;

    public AnnotationCatalogClient(HttpClient http, string? baseUrl, string? catalogKey)
    {
        _http = http;
        _catalogKey = catalogKey ?? "";
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _http.BaseAddress = uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    public async Task<AnnotationPackageDto?> DraftAsync(string callerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "annotations/draft", callerId, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw Failure(text);
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "null")
            return null;
        return JsonSerializer.Deserialize<AnnotationPackageDto>(text, Json) ?? throw Failure(text);
    }

    public async Task<AnnotationWriteResult> ImportAsync(AnnotationPackageDto package, string callerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, "annotations/draft", callerId, package, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ReadFailure(text);
        try
        {
            var saved = JsonSerializer.Deserialize<AnnotationPackageDto>(text, Json);
            return saved is null
                ? AnnotationWriteResult.Fail("execution_failed", "沒有保存。")
                : AnnotationWriteResult.Success(saved);
        }
        catch (JsonException)
        {
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        }
    }

    public async Task<AnnotationWriteResult> ReadStructureAsync(string callerId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "annotations/read-structure", callerId, null, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
            return ReadFailure(text);
        try
        {
            var saved = JsonSerializer.Deserialize<AnnotationPackageDto>(text, Json);
            return saved is null
                ? AnnotationWriteResult.Fail("execution_failed", "沒有保存。")
                : AnnotationWriteResult.Success(saved);
        }
        catch (JsonException)
        {
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
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

    private static AnnotationWriteResult ReadFailure(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
            var code = document.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() ?? "" : "";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "" : "";
            return AnnotationWriteResult.Fail(code, message);
        }
        catch (JsonException)
        {
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        }
    }
}
