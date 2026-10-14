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

    public Task<List<ConnectionDto>> ConnectionsAsync(string surface, CancellationToken cancellationToken = default)
        => ReadAsync<List<ConnectionDto>>("api/connections?surface=" + surface, cancellationToken);

    public Task<SurfacePayload> TablesAsync(string surface, string connection, CancellationToken cancellationToken = default)
        => ReadAsync<SurfacePayload>("api/structure/tables?surface=" + Uri.EscapeDataString(surface) + "&connection=" + Uri.EscapeDataString(connection), cancellationToken);

    public Task<SurfacePayload> FieldsAsync(string surface, string connection, string table, CancellationToken cancellationToken = default)
        => ReadAsync<SurfacePayload>("api/structure/fields?surface=" + Uri.EscapeDataString(surface) + "&connection=" + Uri.EscapeDataString(connection) + "&table=" + Uri.EscapeDataString(table), cancellationToken);

    public Task<PlanResponse> QueryAsync(PlanRequest request, CancellationToken cancellationToken = default)
        => PostAsync<PlanResponse>("api/plans/query", request, cancellationToken);

    public Task<PlanResponse> CountAsync(PlanRequest request, CancellationToken cancellationToken = default)
        => PostAsync<PlanResponse>("api/plans/count", request, cancellationToken);

    public Task<List<GroupDto>> GroupsAsync(CancellationToken cancellationToken = default)
        => ReadAsync<List<GroupDto>>("api/groups", cancellationToken);

    public Task<AssignmentDto> GetAssignmentsAsync(string operatorId, CancellationToken cancellationToken = default)
        => ReadAsync<AssignmentDto>("api/assignments/" + operatorId, cancellationToken);

    public Task<AssignmentDto> SaveAssignmentsAsync(string operatorId, AssignmentDto assignment, CancellationToken cancellationToken = default)
        => PutAsync<AssignmentDto>("api/assignments/" + operatorId, assignment, cancellationToken);

    public Task<FavoriteOpenResponse> OpenFavoriteAsync(string id, CancellationToken cancellationToken = default)
        => PostAsync<FavoriteOpenResponse>("api/favorites/" + id + "/open", new { }, cancellationToken);

    public Task<byte[]> PdfAsync(string snapshotId, CancellationToken cancellationToken = default)
        => BytesAsync("api/preview/" + snapshotId + "/pdf", cancellationToken);

    public Task<string> CsvAsync(string snapshotId, CancellationToken cancellationToken = default)
        => TextAsync("api/preview/" + snapshotId + "/csv", cancellationToken);

    public Task<string> PrintAsync(string snapshotId, CancellationToken cancellationToken = default)
        => TextAsync("api/preview/" + snapshotId + "/print", cancellationToken);

    public Task<bool> AddKeywordAsync(KeywordWriteRequest request, CancellationToken cancellationToken = default)
        => PostOkAsync("api/keyword-rules/from-confirm", request, cancellationToken);

    private async Task<T> ReadAsync<T>(string url, CancellationToken cancellationToken) where T : new()
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new T();
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken) ?? new T();
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken cancellationToken) where T : new()
    {
        using var response = await SendAsync(HttpMethod.Post, url, body, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return payload ?? new T();
    }

    private async Task<T> PutAsync<T>(string url, object body, CancellationToken cancellationToken) where T : new()
    {
        using var response = await SendAsync(HttpMethod.Put, url, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken) ?? new T();
    }

    private async Task<bool> PostOkAsync(string url, object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<byte[]> BytesAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : [];
    }

    private async Task<string> TextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return _http.SendAsync(request, cancellationToken);
    }
}

public sealed class SurfacePayload
{
    public List<string> Tables { get; set; } = [];
    public List<FieldDto> Fields { get; set; } = [];
    public string? ErrorCode { get; set; }
}
