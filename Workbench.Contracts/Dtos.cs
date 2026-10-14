namespace Workbench.Contracts;

public sealed class ErrorDto
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class LoginRequest
{
    public string? DisplayName { get; set; }
    public string? Secret { get; set; }
}

public sealed class SessionResponse
{
    public string SessionId { get; set; } = "";
}

public sealed class RailDto
{
    public bool LivePreview { get; set; }
    public bool Templates { get; set; }
    public bool Intent { get; set; }
    public bool Favorites { get; set; }
    public bool Preview { get; set; }
    public bool Accounts { get; set; }
    public bool Assignments { get; set; }
    public bool Model { get; set; }
    public string? EmptyMessage { get; set; }
}

public sealed class MeResponse
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public RailDto Rail { get; set; } = new();
}

public sealed class OperatorWriteRequest
{
    public string? DisplayName { get; set; }
    public string? Secret { get; set; }
}

public sealed class OperatorDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class GroupWriteRequest
{
    public string? Name { get; set; }
}

public sealed class GroupDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class GroupRoleDto
{
    public string GroupId { get; set; } = "";
    public string? Role { get; set; }
}

public sealed class AssignmentDto
{
    public List<string> WorkRoles { get; set; } = [];
    public List<GroupRoleDto> Groups { get; set; } = [];
}

public sealed class ConnectionDto
{
    public string Name { get; set; } = "";
    public string ServerType { get; set; } = "";
}

public sealed class FieldDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool PrimaryKey { get; set; }
    public bool NotNull { get; set; }
}

public sealed class FilterDto
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

public sealed class PlanRequest
{
    public string? Surface { get; set; }
    public string? Connection { get; set; }
    public string? Table { get; set; }
    public List<FilterDto> Filters { get; set; } = [];
}

public sealed class TraceDto
{
    public string OperatorId { get; set; } = "";
    public string Connection { get; set; } = "";
    public string Tool { get; set; } = "";
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.Ordinal);
    public int? RowCount { get; set; }
    public decimal? Count { get; set; }
    public string? ErrorCode { get; set; }
    public bool? AuditMatched { get; set; }
    public DateTime AtUtc { get; set; }
}

public sealed class PlanResponse
{
    public string? PlanId { get; set; }
    public string? SnapshotId { get; set; }
    public List<string> Tools { get; set; } = [];
    public List<string> ParameterKeys { get; set; } = [];
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public decimal? Count { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? AuditMatched { get; set; }
    public List<TraceDto> Traces { get; set; } = [];
    public string? Notice { get; set; }
}

public sealed class ComponentDto
{
    public string Type { get; set; } = "";
    public int Order { get; set; }
    public string? Text { get; set; }
    public string? Field { get; set; }
    public string? Label { get; set; }
    public string? Table { get; set; }
    public List<string> Columns { get; set; } = [];
}

public sealed class TemplateDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ComponentDto> Components { get; set; } = [];
}

public sealed class SuggestionRequest
{
    public string? Surface { get; set; }
    public string? Connection { get; set; }
    public string? Table { get; set; }
    public string? TemplateId { get; set; }
    public List<ComponentDto>? Proposal { get; set; }
}

public sealed class IntentParseRequest
{
    public string? Utterance { get; set; }
    public string? Surface { get; set; }
}

public sealed class IntentCandidateDto
{
    public string? Connection { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}

public sealed class IntentParseResponse
{
    public string Status { get; set; } = IntentStatuses.Incomplete;
    public string? PlanId { get; set; }
    public List<IntentCandidateDto> Candidates { get; set; } = [];
    public string? ConfirmReason { get; set; }
    public bool CanConfirm { get; set; }
}

public sealed class ConfirmRequest
{
    public string? PlanId { get; set; }
}

public sealed class FavoriteWriteRequest
{
    public string? Name { get; set; }
    public string? TemplateId { get; set; }
    public string? Surface { get; set; }
    public string? ConnectionName { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}

public sealed class FavoriteDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string Surface { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}

public sealed class FavoriteOpenResponse
{
    public TemplateDto? Template { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    public string? Difference { get; set; }
    public string? ErrorCode { get; set; }
}

public sealed class KeywordWriteRequest
{
    public string? Phrase { get; set; }
    public string? TemplateId { get; set; }
    public List<string> ColumnNames { get; set; } = [];
    public string? SnapshotId { get; set; }
}

public sealed class ModelDto
{
    public bool Enabled { get; set; }
    public string Placement { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string ModelId { get; set; } = "";
    public bool ApiKeySet { get; set; }
}

public sealed class ModelWriteRequest
{
    public bool Enabled { get; set; }
    public string? Placement { get; set; }
    public string? Endpoint { get; set; }
    public string? ModelId { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class ProbeRequest
{
    public string? Placement { get; set; }
    public string? Endpoint { get; set; }
    public string? ModelId { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class ModelResultDto
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class SidecarProbeInput
{
    public string Kind { get; set; } = "probe";
    public string Placement { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string ModelId { get; set; } = "";
}

public sealed class SidecarConnectionInput
{
    public string Name { get; set; } = "";
    public string ServerType { get; set; } = "";
}

public sealed class SidecarTemplateInput
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Parameters { get; set; } = [];
    public List<string> Tables { get; set; } = [];
}

public sealed class SidecarParseInput
{
    public string Kind { get; set; } = "parse";
    public string Utterance { get; set; } = "";
    public List<SidecarConnectionInput> Connections { get; set; } = [];
    public List<SidecarTemplateInput> Templates { get; set; } = [];
    public List<string> Surfaces { get; set; } = [];
}
