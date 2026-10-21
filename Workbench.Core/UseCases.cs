using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class Profile
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool IsAdmin { get; init; }
    public IReadOnlyList<string> Entries { get; init; } = [];

    public bool Has(string entry) => Entries.Contains(entry, StringComparer.Ordinal);
}

public sealed class Profiles
{
    private readonly IOperatorStore _operators;

    public Profiles(IOperatorStore operators) => _operators = operators;

    public Profile? Find(string? operatorId)
    {
        if (string.IsNullOrEmpty(operatorId))
            return null;
        var record = _operators.FindById(operatorId);
        if (record is null)
            return null;
        return new Profile
        {
            Id = record.Id,
            DisplayName = record.DisplayName,
            IsAdmin = record.IsAdmin,
            Entries = _operators.EntriesFor(record.Id),
        };
    }
}

public sealed class SeedAdminService
{
    private readonly IOperatorStore _operators;
    private readonly IPasswordHasher _hasher;

    public SeedAdminService(IOperatorStore operators, IPasswordHasher hasher)
    {
        _operators = operators;
        _hasher = hasher;
    }

    public bool Unavailable { get; private set; }

    public void Ensure(string? displayName, string? password)
    {
        if (_operators.Count() > 0)
        {
            Unavailable = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrEmpty(password))
        {
            Unavailable = true;
            return;
        }

        _operators.Add(new OperatorRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = displayName.Trim(),
            PasswordVerifier = _hasher.Hash(password),
            IsAdmin = true,
        });
        Unavailable = false;
    }
}

public sealed class SessionService
{
    private readonly IOperatorStore _operators;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionStore _sessions;
    private readonly SeedAdminService _seed;

    public SessionService(IOperatorStore operators, IPasswordHasher hasher, ISessionStore sessions, SeedAdminService seed)
    {
        _operators = operators;
        _hasher = hasher;
        _sessions = sessions;
        _seed = seed;
    }

    public OpResult<SessionResponse> Login(string? displayName, string? secret)
    {
        if (_seed.Unavailable)
            return OpResult<SessionResponse>.Fail(WorkbenchMessages.Unavailable());

        var record = _operators.FindByName(displayName?.Trim() ?? "");
        if (record is null || secret is null || !_hasher.Verify(secret, record.PasswordVerifier))
            return OpResult<SessionResponse>.Fail(WorkbenchMessages.Unauthenticated());

        return OpResult<SessionResponse>.Success(new SessionResponse { SessionId = _sessions.Create(record.Id) });
    }

    public void Logout(string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId))
            _sessions.Delete(sessionId);
    }

    public string? OperatorId(string? sessionId) =>
        string.IsNullOrEmpty(sessionId) ? null : _sessions.OperatorFor(sessionId);
}

public sealed class OperatorService
{
    private readonly IOperatorStore _operators;
    private readonly IPasswordHasher _hasher;
    private readonly Profiles _profiles;

    public OperatorService(IOperatorStore operators, IPasswordHasher hasher, Profiles profiles)
    {
        _operators = operators;
        _hasher = hasher;
        _profiles = profiles;
    }

    public OpResult<MeResponse> Me(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<MeResponse>.Fail(WorkbenchMessages.Unauthenticated());
        return OpResult<MeResponse>.Success(ToMe(profile));
    }

