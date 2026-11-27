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

public sealed class MeResponse
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public List<string> Entries { get; set; } = [];
    public string Destination { get; set; } = "/";
}

public sealed class OperatorWriteRequest
{
    public string? DisplayName { get; set; }
    public string? Secret { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class OperatorDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public List<string> Entries { get; set; } = [];
}

public sealed class EntryAssignmentRequest
{
    public List<string> Entries { get; set; } = [];
}

public sealed class ViewLinkDto
{
    public string Kind { get; set; } = "";
    public string From { get; set; } = "";
    public string FromColumn { get; set; } = "";
    public string To { get; set; } = "";
    public string ToColumn { get; set; } = "";
    public string Join { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool ViewOnly { get; set; }
}

public sealed class ViewFilterDto
{
    public string Combiner { get; set; } = "and";
    public string Source { get; set; } = "";
    public string Column { get; set; } = "";
    public string Operator { get; set; } = "=";
    public string Value { get; set; } = "";
    public string Parameter { get; set; } = "";
}

public sealed class ViewGroupDto
{
    public string Source { get; set; } = "";
    public string Column { get; set; } = "";
}

public sealed class ViewAggregateDto
{
    public string Function { get; set; } = "";
    public string Source { get; set; } = "";
    public string Column { get; set; } = "";
    public string Alias { get; set; } = "";
}

public sealed class ViewOrderDto
{
    public string Source { get; set; } = "";
    public string Column { get; set; } = "";
    public string Direction { get; set; } = "asc";
}

public sealed class ViewPlacementDto
{
    public string Table { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class ViewParameterDto
{
    public string Name { get; set; } = "";
    public string Column { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ViewColumnDto
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ViewDocumentDto
{
    public string Id { get; set; } = "";
    public string AnnotationId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool Confirmed { get; set; }
    public bool Published { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<ViewLinkDto> Links { get; set; } = [];
    public List<ViewParameterDto> Parameters { get; set; } = [];
    public List<ViewColumnDto> Columns { get; set; } = [];
    public int? Limit { get; set; }
    public bool Distinct { get; set; }
    public List<ViewFilterDto> Filters { get; set; } = [];
    public List<ViewGroupDto> GroupBy { get; set; } = [];
    public List<ViewAggregateDto> Aggregates { get; set; } = [];
    public List<ViewFilterDto> Having { get; set; } = [];
    public List<ViewOrderDto> OrderBy { get; set; } = [];
    public string Sql { get; set; } = "";
    public bool SqlValidated { get; set; }
    public List<ViewPlacementDto> Placements { get; set; } = [];
}

public sealed class ViewSummaryDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool Published { get; set; }
}

public sealed class StyleBlockDto
{
    public string Type { get; set; } = "";
    public int Order { get; set; }
    public string? Text { get; set; }
    public string? Parameter { get; set; }
    public string? Label { get; set; }
    public List<string> Columns { get; set; } = [];
}

public sealed class StyleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ViewId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public bool Shared { get; set; }
    public List<StyleBlockDto> Blocks { get; set; } = [];
}

public sealed class StyleWriteRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ViewId { get; set; }
    public bool Shared { get; set; }
    public List<StyleBlockDto> Blocks { get; set; } = [];
}

public sealed class SuggestRequest
{
    public string? ViewId { get; set; }
}

public sealed class ViewProposalRequest
{
    public string Ask { get; set; } = "";
    public string Grain { get; set; } = "";
    public string Wanted { get; set; } = "";
    public string Filter { get; set; } = "";
    public string Limit { get; set; } = "";
    public AnnotationPackageDto? Package { get; set; }
    public List<string> Scope { get; set; } = [];
}

public sealed class ViewProposalDto
{
    public List<string> Sources { get; set; } = [];
    public List<ViewLinkDto> Links { get; set; } = [];
    public List<ViewColumnDto> Columns { get; set; } = [];
    public List<ViewFilterDto> Filters { get; set; } = [];
    public int? Limit { get; set; }
    public Dictionary<string, string> Reasons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class IgnoreStyleRequest
{
    public string? StyleId { get; set; }
}

public sealed class MoveBlockRequest
{
    public int Index { get; set; }
    public int Direction { get; set; }
}

public sealed class UtteranceRequest
{
    public string? Text { get; set; }
    public string? StyleId { get; set; }
}

public sealed class UtteranceResponse
{
    public string Status { get; set; } = "";
    public string Reason { get; set; } = "";
    public int ReadCount { get; set; }
    public string? StyleId { get; set; }
    public string? ReportId { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public List<StyleBlockDto> Blocks { get; set; } = [];
}

public sealed class RecordWriteRequest
{
    public bool Shared { get; set; }
}

public sealed class RecordDto
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public bool Shared { get; set; }
    public List<StyleBlockDto> Blocks { get; set; } = [];
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
    public DateTime CreatedUtc { get; set; }
}

public sealed class LayoutWriteRequest
{
    public string? Name { get; set; }
    public bool Shared { get; set; }
}

public sealed class SchemaColumnDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool PrimaryKey { get; set; }
    public string? ReferencesTable { get; set; }
    public string? ReferencesColumn { get; set; }
}

public sealed class SchemaTableDto
{
    public string Name { get; set; } = "";
    public List<SchemaColumnDto> Columns { get; set; } = [];
}

public sealed class SchemaGraphDto
{
    public List<SchemaTableDto> Tables { get; set; } = [];
}

public sealed class SchemaPreviewDto
{
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public bool Truncated { get; set; }
    public string Sql { get; set; } = "";
}

public sealed class AnnotationColumnDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool NotNull { get; set; }
    public string Note { get; set; } = "";
    public string NoteOrigin { get; set; } = "";
    public string NoteSource { get; set; } = "";
    public string HName { get; set; } = "";
    public string SName { get; set; } = "";
    public string HType { get; set; } = "";
    public string SType { get; set; } = "";
    public string SLength { get; set; } = "";
    public string Pii { get; set; } = "";
    public bool PrimaryKey { get; set; }
    public bool UniqueIndex { get; set; }
    public string SourceFile { get; set; } = "";
    public string HOrdinal { get; set; } = "";
    public string SOrdinal { get; set; } = "";
}

public sealed class AnnotationIndexDto
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> Columns { get; set; } = [];
}

public sealed class AnnotationTableDto
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string NoteOrigin { get; set; } = "";
    public string NoteSource { get; set; } = "";
    public List<AnnotationColumnDto> Columns { get; set; } = [];
    public List<AnnotationIndexDto> Indexes { get; set; } = [];
}

public sealed class AnnotationLinkDto
{
    public string From { get; set; } = "";
    public string FromColumn { get; set; } = "";
    public string To { get; set; } = "";
    public string ToColumn { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class AnnotationChangeDto
{
    public string Kind { get; set; } = "";
    public string Table { get; set; } = "";
    public string Column { get; set; } = "";
}

public sealed class AnnotationPackageDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Active { get; set; }
    public List<AnnotationTableDto> Tables { get; set; } = [];
    public List<AnnotationLinkDto> Links { get; set; } = [];
    public string Dbml { get; set; } = "";
    public List<AnnotationChangeDto> Changes { get; set; } = [];
    public string SourceKind { get; set; } = "";
    public string SourceKey { get; set; } = "";
}
