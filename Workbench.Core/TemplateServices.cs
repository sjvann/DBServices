using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class TemplateLibraryService
{
    private readonly ITemplateStore _templates;
    private readonly IDirectory _directory;
    private readonly Profiles _profiles;
    private readonly IDataSurface _surface;
    private readonly FillExecutor _fill;

    public TemplateLibraryService(ITemplateStore templates, IDirectory directory, Profiles profiles, IDataSurface surface, FillExecutor fill)
    {
        _templates = templates;
        _directory = directory;
        _profiles = profiles;
        _surface = surface;
        _fill = fill;
    }

    public OpResult<IReadOnlyList<TemplateDto>> List(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<IReadOnlyList<TemplateDto>>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.HasWork(WorkRoles.Editor) && !profile.HasWork(WorkRoles.Terminal) && !profile.IsAdmin)
            return OpResult<IReadOnlyList<TemplateDto>>.Fail(WorkbenchMessages.Forbidden());

        IReadOnlyList<TemplateDto> rows = _templates.List()
            .Where(item => profile.CanViewGroup(item.GroupId))
            .Select(ToDto)
            .ToList();
        return OpResult<IReadOnlyList<TemplateDto>>.Success(rows);
    }

    public OpResult<TemplateDto> Get(string operatorId, string id)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Unauthenticated());
        var record = _templates.Find(id);
        if (record is null || !profile.CanViewGroup(record.GroupId))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<TemplateDto>.Success(ToDto(record));
    }

    public OpResult<TemplateDto> Save(string operatorId, TemplateDto template)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.CanDesignGroup(template.GroupId))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Forbidden());

        var name = template.Name?.Trim() ?? "";
        if (name.Length == 0)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.BlankName());
        var existing = string.IsNullOrEmpty(template.Id) ? null : _templates.Find(template.Id);
        if (existing is not null && existing.GroupId != template.GroupId && !profile.CanDesignGroup(existing.GroupId))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Forbidden());
        if (existing is not null && !profile.CanDesignGroup(template.GroupId))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Forbidden());

        var normalized = NormalizeOrReject(template.Components);
        if (normalized.Error is not null)
            return OpResult<TemplateDto>.Fail(normalized.Error);

        var record = new TemplateRecord
        {
            Id = string.IsNullOrEmpty(template.Id) ? Guid.NewGuid().ToString("N") : template.Id,
            GroupId = template.GroupId,
            Name = name,
            Components = normalized.Value!,
        };
        if (!_templates.TrySave(record, out var error))
        {
            if (existing is not null && error == "duplicate_name")
                return OpResult<TemplateDto>.Fail(WorkbenchMessages.DuplicateName());
            return OpResult<TemplateDto>.Fail(error == "duplicate_name" ? WorkbenchMessages.DuplicateName() : WorkbenchMessages.BlankName());
        }

        return OpResult<TemplateDto>.Success(ToDto(record));
    }

    public OpResult<bool> Delete(string operatorId, string id)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<bool>.Fail(WorkbenchMessages.Unauthenticated());
        var existing = _templates.Find(id);
        if (existing is null || !profile.CanDesignGroup(existing.GroupId))
            return OpResult<bool>.Fail(WorkbenchMessages.Forbidden());
        _templates.Delete(id);
        return OpResult<bool>.Success(true);
    }

    public async Task<OpResult<PlanResponse>> PreviewAsync(string sessionId, string operatorId, string templateId, string surface, string connection, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.Unauthenticated());
        var template = _templates.Find(templateId);
        if (template is null || !profile.CanViewGroup(template.GroupId))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.Forbidden());
        if (surface is not (SurfaceNames.Mcp or SurfaceNames.Web))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.Forbidden());

        var parameterFields = template.Components.Where(item => item.Type == ComponentKinds.Parameter).Select(item => item.Field ?? "").ToList();
        if (parameterFields.Any(string.IsNullOrWhiteSpace) || parameterFields.Any(field => !parameters.ContainsKey(field)))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.MissingColumn());

        var filters = parameters.Select(pair => new ScalarFilter { Key = pair.Key, Value = Canonical.CoerceToken(pair.Value) }).ToList();
        if (surface == SurfaceNames.Web && filters.Count is not (0 or 1))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.SingleKey());
        if (surface == SurfaceNames.Web && template.Components.Any(item => item.Type == ComponentKinds.Count))
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.WebCount());

        var calls = new List<FillCall>();
        foreach (var component in template.Components.OrderBy(item => item.Order))
        {
            if (component.Type == ComponentKinds.Table)
            {
                if (string.IsNullOrWhiteSpace(component.Table) || component.Columns.Count == 0)
                    return OpResult<PlanResponse>.Fail(WorkbenchMessages.MissingColumn());
                calls.Add(new FillCall { Tool = "query", Table = component.Table!, Columns = component.Columns.ToList(), Filters = filters });
            }
            else if (component.Type == ComponentKinds.Count)
            {
                if (string.IsNullOrWhiteSpace(component.Table))
                    return OpResult<PlanResponse>.Fail(WorkbenchMessages.MissingColumn());
                calls.Add(new FillCall { Tool = "count", Table = component.Table!, Filters = filters });
            }
        }

        if (calls.Count == 0)
            return OpResult<PlanResponse>.Fail(WorkbenchMessages.MissingColumn());

        var plan = _fill.CreatePlan(profile, sessionId, surface, connection, template.Id, calls);
        return await _fill.ExecuteAsync(sessionId, plan.Id, cancellationToken);
    }

    public static OpResult<List<ComponentDto>> NormalizeOrReject(IReadOnlyList<ComponentDto>? components)
    {
        var list = new List<ComponentDto>();
        foreach (var component in components ?? [])
        {
            if (component.Type is not (ComponentKinds.Title or ComponentKinds.Text or ComponentKinds.Parameter or ComponentKinds.Table or ComponentKinds.Count))
                return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.SuggestionRejected());
            if (component.Type == ComponentKinds.Parameter && string.IsNullOrWhiteSpace(component.Field))
                return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.MissingColumn());
            if (component.Type == ComponentKinds.Table && (string.IsNullOrWhiteSpace(component.Table) || component.Columns.Count == 0 || component.Columns.Any(string.IsNullOrWhiteSpace)))
                return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.MissingColumn());
            if (component.Type == ComponentKinds.Count && string.IsNullOrWhiteSpace(component.Table))
                return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.MissingColumn());
            list.Add(new ComponentDto
            {
                Type = component.Type,
                Text = component.Text,
                Field = component.Field,
                Label = component.Label,
                Table = component.Table,
                Columns = component.Columns.ToList(),
            });
        }

        return OpResult<List<ComponentDto>>.Success(Canonical.Normalize(list));
    }

    public static TemplateDto ToDto(TemplateRecord record) => new()
    {
        Id = record.Id,
        GroupId = record.GroupId,
        Name = record.Name,
        Components = record.Components.Select(item => new ComponentDto
        {
            Type = item.Type,
            Order = item.Order,
            Text = item.Text,
            Field = item.Field,
            Label = item.Label,
            Table = item.Table,
            Columns = item.Columns.ToList(),
        }).ToList(),
    };
}