    public static MeResponse ToMe(Profile profile)
    {
        var entries = profile.Entries.Where(entry => EntryNames.All.Contains(entry, StringComparer.Ordinal)).ToList();
        var destination = entries.Count switch
        {
            1 => EntryNames.Home(entries[0]),
            > 1 => "/stations",
            _ => profile.IsAdmin ? "/operators" : "/",
        };
        return new MeResponse
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            IsAdmin = profile.IsAdmin,
            Entries = entries,
            Destination = destination,
        };
    }

    public OpResult<IReadOnlyList<OperatorDto>> List(string operatorId)
    {
        if (_profiles.Find(operatorId) is not { IsAdmin: true })
            return AdminOrDeny(operatorId);
        return OpResult<IReadOnlyList<OperatorDto>>.Success(_operators.List().Select(ToDto).ToList());
    }

    public OpResult<OperatorDto> Create(string operatorId, OperatorWriteRequest request)
    {
        if (_profiles.Find(operatorId) is not { IsAdmin: true })
            return OpResult<OperatorDto>.Fail(Deny(operatorId));
        if (request.IsAdmin)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.SecondAdmin));

        var name = request.DisplayName?.Trim() ?? "";
        if (name.Length == 0 || string.IsNullOrEmpty(request.Secret))
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.BlankName());
        if (_operators.FindByName(name) is not null)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.DuplicateName());

        var record = new OperatorRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = name,
            PasswordVerifier = _hasher.Hash(request.Secret),
            IsAdmin = false,
        };
        _operators.Add(record);
        return OpResult<OperatorDto>.Success(ToDto(record));
    }

    public OpResult<OperatorDto> RejectAdminChange(string operatorId, OperatorWriteRequest request)
    {
        if (_profiles.Find(operatorId) is not { IsAdmin: true })
            return OpResult<OperatorDto>.Fail(Deny(operatorId));
        if (request.IsAdmin)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.SecondAdmin));
        return OpResult<OperatorDto>.Fail(WorkbenchMessages.Forbidden());
    }

    public OpResult<OperatorDto> Assign(string operatorId, string targetId, IReadOnlyList<string>? entries)
    {
        if (_profiles.Find(operatorId) is not { IsAdmin: true })
            return OpResult<OperatorDto>.Fail(Deny(operatorId));
        var target = _operators.FindById(targetId);
        if (target is null)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Text("forbidden", "找不到操作者。"));
        var requested = entries ?? [];
        if (requested.Any(entry => !EntryNames.All.Contains(entry, StringComparer.Ordinal)))
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Text("forbidden", "沒有這個入口。"));

        _operators.ReplaceEntries(targetId, requested.Distinct(StringComparer.Ordinal).ToList());
        return OpResult<OperatorDto>.Success(ToDto(target));
    }

    private OpResult<IReadOnlyList<OperatorDto>> AdminOrDeny(string operatorId)
        => OpResult<IReadOnlyList<OperatorDto>>.Fail(Deny(operatorId));

    private ErrorDto Deny(string operatorId)
        => _profiles.Find(operatorId) is null ? WorkbenchMessages.Unauthenticated() : WorkbenchMessages.Forbidden();

    private OperatorDto ToDto(OperatorRecord record) => new()
    {
        Id = record.Id,
        DisplayName = record.DisplayName,
        IsAdmin = record.IsAdmin,
        Entries = _operators.EntriesFor(record.Id).ToList(),
    };
}

public static class ViewShape
{
    public static string? Problem(ViewDocumentDto document, IReadOnlyList<ViewDocumentDto> existing)
    {
        var name = document.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(document.Id) || name.Length == 0)
            return WorkbenchMessages.BlankName().Message;
        if (existing.Any(item =>
                !string.Equals(item.Id, document.Id, StringComparison.Ordinal)
                && string.Equals(item.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            return WorkbenchMessages.DuplicateName().Message;

        var sources = (document.Sources ?? []).Select(source => source?.Trim() ?? "").Where(source => source.Length > 0).ToList();
        if (sources.Count == 0 || sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Count)
            return WorkbenchMessages.Disconnected;

        var known = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        foreach (var link in document.Links ?? [])
        {
            if (link.Kind is not ("foreign-key" or "accepted"))
                return WorkbenchMessages.Disconnected;
            if (!known.Contains(link.From ?? "") || !known.Contains(link.To ?? ""))
                return WorkbenchMessages.Disconnected;
            if (string.IsNullOrWhiteSpace(link.FromColumn) || string.IsNullOrWhiteSpace(link.ToColumn))
                return WorkbenchMessages.Disconnected;
        }

        if (sources.Count > 1 && !Connected(sources, document.Links ?? []))
            return WorkbenchMessages.Disconnected;
        return null;
    }

    public static bool SameStructure(ViewDocumentDto left, ViewDocumentDto right)
        => string.Equals(Canonical(left), Canonical(right), StringComparison.Ordinal);

    private static string Canonical(ViewDocumentDto document)
    {
        var shape = new
        {
            name = document.Name?.Trim() ?? "",
            published = document.Published,
            sources = document.Sources ?? [],
            links = (document.Links ?? []).Select(link => new { link.Kind, link.From, link.FromColumn, link.To, link.ToColumn }),
            parameters = (document.Parameters ?? []).Select(item => new { item.Name, item.Column, item.Source }),
            columns = (document.Columns ?? []).Select(item => new { item.Name, item.Source }),
        };
        return JsonSerializer.Serialize(shape);
    }

    private static bool Connected(IReadOnlyList<string> sources, IReadOnlyList<ViewLinkDto> links)
    {
        var adjacent = sources.ToDictionary(source => source, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            adjacent[link.From].Add(link.To);
            adjacent[link.To].Add(link.From);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(sources[0]);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!seen.Add(node))
                continue;
            foreach (var next in adjacent[node])
                pending.Push(next);
        }

        return seen.Count == sources.Count;
    }
}

public sealed class ViewService
{
    private readonly Profiles _profiles;
    private readonly IViewCatalog _catalog;
    private readonly IStyleStore _styles;
    private readonly EnvironmentService _environment;

