using System.Net;
using System.Text.Json;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class FileModelProfile : IModelProfile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly object _gate = new();

    public FileModelProfile(string path) => _path = path;

    public ModelStatus Read()
    {
        lock (_gate)
            return ToStatus(Load());
    }

    public string ResolveKey(string? submitted)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(submitted))
                return submitted;
            return Load()?.ApiKey ?? "";
        }
    }

    public ModelWriteResult Save(ModelDraft draft)
    {
        lock (_gate)
        {
            var existing = Load();
            var key = string.IsNullOrWhiteSpace(draft.ApiKey) ? existing?.ApiKey ?? "" : draft.ApiKey!;
            var stored = new StoredModel
            {
                Enabled = draft.Enabled,
                Placement = draft.Placement?.Trim() ?? "",
                Endpoint = draft.Endpoint?.Trim() ?? "",
                ModelId = draft.ModelId?.Trim() ?? "",
                ApiKey = key,
            };
            if (stored.Enabled)
            {
                var problem = Problem(stored);
                if (problem is not null)
                    return ModelWriteResult.Fail("model_invalid_settings", problem);
            }

            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(stored, Json));
                File.Move(temporary, _path, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ModelWriteResult.Fail("execution_failed", "無法儲存 AI 設定。");
            }

            return ModelWriteResult.Success(ToStatus(stored));
        }
    }

    private StoredModel? Load()
    {
        if (!File.Exists(_path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<StoredModel>(File.ReadAllText(_path), Json);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static ModelStatus ToStatus(StoredModel? stored)
    {
        if (stored is null)
            return new ModelStatus();
        var problem = Problem(stored);
        return new ModelStatus
        {
            Enabled = stored.Enabled,
            Placement = stored.Placement,
            Endpoint = stored.Endpoint,
            ModelId = stored.ModelId,
            KeyPresent = !string.IsNullOrEmpty(stored.ApiKey),
            Ready = stored.Enabled && problem is null,
        };
    }

    private static string? Problem(StoredModel stored)
    {
        if (stored.Placement is not ("cloud" or "local"))
            return "請選擇雲端或本機。";
        if (stored.Endpoint.Length == 0 || stored.ModelId.Length == 0)
            return "請填模型位址與模型名稱。";
        if (!Uri.TryCreate(stored.Endpoint, UriKind.Absolute, out var endpoint))
            return "模型位址無法使用。";
        if (stored.Placement == "cloud")
        {
            if (!string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return "雲端位址必須是 https。";
            if (string.IsNullOrEmpty(stored.ApiKey))
                return "雲端需要金鑰。";
            return null;
        }

        if (endpoint.Scheme is not ("http" or "https"))
            return "本機位址無法使用。";
        if (endpoint.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return null;
        return IPAddress.TryParse(endpoint.IdnHost, out var address) && IPAddress.IsLoopback(address)
            ? null
            : "本機位址必須在這台機器上。";
    }

    private sealed class StoredModel
    {
        public bool Enabled { get; set; }
        public string Placement { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string ModelId { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }
}
