using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public sealed class Profiles
{
    private readonly IDirectory _directory;

    public Profiles(IDirectory directory) => _directory = directory;

    public Profile? Find(string? operatorId)
    {
        if (string.IsNullOrEmpty(operatorId))
            return null;
        var record = _directory.FindOperatorById(operatorId);
        if (record is null)
            return null;

        return new Profile
        {
            Id = record.Id,
            DisplayName = record.DisplayName,
            IsAdmin = record.IsAdmin,
            WorkRoles = _directory.WorkRolesFor(record.Id).ToHashSet(StringComparer.Ordinal),
            Groups = _directory.GroupRolesFor(record.Id),
        };
    }
}

public sealed class SeedAdminService
{
    private readonly IDirectory _directory;
    private readonly IPasswordHasher _hasher;
    public bool Unavailable { get; private set; }

    public SeedAdminService(IDirectory directory, IPasswordHasher hasher)
    {
        _directory = directory;
        _hasher = hasher;
    }

    public void Ensure(string? displayName, string? password)
    {
        if (_directory.OperatorCount() > 0)
        {
            Unavailable = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrEmpty(password))
        {
            Unavailable = true;
            return;
        }

        _directory.AddOperator(new OperatorRecord
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
    private readonly IDirectory _directory;
    private readonly IPasswordHasher _hasher;
    private readonly ISessionStore _sessions;
    private readonly SeedAdminService _seed;

    public SessionService(IDirectory directory, IPasswordHasher hasher, ISessionStore sessions, SeedAdminService seed)
    {
        _directory = directory;
        _hasher = hasher;
        _sessions = sessions;
        _seed = seed;
    }

    public OpResult<SessionResponse> Login(string? displayName, string? secret)
    {
        if (_seed.Unavailable)
            return OpResult<SessionResponse>.Fail(WorkbenchMessages.Unavailable());

        var name = displayName?.Trim() ?? "";
        var record = _directory.FindOperatorByName(name);
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

public sealed class AccountService
{
    private readonly IDirectory _directory;
    private readonly IPasswordHasher _hasher;
    private readonly Profiles _profiles;

    public AccountService(IDirectory directory, IPasswordHasher hasher, Profiles profiles)
    {
        _directory = directory;
        _hasher = hasher;
        _profiles = profiles;
    }

    public OpResult<IReadOnlyList<OperatorDto>> List(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<IReadOnlyList<OperatorDto>>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsAdmin)
            return OpResult<IReadOnlyList<OperatorDto>>.Fail(WorkbenchMessages.Forbidden());

        IReadOnlyList<OperatorDto> rows = _directory.ListOperators()
            .Select(item => new OperatorDto { Id = item.Id, DisplayName = item.DisplayName })
            .ToList();
        return OpResult<IReadOnlyList<OperatorDto>>.Success(rows);
    }

    public OpResult<OperatorDto> Create(string operatorId, string? displayName, string? secret)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsAdmin)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.Forbidden());

        var name = displayName?.Trim() ?? "";
        if (name.Length == 0 || string.IsNullOrEmpty(secret))
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.BlankName());
        if (_directory.FindOperatorByName(name) is not null)
            return OpResult<OperatorDto>.Fail(WorkbenchMessages.DuplicateName());

        var record = new OperatorRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = name,
            PasswordVerifier = _hasher.Hash(secret),
            IsAdmin = false,
        };
        _directory.AddOperator(record);
        return OpResult<OperatorDto>.Success(new OperatorDto { Id = record.Id, DisplayName = record.DisplayName });
    }
}

public sealed class RoleRailService
{
    private readonly IDirectory _directory;
    private readonly ITemplateStore _templates;
    private readonly Profiles _profiles;