    public ViewService(Profiles profiles, IViewCatalog catalog, IStyleStore styles, EnvironmentService environment)
    {
        _profiles = profiles;
        _catalog = catalog;
        _styles = styles;
        _environment = environment;
    }

    public async Task<OpResult<IReadOnlyList<ViewSummaryDto>>> ListAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<IReadOnlyList<ViewSummaryDto>>.Fail(error);
        var documents = await _catalog.ListAsync(operatorId, cancellationToken);
        IReadOnlyList<ViewSummaryDto> rows = documents.Select(Summary).ToList();
        return OpResult<IReadOnlyList<ViewSummaryDto>>.Success(rows);
    }

    public async Task<OpResult<ViewDocumentDto>> ReadAsync(string operatorId, string id, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<ViewDocumentDto>.Fail(error);
        var document = await _catalog.FindAsync(id, operatorId, cancellationToken);
        return document is null
            ? OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text("not_found", "找不到檢視。"))
            : OpResult<ViewDocumentDto>.Success(document);
    }

    public async Task<OpResult<ViewDocumentDto>> SaveAsync(string operatorId, ViewDocumentDto document, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<ViewDocumentDto>.Fail(error);

        var existing = await _catalog.ListAsync(operatorId, cancellationToken);
        var problem = ViewShape.Problem(document, existing);
        if (problem is not null)
        {
            var code = problem == WorkbenchMessages.DuplicateName().Message ? "duplicate_name"
                : problem == WorkbenchMessages.BlankName().Message ? "blank_name"
                : "disconnected";
            return OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text(code, problem));
        }

        document.Name = document.Name.Trim();
        if (_styles.References(document.Id))
        {
            var current = existing.FirstOrDefault(item => item.Id == document.Id);
            if (current is null || !ViewShape.SameStructure(current, document))
                return OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.FrozenStructure));
        }

        var saved = await _catalog.SaveAsync(document, operatorId, cancellationToken);
        return saved.Ok
            ? OpResult<ViewDocumentDto>.Success(saved.Document!)
            : OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text(saved.Code, saved.Message));
    }

    public async Task<OpResult<bool>> DeleteAsync(string operatorId, string id, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<bool>.Fail(error);
        if (_styles.References(id))
            return OpResult<bool>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.FrozenDelete));
        var deleted = await _catalog.DeleteAsync(id, operatorId, cancellationToken);
        return deleted.Ok
            ? OpResult<bool>.Success(true)
            : OpResult<bool>.Fail(WorkbenchMessages.Text(deleted.Code, deleted.Message));
    }

    public Task<OpResult<ViewDocumentDto>> PublishAsync(string operatorId, string id, CancellationToken cancellationToken)
        => SetPublishedAsync(operatorId, id, true, cancellationToken);

    public async Task<OpResult<ViewDocumentDto>> UnpublishAsync(string operatorId, string id, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<ViewDocumentDto>.Fail(error);
        if (_styles.References(id))
            return OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.FrozenUnpublish));
        return await SetPublishedAsync(operatorId, id, false, cancellationToken);
    }

    private async Task<OpResult<ViewDocumentDto>> SetPublishedAsync(string operatorId, string id, bool published, CancellationToken cancellationToken)
    {
        if (await Closed(operatorId, cancellationToken) is { } error)
            return OpResult<ViewDocumentDto>.Fail(error);
        var result = await _catalog.SetPublishedAsync(id, published, operatorId, cancellationToken);
        return result.Ok
            ? OpResult<ViewDocumentDto>.Success(result.Document!)
            : OpResult<ViewDocumentDto>.Fail(WorkbenchMessages.Text(result.Code, result.Message));
    }

    private async Task<ErrorDto?> Closed(string operatorId, CancellationToken cancellationToken)
    {
        if (Deny(operatorId) is { } error)
            return error;
        return await _environment.BlockAsync(cancellationToken);
    }

    private ErrorDto? Deny(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.Has(EntryNames.Analyst) ? null : WorkbenchMessages.Forbidden();
    }

    private static ViewSummaryDto Summary(ViewDocumentDto document) => new()
    {
        Id = document.Id,
        Name = document.Name,
        Purpose = document.Purpose,
        Published = document.Published,
    };
}

