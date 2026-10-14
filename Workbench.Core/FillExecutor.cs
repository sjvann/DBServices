using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class FillExecutor
{
    private readonly IDataSurface _surface;
    private readonly IAuditReader _audit;
    private readonly IPreviewMemory _memory;
    private readonly IClock _clock;
    private readonly Profiles _profiles;
    private readonly ITemplateStore _templates;

    public FillExecutor(
        IDataSurface surface,
        IAuditReader audit,
        IPreviewMemory memory,
        IClock clock,
        Profiles profiles,
        ITemplateStore templates)
    {
        _surface = surface;
        _audit = audit;
        _memory = memory;
        _clock = clock;
        _profiles = profiles;
        _templates = templates;
    }

    public FillPlan CreatePlan(Profile profile, string sessionId, string surface, string connection, string? templateId, IReadOnlyList<FillCall> calls)
    {
        var plan = new FillPlan
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            OperatorId = profile.Id,
            Surface = surface,
            Connection = connection,
            TemplateId = templateId,
            RoleHash = Canonical.RoleHash(profile),
            Calls = calls.ToList(),
        };
        _memory.SavePlan(plan);
        return plan;
    }

    public async Task<OpResult<PlanResponse>> ExecuteAsync(string sessionId, string planId, CancellationToken cancellationToken)
    {
        var plan = _memory.FindPlan(planId);
        if (plan is null || plan.SessionId != sessionId)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.PlanMismatch());
        if (plan.Used)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.PlanUsed());

        var profile = _profiles.Find(plan.OperatorId);
        if (profile is null)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.Unauthenticated());
        if (!string.Equals(Canonical.RoleHash(profile), plan.RoleHash, StringComparison.Ordinal))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.PlanMismatch());

        if (plan.TemplateId is not null)
        {
            var template = _templates.Find(plan.TemplateId);
            if (template is null || !profile.CanViewGroup(template.GroupId) || !TemplateMatches(template, plan))
                return OpResult<PlanResponse>.Fail(WorkbenchMessages.PlanMismatch());
        }

        if (!SurfaceAllows(plan))
            return OpResult<PlanResponse>.Fail(plan.Surface == SurfaceNames.Web && plan.Calls.Any(call => call.Tool == "count")
                ? WorkbenchMessages.WebCount()
                : WorkbenchMessages.SingleKey());

        foreach (var call in plan.Calls)
        {
            var fields = await _surface.FieldsAsync(new SurfaceRequest
            {
                Surface = plan.Surface,
                Connection = plan.Connection,
                Table = call.Table,
                CallerId = profile.Id,
            }, cancellationToken);
            if (!fields.Ok)
                return KeepPrevious(fields);
            var names = fields.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            if (call.Filters.Any(filter => !names.Contains(filter.Key)) || call.Columns.Any(column => !names.Contains(column)))
                return OpResult<PlanResponse>.Fail(WorkbenchMessages.MissingColumn());
        }

        var blocks = new List<SnapshotBlock>();
        var traces = new List<TraceDto>();
        string? snapshotErrorCode = null;
        string? snapshotErrorMessage = null;
        foreach (var call in plan.Calls)
        {
            var request = new SurfaceRequest
            {
                Surface = plan.Surface,
                Connection = plan.Connection,
                Table = call.Table,
                CallerId = profile.Id,
                Filters = call.Filters,
            };
            var outcome = call.Tool == "count"
                ? await _surface.CountAsync(request, cancellationToken)
                : await _surface.QueryAsync(request, cancellationToken);
            if (!outcome.Ok)
            {
                traces.Add(TraceOf(profile.Id, plan.Connection, call, outcome, null));
                snapshotErrorCode = outcome.ErrorCode;
                snapshotErrorMessage = outcome.ErrorMessage;
                return KeepPrevious(outcome, traces);
            }

            bool? matched = null;
            if (plan.Surface == SurfaceNames.Mcp)
                matched = AuditMatches(profile.Id, plan.Connection, call, outcome);

            traces.Add(TraceOf(profile.Id, plan.Connection, call, outcome, matched));
            if (call.Tool == "count")
            {
                blocks.Add(new SnapshotBlock
                {
                    Type = ComponentKinds.Count,
                    Table = call.Table,
                    Count = outcome.Count ?? 0,
                });
            }
            else
            {
                blocks.Add(new SnapshotBlock
                {
                    Type = ComponentKinds.Table,
                    Table = call.Table,
                    Columns = outcome.Columns,
                    Rows = outcome.Rows,
                });
            }
        }

        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            PlanId = plan.Id,
            Surface = plan.Surface,
            Connection = plan.Connection,
            Blocks = blocks,
            Traces = traces,
        };
        if (!_memory.MarkUsed(planId))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.PlanUsed());

        _memory.ReplaceSnapshot(snapshot);
        return OpResult<PlanResponse>.Success(ToResponse(plan, snapshot, snapshotErrorCode, snapshotErrorMessage));
    }

    private OpResult<PlanResponse> KeepPrevious(SurfaceOutcome outcome, List<TraceDto>? traces = null)
    {
        var response = new PlanResponse
        {
            ErrorCode = outcome.ErrorCode,
            ErrorMessage = Scrub.Message(outcome.ErrorMessage),
            Traces = traces ?? [],
        };
        return OpResult<PlanResponse>.Success(response);
    }

    private static bool TemplateMatches(TemplateRecord template, FillPlan plan)
    {
        var expected = new List<FillCall>();
        foreach (var component in template.Components.OrderBy(item => item.Order))
        {
            if (component.Type == ComponentKinds.Table)
                expected.Add(new FillCall { Tool = "query", Table = component.Table ?? "", Columns = component.Columns.ToList() });
            else if (component.Type == ComponentKinds.Count)
                expected.Add(new FillCall { Tool = "count", Table = component.Table ?? "" });
        }

        if (expected.Count != plan.Calls.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
        {
            if (plan.Calls[index].Tool != expected[index].Tool || plan.Calls[index].Table != expected[index].Table)
                return false;
            if (!plan.Calls[index].Columns.SequenceEqual(expected[index].Columns, StringComparer.Ordinal))
                return false;
        }

        return true;
    }

    private static bool SurfaceAllows(FillPlan plan)
    {
        if (plan.Surface is not (SurfaceNames.Mcp or SurfaceNames.Web))
            return false;
        foreach (var call in plan.Calls)
        {
            if (plan.Surface == SurfaceNames.Web && call.Tool == "count")
                return false;
            if (plan.Surface == SurfaceNames.Web && call.Filters.Count > 1)
                return false;
            if (call.Tool is not ("query" or "count"))
                return false;
        }

        return true;
    }

    private bool AuditMatches(string operatorId, string connection, FillCall call, SurfaceOutcome outcome)
    {
        AuditRead read;
        try
        {
            read = _audit.ReadAll();
        }
        catch (IOException)
        {
            return false;
        }

        foreach (var entry in read.Entries)
        {
            if (entry.Error is not null || entry.Caller != operatorId || entry.ConnectionId != connection || entry.Tool != call.Tool)
                continue;
            if (!Canonical.ArgumentsMatch(call.Filters, entry.ArgumentsJson, connection, call.Table))
                continue;
            if (call.Tool == "count")
            {
                if (entry.ValueJson is null)
                    continue;
                if (!decimal.TryParse(entry.ValueJson, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var audited))
                    continue;
                if (audited == (outcome.Count ?? 0))
                    return true;
                continue;
            }

            if (entry.RowCount == outcome.Rows.Count)
                return true;
        }

        return false;
    }

    private TraceDto TraceOf(string operatorId, string connection, FillCall call, SurfaceOutcome outcome, bool? matched) => new()
    {
        OperatorId = operatorId,
        Connection = connection,
        Tool = call.Tool,
        Filters = call.Filters.ToDictionary(item => item.Key, item => item.Value?.ToString() ?? "", StringComparer.Ordinal),
        RowCount = call.Tool == "query" ? outcome.Rows.Count : null,
        Count = call.Tool == "count" ? outcome.Count : null,
        ErrorCode = outcome.Ok ? null : outcome.ErrorCode,
        AuditMatched = matched,
        AtUtc = _clock.UtcNow,
    };

    public static PlanResponse ToResponse(FillPlan plan, Snapshot snapshot, string? errorCode, string? errorMessage)
    {
        var table = snapshot.Blocks.FirstOrDefault(block => block.Type == ComponentKinds.Table);
        var count = snapshot.Blocks.FirstOrDefault(block => block.Type == ComponentKinds.Count);
        return new PlanResponse
        {
            PlanId = plan.Id,
            SnapshotId = snapshot.Id,
            Tools = plan.Calls.Select(call => call.Tool).ToList(),
            ParameterKeys = plan.Calls.SelectMany(call => call.Filters.Select(filter => filter.Key)).Distinct(StringComparer.Ordinal).ToList(),
            Columns = table?.Columns ?? [],
            Rows = table?.Rows ?? [],
            Count = count?.Count,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            AuditMatched = snapshot.Traces.LastOrDefault()?.AuditMatched,
            Traces = snapshot.Traces,
            Notice = table is { Rows.Count: 0 } ? WorkbenchMessages.NoRows : null,
        };
    }
}
