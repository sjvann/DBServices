using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class LivePreviewService
{
    private readonly IConnectionCatalog _catalog;
    private readonly IDataSurface _surface;
    private readonly FillExecutor _fill;
    private readonly Profiles _profiles;
    private readonly IPreviewMemory _memory;
    private readonly IClock _clock;

    public LivePreviewService(IConnectionCatalog catalog, IDataSurface surface, FillExecutor fill, Profiles profiles, IPreviewMemory memory, IClock clock)
    {
        _catalog = catalog;
        _surface = surface;
        _fill = fill;
        _profiles = profiles;
        _memory = memory;
        _clock = clock;
    }

    public async Task<OpResult<CatalogOutcome>> ConnectionsAsync(string operatorId, string surface, CancellationToken cancellationToken)
    {
        var profile = Gate(operatorId, surface);
        if (profile.Error is not null)
            return OpResult<CatalogOutcome>.Fail(profile.Error);
        var listed = await _catalog.ListAsync(surface, cancellationToken);
        return OpResult<CatalogOutcome>.Success(listed);
    }

    public async Task<OpResult<SurfaceOutcome>> TablesAsync(string operatorId, string surface, string connection, CancellationToken cancellationToken)
    {
        var profile = Gate(operatorId, surface);
        if (profile.Error is not null)
            return OpResult<SurfaceOutcome>.Fail(profile.Error);
        var outcome = await _surface.TablesAsync(new SurfaceRequest
        {
            Surface = surface,
            Connection = connection,
            CallerId = profile.Value!.Id,
            IncludeViewOmitted = true,
        }, cancellationToken);
        return OpResult<SurfaceOutcome>.Success(Scrubbed(outcome, profile.Value!.Id, connection));
    }

    public async Task<OpResult<SurfaceOutcome>> FieldsAsync(string operatorId, string surface, string connection, string? table, CancellationToken cancellationToken)
    {
        var profile = Gate(operatorId, surface);
        if (profile.Error is not null)
            return OpResult<SurfaceOutcome>.Fail(profile.Error);
        if (string.IsNullOrWhiteSpace(table))
            return OpResult<SurfaceOutcome>.Success(Scrubbed(Failed("invalid_arguments", "未提供資料表名稱。"), profile.Value!.Id, connection));
        var outcome = await _surface.FieldsAsync(new SurfaceRequest
        {
            Surface = surface,
            Connection = connection,
            Table = table,
            CallerId = profile.Value!.Id,
        }, cancellationToken);
        return OpResult<SurfaceOutcome>.Success(Scrubbed(outcome, profile.Value!.Id, connection));
    }

    public async Task<OpResult<PlanResponse>> ConfirmQueryAsync(string sessionId, string operatorId, PlanRequest request, CancellationToken cancellationToken)
        => await ConfirmAsync(sessionId, operatorId, request, "query", cancellationToken);

    public async Task<OpResult<PlanResponse>> ConfirmCountAsync(string sessionId, string operatorId, PlanRequest request, CancellationToken cancellationToken)
        => await ConfirmAsync(sessionId, operatorId, request, "count", cancellationToken);

    public PlanResponse? Current(string sessionId)
    {
        var snapshot = _memory.Current(sessionId);
        if (snapshot is null)
            return null;
        var table = snapshot.Blocks.FirstOrDefault(block => block.Type == ComponentKinds.Table);
        return new PlanResponse
        {
            SnapshotId = snapshot.Id,
            Columns = table?.Columns ?? [],
            Rows = table?.Rows ?? [],
            Count = snapshot.Blocks.FirstOrDefault(block => block.Type == ComponentKinds.Count)?.Count,
            Traces = snapshot.Traces,
            Notice = table is { Rows.Count: 0 } ? WorkbenchMessages.NoRows : null,
        };
    }

    private async Task<OpResult<PlanResponse>> ConfirmAsync(string sessionId, string operatorId, PlanRequest request, string tool, CancellationToken cancellationToken)
    {
        var profile = Gate(operatorId, request.Surface ?? "");
        if (profile.Error is not null)
            return OpResult<PlanResponse>.Fail(profile.Error);
        if (string.IsNullOrWhiteSpace(request.Table))
            return OpResult<PlanResponse>.Fail(new ErrorDto { Code = "invalid_arguments", Message = "未提供資料表名稱。" });

        var filters = ReadFilters(request.Filters);
        if (request.Surface == SurfaceNames.Web && tool == "count")
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.WebCount());
        if (request.Surface == SurfaceNames.Web && filters.Count > 1)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.SingleKey());

        var plan = _fill.CreatePlan(profile.Value!, sessionId, request.Surface!, request.Connection ?? "", null,
        [
            new FillCall { Tool = tool, Table = request.Table!, Filters = filters },
        ]);
        return await _fill.ExecuteAsync(sessionId, plan.Id, cancellationToken);
    }

    private OpResult<Profile> Gate(string operatorId, string surface)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<Profile>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.HasWork(WorkRoles.Planner))
            return OpResult<Profile>.Fail(WorkbenchMessages.Forbidden());
        if (surface is not (SurfaceNames.Mcp or SurfaceNames.Web))
            return OpResult<Profile>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<Profile>.Success(profile);
    }

    internal static List<ScalarFilter> ReadFilters(IReadOnlyList<FilterDto>? filters)
    {
        var list = new List<ScalarFilter>();
        if (filters is null)
            return list;
        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter.Key))
                continue;
            list.Add(new ScalarFilter { Key = filter.Key, Value = filter.Value });
        }

        return list;
    }

    private SurfaceOutcome Scrubbed(SurfaceOutcome outcome, string operatorId, string connection)
    {
        outcome.ErrorMessage = Scrub.Message(outcome.ErrorMessage);
        if (!outcome.Ok)
        {
            outcome.Traces =
            [
                new TraceDto
                {
                    OperatorId = operatorId,
                    Connection = connection,
                    Tool = outcome.Tool,
                    ErrorCode = outcome.ErrorCode,
                    AtUtc = _clock.UtcNow,
                },
            ];
        }

        return outcome;
    }

    private static SurfaceOutcome Failed(string code, string message) => new()
    {
        Ok = false,
        ErrorCode = code,
        ErrorMessage = message,
    };
}
