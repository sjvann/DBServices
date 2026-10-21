namespace DbServices.Mcp;

/// <summary>
/// 六個唯讀工具與具名連線快照。不建立資料庫工作階段，也不建立關閉名稱的處理函式。
/// </summary>
public sealed class ToolCatalog
{
    private static readonly HashSet<string> KnownServerTypes = new(StringComparer.Ordinal)
    {
        "Sqlite",
        "SqlServer",
        "MySQL",
        "Oracle",
        "PostgreSQL",
    };

    public static bool KnownServerType(string? serverType) => KnownServerTypes.Contains(serverType ?? "");

    public static readonly IReadOnlyList<ToolDefinition> Definitions =
    [
        new("tables", "列出資料表"),
        new("fields", "列出欄位"),
        new("get", "依 Id 取一列"),
        new("query", "鍵值查詢"),
        new("count", "計數"),
        new("exists", "是否存在"),
    ];

    public ToolCatalog(IReadOnlyDictionary<string, ConnectionSetting> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var snapshots = new Dictionary<string, ConnectionSnapshot>(connections.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (connectionId, setting) in connections)
            snapshots[connectionId] = ToSnapshot(setting);

        Connections = snapshots;
        Tools = Definitions;
    }

    public IReadOnlyList<ToolDefinition> Tools { get; }

    public IReadOnlyDictionary<string, ConnectionSnapshot> Connections { get; }

    private static ConnectionSnapshot ToSnapshot(ConnectionSetting? setting)
    {
        var serverType = setting?.ServerType ?? "";
        var connectionString = setting?.ConnectionString ?? "";
        var available = KnownServerTypes.Contains(serverType)
            && !string.IsNullOrWhiteSpace(connectionString);
        return new ConnectionSnapshot(serverType, connectionString, available);
    }
}

/// <summary>
/// 組態裡的一筆連線。只有伺服器類型與連線字串。
/// </summary>
public sealed class ConnectionSetting
{
    public string? ServerType { get; set; }

    public string? ConnectionString { get; set; }
}

/// <summary>
/// 啟動時對一筆連線的判斷。未知引擎或空白連線字串為不可用。
/// </summary>
public sealed class ConnectionSnapshot
{
    public ConnectionSnapshot(string serverType, string connectionString, bool available)
    {
        ServerType = serverType;
        ConnectionString = connectionString;
        Available = available;
    }

    public string ServerType { get; }

    public string ConnectionString { get; }

    public bool Available { get; }
}

/// <summary>
/// 廣告出去的工具。皆唯讀、非破壞性，說明不含 SQL。
/// </summary>
public sealed class ToolDefinition
{
    public ToolDefinition(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string Description { get; }

    public bool ReadOnly { get; } = true;

    public bool NonDestructive { get; } = true;
}
