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
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool Published { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<ViewLinkDto> Links { get; set; } = [];
    public List<ViewParameterDto> Parameters { get; set; } = [];
    public List<ViewColumnDto> Columns { get; set; } = [];
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
}