public sealed class StyleService
{
    private readonly Profiles _profiles;
    private readonly IStyleStore _styles;
    private readonly IViewCatalog _catalog;
    private readonly IReportMemory _reports;
    private readonly EnvironmentService _environment;

    public StyleService(Profiles profiles, IStyleStore styles, IViewCatalog catalog, IReportMemory reports, EnvironmentService environment)
    {
        _profiles = profiles;
        _styles = styles;
        _catalog = catalog;
        _reports = reports;
        _environment = environment;
    }

    public OpResult<IReadOnlyList<StyleDto>> List(string operatorId)
    {
        if (Closed(operatorId, EntryNames.Designer) is { } error)
            return OpResult<IReadOnlyList<StyleDto>>.Fail(error);
        return OpResult<IReadOnlyList<StyleDto>>.Success(_styles.ReadableBy(operatorId).Select(ToDto).ToList());
    }

    public async Task<OpResult<IReadOnlyList<ViewSummaryDto>>> PublishedViewsAsync(string operatorId, CancellationToken cancellationToken)
    {
        if (await ClosedAsync(operatorId, EntryNames.Designer, cancellationToken) is { } error)
            return OpResult<IReadOnlyList<ViewSummaryDto>>.Fail(error);
        var documents = await _catalog.ListAsync(operatorId, cancellationToken);
        IReadOnlyList<ViewSummaryDto> rows = documents
            .Where(item => item.Published)
            .Select(item => new ViewSummaryDto
            {
                Id = item.Id,
                Name = item.Name,
                Purpose = item.Purpose,
                Published = item.Published,
            })
            .ToList();
        return OpResult<IReadOnlyList<ViewSummaryDto>>.Success(rows);
    }

    public async Task<OpResult<IReadOnlyList<StyleBlockDto>>> SuggestAsync(string operatorId, string? viewId, CancellationToken cancellationToken)
    {
        if (await ClosedAsync(operatorId, EntryNames.Designer, cancellationToken) is { } error)
            return OpResult<IReadOnlyList<StyleBlockDto>>.Fail(error);
        var view = await _catalog.FindAsync(viewId ?? "", operatorId, cancellationToken);
        if (view is null)
            return OpResult<IReadOnlyList<StyleBlockDto>>.Fail(WorkbenchMessages.Text("not_found", "找不到檢視。"));

        var blocks = new List<StyleBlockDto>
        {
            new() { Type = BlockKinds.Title, Order = 0, Text = view.Name },
            new() { Type = BlockKinds.Text, Order = 1, Text = view.Purpose },
        };
        var order = 2;
        foreach (var parameter in view.Parameters ?? [])
            blocks.Add(new StyleBlockDto { Type = BlockKinds.Parameter, Order = order++, Parameter = parameter.Name, Label = parameter.Name });
        blocks.Add(new StyleBlockDto
        {
            Type = BlockKinds.Table,
            Order = order,
            Columns = (view.Columns ?? []).Select(column => column.Name).ToList(),
        });
        return OpResult<IReadOnlyList<StyleBlockDto>>.Success(blocks);
    }

