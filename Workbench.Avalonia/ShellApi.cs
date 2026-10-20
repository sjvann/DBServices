using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Workbench.Contracts;

namespace Workbench.Avalonia;

public sealed class ShellApi
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;

    public ShellApi(string baseUrl)
        : this(new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") })
    {
    }

    public ShellApi(HttpClient http) => _http = http;

    public string? SessionId { get; private set; }
    public string? LastError { get; private set; }

    public async Task<bool> LoginAsync(string displayName, string secret, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("api/sessions", new LoginRequest { DisplayName = displayName, Secret = secret }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LastError = body;
            return false;
        }

        var session = JsonSerializer.Deserialize<SessionResponse>(body, Json);
        SessionId = session?.SessionId;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SessionId);
        LastError = null;
        return !string.IsNullOrEmpty(SessionId);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await SendAsync(HttpMethod.Delete, "api/sessions", null, cancellationToken);
        SessionId = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public Task<MeResponse?> MeAsync(CancellationToken cancellationToken = default)
        => ReadAsync<MeResponse>("api/me", cancellationToken);

    public Task<List<OperatorDto>?> OperatorsAsync(CancellationToken cancellationToken = default)
        => ReadAsync<List<OperatorDto>>("api/operators", cancellationToken);

    public Task<OperatorDto?> CreateOperatorAsync(string displayName, string secret, CancellationToken cancellationToken = default)
        => PostAsync<OperatorDto>("api/operators", new OperatorWriteRequest { DisplayName = displayName, Secret = secret }, cancellationToken);

    public Task<OperatorDto?> AssignEntriesAsync(string operatorId, IReadOnlyList<string> entries, CancellationToken cancellationToken = default)
        => PutAsync<OperatorDto>("api/operators/" + operatorId + "/entries", new EntryAssignmentRequest { Entries = entries.ToList() }, cancellationToken);

    public Task<List<ViewSummaryDto>?> ViewsAsync(CancellationToken cancellationToken = default)
        => ReadAsync<List<ViewSummaryDto>>("api/views", cancellationToken);

    public Task<ViewDocumentDto?> SaveViewAsync(ViewDocumentDto document, CancellationToken cancellationToken = default)
        => PutAsync<ViewDocumentDto>("api/views/" + Uri.EscapeDataString(document.Id), document, cancellationToken);

    public Task<ViewDocumentDto?> PublishViewAsync(string id, CancellationToken cancellationToken = default)
        => PostAsync<ViewDocumentDto>("api/views/" + Uri.EscapeDataString(id) + "/publish", new { }, cancellationToken);

    public Task<List<StyleDto>?> StylesAsync(CancellationToken cancellationToken = default)
        => ReadAsync<List<StyleDto>>("api/styles", cancellationToken);

    public Task<StyleDto?> AcceptStyleAsync(StyleWriteRequest request, CancellationToken cancellationToken = default)
        => PostAsync<StyleDto>("api/styles", request, cancellationToken);

    public Task<UtteranceResponse?> SubmitAsync(string text, CancellationToken cancellationToken = default)
        => PostAsync<UtteranceResponse>("api/utterance", new UtteranceRequest { Text = text }, cancellationToken);

    public Task<List<RecordDto>?> RecordsAsync(CancellationToken cancellationToken = default)
        => ReadAsync<List<RecordDto>>("api/records", cancellationToken);

    public Task<RecordDto?> OpenRecordAsync(string id, CancellationToken cancellationToken = default)
        => ReadAsync<RecordDto>("api/records/" + Uri.EscapeDataString(id), cancellationToken);

    public Task<RecordDto?> SaveRecordAsync(bool shared, CancellationToken cancellationToken = default)
        => PostAsync<RecordDto>("api/records", new RecordWriteRequest { Shared = shared }, cancellationToken);

    public Task<byte[]> PdfAsync(string? recordId, CancellationToken cancellationToken = default)
        => BytesAsync(string.IsNullOrEmpty(recordId) ? "api/export/pdf" : "api/export/pdf?recordId=" + Uri.EscapeDataString(recordId), cancellationToken);

    private async Task<T?> ReadAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LastError = await response.Content.ReadAsStringAsync(cancellationToken);
            return default;
        }

        LastError = null;
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
    }

    private async Task<T?> PostAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body, cancellationToken);
        return await ReadBodyAsync<T>(response, cancellationToken);
    }

    private async Task<T?> PutAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, url, body, cancellationToken);
        return await ReadBodyAsync<T>(response, cancellationToken);
    }

    private async Task<T?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LastError = text;
            return default;
        }

        LastError = null;
        return JsonSerializer.Deserialize<T>(text, Json);
    }

    private async Task<byte[]> BytesAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LastError = await response.Content.ReadAsStringAsync(cancellationToken);
            return [];
        }

        LastError = null;
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return _http.SendAsync(request, cancellationToken);
    }
}
