using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class McpSurface
{
    private readonly HttpClient _http;
    private readonly Uri? _endpoint;

    public McpSurface(HttpClient http, string? baseUrl)
    {
        _http = http;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _endpoint = new Uri(uri, "mcp");
    }

    public Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => CallAsync("tables", request, includeFilters: false, includeTable: false, cancellationToken);

    public Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => CallAsync("fields", request, includeFilters: false, includeTable: true, cancellationToken);

    public Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => CallAsync("query", request, includeFilters: true, includeTable: true, cancellationToken);

    public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => CallAsync("count", request, includeFilters: true, includeTable: true, cancellationToken);

    private async Task<SurfaceOutcome> CallAsync(string tool, SurfaceRequest request, bool includeFilters, bool includeTable, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CallerId) || request.CallerId == "anonymous")
            return Failed(tool, "unauthenticated", "尚未登入。");
        if (_endpoint is null)
            return Failed(tool, "connection_failed", "無法建立連線。");
        if (includeTable && string.IsNullOrWhiteSpace(request.Table))
            return Failed(tool, "invalid_arguments", "未提供資料表名稱。");

        var arguments = new Dictionary<string, object?>
        {
            ["connectionId"] = request.Connection,
        };
        if (includeTable)
            arguments["table"] = request.Table;
        if (includeFilters && request.Filters.Count > 0)
        {
            var filters = new Dictionary<string, object?>();
            foreach (var filter in request.Filters)
                filters[filter.Key] = filter.Value;
            arguments["filters"] = filters;
        }

        try
        {
            var options = new HttpClientTransportOptions
            {
                Endpoint = _endpoint,
                Name = "workbench",
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["X-DBServices-Caller"] = request.CallerId,
                },
            };
            await using var transport = new HttpClientTransport(options, _http, loggerFactory: null, ownsHttpClient: false);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var result = await client.CallToolAsync(tool, arguments, cancellationToken: cancellationToken);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
            if (result.IsError == true)
                return ReadError(tool, text);
            return tool switch
            {
                "tables" => ReadTables(text),
                "fields" => ReadFields(text),
                "count" => ReadCount(tool, text, request),
                _ => ReadRows(tool, text, request),
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or IOException)
        {
            return Failed(tool, "connection_failed", Scrub.Message(exception.Message));
        }
    }

    private static SurfaceOutcome ReadError(string tool, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Failed(tool, "connection_failed", "無法建立連線。");
            var code = document.RootElement.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String
                ? codeElement.GetString() ?? "connection_failed"
                : "connection_failed";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : "";
            return Failed(tool, code, Scrub.Message(message));
        }
        catch (JsonException)
        {
            return Failed(tool, "connection_failed", "無法建立連線。");
        }
    }

    private static SurfaceOutcome ReadTables(string text)
    {
        if (!TryRows(text, out var columns, out var rows, out var error))
            return error!;
        var nameIndex = Index(columns, "name");
        if (nameIndex < 0)
            return Failed("tables", "connection_failed", "無法建立連線。");
        var names = new List<string>();
        foreach (var row in rows)
        {
            if (nameIndex < row.Count)
                names.Add(Cell(row[nameIndex]));
        }

        return new SurfaceOutcome { Ok = true, Tool = "tables", Tables = names };
    }

    private static SurfaceOutcome ReadFields(string text)
    {
        if (!TryRows(text, out var columns, out var rows, out var error))
            return error!;
        var nameIndex = Index(columns, "name");
        var typeIndex = Index(columns, "type");
        var pkIndex = Index(columns, "pk");
        var nullIndex = Index(columns, "notNull");
        if (nameIndex < 0)
            return Failed("fields", "connection_failed", "無法建立連線。");

        var fields = new List<FieldDto>();
        foreach (var row in rows)
        {
            fields.Add(new FieldDto
            {
                Name = nameIndex < row.Count ? Cell(row[nameIndex]) : "",
                Type = typeIndex >= 0 && typeIndex < row.Count ? Cell(row[typeIndex]) : "",
                PrimaryKey = pkIndex >= 0 && pkIndex < row.Count && IsTrue(row[pkIndex]),
                NotNull = nullIndex >= 0 && nullIndex < row.Count && IsTrue(row[nullIndex]),
            });
        }

        return new SurfaceOutcome { Ok = true, Tool = "fields", Fields = fields };
    }

    private static SurfaceOutcome ReadRows(string tool, string text, SurfaceRequest request)
    {
        if (!TryRows(text, out var columns, out var rows, out var error))
            return error!;
        var shaped = new List<List<string>>();
        foreach (var row in rows)
        {
            var line = new List<string>();
            for (var index = 0; index < columns.Count; index++)
                line.Add(index < row.Count ? Cell(row[index]) : "");
            shaped.Add(line);
        }

        return new SurfaceOutcome
        {
            Ok = true,
            Tool = tool,
            Columns = columns,
            Rows = shaped,
            ParameterKeys = request.Filters.Select(filter => filter.Key).ToList(),
        };
    }

    private static SurfaceOutcome ReadCount(string tool, string text, SurfaceRequest request)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Number || !document.RootElement.TryGetDecimal(out var number))
                return Failed(tool, "connection_failed", "無法建立連線。");
            return new SurfaceOutcome
            {
                Ok = true,
                Tool = tool,
                Count = number,
                ParameterKeys = request.Filters.Select(filter => filter.Key).ToList(),
            };
        }
        catch (JsonException)
        {
            return Failed(tool, "connection_failed", "無法建立連線。");
        }
    }

    private static bool TryRows(string text, out List<string> columns, out List<List<JsonElement>> rows, out SurfaceOutcome? error)
    {
        columns = [];
        rows = [];
        error = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("columns", out var columnElement)
                || columnElement.ValueKind != JsonValueKind.Array
                || !document.RootElement.TryGetProperty("rows", out var rowElement)
                || rowElement.ValueKind != JsonValueKind.Array)
            {
                error = Failed("query", "connection_failed", "無法建立連線。");
                return false;
            }

            foreach (var column in columnElement.EnumerateArray())
            {
                if (column.ValueKind != JsonValueKind.String)
                {
                    error = Failed("query", "connection_failed", "無法建立連線。");
                    return false;
                }

                columns.Add(column.GetString() ?? "");
            }

            foreach (var row in rowElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                {
                    error = Failed("query", "connection_failed", "無法建立連線。");
                    return false;
                }

                var cells = new List<JsonElement>();
                foreach (var cell in row.EnumerateArray())
                    cells.Add(cell.Clone());
                rows.Add(cells);
            }

            return true;
        }
        catch (JsonException)
        {
            error = Failed("query", "connection_failed", "無法建立連線。");
            return false;
        }
    }

    private static int Index(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index], name, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    private static string Cell(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "",
        _ => "",
    };

    private static bool IsTrue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.True)
            return true;
        if (element.ValueKind == JsonValueKind.String)
            return string.Equals(element.GetString(), "true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static SurfaceOutcome Failed(string tool, string code, string message) => new()
    {
        Ok = false,
        Tool = tool,
        ErrorCode = code,
        ErrorMessage = message,
        ParameterKeys = [],
    };
}