    public async Task<OpResult<StyleDto>> AcceptAsync(string operatorId, StyleWriteRequest request, CancellationToken cancellationToken)
    {
        if (await ClosedAsync(operatorId, EntryNames.Designer, cancellationToken) is { } error)
            return OpResult<StyleDto>.Fail(error);
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.BlankName());
        var view = await _catalog.FindAsync(request.ViewId ?? "", operatorId, cancellationToken);
        if (view is null)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.Text("not_found", "找不到檢視。"));
        if (!Accepts(view, request.Blocks))
            return OpResult<StyleDto>.Fail(WorkbenchMessages.UnknownColumn());

        var blocks = Reindex(request.Blocks);
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            var current = _styles.Find(request.Id);
            if (current is null || current.OwnerId != operatorId)
                return OpResult<StyleDto>.Fail(WorkbenchMessages.Forbidden());
            current.Name = name;
            current.ViewId = view.Id;
            current.Shared = request.Shared;
            current.Blocks = blocks;
            _styles.TryUpdate(current);
            return OpResult<StyleDto>.Success(ToDto(current));
        }

        var created = new StyleRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ViewId = view.Id,
            OwnerId = operatorId,
            Shared = request.Shared,
            Blocks = blocks,
        };
        _styles.Add(created);
        return OpResult<StyleDto>.Success(ToDto(created));
    }

    public OpResult<StyleDto?> Ignore(string operatorId, string? styleId)
    {
        if (Closed(operatorId, EntryNames.Designer) is { } error)
            return OpResult<StyleDto?>.Fail(error);
        if (string.IsNullOrWhiteSpace(styleId))
            return OpResult<StyleDto?>.Success(null);
        var current = _styles.Find(styleId);
        if (current is null || (current.OwnerId != operatorId && !current.Shared))
            return OpResult<StyleDto?>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<StyleDto?>.Success(ToDto(current));
    }

    public OpResult<StyleDto> Move(string operatorId, string styleId, int index, int direction)
    {
        if (Closed(operatorId, EntryNames.Designer) is { } error)
            return OpResult<StyleDto>.Fail(error);
        var current = _styles.Find(styleId);
        if (current is null || current.OwnerId != operatorId)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.Forbidden());
        var blocks = current.Blocks.OrderBy(block => block.Order).ToList();
        var target = index + direction;
        if (index < 0 || index >= blocks.Count || target < 0 || target >= blocks.Count)
            return OpResult<StyleDto>.Success(ToDto(current));
        (blocks[index], blocks[target]) = (blocks[target], blocks[index]);
        current.Blocks = Reindex(blocks);
        _styles.TryUpdate(current);
        return OpResult<StyleDto>.Success(ToDto(current));
    }

    public OpResult<StyleDto> SaveLayout(string operatorId, string sessionId, LayoutWriteRequest request)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.Has(EntryNames.Terminal))
            return OpResult<StyleDto>.Fail(WorkbenchMessages.Forbidden());
        if (_environment.Block() is { } blocked)
            return OpResult<StyleDto>.Fail(blocked);
        var report = _reports.Current(sessionId);
        if (report is null)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.Text("forbidden", "沒有可以存回的版面。"));

        var name = string.IsNullOrWhiteSpace(request.Name) ? report.Name : request.Name.Trim();
        if (name.Length == 0)
            return OpResult<StyleDto>.Fail(WorkbenchMessages.BlankName());
        var created = new StyleRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            ViewId = report.ViewId,
            OwnerId = operatorId,
            Shared = request.Shared,
            Blocks = Reindex(report.Blocks.Select(CopyTemplate).ToList()),
        };
        _styles.Add(created);
        return OpResult<StyleDto>.Success(ToDto(created));
    }

    private static bool Accepts(ViewDocumentDto view, IReadOnlyList<StyleBlockDto> blocks)
    {
        var columns = new HashSet<string>(view.Columns.Select(column => column.Name), StringComparer.OrdinalIgnoreCase);
        var parameters = new HashSet<string>(view.Parameters.Select(parameter => parameter.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var block in blocks)
        {
            if (!BlockKinds.Known(block.Type))
                return false;
            if (block.Type == BlockKinds.Table && (block.Columns ?? []).Any(column => !columns.Contains(column)))
                return false;
            if (block.Type == BlockKinds.Parameter && !parameters.Contains(block.Parameter ?? ""))
                return false;
        }

        return true;
    }

    public static List<StyleBlockDto> Reindex(IReadOnlyList<StyleBlockDto> blocks)
    {
        var ordered = blocks.OrderBy(block => block.Order).ThenBy(block => block.Type).ToList();
        for (var index = 0; index < ordered.Count; index++)
            ordered[index].Order = index;
        return ordered;
    }

    public static StyleBlockDto CopyTemplate(StyleBlockDto block) => new()
    {
        Type = block.Type,
        Order = block.Order,
        Text = block.Type is BlockKinds.Title or BlockKinds.Text ? block.Text : null,
        Parameter = block.Parameter,
        Label = block.Label,
        Columns = (block.Columns ?? []).ToList(),
    };

    public static StyleDto ToDto(StyleRecord record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        ViewId = record.ViewId,
        OwnerId = record.OwnerId,
        Shared = record.Shared,
        Blocks = record.Blocks.OrderBy(block => block.Order).Select(CopyTemplate).ToList(),
    };

    private ErrorDto? Closed(string operatorId, string entry)
    {
        if (Deny(operatorId, entry) is { } error)
            return error;
        return _environment.Block();
    }

    private async Task<ErrorDto?> ClosedAsync(string operatorId, string entry, CancellationToken cancellationToken)
    {
        if (Deny(operatorId, entry) is { } error)
            return error;
        return await _environment.BlockAsync(cancellationToken);
    }

    private ErrorDto? Deny(string operatorId, string entry)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.Has(entry) ? null : WorkbenchMessages.Forbidden();
    }
}

