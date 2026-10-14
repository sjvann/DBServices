using Workbench.Contracts;

namespace Workbench.Core.Ports;

public interface IPasswordHasher
{
    string Hash(string secret);
    bool Verify(string secret, string verifier);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class OperatorRecord
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordVerifier { get; set; } = "";
    public bool IsAdmin { get; set; }
}

public sealed class GroupRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class StoredGroupRole
{
    public string GroupId { get; set; } = "";
    public string Role { get; set; } = "";
}

public interface IDirectory
{
    int OperatorCount();
    void AddOperator(OperatorRecord record);
    OperatorRecord? FindOperatorByName(string name);
    OperatorRecord? FindOperatorById(string id);
    IReadOnlyList<OperatorRecord> ListOperators();
    bool TryAddGroup(string id, string name, out string? errorCode);
    bool TryRenameGroup(string id, string name, out string? errorCode);
    bool TryDeleteGroup(string id, out string? errorCode);
    IReadOnlyList<GroupRecord> ListGroups();
    GroupRecord? FindGroup(string id);
    void ReplaceAssignments(string operatorId, IReadOnlyCollection<string> workRoles, IReadOnlyList<StoredGroupRole> groups);
    IReadOnlyCollection<string> WorkRolesFor(string operatorId);
    IReadOnlyList<StoredGroupRole> GroupRolesFor(string operatorId);
}

public sealed class TemplateRecord
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ComponentDto> Components { get; set; } = [];
}

public interface ITemplateStore
{
    bool TrySave(TemplateRecord record, out string? errorCode);
    TemplateRecord? Find(string id);
    IReadOnlyList<TemplateRecord> List();
    bool Delete(string id);
    bool GroupHasTemplates(string groupId);
}

public sealed class FavoriteRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string Surface { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    public string OwnerId { get; set; } = "";
}

public interface IFavoriteStore
{
    bool TrySave(FavoriteRecord record, out string? errorCode);
    FavoriteRecord? Find(string id);
    IReadOnlyList<FavoriteRecord> ListByOwner(string ownerId);
}

public sealed class KeywordRecord
{
    public string Id { get; set; } = "";
    public string Phrase { get; set; } = "";
    public List<string> ColumnNames { get; set; } = [];
    public string Source { get; set; } = "";
    public string? TemplateId { get; set; }
    public string OwnerId { get; set; } = "";
}

public interface IKeywordStore
{
    void Add(KeywordRecord record);
    IReadOnlyList<KeywordRecord> List();
}

public interface ISessionStore
{
    string Create(string operatorId);
    string? OperatorFor(string sessionId);
    void Delete(string sessionId);
    void Clear();
}

public sealed class CatalogOutcome
{
    public List<ConnectionDto> Connections { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Ok => ErrorCode is null;
}

public interface IConnectionCatalog
{
    Task<CatalogOutcome> ListAsync(string surface, CancellationToken cancellationToken);
}

public sealed class ScalarFilter
{
    public string Key { get; init; } = "";
    public object? Value { get; init; }
}

public sealed class SurfaceRequest
{
    public string Surface { get; init; } = "";
    public string Connection { get; init; } = "";
    public string? Table { get; init; }
    public string CallerId { get; init; } = "";
    public bool IncludeViewOmitted { get; init; } = true;
    public IReadOnlyList<ScalarFilter> Filters { get; init; } = [];
}

public sealed class SurfaceOutcome
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; set; }
    public List<string> Tables { get; init; } = [];
    public List<FieldDto> Fields { get; init; } = [];
    public List<string> Columns { get; init; } = [];
    public List<List<string>> Rows { get; init; } = [];
    public decimal? Count { get; init; }
    public string Tool { get; init; } = "";
    public List<string> ParameterKeys { get; init; } = [];
    public List<TraceDto> Traces { get; set; } = [];
}

public interface IDataSurface
{
    Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken);
    Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken);
    Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken);
    Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken);
}

public sealed class AuditEntry
{
    public string Caller { get; init; } = "";
    public string ConnectionId { get; init; } = "";
    public string Tool { get; init; } = "";
    public string ArgumentsJson { get; init; } = "{}";
    public int? RowCount { get; init; }
    public string? ValueJson { get; init; }
    public string? Error { get; init; }
}

public sealed class AuditRead
{
    public List<AuditEntry> Entries { get; init; } = [];
}

public interface IAuditReader
{
    AuditRead ReadAll();
}

public sealed class FillCall
{
    public string Tool { get; set; } = "";
    public string Table { get; set; } = "";
    public List<ScalarFilter> Filters { get; set; } = [];
    public List<string> Columns { get; set; } = [];
}

public sealed class FillPlan
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public string Surface { get; set; } = "";
    public string Connection { get; set; } = "";
    public string? TemplateId { get; set; }
    public string RoleHash { get; set; } = "";
    public List<FillCall> Calls { get; set; } = [];
    public bool Used { get; set; }
}

public sealed class SnapshotBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public string? Table { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public decimal? Count { get; set; }
}

public sealed class Snapshot
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string Surface { get; set; } = "";
    public string Connection { get; set; } = "";
    public List<SnapshotBlock> Blocks { get; set; } = [];
    public List<TraceDto> Traces { get; set; } = [];
}

public interface IPreviewMemory
{
    void SavePlan(FillPlan plan);
    FillPlan? FindPlan(string planId);
    bool MarkUsed(string planId);
    void ReplaceSnapshot(Snapshot snapshot);
    Snapshot? Current(string sessionId);
    Snapshot? FindSnapshot(string sessionId, string snapshotId);
}

public sealed class SidecarRequest
{
    public string Kind { get; init; } = "";
    public string Placement { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public string ModelId { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string? ParseJson { get; init; }
}

public sealed class SidecarResponse
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Stdout { get; init; } = "";
    public bool Started { get; init; }
}

public interface IModelSidecar
{
    Task<SidecarResponse> InvokeAsync(SidecarRequest request, CancellationToken cancellationToken);
}

public interface ISnapshotPdfWriter
{
    byte[] Write(Snapshot snapshot);
}
