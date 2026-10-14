using System.Text.Json;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class McpAuditReader : IAuditReader
{
    private readonly string _path;

    public McpAuditReader(string path) => _path = path;

    public AuditRead ReadAll()
    {
        var entries = new List<AuditEntry>();
        if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
            return new AuditRead { Entries = entries };

        string[] lines;
        try
        {
            lines = File.ReadAllLines(_path);
        }
        catch (IOException)
        {
            return new AuditRead { Entries = entries };
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    continue;
                entries.Add(new AuditEntry
                {
                    Caller = ReadString(root, "caller"),
                    ConnectionId = ReadString(root, "connectionId"),
                    Tool = ReadString(root, "tool"),
                    ArgumentsJson = root.TryGetProperty("arguments", out var arguments) ? arguments.GetRawText() : "{}",
                    RowCount = root.TryGetProperty("rowCount", out var rowCount) && rowCount.ValueKind == JsonValueKind.Number && rowCount.TryGetInt32(out var count)
                        ? count
                        : null,
                    ValueJson = root.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null ? value.GetRawText() : null,
                    Error = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() : null,
                });
            }
            catch (JsonException)
            {
            }
        }

        return new AuditRead { Entries = entries };
    }

    private static string ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
}