public sealed class UtteranceService
{
    private readonly Profiles _profiles;
    private readonly IStyleStore _styles;
    private readonly IViewCatalog _catalog;
    private readonly IConnectionCount _connections;
    private readonly IViewCaller _caller;
    private readonly IReportMemory _reports;
    private readonly EnvironmentService _environment;

    public UtteranceService(
        Profiles profiles,
        IStyleStore styles,
        IViewCatalog catalog,
        IConnectionCount connections,
        IViewCaller caller,
        IReportMemory reports,
        EnvironmentService environment)
    {
        _profiles = profiles;
        _styles = styles;
        _catalog = catalog;
        _connections = connections;
        _caller = caller;
        _reports = reports;
        _environment = environment;
    }

    public async Task<OpResult<UtteranceResponse>> SubmitAsync(
        string operatorId,
        string sessionId,
        string? text,
        string? styleId,
        CancellationToken cancellationToken)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.Has(EntryNames.Terminal))
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Forbidden());
        if (await _environment.BlockAsync(cancellationToken) is { } blocked)
            return OpResult<UtteranceResponse>.Fail(blocked);

        var utterance = text?.Trim() ?? "";
        var readable = _styles.ReadableBy(operatorId);
        StyleRecord? style;
        if (!string.IsNullOrWhiteSpace(styleId))
        {
            style = readable.FirstOrDefault(item => item.Id == styleId);
            if (style is null)
                return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.UnreadableStyle));
        }
        else
        {
            var matches = Match(utterance, readable);
            if (matches.Count == 0)
                return OpResult<UtteranceResponse>.Success(NotReady(WorkbenchMessages.NoReport(readable.Select(item => item.Name))));
            if (matches.Count > 1)
                return OpResult<UtteranceResponse>.Success(NotReady(WorkbenchMessages.SeveralReports(matches.Select(item => item.Name))));
            style = matches[0];
        }

        var view = await _catalog.FindAsync(style.ViewId, operatorId, cancellationToken);
        if (view is null)
            return OpResult<UtteranceResponse>.Success(NotReady(WorkbenchMessages.ReportUnavailable));

        var parameters = Parse(utterance, view.Parameters);
        var missing = view.Parameters.Select(item => item.Name).Where(name => !parameters.ContainsKey(name)).ToList();
        if (missing.Count > 0)
            return OpResult<UtteranceResponse>.Success(NotReady(WorkbenchMessages.MissingConditions(missing)));

        var tally = await _connections.TallyAsync(cancellationToken);
        if (tally.Available != 1 || string.IsNullOrWhiteSpace(tally.OnlyName))
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.UndecidedConnection());

        var call = await _caller.CallAsync(view.Name, tally.OnlyName, parameters, operatorId, cancellationToken);
        if (!call.Ok)
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Text(call.Code, call.Message));

        var report = new ReportDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            StyleId = style.Id,
            ViewId = view.Id,
            Name = style.Name,
            Blocks = style.Blocks.Select(StyleService.CopyTemplate).ToList(),
            Columns = call.Columns,
            Rows = call.Rows,
            Parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal),
        };
        _reports.Save(sessionId, report);
        return OpResult<UtteranceResponse>.Success(new UtteranceResponse
        {
            Status = "ready",
            ReadCount = 1,
            StyleId = style.Id,
            ReportId = report.Id,
            Parameters = report.Parameters,
            Columns = report.Columns,
            Rows = report.Rows,
            Blocks = report.Blocks,
        });
    }

    public void Leave(string sessionId) => _reports.Clear(sessionId);

    public OpResult<UtteranceResponse> Current(string operatorId, string sessionId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.Has(EntryNames.Terminal))
            return OpResult<UtteranceResponse>.Fail(WorkbenchMessages.Forbidden());
        var report = _reports.Current(sessionId);
        if (report is null)
            return OpResult<UtteranceResponse>.Success(new UtteranceResponse { Status = "empty", ReadCount = 0 });
        return OpResult<UtteranceResponse>.Success(new UtteranceResponse
        {
            Status = "ready",
            ReadCount = 0,
            StyleId = report.StyleId,
            ReportId = report.Id,
            Parameters = report.Parameters,
            Columns = report.Columns,
            Rows = report.Rows,
            Blocks = report.Blocks,
        });
    }

    private static UtteranceResponse NotReady(string reason) => new()
    {
        Status = "not_ready",
        Reason = reason,
        ReadCount = 0,
    };

    public static List<StyleRecord> Match(string utterance, IReadOnlyList<StyleRecord> styles)
    {
        var hits = styles
            .Where(style => style.Name.Length > 0 && utterance.Contains(style.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(style => style.Name.Length)
            .ToList();
        var kept = new List<StyleRecord>();
        foreach (var hit in hits)
        {
            var covered = kept.Any(longer => longer.Name.Contains(hit.Name, StringComparison.OrdinalIgnoreCase));
            if (!covered)
                kept.Add(hit);
        }

        return kept;
    }

    public static Dictionary<string, string> Parse(string utterance, IReadOnlyList<ViewParameterDto> parameters)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            var index = 0;
            while (index < utterance.Length)
            {
                var found = utterance.IndexOf(parameter.Name, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                    break;
                var cursor = found + parameter.Name.Length;
                while (cursor < utterance.Length && utterance[cursor] is ' ' or '\t')
                    cursor++;
                if (cursor >= utterance.Length || utterance[cursor] is not ('=' or ':' or '：'))
                {
                    index = found + parameter.Name.Length;
                    continue;
                }

                cursor++;
                while (cursor < utterance.Length && utterance[cursor] is ' ' or '\t')
                    cursor++;
                var end = cursor;
                while (end < utterance.Length && !char.IsWhiteSpace(utterance[end]))
                    end++;
                if (end > cursor)
                    values[parameter.Name] = utterance[cursor..end];
                break;
            }
        }

        return values;
    }
}

