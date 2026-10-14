using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class IntentService
{
    private readonly IConnectionCatalog _catalog;
    private readonly ITemplateStore _templates;
    private readonly IKeywordStore _keywords;
    private readonly IModelSidecar _sidecar;
    private readonly ModelSettingsService _model;
    private readonly FillExecutor _fill;
    private readonly Profiles _profiles;

    public IntentService(
        IConnectionCatalog catalog,
        ITemplateStore templates,
        IKeywordStore keywords,
        IModelSidecar sidecar,
        ModelSettingsService model,
        FillExecutor fill,
        Profiles profiles)
    {
        _catalog = catalog;
        _templates = templates;
        _keywords = keywords;
        _sidecar = sidecar;
        _model = model;
        _fill = fill;
        _profiles = profiles;
    }

    public async Task<OpResult<IntentParseResponse>> ParseAsync(string sessionId, string operatorId, string? utterance, string? surface, CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<IntentParseResponse>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsTerminal)
            return OpResult<IntentParseResponse>.Fail(WorkbenchMessages.Forbidden());
        if (surface is not (SurfaceNames.Mcp or SurfaceNames.Web))
            return OpResult<IntentParseResponse>.Fail(WorkbenchMessages.Forbidden());

        var text = utterance ?? "";
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var catalog = await _catalog.ListAsync(surface, cancellationToken);
        if (!catalog.Ok)
            return OpResult<IntentParseResponse>.Fail(new ErrorDto { Code = catalog.ErrorCode ?? "connection_failed", Message = Scrub.Message(catalog.ErrorMessage) });

        var visibleTemplates = _templates.List().Where(item => profile.CanViewGroup(item.GroupId)).ToList();
        var local = MatchLocal(tokens, catalog.Connections, visibleTemplates);
        var status = StatusOf(local, visibleTemplates);
        if (status != IntentStatuses.Ready && _model.Enabled)
        {
            var sidecar = await AskSidecarAsync(text, surface, catalog.Connections, visibleTemplates, cancellationToken);
            if (sidecar is not null)
            {
                local = sidecar;
                status = StatusOf(local, visibleTemplates);
            }
        }

        string? planId = null;
        if (status == IntentStatuses.Ready)
        {
            var chosen = local[0];
            var template = visibleTemplates.Single(item => item.Id == chosen.TemplateId);
            var filters = chosen.Parameters.Select(pair => new ScalarFilter { Key = pair.Key, Value = Canonical.CoerceToken(pair.Value) }).ToList();
            var calls = BuildCalls(template, filters);
            if (calls is null || !CallsFit(surface, calls))
            {
                status = IntentStatuses.Incomplete;
            }
            else
            {
                var plan = _fill.CreatePlan(profile, sessionId, surface, chosen.Connection ?? "", template.Id, calls);
                planId = plan.Id;
            }
        }

        return OpResult<IntentParseResponse>.Success(new IntentParseResponse
        {
            Status = status,
            PlanId = planId,
            Candidates = local,
            CanConfirm = status == IntentStatuses.Ready && planId is not null,
            ConfirmReason = status switch
            {
                IntentStatuses.Ready => null,
                IntentStatuses.Ambiguous => WorkbenchMessages.Ambiguous,
                _ => WorkbenchMessages.Incomplete,
            },
        });
    }

    public Task<OpResult<PlanResponse>> ConfirmAsync(string sessionId, string planId, CancellationToken cancellationToken)
        => _fill.ExecuteAsync(sessionId, planId, cancellationToken);

    private List<IntentCandidateDto> MatchLocal(string[] tokens, IReadOnlyList<ConnectionDto> connections, IReadOnlyList<TemplateRecord> templates)
    {
        var connectionHits = connections.Where(item => tokens.Contains(item.Name, StringComparer.Ordinal)).Select(item => item.Name).Distinct(StringComparer.Ordinal).ToList();
        var templateHits = new List<TemplateRecord>();
        foreach (var rule in _keywords.List())
        {
            if (string.IsNullOrWhiteSpace(rule.Phrase) || rule.ColumnNames.Count == 0 || rule.ColumnNames.Any(string.IsNullOrWhiteSpace))
                continue;
            if (!tokens.Contains(rule.Phrase, StringComparer.Ordinal))
                continue;
            var template = templates.FirstOrDefault(item => item.Id == rule.TemplateId);
            if (template is null)
                continue;
            var columns = template.Components.Where(item => item.Type == ComponentKinds.Table).SelectMany(item => item.Columns).ToHashSet(StringComparer.Ordinal);
            if (!rule.ColumnNames.All(columns.Contains))
                continue;
            if (templateHits.All(item => item.Id != template.Id))
                templateHits.Add(template);
        }

        foreach (var template in templates)
        {
            if (tokens.Contains(template.Name, StringComparer.Ordinal) && templateHits.All(item => item.Id != template.Id))
                templateHits.Add(template);
        }

        var results = new List<IntentCandidateDto>();
        var templatesForPairs = templateHits.Count == 0 ? [] : templateHits;
        var connectionsForPairs = connectionHits.Count == 0 ? [null] : connectionHits.Cast<string?>().ToList();
        if (connectionHits.Count == 0 && templateHits.Count == 0)
            return results;

        foreach (var connection in connectionsForPairs)
        {
            foreach (var template in templatesForPairs)
            {
                var parameters = ReadParameters(tokens, template);
                results.Add(new IntentCandidateDto
                {
                    Connection = connection,
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    Parameters = parameters,
                });
            }
        }

        if (templateHits.Count == 0)
        {
            foreach (var connection in connectionHits)
                results.Add(new IntentCandidateDto { Connection = connection });
        }

        return results;
    }

    private static Dictionary<string, string> ReadParameters(string[] tokens, TemplateRecord template)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var component in template.Components.Where(item => item.Type == ComponentKinds.Parameter))
        {
            var field = component.Field ?? "";
            if (field.Length == 0)
                continue;
            var index = Array.IndexOf(tokens, field);
            if (index >= 0 && index + 1 < tokens.Length)
                parameters[field] = tokens[index + 1];
        }

        return parameters;
    }

    private static string StatusOf(IReadOnlyList<IntentCandidateDto> candidates, IReadOnlyList<TemplateRecord> templates)
    {
        var connections = candidates.Select(item => item.Connection).Where(item => !string.IsNullOrEmpty(item)).Distinct(StringComparer.Ordinal).ToList();
        var templateIds = candidates.Select(item => item.TemplateId).Where(item => !string.IsNullOrEmpty(item)).Distinct(StringComparer.Ordinal).ToList();
        if (connections.Count != 1 || templateIds.Count != 1)
            return connections.Count > 1 || templateIds.Count > 1 ? IntentStatuses.Ambiguous : IntentStatuses.Incomplete;

        var template = templates.FirstOrDefault(item => item.Id == templateIds[0]);
        if (template is null)
            return IntentStatuses.Incomplete;
        var required = template.Components.Where(item => item.Type == ComponentKinds.Parameter).Select(item => item.Field ?? "").ToList();
        var chosen = candidates.First(item => item.Connection == connections[0] && item.TemplateId == templateIds[0]);
        if (required.Any(string.IsNullOrWhiteSpace) || required.Any(field => !chosen.Parameters.TryGetValue(field, out var value) || value.Length == 0))
            return IntentStatuses.Incomplete;
        return IntentStatuses.Ready;
    }

    private async Task<List<IntentCandidateDto>?> AskSidecarAsync(
        string utterance,
        string surface,
        IReadOnlyList<ConnectionDto> connections,
        IReadOnlyList<TemplateRecord> templates,
        CancellationToken cancellationToken)
    {
        var input = new SidecarParseInput
        {
            Utterance = utterance,
            Connections = connections.Select(item => new SidecarConnectionInput { Name = item.Name, ServerType = item.ServerType }).ToList(),
            Templates = templates.Select(item => new SidecarTemplateInput
            {
                Id = item.Id,
                Name = item.Name,
                Parameters = item.Components.Where(component => component.Type == ComponentKinds.Parameter).Select(component => component.Field ?? "").ToList(),
                Tables = item.Components.Where(component => component.Type is ComponentKinds.Table or ComponentKinds.Count).Select(component => component.Table ?? "").ToList(),
            }).ToList(),
            Surfaces = [surface],
        };
        var response = await _sidecar.InvokeAsync(new SidecarRequest
        {
            Kind = "parse",
            Placement = _model.Placement,
            Endpoint = _model.Endpoint,
            ModelId = _model.ModelId,
            ApiKey = _model.ResolveKey(null),
            ParseJson = JsonSerializer.Serialize(input),
        }, cancellationToken);
        if (response.Code is ModelCodes.InvalidSettings or ModelCodes.Unreachable or ModelCodes.Timeout)
            return null;

        var mapped = MapParse.Map(response.Stdout);
        if (!mapped.Parsed)
            return null;

        var allowedConnections = connections.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var allowedTemplates = templates.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in mapped.Candidates)
        {
            if (string.IsNullOrEmpty(candidate.Connection) || !allowedConnections.Contains(candidate.Connection))
                return [];
            if (string.IsNullOrEmpty(candidate.TemplateId) || !allowedTemplates.Contains(candidate.TemplateId))
                return [];
        }

        return mapped.Candidates;
    }

    private static List<FillCall>? BuildCalls(TemplateRecord template, List<ScalarFilter> filters)
    {
        var calls = new List<FillCall>();
        foreach (var component in template.Components.OrderBy(item => item.Order))
        {
            if (component.Type == ComponentKinds.Table)
            {
                if (string.IsNullOrWhiteSpace(component.Table) || component.Columns.Count == 0)
                    return null;
                calls.Add(new FillCall { Tool = "query", Table = component.Table!, Columns = component.Columns.ToList(), Filters = filters });
            }
            else if (component.Type == ComponentKinds.Count)
            {
                if (string.IsNullOrWhiteSpace(component.Table))
                    return null;
                calls.Add(new FillCall { Tool = "count", Table = component.Table!, Filters = filters });
            }
        }

        return calls.Count == 0 ? null : calls;
    }

    private static bool CallsFit(string surface, IReadOnlyList<FillCall> calls)
    {
        if (surface != SurfaceNames.Web)
            return true;
        return calls.All(call => call.Tool != "count" && call.Filters.Count <= 1);
    }
}

