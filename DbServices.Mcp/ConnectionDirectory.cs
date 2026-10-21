using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbServices.Mcp;

/// <summary>
/// 具名連線的有效集合。組態是起點，覆寫檔只留工作台寫入的連線與移除名單。
/// </summary>
public sealed class ConnectionDirectory
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly Dictionary<string, ConnectionSetting> _configured;
    private List<StoredConnection> _saved = [];
    private HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);
    private ToolCatalog _catalog;

    public ConnectionDirectory(IReadOnlyDictionary<string, ConnectionSetting> configured, string overlayPath)
    {
        ArgumentNullException.ThrowIfNull(configured);
        _path = overlayPath;
        _configured = new Dictionary<string, ConnectionSetting>(configured, StringComparer.OrdinalIgnoreCase);
        Load();
        _catalog = new ToolCatalog(Effective());
    }

    public ToolCatalog Catalog
    {
        get
        {
            lock (_gate)
                return _catalog;
        }
    }

    public ConnectionChangeResult Save(string? name, string? serverType, string? connectionString)
    {
        var key = name?.Trim() ?? "";
        if (!ValidName(key))
            return ConnectionChangeResult.Fail("invalid_arguments", "連線名稱只能使用英數、減號與底線。");
        var engine = serverType?.Trim() ?? "";
        if (!ToolCatalog.KnownServerType(engine))
            return ConnectionChangeResult.Fail("invalid_arguments", "不支援這個資料庫種類。");
        if (string.IsNullOrWhiteSpace(connectionString))
            return ConnectionChangeResult.Fail("invalid_arguments", "連線內容不完整。");

        lock (_gate)
        {
            var previousSaved = _saved.ToList();
            var previousRemoved = new HashSet<string>(_removed, StringComparer.OrdinalIgnoreCase);
            _removed.Remove(key);
            var index = _saved.FindIndex(item => string.Equals(item.Name, key, StringComparison.OrdinalIgnoreCase));
            var stored = new StoredConnection
            {
                Name = key,
                ServerType = engine,
                ConnectionString = connectionString,
            };
            if (index < 0)
                _saved.Add(stored);
            else
                _saved[index] = stored;
            if (!Persist())
            {
                _saved = previousSaved;
                _removed = previousRemoved;
                return ConnectionChangeResult.Fail("execution_failed", "無法儲存連線。");
            }

            _catalog = new ToolCatalog(Effective());
            return ConnectionChangeResult.Success(key, engine);
        }
    }

    public ConnectionChangeResult Remove(string? name)
    {
        var key = name?.Trim() ?? "";
        if (!ValidName(key))
            return ConnectionChangeResult.Fail("invalid_arguments", "連線名稱只能使用英數、減號與底線。");

        lock (_gate)
        {
            var effective = Effective();
            if (!effective.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)))
                return ConnectionChangeResult.Fail("not_found", "找不到連線。");
            var previousSaved = _saved.ToList();
            var previousRemoved = new HashSet<string>(_removed, StringComparer.OrdinalIgnoreCase);
            _saved.RemoveAll(item => string.Equals(item.Name, key, StringComparison.OrdinalIgnoreCase));
            _removed.Add(key);
            if (!Persist())
            {
                _saved = previousSaved;
                _removed = previousRemoved;
                return ConnectionChangeResult.Fail("execution_failed", "無法儲存連線。");
            }
            _catalog = new ToolCatalog(Effective());
            return ConnectionChangeResult.Success(key, "");
        }
    }

    private Dictionary<string, ConnectionSetting> Effective()
    {
        var map = new Dictionary<string, ConnectionSetting>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _configured)
        {
            if (_removed.Contains(pair.Key) || pair.Value is null)
                continue;
            map[pair.Key] = pair.Value;
        }

        foreach (var saved in _saved)
        {
            if (_removed.Contains(saved.Name))
                continue;
            map[saved.Name] = new ConnectionSetting
            {
                ServerType = saved.ServerType,
                ConnectionString = saved.ConnectionString,
            };
        }

        return map;
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;
        try
        {
            var overlay = JsonSerializer.Deserialize<OverlayFile>(File.ReadAllText(_path), Json);
            _saved = overlay?.Saved ?? [];
            _removed = new HashSet<string>(overlay?.Removed ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            _saved = [];
            _removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var payload = JsonSerializer.Serialize(new OverlayFile
            {
                Saved = _saved,
                Removed = _removed.ToList(),
            }, Json);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, payload);
            File.Move(temporary, _path, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ValidName(string name)
    {
        if (name.Length is 0 or > 64)
            return false;
        foreach (var character in name)
        {
            if (character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_')
                continue;
            return false;
        }

        return true;
    }

    private sealed class OverlayFile
    {
        public List<StoredConnection> Saved { get; set; } = [];
        public List<string> Removed { get; set; } = [];
    }

    private sealed class StoredConnection
    {
        public string Name { get; set; } = "";
        public string ServerType { get; set; } = "";
        public string ConnectionString { get; set; } = "";
    }
}

public sealed class ConnectionInput
{
    public string? Name { get; set; }
    public string? ServerType { get; set; }
    public string? ConnectionString { get; set; }
}

public sealed class ConnectionChangeResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Name { get; init; } = "";
    public string ServerType { get; init; } = "";

    public static ConnectionChangeResult Success(string name, string serverType) => new() { Ok = true, Name = name, ServerType = serverType };
    public static ConnectionChangeResult Fail(string code, string message) => new() { Code = code, Message = message };
}
