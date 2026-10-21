using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class SchemaService
{
    private readonly Profiles _profiles;
    private readonly ISchemaBrowser _browser;
    private readonly IConnectionCount _connections;
    private readonly EnvironmentService _environment;

    public SchemaService(Profiles profiles, ISchemaBrowser browser, IConnectionCount connections, EnvironmentService environment)
    {
        _profiles = profiles;
        _browser = browser;
        _connections = connections;
        _environment = environment;
    }

    public async Task<OpResult<SchemaGraphDto>> GraphAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<SchemaGraphDto>.Fail(error);
        if (await Connection(cancellationToken) is not { } connection)
            return OpResult<SchemaGraphDto>.Fail(Undecided());

        var read = await _browser.GraphAsync(connection, operatorId, cancellationToken);
        return read.Ok && read.Graph is not null
            ? OpResult<SchemaGraphDto>.Success(read.Graph)
            : OpResult<SchemaGraphDto>.Fail(WorkbenchMessages.Text(read.Code, read.Message));
    }

    public async Task<OpResult<SchemaPreviewDto>> PreviewAsync(
        string operatorId,
        ViewDocumentDto document,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<SchemaPreviewDto>.Fail(error);
        if (await Connection(cancellationToken) is not { } connection)
            return OpResult<SchemaPreviewDto>.Fail(Undecided());

        var read = await _browser.PreviewAsync(connection, document, parameters, operatorId, cancellationToken);
        return read.Ok && read.Preview is not null
            ? OpResult<SchemaPreviewDto>.Success(read.Preview)
            : OpResult<SchemaPreviewDto>.Fail(WorkbenchMessages.Text(read.Code, read.Message));
    }

    private async Task<string?> Connection(CancellationToken cancellationToken)
    {
        var tally = await _connections.TallyAsync(cancellationToken);
        return tally.Available == 1 && !string.IsNullOrWhiteSpace(tally.OnlyName) ? tally.OnlyName : null;
    }

    private static ErrorDto Undecided()
        => WorkbenchMessages.Text("undecided_connection", "有多筆資料庫連線，沒辦法決定要讀哪一筆。請先在環境設定留一筆。");

    private async Task<ErrorDto?> Closed(string operatorId, CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        if (!profile.Has(EntryNames.Analyst))
            return WorkbenchMessages.Forbidden();
        return await _environment.BlockAsync(cancellationToken);
    }
}