public sealed class FavoriteService
{
    private readonly IFavoriteStore _favorites;
    private readonly ITemplateStore _templates;
    private readonly Profiles _profiles;

    public FavoriteService(IFavoriteStore favorites, ITemplateStore templates, Profiles profiles)
    {
        _favorites = favorites;
        _templates = templates;
        _profiles = profiles;
    }

    public OpResult<IReadOnlyList<FavoriteDto>> List(string operatorId)
    {
        var profile = RequireTerminal(operatorId);
        if (profile.Error is not null)
            return OpResult<IReadOnlyList<FavoriteDto>>.Fail(profile.Error);
        IReadOnlyList<FavoriteDto> rows = _favorites.ListByOwner(profile.Value!.Id).Select(ToDto).ToList();
        return OpResult<IReadOnlyList<FavoriteDto>>.Success(rows);
    }

    public OpResult<FavoriteDto> Save(string operatorId, FavoriteWriteRequest request)
    {
        var profile = RequireTerminal(operatorId);
        if (profile.Error is not null)
            return OpResult<FavoriteDto>.Fail(profile.Error);
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
            return OpResult<FavoriteDto>.Fail(WorkbenchMessages.BlankName());
        var template = _templates.Find(request.TemplateId ?? "");
        if (template is null || !profile.Value!.CanViewGroup(template.GroupId))
            return OpResult<FavoriteDto>.Fail(WorkbenchMessages.Forbidden());

        var record = new FavoriteRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            TemplateId = template.Id,
            Surface = request.Surface ?? "",
            ConnectionName = request.ConnectionName ?? "",
            Parameters = new Dictionary<string, string>(request.Parameters, StringComparer.Ordinal),
            OwnerId = profile.Value.Id,
        };
        if (!_favorites.TrySave(record, out _))
            return OpResult<FavoriteDto>.Fail(WorkbenchMessages.BlankName());
        return OpResult<FavoriteDto>.Success(ToDto(record));
    }

    public OpResult<FavoriteOpenResponse> Open(string operatorId, string id)
    {
        var profile = RequireTerminal(operatorId);
        if (profile.Error is not null)
            return OpResult<FavoriteOpenResponse>.Fail(profile.Error);
        var favorite = _favorites.Find(id);
        if (favorite is null || favorite.OwnerId != profile.Value!.Id)
            return OpResult<FavoriteOpenResponse>.Fail(WorkbenchMessages.Forbidden());

        var template = _templates.Find(favorite.TemplateId);
        if (template is null)
            return OpResult<FavoriteOpenResponse>.Success(FailedOpen("範本已刪除。"));
        if (!profile.Value.CanViewGroup(template.GroupId))
            return OpResult<FavoriteOpenResponse>.Success(FailedOpen("已失去檢視角色。"));

        var required = template.Components.Where(item => item.Type == ComponentKinds.Parameter).Select(item => item.Field ?? "").ToList();
        if (required.Any(string.IsNullOrWhiteSpace) || required.Any(field => !favorite.Parameters.ContainsKey(field)) || favorite.Parameters.Keys.Any(key => !required.Contains(key, StringComparer.Ordinal)))
            return OpResult<FavoriteOpenResponse>.Success(FailedOpen("參數對不齊。"));

        return OpResult<FavoriteOpenResponse>.Success(new FavoriteOpenResponse
        {
            Template = TemplateLibraryService.ToDto(template),
            Parameters = new Dictionary<string, string>(favorite.Parameters, StringComparer.Ordinal),
        });
    }

    private OpResult<Profile> RequireTerminal(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<Profile>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsTerminal)
            return OpResult<Profile>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<Profile>.Success(profile);
    }

    private static FavoriteOpenResponse FailedOpen(string difference) => new()
    {
        ErrorCode = "alignment_failed",
        Difference = difference,
    };

    private static FavoriteDto ToDto(FavoriteRecord record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        TemplateId = record.TemplateId,
        Surface = record.Surface,
        ConnectionName = record.ConnectionName,
        Parameters = new Dictionary<string, string>(record.Parameters, StringComparer.Ordinal),
    };
}

