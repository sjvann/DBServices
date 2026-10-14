using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

public sealed class WebSurface
{
    private readonly HttpClient _http;
    private readonly Uri? _base;

    public WebSurface(HttpClient http, string? baseUrl)
    {
        _http = http;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            _base = uri;
    }

    public async Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        using var document = await GetAsync("meta", cancellationToken);
        if (document.Error is not null)
            return document.Error;
        if (document.Root.ValueKind != JsonValueKind.Array)
            return Failed("tables", "connection_failed", "無法建立連線。");
        var names = new List<string>();
        foreach (var item in document.Root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return Failed("tables", "connection_failed", "無法建立連線。");
            names.Add(item.GetString() ?? "");
        }

        return new SurfaceOutcome { Ok = true, Tool = "tables", Tables = names };
    }

    public async Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Table))
            return Failed("fields", "invalid_arguments", "未提供資料表名稱。");
        using var document = await GetAsync(Uri.EscapeDataString(request.Table) + "/fieldSet", cancellationToken);
        if (document.Error is not null)
            return document.Error;
        if (document.Root.ValueKind != JsonValueKind.Object
            || !document.Root.TryGetProperty("FieldDefine", out var fields)
            || fields.ValueKind != JsonValueKind.Array)
            return Failed("fields", "connection_failed", "無法建立連線。");

        var list = new List<FieldDto>();
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object)
                return Failed("fields", "connection_failed", "無法建立連線。");
            list.Add(new FieldDto
            {
                Name = StringOf(field, "FieldName"),
                Type = StringOf(field, "FieldType"),
                PrimaryKey = Flag(field, "IsPK"),
                NotNull = Flag(field, "IsNotNull"),
            });
        }

        return new SurfaceOutcome { Ok = true, Tool = "fields", Fields = list };
    }

    public async Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Table))
            return Failed("query", "invalid_arguments", "未提供資料表名稱。");
        if (request.Filters.Count > 1)
            return Failed("query", "single_key_only", "此表面只接受單鍵");

        var path = Uri.EscapeDataString(request.Table);
        if (request.Filters.Count == 1)
        {
            var filter = request.Filters[0];
            path += "?Key=" + Uri.EscapeDataString(filter.Key) + "&Value=" + Uri.EscapeDataString(filter.Value?.ToString() ?? "");
        }

        using var document = await GetAsync(path, cancellationToken);
        if (document.NotFound)
            return new SurfaceOutcome { Ok = true, Tool = "query", Rows = [], Columns = [], ParameterKeys = request.Filters.Select(item => item.Key).ToList() };
        if (document.Error is not null)
            return document.Error;
        if (document.Root.ValueKind != JsonValueKind.Object)
            return Failed("query", "connection_failed", "無法建立連線。");

        var columns = new List<string>();
        var rows = new List<List<string>>();
        if (document.Root.TryGetProperty("RecordData", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var record in records.EnumerateArray())
            {
                if (record.ValueKind != JsonValueKind.Object)
                    continue;
                var row = new List<string>();
                if (columns.Count == 0)
                {
                    foreach (var property in record.EnumerateObject())
                    {
                        if (property.NameEquals("ConnectString"))
                            continue;
                        columns.Add(property.Name);
                    }
                }

                foreach (var column in columns)
                {
                    if (record.TryGetProperty(column, out var cell))
                        row.Add(Cell(cell));
                    else
                        row.Add("");
                }

                rows.Add(row);
            }
        }

        return new SurfaceOutcome
        {
            Ok = true,
            Tool = "query",
            Columns = columns,
            Rows = rows,
            ParameterKeys = request.Filters.Select(item => item.Key).ToList(),
        };
    }

    public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Failed("count", "forbidden", "此表面沒有計數動作。"));

    private async Task<WebDocument> GetAsync(string relative, CancellationToken cancellationToken)
    {
        if (_base is null)
            return WebDocument.Fail(Failed("tables", "connection_failed", "無法建立連線。"));
        try
        {
            using var response = await _http.GetAsync(new Uri(_base, relative), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return WebDocument.Missing();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return WebDocument.Fail(Failed("query", "connection_failed", Scrub.Message(body)));
            var parsed = JsonDocument.Parse(body);
            return WebDocument.Ok(parsed);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            return WebDocument.Fail(Failed("query", "connection_failed", Scrub.Message(exception.Message)));
        }
    }

    private static string StringOf(JsonElement field, string name)
        => field.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static bool Flag(JsonElement field, string name)
    {
        if (!field.TryGetProperty(name, out var property))
            return false;
        if (property.ValueKind == JsonValueKind.True)
            return true;
        if (property.ValueKind == JsonValueKind.String)
            return string.Equals(property.GetString(), "True", StringComparison.OrdinalIgnoreCase);
        return false;
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

    private static SurfaceOutcome Failed(string tool, string code, string message) => new()
    {
        Ok = false,
        Tool = tool,
        ErrorCode = code,
        ErrorMessage = message,
    };

    private sealed class WebDocument : IDisposable
    {
        private readonly JsonDocument? _document;
        public JsonElement Root => _document?.RootElement ?? default;
        public SurfaceOutcome? Error { get; private init; }
        public bool NotFound { get; private init; }

        private WebDocument(JsonDocument? document) => _document = document;

        public static WebDocument Ok(JsonDocument document) => new(document);
        public static WebDocument Fail(SurfaceOutcome error) => new(null) { Error = error };
        public static WebDocument Missing() => new(null) { NotFound = true };
        public void Dispose() => _document?.Dispose();
    }
}

public sealed class RoutingDataSurface : IDataSurface
{
    private readonly McpSurface _mcp;
    private readonly WebSurface _web;

    public RoutingDataSurface(McpSurface mcp, WebSurface web)
    {
        _mcp = mcp;
        _web = web;
    }

    public Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => Route(request).TablesAsync(request, cancellationToken);

    public Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => Route(request).FieldsAsync(request, cancellationToken);

    public Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => Route(request).QueryAsync(request, cancellationToken);

    public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken)
        => Route(request).CountAsync(request, cancellationToken);

    private SurfaceClient Route(SurfaceRequest request)
    {
        if (request.Surface == SurfaceNames.Web)
            return new SurfaceClient(_web.TablesAsync, _web.FieldsAsync, _web.QueryAsync, _web.CountAsync);
        if (request.Surface == SurfaceNames.Mcp)
            return new SurfaceClient(_mcp.TablesAsync, _mcp.FieldsAsync, _mcp.QueryAsync, _mcp.CountAsync);
        return new SurfaceClient(
            (_, _) => Task.FromResult(Rejected()),
            (_, _) => Task.FromResult(Rejected()),
            (_, _) => Task.FromResult(Rejected()),
            (_, _) => Task.FromResult(Rejected()));
    }

    private static SurfaceOutcome Rejected() => new()
    {
        Ok = false,
        ErrorCode = "forbidden",
        ErrorMessage = "沒有權限。",
    };

    private sealed class SurfaceClient : IDataSurface
    {
        private readonly Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> _tables;
        private readonly Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> _fields;
        private readonly Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> _query;
        private readonly Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> _count;

        public SurfaceClient(
            Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> tables,
            Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> fields,
            Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> query,
            Func<SurfaceRequest, CancellationToken, Task<SurfaceOutcome>> count)
        {
            _tables = tables;
            _fields = fields;
            _query = query;
            _count = count;
        }

        public Task<SurfaceOutcome> TablesAsync(SurfaceRequest request, CancellationToken cancellationToken) => _tables(request, cancellationToken);
        public Task<SurfaceOutcome> FieldsAsync(SurfaceRequest request, CancellationToken cancellationToken) => _fields(request, cancellationToken);
        public Task<SurfaceOutcome> QueryAsync(SurfaceRequest request, CancellationToken cancellationToken) => _query(request, cancellationToken);
        public Task<SurfaceOutcome> CountAsync(SurfaceRequest request, CancellationToken cancellationToken) => _count(request, cancellationToken);
    }
}
