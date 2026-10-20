using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbServices.Mcp;

/// <summary>
/// 檢視文件。持久化在 McpService 內容根，不進工作台 SQLite。
/// </summary>
public sealed class ViewDocument
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool Published { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<ViewLink> Links { get; set; } = [];
    public List<ViewParameter> Parameters { get; set; } = [];
    public List<ViewColumn> Columns { get; set; } = [];
}

public sealed class ViewLink
{
    public string Kind { get; set; } = "";
    public string From { get; set; } = "";
    public string FromColumn { get; set; } = "";
    public string To { get; set; } = "";
    public string ToColumn { get; set; } = "";
}

public sealed class ViewParameter
{
    public string Name { get; set; } = "";
    public string Column { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ViewColumn
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ViewWriteResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public ViewDocument? Document { get; init; }

    public static ViewWriteResult Success(ViewDocument document) => new() { Ok = true, Document = document };

    public static ViewWriteResult Fail(string code, string message) => new() { Code = code, Message = message };
}

/// <summary>
/// 檢視文件的讀寫、發布與名稱唯一（不分大小寫）。
/// </summary>
public sealed class ViewCatalog
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
    private List<ViewDocument>? _documents;

    public ViewCatalog(string path) => _path = path;

    public IReadOnlyList<ViewDocument> List()
    {
        lock (_gate)
            return Load().Select(Clone).ToList();
    }

    public IReadOnlyList<ViewDocument> Published()
        => List().Where(document => document.Published && !string.IsNullOrWhiteSpace(document.Name)).ToList();

    public ViewDocument? Find(string id)
    {
        lock (_gate)
        {
            var found = Load().FirstOrDefault(document => string.Equals(document.Id, id, StringComparison.Ordinal));
            return found is null ? null : Clone(found);
        }
    }

    public ViewDocument? FindPublishedByName(string name)
    {
        lock (_gate)
        {
            var found = Load().FirstOrDefault(document =>
                document.Published && string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase));
            return found is null ? null : Clone(found);
        }
    }

    public ViewWriteResult Save(ViewDocument incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        lock (_gate)
        {
            var documents = Load();
            var check = Check(incoming, documents);
            if (check is not null)
                return check;

            var copy = Clone(incoming);
            var index = documents.FindIndex(document => string.Equals(document.Id, copy.Id, StringComparison.Ordinal));
            if (index < 0)
                documents.Add(copy);
            else
                documents[index] = copy;
            if (!TryFlush(documents))
            {
                Reload();
                return ViewWriteResult.Fail("execution_failed", "目錄沒有寫入。");
            }

            return ViewWriteResult.Success(Clone(copy));
        }
    }

    public ViewWriteResult Delete(string id)
    {
        lock (_gate)
        {
            var documents = Load();
            var index = documents.FindIndex(document => string.Equals(document.Id, id, StringComparison.Ordinal));
            if (index < 0)
                return ViewWriteResult.Fail("not_found", "找不到檢視。");

            documents.RemoveAt(index);
            if (!TryFlush(documents))
            {
                Reload();
                return ViewWriteResult.Fail("execution_failed", "目錄沒有寫入。");
            }

            return ViewWriteResult.Success(new ViewDocument { Id = id });
        }
    }

    public ViewWriteResult SetPublished(string id, bool published)
    {
        lock (_gate)
        {
            var documents = Load();
            var index = documents.FindIndex(document => string.Equals(document.Id, id, StringComparison.Ordinal));
            if (index < 0)
                return ViewWriteResult.Fail("not_found", "找不到檢視。");

            var copy = Clone(documents[index]);
            copy.Published = published;
            var check = Check(copy, documents);
            if (check is not null)
                return check;

            documents[index] = copy;
            if (!TryFlush(documents))
            {
                Reload();
                return ViewWriteResult.Fail("execution_failed", "目錄沒有寫入。");
            }

            return ViewWriteResult.Success(Clone(copy));
        }
    }

    public static ViewWriteResult? Check(ViewDocument incoming, IReadOnlyList<ViewDocument> existing)
    {
        var name = incoming.Name?.Trim() ?? "";
        if (incoming.Id is not { Length: > 0 } || name.Length == 0)
            return ViewWriteResult.Fail("blank_name", "名稱不可空白。");

        incoming.Name = name;
        if (existing.Any(document =>
                !string.Equals(document.Id, incoming.Id, StringComparison.Ordinal)
                && string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase)))
            return ViewWriteResult.Fail("duplicate_name", "名稱已存在。");

        var sources = (incoming.Sources ?? []).Select(source => source?.Trim() ?? "").Where(source => source.Length > 0).ToList();
        if (sources.Count == 0 || sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Count)
            return ViewWriteResult.Fail("disconnected", "來源連不起來。");

        incoming.Sources = sources;
        var sourceSet = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        foreach (var link in incoming.Links ?? [])
        {
            if (link.Kind is not ("foreign-key" or "accepted"))
                return ViewWriteResult.Fail("disconnected", "來源連不起來。");
            if (!sourceSet.Contains(link.From ?? "") || !sourceSet.Contains(link.To ?? ""))
                return ViewWriteResult.Fail("disconnected", "來源連不起來。");
            if (string.IsNullOrWhiteSpace(link.FromColumn) || string.IsNullOrWhiteSpace(link.ToColumn))
                return ViewWriteResult.Fail("disconnected", "來源連不起來。");
        }

        if (sources.Count > 1 && !Connected(sources, incoming.Links ?? []))
            return ViewWriteResult.Fail("disconnected", "來源連不起來。");

        return null;
    }

    private static bool Connected(IReadOnlyList<string> sources, IReadOnlyList<ViewLink> links)
    {
        var adjacent = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
            adjacent[source] = [];
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
            if (!adjacent.TryGetValue(node, out var next))
                continue;
            foreach (var item in next)
                pending.Push(item);
        }

        return seen.Count == sources.Count;
    }

    private List<ViewDocument> Load()
    {
        if (_documents is not null)
            return _documents;

        if (!File.Exists(_path))
        {
            _documents = [];
            return _documents;
        }

        try
        {
            var text = File.ReadAllText(_path);
            _documents = JsonSerializer.Deserialize<List<ViewDocument>>(text, Json) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            _documents = [];
        }

        return _documents;
    }

    private void Reload() => _documents = null;

    private bool TryFlush(List<ViewDocument> documents)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(documents, Json));
            File.Move(temp, _path, overwrite: true);
            _documents = documents;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ViewDocument Clone(ViewDocument document)
        => JsonSerializer.Deserialize<ViewDocument>(JsonSerializer.Serialize(document, Json), Json)!;
}