public sealed class KeywordService
{
    private readonly IKeywordStore _keywords;
    private readonly ITemplateStore _templates;
    private readonly IPreviewMemory _memory;
    private readonly Profiles _profiles;

    public KeywordService(IKeywordStore keywords, ITemplateStore templates, IPreviewMemory memory, Profiles profiles)
    {
        _keywords = keywords;
        _templates = templates;
        _memory = memory;
        _profiles = profiles;
    }

    public OpResult<bool> AddHuman(string operatorId, KeywordWriteRequest request)
    {
        var profile = RequireTerminal(operatorId);
        if (profile.Error is not null)
            return OpResult<bool>.Fail(profile.Error);
        var phrase = request.Phrase?.Trim() ?? "";
        if (phrase.Length == 0)
            return OpResult<bool>.Fail(WorkbenchMessages.BlankPhrase());
        if (request.ColumnNames.Count == 0 || request.ColumnNames.Any(string.IsNullOrWhiteSpace))
            return OpResult<bool>.Fail(WorkbenchMessages.SuggestionRejected());
        var template = _templates.Find(request.TemplateId ?? "");
        if (template is null || !profile.Value!.CanViewGroup(template.GroupId))
            return OpResult<bool>.Fail(WorkbenchMessages.Forbidden());

        _keywords.Add(new KeywordRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Phrase = phrase,
            ColumnNames = request.ColumnNames.ToList(),
            Source = KeywordSources.Human,
            TemplateId = template.Id,
            OwnerId = profile.Value.Id,
        });
        return OpResult<bool>.Success(true);
    }

    public OpResult<bool> AddFromConfirm(string sessionId, string operatorId, KeywordWriteRequest request)
    {
        var profile = RequireTerminal(operatorId);
        if (profile.Error is not null)
            return OpResult<bool>.Fail(profile.Error);
        var phrase = request.Phrase?.Trim() ?? "";
        if (phrase.Length == 0)
            return OpResult<bool>.Fail(WorkbenchMessages.BlankPhrase());
        var snapshot = _memory.FindSnapshot(sessionId, request.SnapshotId ?? "");
        if (snapshot is null)
            return OpResult<bool>.Fail(WorkbenchMessages.SnapshotGone());
        var plan = _memory.FindPlan(snapshot.PlanId);
        if (plan?.TemplateId is null)
            return OpResult<bool>.Fail(WorkbenchMessages.Forbidden());
        var template = _templates.Find(plan.TemplateId);
        if (template is null || !profile.Value!.CanViewGroup(template.GroupId))
            return OpResult<bool>.Fail(WorkbenchMessages.Forbidden());

        var columns = template.Components.Where(item => item.Type == ComponentKinds.Table).SelectMany(item => item.Columns).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal).ToList();
        if (columns.Count == 0)
            return OpResult<bool>.Fail(WorkbenchMessages.SuggestionRejected());

        _keywords.Add(new KeywordRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Phrase = phrase,
            ColumnNames = columns,
            Source = KeywordSources.Ai,
            TemplateId = template.Id,
            OwnerId = profile.Value.Id,
        });
        return OpResult<bool>.Success(true);
    }

    private OpResult<Profile> RequireTerminal(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<Profile>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsTerminal)
            return OpResult<Profile>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<Profile>.Success(profile);
    }
}

