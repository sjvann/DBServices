using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class EnvironmentService
{
    private readonly object _gate = new();
    private readonly IConnectionCount _connections;
    private readonly IConnectionAdmin _admin;
    private readonly IModelProfile _models;
    private readonly IModelEndpoint _modelEndpoint;
    private readonly Profiles _profiles;
    private EnvironmentSnapshot _current = EnvironmentSnapshot.NotReady;

    public EnvironmentService(IConnectionCount connections, IConnectionAdmin admin, IModelProfile models, IModelEndpoint modelEndpoint, Profiles profiles)
    {
        _connections = connections;
        _admin = admin;
        _models = models;
        _modelEndpoint = modelEndpoint;
        _profiles = profiles;
    }

    public EnvironmentSnapshot Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        try
        {
            count = (await _connections.TallyAsync(cancellationToken)).Available;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            count = 0;
        }

        var modelReady = false;
        try
        {
            modelReady = _models.Read().Ready;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            modelReady = false;
        }

        lock (_gate)
            _current = EnvironmentSnapshot.From(count, modelReady);
    }

    public ErrorDto? Block()
    {
        var current = Current;
        return current.Ready ? null : WorkbenchMessages.EnvironmentIncomplete(current);
    }

    public async Task<ErrorDto?> BlockAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        return Block();
    }

    public OpResult<ModelStatus> Model(string operatorId)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<ModelStatus>.Fail(error);
        return OpResult<ModelStatus>.Success(_models.Read());
    }

    public async Task<OpResult<ModelStatus>> SaveModelAsync(string operatorId, ModelDraft draft, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<ModelStatus>.Fail(error);
        var saved = _models.Save(draft);
        if (!saved.Ok || saved.Status is null)
            return OpResult<ModelStatus>.Fail(WorkbenchMessages.Text(saved.Code, saved.Message));
        await RefreshAsync(cancellationToken);
        return OpResult<ModelStatus>.Success(saved.Status);
    }

    public async Task<OpResult<IReadOnlyList<NamedConnection>>> ConnectionsAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<IReadOnlyList<NamedConnection>>.Fail(error);
        var rows = await _admin.ListAsync(cancellationToken);
        return OpResult<IReadOnlyList<NamedConnection>>.Success(rows);
    }

    public async Task<OpResult<bool>> SaveConnectionAsync(string operatorId, string? name, string? serverType, string? connectionString, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<bool>.Fail(error);
        var key = name?.Trim() ?? "";
        if (!ValidName(key))
            return OpResult<bool>.Fail(WorkbenchMessages.Text("invalid_arguments", "連線名稱只能使用英數、減號與底線。"));
        if (string.IsNullOrWhiteSpace(serverType))
            return OpResult<bool>.Fail(WorkbenchMessages.Text("invalid_arguments", "請選擇資料庫種類。"));
        if (string.IsNullOrWhiteSpace(connectionString))
            return OpResult<bool>.Fail(WorkbenchMessages.Text("invalid_arguments", "連線內容不完整。"));

        var saved = await _admin.SaveAsync(key, serverType.Trim(), connectionString, operatorId, cancellationToken);
        if (!saved.Ok)
            return OpResult<bool>.Fail(WorkbenchMessages.Text(saved.Code, saved.Message));
        await RefreshAsync(cancellationToken);
        return OpResult<bool>.Success(true);
    }

    public async Task<OpResult<string>> ProbeConnectionAsync(string operatorId, string? serverType, string? connectionString, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<string>.Fail(error);
        if (string.IsNullOrWhiteSpace(serverType))
            return OpResult<string>.Fail(WorkbenchMessages.Text("invalid_arguments", "請選擇資料庫種類。"));
        if (string.IsNullOrWhiteSpace(connectionString))
            return OpResult<string>.Fail(WorkbenchMessages.Text("invalid_arguments", "連線內容不完整。"));

        var probed = await _admin.ProbeAsync(serverType.Trim(), connectionString, operatorId, cancellationToken);
        if (!probed.Ok)
            return OpResult<string>.Fail(WorkbenchMessages.Text(probed.Code, probed.Message));
        var message = string.IsNullOrWhiteSpace(probed.Message) ? "已連上資料庫。" : probed.Message;
        return OpResult<string>.Success(message);
    }

    public async Task<OpResult<IReadOnlyList<string>>> DatabasesAsync(string operatorId, string? serverType, string? connectionString, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<IReadOnlyList<string>>.Fail(error);
        if (string.IsNullOrWhiteSpace(serverType))
            return OpResult<IReadOnlyList<string>>.Fail(WorkbenchMessages.Text("invalid_arguments", "請選擇資料庫種類。"));
        if (string.IsNullOrWhiteSpace(connectionString))
            return OpResult<IReadOnlyList<string>>.Fail(WorkbenchMessages.Text("invalid_arguments", "連線內容不完整。"));

        var listed = await _admin.DatabasesAsync(serverType.Trim(), connectionString, operatorId, cancellationToken);
        return listed.Ok
            ? OpResult<IReadOnlyList<string>>.Success(listed.Names)
            : OpResult<IReadOnlyList<string>>.Fail(WorkbenchMessages.Text(listed.Code, listed.Message));
    }

    public async Task<OpResult<ModelProbe>> ProbeModelAsync(string operatorId, ModelDraft draft, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<ModelProbe>.Fail(error);
        var probed = await _modelEndpoint.ProbeAsync(draft, cancellationToken);
        if (!probed.Ok)
            return OpResult<ModelProbe>.Fail(WorkbenchMessages.Text(probed.Code, probed.Message));
        return OpResult<ModelProbe>.Success(probed);
    }

    public async Task<OpResult<ModelPull>> PullModelAsync(string operatorId, ModelDraft draft, string modelName, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<ModelPull>.Fail(error);
        var pulled = await _modelEndpoint.PullAsync(draft, modelName, progress, cancellationToken);
        if (!pulled.Ok)
            return OpResult<ModelPull>.Fail(WorkbenchMessages.Text(pulled.Code, pulled.Message));
        return OpResult<ModelPull>.Success(pulled);
    }

    public async Task<OpResult<bool>> RemoveConnectionAsync(string operatorId, string name, CancellationToken cancellationToken)
    {
        if (Admin(operatorId) is { } error)
            return OpResult<bool>.Fail(error);
        var removed = await _admin.RemoveAsync(name, operatorId, cancellationToken);
        if (!removed.Ok)
            return OpResult<bool>.Fail(WorkbenchMessages.Text(removed.Code, removed.Message));
        await RefreshAsync(cancellationToken);
        return OpResult<bool>.Success(true);
    }

    private ErrorDto? Admin(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.IsAdmin ? null : WorkbenchMessages.Forbidden();
    }

    private static bool ValidName(string name)
    {
        if (name.Length is 0 or > 64)
            return false;
        foreach (var character in name)
        {
            if (character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_')
                continue;
            return false;
        }

        return true;
    }
}