public sealed class RecordService
{
    private readonly Profiles _profiles;
    private readonly IRecordStore _records;
    private readonly IReportMemory _reports;
    private readonly IClock _clock;
    private readonly EnvironmentService _environment;

    public RecordService(Profiles profiles, IRecordStore records, IReportMemory reports, IClock clock, EnvironmentService environment)
    {
        _profiles = profiles;
        _records = records;
        _reports = reports;
        _clock = clock;
        _environment = environment;
    }

    public OpResult<IReadOnlyList<RecordDto>> List(string operatorId)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<IReadOnlyList<RecordDto>>.Fail(error);
        return OpResult<IReadOnlyList<RecordDto>>.Success(_records.ReadableBy(operatorId).Select(ToDto).ToList());
    }

    public OpResult<RecordDto> Open(string operatorId, string id)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<RecordDto>.Fail(error);
        var record = _records.Find(id);
        if (record is null || !CanRead(record, operatorId))
            return OpResult<RecordDto>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<RecordDto>.Success(ToDto(record));
    }

    public OpResult<RecordDto> Save(string operatorId, string sessionId, bool shared)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<RecordDto>.Fail(error);
        var report = _reports.Current(sessionId);
        if (report is null)
            return OpResult<RecordDto>.Fail(WorkbenchMessages.Text("forbidden", "沒有可以保存的報表。"));
        var record = new GenerationRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = operatorId,
            Shared = shared,
            Blocks = report.Blocks.Select(StyleService.CopyTemplate).ToList(),
            Columns = report.Columns.ToList(),
            Rows = report.Rows.Select(row => row.ToList()).ToList(),
            Parameters = new Dictionary<string, string>(report.Parameters, StringComparer.Ordinal),
            CreatedUtc = _clock.UtcNow,
        };
        _records.Add(record);
        return OpResult<RecordDto>.Success(ToDto(record));
    }

    public OpResult<RecordDto> SetShared(string operatorId, string id, bool shared)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<RecordDto>.Fail(error);
        if (!_records.TryUpdateShared(id, operatorId, shared))
            return OpResult<RecordDto>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<RecordDto>.Success(ToDto(_records.Find(id)!));
    }

    public OpResult<bool> Delete(string operatorId, string id)
    {
        if (Closed(operatorId) is { } error)
            return OpResult<bool>.Fail(error);
        return _records.Delete(id, operatorId)
            ? OpResult<bool>.Success(true)
            : OpResult<bool>.Fail(WorkbenchMessages.Forbidden());
    }

    private static bool CanRead(GenerationRecord record, string operatorId)
        => record.OwnerId == operatorId || record.Shared;

    private ErrorDto? Closed(string operatorId)
    {
        if (Deny(operatorId) is { } error)
            return error;
        return _environment.Block();
    }

    private ErrorDto? Deny(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        return profile.Has(EntryNames.Terminal) ? null : WorkbenchMessages.Forbidden();
    }

    public static RecordDto ToDto(GenerationRecord record) => new()
    {
        Id = record.Id,
        OwnerId = record.OwnerId,
        Shared = record.Shared,
        Blocks = record.Blocks.Select(StyleService.CopyTemplate).ToList(),
        Columns = record.Columns.ToList(),
        Rows = record.Rows.Select(row => row.ToList()).ToList(),
        Parameters = new Dictionary<string, string>(record.Parameters, StringComparer.Ordinal),
        CreatedUtc = record.CreatedUtc,
    };
}