public sealed class SnapshotExportService
{
    private readonly IPreviewMemory _memory;
    private readonly ISnapshotPdfWriter _pdf;
    private readonly Profiles _profiles;

    public SnapshotExportService(IPreviewMemory memory, ISnapshotPdfWriter pdf, Profiles profiles)
    {
        _memory = memory;
        _pdf = pdf;
        _profiles = profiles;
    }

    public OpResult<Snapshot> Get(string sessionId, string operatorId, string snapshotId)
    {
        if (_profiles.Find(operatorId) is null)
            return OpResult<Snapshot>.Fail(WorkbenchMessages.Unauthenticated());
        var snapshot = _memory.FindSnapshot(sessionId, snapshotId);
        if (snapshot is null)
            return OpResult<Snapshot>.Fail(WorkbenchMessages.SnapshotGone());
        return OpResult<Snapshot>.Success(snapshot);
    }

    public OpResult<byte[]> Pdf(string sessionId, string operatorId, string snapshotId)
    {
        var snapshot = Get(sessionId, operatorId, snapshotId);
        if (!snapshot.Ok)
            return OpResult<byte[]>.Fail(snapshot.Error!);
        return OpResult<byte[]>.Success(_pdf.Write(snapshot.Value!));
    }

    public OpResult<string> Csv(string sessionId, string operatorId, string snapshotId)
    {
        var snapshot = Get(sessionId, operatorId, snapshotId);
        if (!snapshot.Ok)
            return OpResult<string>.Fail(snapshot.Error!);
        return OpResult<string>.Success(Canonical.Csv(snapshot.Value!));
    }

    public OpResult<string> Print(string sessionId, string operatorId, string snapshotId)
    {
        var snapshot = Get(sessionId, operatorId, snapshotId);
        if (!snapshot.Ok)
            return OpResult<string>.Fail(snapshot.Error!);
        return OpResult<string>.Success(Canonical.PrintHtml(snapshot.Value!));
    }
}
