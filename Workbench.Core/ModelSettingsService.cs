using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class ModelSettings
{
    public bool Enabled { get; set; }
    public string Placement { get; set; } = "local";
    public string Endpoint { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed class ModelSettingsService
{
    private readonly IModelSidecar _sidecar;
    private readonly Profiles _profiles;
    private readonly string _path;
    private readonly object _gate = new();
    private ModelSettings _current;

    public ModelSettingsService(IModelSidecar sidecar, Profiles profiles, string path, ModelSettings initial)
    {
        _sidecar = sidecar;
        _profiles = profiles;
        _path = path;
        _current = Clone(initial);
    }

    public bool Enabled => _current.Enabled;
    public string Placement => _current.Placement;
    public string Endpoint => _current.Endpoint;
    public string ModelId => _current.ModelId;

    public string ResolveKey(string? draft)
    {
        if (!string.IsNullOrEmpty(draft))
            return draft;
        return _current.ApiKey;
    }

    public OpResult<ModelDto> Read(string operatorId)
    {
        if (RequireAdmin(operatorId) is { } error)
            return OpResult<ModelDto>.Fail(error);
        lock (_gate)
            return OpResult<ModelDto>.Success(Project(_current));
    }

    public OpResult<ModelDto> Save(string operatorId, ModelWriteRequest request)
    {
        if (RequireAdmin(operatorId) is { } error)
            return OpResult<ModelDto>.Fail(error);

        lock (_gate)
        {
            var next = Clone(_current);
            next.Enabled = request.Enabled;
            next.Placement = string.IsNullOrWhiteSpace(request.Placement) ? next.Placement : request.Placement.Trim();
            next.Endpoint = request.Endpoint?.Trim() ?? "";
            next.ModelId = request.ModelId?.Trim() ?? "";
            if (!string.IsNullOrEmpty(request.ApiKey))
                next.ApiKey = request.ApiKey;

            var check = Validate(next.Placement, next.Endpoint, next.ApiKey, allowBlankLocalKey: true);
            if (check is not null)
                return OpResult<ModelDto>.Fail(check);

            var json = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Model"] = new Dictionary<string, object?>
                {
                    ["enabled"] = next.Enabled,
                    ["placement"] = next.Placement,
                    ["endpoint"] = next.Endpoint,
                    ["modelId"] = next.ModelId,
                    ["apiKey"] = next.ApiKey,
                },
            });
            File.WriteAllText(_path, json);
            _current = next;
            return OpResult<ModelDto>.Success(Project(next));
        }
    }

    public async Task<OpResult<ModelResultDto>> ProbeAsync(string operatorId, ProbeRequest request, CancellationToken cancellationToken)
    {
        if (RequireAdmin(operatorId) is { } error)
            return OpResult<ModelResultDto>.Fail(error);

        var placement = request.Placement?.Trim() ?? "";
        var endpoint = request.Endpoint?.Trim() ?? "";
        var modelId = request.ModelId?.Trim() ?? "";
        var key = string.IsNullOrEmpty(request.ApiKey) ? _current.ApiKey : request.ApiKey;
        var check = Validate(placement, endpoint, key, allowBlankLocalKey: true);
        if (check is not null)
            return OpResult<ModelResultDto>.Success(new ModelResultDto { Code = check.Code, Message = check.Message });

        var response = await _sidecar.InvokeAsync(new SidecarRequest
        {
            Kind = "probe",
            Placement = placement,
            Endpoint = endpoint,
            ModelId = modelId,
            ApiKey = placement == "local" && string.IsNullOrEmpty(key) ? "local" : key,
        }, cancellationToken);
        return OpResult<ModelResultDto>.Success(new ModelResultDto { Code = response.Code, Message = Scrub.Message(response.Message) });
    }

    public static ErrorDto? Validate(string placement, string endpoint, string? apiKey, bool allowBlankLocalKey)
    {
        if (placement is not ("cloud" or "local"))
            return WorkbenchMessages.Model(ModelCodes.InvalidSettings, "模型設定不正確。");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return WorkbenchMessages.Model(ModelCodes.InvalidSettings, "模型設定不正確。");

        if (placement == "cloud")
        {
            if (uri.Scheme != "https" || string.IsNullOrEmpty(apiKey))
                return WorkbenchMessages.Model(ModelCodes.InvalidSettings, "模型設定不正確。");
            return null;
        }

        var host = uri.Host;
        if (uri.HostNameType == UriHostNameType.IPv6)
            host = uri.IdnHost;
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
            return WorkbenchMessages.Model(ModelCodes.InvalidSettings, "模型設定不正確。");
        if (!allowBlankLocalKey && string.IsNullOrEmpty(apiKey))
            return WorkbenchMessages.Model(ModelCodes.InvalidSettings, "模型設定不正確。");
        return null;
    }

    private ErrorDto? RequireAdmin(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        if (!profile.IsAdmin)
            return WorkbenchMessages.Forbidden();
        return null;
    }

    private static ModelDto Project(ModelSettings settings) => new()
    {
        Enabled = settings.Enabled,
        Placement = settings.Placement,
        Endpoint = settings.Endpoint,
        ModelId = settings.ModelId,
        ApiKeySet = !string.IsNullOrEmpty(settings.ApiKey),
    };

    private static ModelSettings Clone(ModelSettings settings) => new()
    {
        Enabled = settings.Enabled,
        Placement = settings.Placement,
        Endpoint = settings.Endpoint,
        ModelId = settings.ModelId,
        ApiKey = settings.ApiKey,
    };
}
