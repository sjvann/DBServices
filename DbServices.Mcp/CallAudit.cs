using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DbServices.Mcp;

/// <summary>
/// 把一次已進入調度器的呼叫追加成一行 JSONL。不讀 HTTP，也不參考 ASP.NET 或 MCP。
/// </summary>
public static class CallAudit
{
    private static readonly object Gate = new();

    public static TableDispatchResult Commit(
        string absolutePath,
        string caller,
        string connectionId,
        string tool,
        IReadOnlyList<KeyValuePair<string, object?>> arguments,
        TableDispatchResult outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(outcome);

        var line = Format(caller, connectionId, tool, arguments, outcome);
        if (!TryAppend(absolutePath, line))
            return WriteFailed(tool);

        return outcome;
    }

    private static TableDispatchResult WriteFailed(string tool) => tool switch
    {
        "fields" => TableDispatchResult.FieldsExecutionFailed(),
        "get" => TableDispatchResult.GetExecutionFailed(),
        "query" => TableDispatchResult.QueryExecutionFailed(),
        "count" => TableDispatchResult.CountExecutionFailed(),
        "exists" => TableDispatchResult.ExistsExecutionFailed(),
        _ => TableDispatchResult.ExecutionFailed(),
    };

    private static string Format(
        string caller,
        string connectionId,
        string tool,
        IReadOnlyList<KeyValuePair<string, object?>> arguments,
        TableDispatchResult outcome)
    {
        Describe(outcome, out var rowCount, out var value, out var error);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(
                "timestamp",
                DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
            writer.WriteString("caller", caller);
            writer.WriteString("connectionId", connectionId);
            writer.WriteString("tool", tool);
            writer.WritePropertyName("arguments");
            WriteArguments(writer, connectionId, arguments);
            if (rowCount is int count)
                writer.WriteNumber("rowCount", count);
            else
                writer.WriteNull("rowCount");

            writer.WritePropertyName("value");
            if (value is JsonElement element)
                writer.WriteRawValue(element.GetRawText());
            else
                writer.WriteNullValue();

            if (error is null)
                writer.WriteNull("error");
            else
                writer.WriteString("error", error);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Describe(
        TableDispatchResult outcome,
        out int? rowCount,
        out JsonElement? value,
        out string? error)
    {
        using var document = JsonDocument.Parse(outcome.Json);
        var root = document.RootElement;
        if (outcome.IsError)
        {
            rowCount = null;
            value = null;
            error = root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
            return;
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("rows", out var rows)
            && rows.ValueKind == JsonValueKind.Array)
        {
            rowCount = rows.GetArrayLength();
            value = null;
            error = null;
            return;
        }

        rowCount = null;
        value = root.Clone();
        error = null;
    }

    private static void WriteArguments(
        Utf8JsonWriter writer,
        string connectionId,
        IReadOnlyList<KeyValuePair<string, object?>> arguments)
    {
        writer.WriteStartObject();
        writer.WriteString("connectionId", connectionId);
        foreach (var (key, value) in arguments)
        {
            if (IsConnectionId(key) || IsSecret(key))
                continue;

            writer.WritePropertyName(key);
            WriteValue(writer, value);
        }

        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonElement element:
                WriteElement(writer, element);
                return;
            case bool flag:
                writer.WriteBooleanValue(flag);
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            default:
                JsonSerializer.Serialize(writer, value);
                return;
        }
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (IsSecret(property.Name))
                        continue;

                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteElement(writer, item);
                writer.WriteEndArray();
                return;
            default:
                writer.WriteRawValue(element.GetRawText());
                return;
        }
    }

    private static bool IsConnectionId(string name)
        => name.Equals("connectionId", StringComparison.OrdinalIgnoreCase);

    private static bool IsSecret(string name)
        => name.Equals("connectionString", StringComparison.OrdinalIgnoreCase)
            || name.Equals("connectString", StringComparison.OrdinalIgnoreCase);

    private static bool TryAppend(string absolutePath, string line)
    {
        var payload = Encoding.UTF8.GetBytes(line + "\n");
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using var stream = new FileStream(
                    absolutePath,
                    FileMode.OpenOrCreate,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                var prior = stream.Length;
                stream.Seek(prior, SeekOrigin.Begin);
                try
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
                catch
                {
                    stream.SetLength(prior);
                    throw;
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }
}