public sealed class ExportService
{
    private readonly Profiles _profiles;
    private readonly IReportMemory _reports;
    private readonly IRecordStore _records;
    private readonly IReportPdf _pdf;
    private readonly EnvironmentService _environment;

    public ExportService(Profiles profiles, IReportMemory reports, IRecordStore records, IReportPdf pdf, EnvironmentService environment)
    {
        _profiles = profiles;
        _reports = reports;
        _records = records;
        _pdf = pdf;
        _environment = environment;
    }

    public OpResult<byte[]> Pdf(string operatorId, string sessionId, string? recordId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<byte[]>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.Has(EntryNames.Terminal))
            return OpResult<byte[]>.Fail(WorkbenchMessages.Forbidden());
        if (_environment.Block() is { } blocked)
            return OpResult<byte[]>.Fail(blocked);

        PrintReport print;
        if (!string.IsNullOrWhiteSpace(recordId))
        {
            var record = _records.Find(recordId);
            if (record is null || (record.OwnerId != operatorId && !record.Shared))
                return OpResult<byte[]>.Fail(WorkbenchMessages.Forbidden());
            print = Print(record.Blocks, record.Columns, record.Rows, record.Parameters);
        }
        else
        {
            var current = _reports.Current(sessionId);
            if (current is null)
                return OpResult<byte[]>.Fail(WorkbenchMessages.Text("forbidden", WorkbenchMessages.LeftUnsaved));
            print = Print(current.Blocks, current.Columns, current.Rows, current.Parameters);
        }

        return OpResult<byte[]>.Success(_pdf.Write(print));
    }

    public static PrintReport Print(
        IReadOnlyList<StyleBlockDto> blocks,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyDictionary<string, string> parameters)
    {
        var print = new PrintReport();
        foreach (var block in blocks.OrderBy(item => item.Order))
        {
            if (block.Type == BlockKinds.Table)
            {
                var headers = block.Columns.Count > 0 ? block.Columns.ToList() : columns.ToList();
                var indexes = headers.Select(header => ColumnIndex(columns, header)).ToList();
                print.Blocks.Add(new PrintBlock
                {
                    Type = BlockKinds.Table,
                    Columns = headers,
                    Rows = rows.Select(row => indexes.Select(index => index >= 0 && index < row.Count ? row[index] ?? "" : "").ToList()).ToList(),
                });
                continue;
            }

            var text = block.Type == BlockKinds.Parameter
                ? ParameterText(block, parameters)
                : block.Text ?? "";
            print.Blocks.Add(new PrintBlock { Type = block.Type, Text = text });
        }

        return print;
    }

    private static int ColumnIndex(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index], name, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static string ParameterText(StyleBlockDto block, IReadOnlyDictionary<string, string> parameters)
    {
        var name = block.Parameter ?? "";
        parameters.TryGetValue(name, out var value);
        var label = string.IsNullOrWhiteSpace(block.Label) ? name : block.Label;
        return string.IsNullOrEmpty(label) ? value ?? "" : label + " " + (value ?? "");
    }
}