    public RoleRailService(IDirectory directory, ITemplateStore templates, Profiles profiles)
    {
        _directory = directory;
        _templates = templates;
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
        var planner = profile.HasWork(WorkRoles.Planner);
        var editor = profile.HasWork(WorkRoles.Editor);
        var terminal = profile.HasWork(WorkRoles.Terminal);
        var any = profile.IsAdmin || planner || editor || terminal;
        return new MeResponse
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            IsAdmin = profile.IsAdmin,
            Rail = new RailDto
            {
                LivePreview = planner,
                Templates = editor || terminal,
                Intent = terminal,
                Favorites = terminal,
                Preview = any && (profile.IsAdmin || planner || editor || terminal),
                Accounts = profile.IsAdmin,
                Assignments = profile.IsAdmin,
                Model = profile.IsAdmin,
                EmptyMessage = any ? null : WorkbenchMessages.NoWorkRole,
            },
        };
    }

    public OpResult<IReadOnlyList<GroupDto>> ListGroups(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return OpResult<IReadOnlyList<GroupDto>>.Fail(WorkbenchMessages.Unauthenticated());
        if (!profile.IsAdmin)
            return OpResult<IReadOnlyList<GroupDto>>.Fail(WorkbenchMessages.Forbidden());

        IReadOnlyList<GroupDto> rows = _directory.ListGroups()
            .Select(item => new GroupDto { Id = item.Id, Name = item.Name })
            .ToList();
        return OpResult<IReadOnlyList<GroupDto>>.Success(rows);
    }

    public OpResult<GroupDto> AddGroup(string operatorId, string? name)
    {
        var gate = RequireAdmin(operatorId);
        if (gate is not null)
            return OpResult<GroupDto>.Fail(gate);
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            return OpResult<GroupDto>.Fail(WorkbenchMessages.BlankName());
        var id = Guid.NewGuid().ToString("N");
        if (!_directory.TryAddGroup(id, trimmed, out var error))
            return OpResult<GroupDto>.Fail(error == "duplicate_name" ? WorkbenchMessages.DuplicateName() : WorkbenchMessages.BlankName());
        return OpResult<GroupDto>.Success(new GroupDto { Id = id, Name = trimmed });
    }

    public OpResult<GroupDto> RenameGroup(string operatorId, string groupId, string? name)
    {
        var gate = RequireAdmin(operatorId);
        if (gate is not null)
            return OpResult<GroupDto>.Fail(gate);
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            return OpResult<GroupDto>.Fail(WorkbenchMessages.BlankName());
        if (!_directory.TryRenameGroup(groupId, trimmed, out var error))
            return OpResult<GroupDto>.Fail(error == "duplicate_name" ? WorkbenchMessages.DuplicateName() : WorkbenchMessages.BlankName());
        return OpResult<GroupDto>.Success(new GroupDto { Id = groupId, Name = trimmed });
    }

    public OpResult<bool> DeleteGroup(string operatorId, string groupId)
    {
        var gate = RequireAdmin(operatorId);
        if (gate is not null)
            return OpResult<bool>.Fail(gate);
        if (_templates.GroupHasTemplates(groupId))
            return OpResult<bool>.Fail(WorkbenchMessages.GroupNotEmpty());
        if (!_directory.TryDeleteGroup(groupId, out _))
            return OpResult<bool>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<bool>.Success(true);
    }

    public OpResult<AssignmentDto> GetAssignments(string operatorId, string targetId)
    {
        var gate = RequireAdmin(operatorId);
        if (gate is not null)
            return OpResult<AssignmentDto>.Fail(gate);
        if (_directory.FindOperatorById(targetId) is null)
            return OpResult<AssignmentDto>.Fail(WorkbenchMessages.Forbidden());
        return OpResult<AssignmentDto>.Success(ReadAssignments(targetId));
    }

    public OpResult<AssignmentDto> SaveAssignments(string operatorId, string targetId, AssignmentDto request)
    {
        var gate = RequireAdmin(operatorId);
        if (gate is not null)
            return OpResult<AssignmentDto>.Fail(gate);
        var target = _directory.FindOperatorById(targetId);
        if (target is null || target.IsAdmin)
            return OpResult<AssignmentDto>.Fail(WorkbenchMessages.Forbidden());

        var work = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in request.WorkRoles)
        {
            if (role is WorkRoles.Planner or WorkRoles.Editor or WorkRoles.Terminal)
                work.Add(role);
        }

        var groups = new List<StoredGroupRole>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Groups)
        {
            if (string.IsNullOrWhiteSpace(item.Role))
                continue;
            if (item.Role is not (GroupRoles.Design or GroupRoles.View))
                return OpResult<AssignmentDto>.Fail(WorkbenchMessages.Forbidden());
            if (!seen.Add(item.GroupId))
                return OpResult<AssignmentDto>.Fail(WorkbenchMessages.DuplicateName());
            if (_directory.FindGroup(item.GroupId) is null)
                return OpResult<AssignmentDto>.Fail(WorkbenchMessages.Forbidden());
            groups.Add(new StoredGroupRole { GroupId = item.GroupId, Role = item.Role });
        }

        _directory.ReplaceAssignments(targetId, work, groups);
        return OpResult<AssignmentDto>.Success(ReadAssignments(targetId));
    }

    private AssignmentDto ReadAssignments(string targetId) => new()
    {
        WorkRoles = _directory.WorkRolesFor(targetId).OrderBy(item => item, StringComparer.Ordinal).ToList(),
        Groups = _directory.GroupRolesFor(targetId)
            .OrderBy(item => item.GroupId, StringComparer.Ordinal)
            .Select(item => new GroupRoleDto { GroupId = item.GroupId, Role = item.Role })
            .ToList(),
    };

    private ErrorDto? RequireAdmin(string operatorId)
    {
        var profile = _profiles.Find(operatorId);
        if (profile is null)
            return WorkbenchMessages.Unauthenticated();
        if (!profile.IsAdmin)
            return WorkbenchMessages.Forbidden();
        return null;
    }
}
