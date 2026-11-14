using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbServices.Mcp;

public sealed class AnnotationPackage
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Active { get; set; }
    public List<AnnotationTable> Tables { get; set; } = [];
    public List<AnnotationLink> Links { get; set; } = [];
    public string Dbml { get; set; } = "";
}

public sealed class AnnotationTable
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public List<AnnotationColumn> Columns { get; set; } = [];
    public List<AnnotationIndex> Indexes { get; set; } = [];
}

public sealed class AnnotationColumn
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool NotNull { get; set; }
    public string Note { get; set; } = "";
    public string HName { get; set; } = "";
    public string SName { get; set; } = "";
    public string HType { get; set; } = "";
    public string SType { get; set; } = "";
    public string SLength { get; set; } = "";
    public string Pii { get; set; } = "";
    public bool UniqueIndex { get; set; }
    public string SourceFile { get; set; } = "";
    public string HOrdinal { get; set; } = "";
    public string SOrdinal { get; set; } = "";
}

public sealed class AnnotationIndex
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> Columns { get; set; } = [];
}

public sealed class AnnotationLink
{
    public string From { get; set; } = "";
    public string FromColumn { get; set; } = "";
    public string To { get; set; } = "";
    public string ToColumn { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class AnnotationReadException : Exception;

public sealed class AnnotationWriteResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public AnnotationPackage? Package { get; init; }

    public static AnnotationWriteResult Success(AnnotationPackage package) => new() { Ok = true, Package = package };

    public static AnnotationWriteResult Fail(string code, string message) => new() { Code = code, Message = message };
}

/// <summary>
/// 註記包的唯一寫入者。檔在內容根，不寫進檢視檔。
/// </summary>
public sealed class AnnotationCatalog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly object _gate = new();
    private List<AnnotationPackage>? _packages;

    public AnnotationCatalog(string path) => _path = path;

    public AnnotationPackage? Draft()
    {
        lock (_gate)
        {
            var found = Load().FirstOrDefault(package => string.Equals(package.Status, "draft", StringComparison.Ordinal));
            return found is null ? null : Clone(found);
        }
    }

    public AnnotationWriteResult Import(AnnotationPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        lock (_gate)
        {
            var packages = Load();
            if (packages.Any(package => string.Equals(package.Status, "draft", StringComparison.Ordinal)))
                return AnnotationWriteResult.Fail("draft_exists", "已有工作中的草稿，沒有改。");

            var copy = Clone(incoming);
            copy.Status = "draft";
            copy.Active = false;
            if (string.IsNullOrWhiteSpace(copy.Id))
                copy.Id = Guid.NewGuid().ToString("N");
            copy.Purpose ??= "";
            copy.Dbml ??= "";
            copy.Tables ??= [];
            copy.Links ??= [];
            packages.Add(copy);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(copy));
        }
    }

    private List<AnnotationPackage> Load()
    {
        if (_packages is not null)
            return _packages;

        if (!File.Exists(_path))
        {
            _packages = [];
            return _packages;
        }

        try
        {
            var text = File.ReadAllText(_path);
            _packages = JsonSerializer.Deserialize<List<AnnotationPackage>>(text, Json) ?? [];
            return _packages;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new AnnotationReadException();
        }
    }

    private void Reload() => _packages = null;

    private bool TryFlush(List<AnnotationPackage> packages)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(packages, Json));
            File.Move(temp, _path, overwrite: true);
            _packages = packages;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static AnnotationPackage Clone(AnnotationPackage package)
        => JsonSerializer.Deserialize<AnnotationPackage>(JsonSerializer.Serialize(package, Json), Json)!;
}
