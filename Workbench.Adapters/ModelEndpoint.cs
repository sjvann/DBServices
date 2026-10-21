using System.Net;
using System.Text;
using System.Text.Json;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class ModelEndpoint : IModelEndpoint
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(8);
    private const string LocalDefault = "http://127.0.0.1:11434/v1";
    private static readonly string[] Hidden = ["embed", "embedding", "whisper", "tts", "dalle", "moderation", "gpt-image", "text-embedding"];

    private readonly IModelProfile _profiles;
    private readonly HttpMessageHandler? _handler;

    public ModelEndpoint(IModelProfile profiles, HttpMessageHandler? handler = null)
    {
        _profiles = profiles;
        _handler = handler;
    }

    public async Task<ModelProbe> ProbeAsync(ModelDraft draft, CancellationToken cancellationToken)
    {
        if (!TryAddress(draft, out var endpoint, out var failure))
            return failure!;

        var key = _profiles.ResolveKey(draft.ApiKey);
        if (string.Equals(draft.Placement?.Trim(), "cloud", StringComparison.Ordinal) && string.IsNullOrEmpty(key))
            return ModelProbe.Fail("model_invalid_settings", "雲端需要金鑰。");

        try
        {
            using var client = Open();
            using var listed = await SendAsync(client, HttpMethod.Get, ModelsUrl(endpoint), key, null, cancellationToken);
            if (listed.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
                return await ChatAsync(client, endpoint, draft.ModelId, key, cancellationToken);
            if (!listed.IsSuccessStatusCode)
                return ModelProbe.Fail("probe_failed", "連不上這個位址。");

            var body = await listed.Content.ReadAsStringAsync(cancellationToken);
            return Listed(draft.ModelId, ReadNames(body), key);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ModelProbe.Fail("probe_timeout", "連線逾時，沒有得到回應。");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return ModelProbe.Fail("probe_failed", "連不上這個位址。");
        }
        catch (JsonException)
        {
            return ModelProbe.Fail("probe_failed", "這個位址沒有正常回應。");
        }
    }

    private async Task<ModelProbe> ChatAsync(HttpClient client, string endpoint, string? modelId, string key, CancellationToken cancellationToken)
    {
        var typed = modelId?.Trim() ?? "";
        if (typed.Length == 0)
            return ModelProbe.Fail("invalid_arguments", "請先填模型名稱。");

        var payload = "{\"model\":" + JsonSerializer.Serialize(typed) + ",\"messages\":[{\"role\":\"user\",\"content\":\".\"}],\"max_tokens\":1,\"stream\":false}";
        using var response = await SendAsync(client, HttpMethod.Post, ChatUrl(endpoint), key, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ModelProbe.Fail("probe_failed", "連不上這個模型。");
        return ModelProbe.Success("已連上，但這個位址沒有模型清單。", []);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string key, string? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(key))
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request, cancellationToken);
    }

    private HttpClient Open()
    {
        var client = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Limit;
        return client;
    }

    private static bool TryAddress(ModelDraft draft, out string endpoint, out ModelProbe? failure)
    {
        failure = null;
        endpoint = draft.Endpoint?.Trim() ?? "";
        var placement = draft.Placement?.Trim() ?? "";
        if (placement is not ("cloud" or "local"))
        {
            failure = ModelProbe.Fail("model_invalid_settings", "請選擇雲端或本機。");
            return false;
        }

        if (placement == "local" && endpoint.Length == 0)
            endpoint = LocalDefault;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            failure = ModelProbe.Fail("model_invalid_settings", "模型位址無法使用。");
            return false;
        }

        if (placement == "cloud")
        {
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                failure = ModelProbe.Fail("model_invalid_settings", "雲端位址必須是 https。");
            return failure is null;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            failure = ModelProbe.Fail("model_invalid_settings", "本機位址無法使用。");
            return false;
        }

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(uri.IdnHost, out var address) && IPAddress.IsLoopback(address)))
            return true;

        failure = ModelProbe.Fail("model_invalid_settings", "本機位址必須在這台機器上。");
        return false;
    }

    private static ModelProbe Listed(string? modelId, IEnumerable<string> names, string key)
    {
        var visible = new List<string>();
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name) || HiddenName(name))
                continue;
            if (!string.IsNullOrEmpty(key) && name.Contains(key, StringComparison.Ordinal))
                continue;
            if (!visible.Contains(name, StringComparer.Ordinal))
                visible.Add(name);
        }

        var typed = modelId?.Trim() ?? "";
        if (typed.Length == 0 || visible.Contains(typed, StringComparer.Ordinal))
            return ModelProbe.Success("已連上。", visible);
        return ModelProbe.Success("已連上，但清單沒有這個模型。", visible);
    }

    private static bool HiddenName(string name)
    {
        foreach (var token in Hidden)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static List<string> ReadNames(string body)
    {
        using var document = JsonDocument.Parse(body);
        var names = new List<string>();
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return names;
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
                continue;
            var name = id.GetString();
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }

    private static string ModelsUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return trimmed + "/models";
    }

    private static string ChatUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/models".Length].TrimEnd('/');
        return trimmed + "/chat/completions";
    }
}
