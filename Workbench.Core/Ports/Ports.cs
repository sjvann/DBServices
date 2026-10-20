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

public interface IOperatorStore
{
    int Count();
    void Add(OperatorRecord record);
    OperatorRecord? FindByName(string name);
    OperatorRecord? FindById(string id);
    IReadOnlyList<OperatorRecord> List();
    void ReplaceEntries(string operatorId, IReadOnlyList<string> entries);
    IReadOnlyList<string> EntriesFor(string operatorId);
}

public sealed class StyleRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ViewId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public bool Shared { get; set; }
    public List<StyleBlockDto> Blocks { get; set; } = [];
}

public interface IStyleStore
{
    void Add(StyleRecord record);
    bool TryUpdate(StyleRecord record);
    StyleRecord? Find(string id);
    IReadOnlyList<StyleRecord> ReadableBy(string operatorId);
    bool References(string viewId);
    bool Delete(string id, string ownerId);
}

public sealed class GenerationRecord
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

public interface IRecordStore
{
    void Add(GenerationRecord record);
    GenerationRecord? Find(string id);
    IReadOnlyList<GenerationRecord> ReadableBy(string operatorId);
    bool TryUpdateShared(string id, string ownerId, bool shared);
    bool Delete(string id, string ownerId);
}

public interface ISessionStore
{
    string Create(string operatorId);
    string? OperatorFor(string sessionId);
    void Delete(string sessionId);
    void Clear();
}

public sealed class CatalogWriteResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public ViewDocumentDto? Document { get; init; }

    public static CatalogWriteResult Success(ViewDocumentDto? document) => new() { Ok = true, Document = document };

    public static CatalogWriteResult Fail(string code, string message) => new() { Code = code, Message = message };
}

public interface IViewCatalog
{
    Task<IReadOnlyList<ViewDocumentDto>> ListAsync(string callerId, CancellationToken cancellationToken);
    Task<ViewDocumentDto?> FindAsync(string id, string callerId, CancellationToken cancellationToken);
    Task<CatalogWriteResult> SaveAsync(ViewDocumentDto document, string callerId, CancellationToken cancellationToken);
    Task<CatalogWriteResult> DeleteAsync(string id, string callerId, CancellationToken cancellationToken);
    Task<CatalogWriteResult> SetPublishedAsync(string id, bool published, string callerId, CancellationToken cancellationToken);
}

public sealed class ConnectionTally
{
    public int Available { get; init; }
    public string? OnlyName { get; init; }
}

public interface IConnectionCount
{
    Task<ConnectionTally> TallyAsync(CancellationToken cancellationToken);
}

public sealed class ViewCallOutcome
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public List<string> Columns { get; init; } = [];
    public List<List<string>> Rows { get; init; } = [];
}

public interface IViewCaller
{
    Task<ViewCallOutcome> CallAsync(
        string viewName,
        string connectionId,
        IReadOnlyDictionary<string, string> parameters,
        string callerId,
        CancellationToken cancellationToken);
}

public sealed class ReportDocument
{
    public string Id { get; set; } = "";
    public string StyleId { get; set; } = "";
    public string ViewId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<StyleBlockDto> Blocks { get; set; } = [];
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);
}

public interface IReportMemory
{
    void Save(string sessionId, ReportDocument report);
    ReportDocument? Current(string sessionId);
    void Clear(string sessionId);
}

public sealed class PrintBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
}

public sealed class PrintReport
{
    public List<PrintBlock> Blocks { get; set; } = [];
}

public interface IReportPdf
{
    byte[] Write(PrintReport report);
}
