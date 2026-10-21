using System.Text.Json;
using DbServices.Api.Shared;

namespace DbServices.Mcp;

/// <summary>
/// 從已登記連線讀出資料表、欄位與外鍵，並用既有檢視查詢做有上限的預覽。
/// </summary>
public static class SchemaGraph
{
    public const int PreviewLimit = 40;

    public static SchemaGraphResult Read(ToolCatalog catalog, string? connectionId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!TrySnapshot(catalog, connectionId, out var snapshot, out var failed))
            return failed;

        var service = DbServiceFactory.Create(snapshot.ConnectionString, snapshot.ServerType);
        if (service is null)
            return SchemaGraphResult.Fail("connection_failed", "無法建立連線。");

        try
        {
            var names = service.GetAllTableNames(includeView: true) ?? [];
            var tables = new List<SchemaTableInfo>();
            foreach (var name in names.Where(item => !string.IsNullOrWhiteSpace(item)).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                var columns = new List<SchemaColumnInfo>();
                try
                {
                    foreach (var field in service.GetFieldsByTableName(name) ?? [])
                    {
                        var columnName = field.FieldName?.Trim() ?? "";
                        if (columnName.Length == 0)
                            continue;
                        var referencedTable = field.ForeignInfo?.ComeFromTableName?.Trim();
                        var referencedColumn = field.ForeignInfo?.ComeFromFieldName?.Trim();
                        var linked = field.IsForeignKey
                            && !string.IsNullOrWhiteSpace(referencedTable)
                            && !string.IsNullOrWhiteSpace(referencedColumn);
                        columns.Add(new SchemaColumnInfo
                        {
                            Name = columnName,
                            Type = field.FieldType?.Trim() ?? "",
                            PrimaryKey = field.IsPrimaryKey,
                            ReferencesTable = linked ? referencedTable : null,
                            ReferencesColumn = linked ? referencedColumn : null,
                        });
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    columns.Clear();
                }

                tables.Add(new SchemaTableInfo { Name = name, Columns = columns });
            }

            return SchemaGraphResult.Success(tables);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return SchemaGraphResult.Fail("execution_failed", "讀取資料表結構失敗。");
        }
        finally
        {
            (service as IDisposable)?.Dispose();
        }
    }

    public static SchemaPreviewResult Preview(
        ToolCatalog catalog,
        string? connectionId,
        ViewDocument? view,
        IReadOnlyDictionary<string, string>? parameters)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (view is null || view.Sources is null || view.Sources.Count == 0)
            return SchemaPreviewResult.Fail("invalid_arguments", "請先選擇資料表。");
        if (!TrySnapshot(catalog, connectionId, out _, out var failed))
            return SchemaPreviewResult.Fail(failed.Code, failed.Message);

        try
        {
            var prepared = Prepare(view, parameters);
            var outcome = ViewDispatch.Execute(catalog, connectionId ?? "", prepared.View, prepared.Parameters);
            if (outcome.IsError)
                return ReadError(outcome.Json);
            if (!TryRead(outcome.Json, out var columns, out var rows))
                return SchemaPreviewResult.Fail("execution_failed", "鍵值查詢失敗。");

            var truncated = rows.Count > PreviewLimit;
            if (truncated)
                rows = rows.Take(PreviewLimit).ToList();
            return SchemaPreviewResult.Success(columns, rows, truncated);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return SchemaPreviewResult.Fail("execution_failed", "鍵值查詢失敗。");
        }
    }

    public static PreparedView Prepare(ViewDocument view, IReadOnlyDictionary<string, string>? parameters)
    {
        var supplied = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is not null)
        {
            foreach (var pair in parameters)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                    supplied[pair.Key] = pair.Value.Trim();
            }
        }

        var copy = new ViewDocument
        {
            Id = view.Id,
            Name = view.Name,
            Purpose = view.Purpose,
            Published = view.Published,
            Sources = (view.Sources ?? []).ToList(),
            Links = (view.Links ?? []).Select(link => new ViewLink
            {
                Kind = link.Kind,
                From = link.From,
                FromColumn = link.FromColumn,
                To = link.To,
                ToColumn = link.ToColumn,
            }).ToList(),
            Columns = (view.Columns ?? []).Select(column => new ViewColumn
            {
                Name = column.Name,
                Source = column.Source,
            }).ToList(),
            Parameters = (view.Parameters ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) && supplied.ContainsKey(item.Name))
                .Select(item => new ViewParameter
                {
                    Name = item.Name,
                    Column = item.Column,
                    Source = item.Source,
                })
                .ToList(),
        };
        return new PreparedView(copy, supplied);
    }

    private static bool TrySnapshot(ToolCatalog catalog, string? connectionId, out ConnectionSnapshot snapshot, out SchemaGraphResult failed)
    {
        snapshot = null!;
        if (string.IsNullOrWhiteSpace(connectionId) || !catalog.Connections.TryGetValue(connectionId, out var found) || !found.Available)
        {
            failed = SchemaGraphResult.Fail("connection_failed", "無法建立連線。");
            return false;
        }

        snapshot = found;
        failed = null!;
        return true;
    }

    private static SchemaPreviewResult ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var code = document.RootElement.TryGetProperty("code", out var codeElement) ? codeElement.GetString() ?? "" : "";
            var message = document.RootElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "" : "";
            if (message.Length == 0)
                message = "鍵值查詢失敗。";
            return SchemaPreviewResult.Fail(code.Length == 0 ? "execution_failed" : code, message);
        }
        catch (JsonException)
        {
            return SchemaPreviewResult.Fail("execution_failed", "鍵值查詢失敗。");
        }
    }

    private static bool TryRead(string json, out List<string> columns, out List<List<string>> rows)
    {
        columns = [];
        rows = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("columns", out var columnElement)
                || !document.RootElement.TryGetProperty("rows", out var rowElement))
                return false;

            columns = columnElement.EnumerateArray().Select(item => item.GetString() ?? "").ToList();
            foreach (var row in rowElement.EnumerateArray())
                rows.Add(row.EnumerateArray().Select(Cell).ToList());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Cell(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Null or JsonValueKind.Undefined => "",
        _ => element.GetRawText(),
    };
}

public sealed class SchemaColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool PrimaryKey { get; set; }
    public string? ReferencesTable { get; set; }
    public string? ReferencesColumn { get; set; }
}

public sealed class SchemaTableInfo
{
    public string Name { get; set; } = "";
    public List<SchemaColumnInfo> Columns { get; set; } = [];
}

public sealed class SchemaGraphResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public List<SchemaTableInfo> Tables { get; init; } = [];

    public static SchemaGraphResult Success(List<SchemaTableInfo> tables) => new() { Ok = true, Tables = tables };
    public static SchemaGraphResult Fail(string code, string message) => new() { Code = code, Message = message };
}

public sealed class SchemaPreviewResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public List<string> Columns { get; init; } = [];
    public List<List<string>> Rows { get; init; } = [];
    public bool Truncated { get; init; }

    public static SchemaPreviewResult Success(List<string> columns, List<List<string>> rows, bool truncated)
        => new() { Ok = true, Columns = columns, Rows = rows, Truncated = truncated };

    public static SchemaPreviewResult Fail(string code, string message) => new() { Code = code, Message = message };
}

public sealed record PreparedView(ViewDocument View, Dictionary<string, object?> Parameters);
