using System.Net;
using System.Text;
using System.Text.Json;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class ModelEndpoint : IModelEndpoint
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(8);
    private const string LocalDefault = OllamaCatalog.Address;
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

    public async Task<ModelPull> PullAsync(ModelDraft draft, string modelName, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var requested = modelName?.Trim() ?? "";
        if (!OllamaCatalog.Suggestions.Contains(requested, StringComparer.Ordinal))
            return ModelPull.Fail("model_invalid_settings", "這個模型不能從這裡下載。");
        if (!string.Equals(draft.Placement?.Trim(), "local", StringComparison.Ordinal) || !OllamaCatalog.CanDownload(AddressOf(draft)))
            return ModelPull.Fail("model_invalid_settings", "只有本機 Ollama 可以下載。");

        var key = _profiles.ResolveKey(draft.ApiKey);
        var payload = "{\"model\":" + JsonSerializer.Serialize(requested) + ",\"name\":" + JsonSerializer.Serialize(requested) + ",\"stream\":true}";
        try
        {
            using var client = OpenPull();
            using var request = new HttpRequestMessage(HttpMethod.Post, PullUrl(AddressOf(draft)));
            if (!string.IsNullOrEmpty(key))
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ModelPull.Fail("probe_failed", "下載沒有完成。");

            var finished = false;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    return ModelPull.Fail("probe_failed", "下載沒有完成。");
                if (!document.RootElement.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String)
                    continue;
                var text = status.GetString() ?? "";
                if (!string.IsNullOrEmpty(key) && text.Contains(key, StringComparison.Ordinal))
                    text = "";
                var shown = Shown(text);
                if (shown.Length > 0)
                    progress?.Report(shown);
                if (text.Equals("success", StringComparison.Ordinal))
                {
                    finished = true;
                    break;
                }
            }

            if (!finished)
                return ModelPull.Fail("probe_failed", "下載沒有完成。");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ModelPull.Fail("cancelled", "已取消下載。");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return ModelPull.Fail("probe_failed", "連不上這個位址。");
        }
        catch (JsonException)
        {
            return ModelPull.Fail("probe_failed", "這個位址沒有正常回應。");
        }

        var listed = await ProbeAsync(new ModelDraft
        {
            Enabled = draft.Enabled,
            Placement = draft.Placement,
            Endpoint = AddressOf(draft),
            ModelId = "",
            ApiKey = draft.ApiKey,
        }, cancellationToken);
        var names = listed.Ok ? listed.Names : [];
        return ModelPull.Success(InstalledName(requested, names), names);
    }

    private static string AddressOf(ModelDraft draft)
    {
        var endpoint = draft.Endpoint?.Trim() ?? "";
        return endpoint.Length == 0 ? LocalDefault : endpoint;
    }

    private static string InstalledName(string requested, IReadOnlyList<string> names)
    {
        if (names.Contains(requested, StringComparer.Ordinal))
            return requested;
        var tagged = names.FirstOrDefault(name => name.Equals(requested + ":latest", StringComparison.Ordinal))
            ?? names.FirstOrDefault(name => name.StartsWith(requested + ":", StringComparison.Ordinal));
        return tagged ?? requested;
    }

    private static string Shown(string status) => status switch
    {
        "success" => "已下載。",
        "pulling manifest" => "正在取得模型資訊。",
        "downloading" => "正在下載。",
        "verifying digest" => "正在檢查檔案。",
        "writing manifest" => "正在寫入。",
        _ => status,
    };

    private HttpClient OpenPull()
    {
        var client = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    private static string PullUrl(string endpoint)
    {
        var uri = new Uri(endpoint);
        var path = uri.AbsolutePath;
        path = Cut(path, "/chat/completions");
        path = Cut(path, "/models");
        path = Cut(path, "/v1");
        path = path.TrimEnd('/');
        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, path + "/api/pull");
        return builder.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped);
    }

    private static string Cut(string path, string suffix)
    {
        var trimmed = path.TrimEnd('/');
        if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return trimmed[..^suffix.Length];
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