public sealed class SuggestionService
{
    private readonly IDataSurface _surface;
    private readonly Profiles _profiles;
    private readonly ITemplateStore _templates;

    public SuggestionService(IDataSurface surface, Profiles profiles, ITemplateStore templates)
    {
        _surface = surface;
        _profiles = profiles;
        _templates = templates;
    }

    public async Task<OpResult<List<ComponentDto>>> ProposeAsync(string operatorId, SuggestionRequest request, CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.HasWork(WorkRoles.Editor))
            return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.Forbidden());
        if (string.IsNullOrWhiteSpace(request.Table) || request.Surface is not (SurfaceNames.Mcp or SurfaceNames.Web))
            return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.SuggestionRejected());

        var fields = await _surface.FieldsAsync(new SurfaceRequest
        {
            Surface = request.Surface,
            Connection = request.Connection ?? "",
            Table = request.Table,
            CallerId = profile.Id,
        }, cancellationToken);
        if (!fields.Ok)
            return OpResult<List<ComponentDto>>.Fail(new ErrorDto { Code = fields.ErrorCode ?? "connection_failed", Message = Scrub.Message(fields.ErrorMessage) });

        var names = fields.Fields.Select(field => field.Name).Where(name => !string.IsNullOrEmpty(name)).ToList();
        if (names.Count == 0)
            return OpResult<List<ComponentDto>>.Fail(WorkbenchMessages.SuggestionRejected());

        return OpResult<List<ComponentDto>>.Success(
        [
            new ComponentDto
            {
                Type = ComponentKinds.Table,
                Order = 0,
                Table = request.Table,
                Columns = names,
            },
        ]);
    }

    public async Task<OpResult<TemplateDto>> AcceptAsync(string operatorId, SuggestionRequest request, CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Unauthenticated());
        var template = string.IsNullOrEmpty(request.TemplateId) ? null : _templates.Find(request.TemplateId);
        if (template is null || !profile.CanDesignGroup(template.GroupId))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.Forbidden());
        if (request.Proposal is null || request.Proposal.Count == 0)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.SuggestionRejected());

        var tables = request.Proposal.Where(item => item.Type == ComponentKinds.Table).ToList();
        if (tables.Count == 0)
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.SuggestionRejected());

        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.Table))
                return OpResult<TemplateDto>.Fail(WorkbenchMessages.SuggestionRejected());
            var fields = await _surface.FieldsAsync(new SurfaceRequest
            {
                Surface = request.Surface ?? "",
                Connection = request.Connection ?? "",
                Table = table.Table,
                CallerId = profile.Id,
            }, cancellationToken);
            if (!fields.Ok)
                return OpResult<TemplateDto>.Fail(WorkbenchMessages.SuggestionRejected());
            var known = fields.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
            if (table.Columns.Count == 0 || table.Columns.Any(column => !known.Contains(column)))
                return OpResult<TemplateDto>.Fail(WorkbenchMessages.SuggestionRejected());
        }

        var normalized = TemplateLibraryService.NormalizeOrReject(request.Proposal);
        if (!normalized.Ok)
            return OpResult<TemplateDto>.Fail(normalized.Error!);
        template.Components = normalized.Value!;
        if (!_templates.TrySave(template, out _))
            return OpResult<TemplateDto>.Fail(WorkbenchMessages.DuplicateName());
        return OpResult<TemplateDto>.Success(TemplateLibraryService.ToDto(template));
    }
}
