using System.Text;
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
    public bool PrimaryKey { get; set; }
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

public sealed class AnnotationActive
{
    public AnnotationPackage? Package { get; init; }
    public List<AnnotationPackage> Others { get; init; } = [];
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

    /// <summary>
    /// 只改目前這份草稿的說明、表註、欄註、個資與關聯。沒有草稿不建立。
    /// </summary>
    public AnnotationWriteResult Save(AnnotationPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        lock (_gate)
        {
            var packages = Load();
            var index = packages.FindIndex(package => string.Equals(package.Status, "draft", StringComparison.Ordinal));
            if (index < 0 || !string.Equals(packages[index].Id, incoming.Id ?? "", StringComparison.Ordinal))
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            var stored = packages[index];
            var links = incoming.Links ?? [];
            foreach (var link in links)
            {
                if (link is null
                    || !Inside(stored, link.From, link.FromColumn)
                    || !Inside(stored, link.To, link.ToColumn))
                    return AnnotationWriteResult.Fail("link_outside", "關聯的一端不在這份註記包裡。");
            }

            stored.Status = "draft";
            stored.Active = false;
            ApplyAnnotations(stored, incoming);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(stored));
        }
    }

    /// <summary>
    /// 先讀結構，再在鎖內確認仍無草稿且要求未中止，才寫成草稿。
    /// </summary>
    public AnnotationWriteResult ReadStructure(ToolCatalog catalog, string connectionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        SchemaGraphResult read;
        try
        {
            read = SchemaGraph.Read(catalog, connectionId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return AnnotationWriteResult.Fail("execution_failed", "讀取資料表結構失敗。");
        }

        if (!read.Ok)
            return AnnotationWriteResult.Fail(read.Code, read.Message);

        var copy = FromSchema(read, connectionId);
        lock (_gate)
        {
            var packages = Load();
            if (packages.Any(package => string.Equals(package.Status, "draft", StringComparison.Ordinal)))
                return AnnotationWriteResult.Fail("draft_exists", "已有工作中的草稿，沒有改。");
            if (cancellationToken.IsCancellationRequested)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            packages.Add(copy);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(copy));
        }
    }

    public AnnotationActive Active()
    {
        lock (_gate)
        {
            var packages = Load();
            var active = packages.FirstOrDefault(package => package.Active && string.Equals(package.Status, "confirmed", StringComparison.Ordinal));
            var others = packages
                .Where(package => !package.Active && string.Equals(package.Status, "confirmed", StringComparison.Ordinal))
                .Select(Clone)
                .ToList();
            return new AnnotationActive
            {
                Package = active is null ? null : Clone(active),
                Others = others,
            };
        }
    }

    public bool IsActiveId(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        lock (_gate)
        {
            return Load().Any(package =>
                package.Active
                && string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
                && string.Equals(package.Id, id, StringComparison.Ordinal));
        }
    }

    /// <summary>從啟用包另寫一份草稿。已有草稿、或關聯落在包外時不寫檔。</summary>
    public AnnotationWriteResult ForkFromActive(AnnotationPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        lock (_gate)
        {
            var packages = Load();
            if (packages.Any(package => string.Equals(package.Status, "draft", StringComparison.Ordinal)))
                return AnnotationWriteResult.Fail("draft_exists", "已有工作中的草稿，沒有改。");

            var stored = packages.FirstOrDefault(package =>
                package.Active
                && string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
                && string.Equals(package.Id, incoming.Id ?? "", StringComparison.Ordinal));
            if (stored is null)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            var links = incoming.Links ?? [];
            foreach (var link in links)
            {
                if (link is null
                    || !Inside(stored, link.From, link.FromColumn)
                    || !Inside(stored, link.To, link.ToColumn))
                    return AnnotationWriteResult.Fail("link_outside", "關聯的一端不在這份註記包裡。");
            }

            var copy = Clone(stored);
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Status = "draft";
            copy.Active = false;
            copy.Dbml = "";
            ApplyAnnotations(copy, incoming);
            packages.Add(copy);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(copy));
        }
    }

    /// <summary>草稿的 DBML 文字。不寫進 dbml 欄，也不寫檔。</summary>
    public string? DraftDbml()
    {
        lock (_gate)
        {
            var draft = Load().FirstOrDefault(package => string.Equals(package.Status, "draft", StringComparison.Ordinal));
            return draft is null ? null : Render(draft);
        }
    }

    /// <summary>說明、表或欄不齊時回傳拒絕，且不讀來源。齊了則回傳 null。</summary>
    public AnnotationWriteResult? RefuseConfirm()
    {
        lock (_gate)
        {
            var draft = Load().FirstOrDefault(package => string.Equals(package.Status, "draft", StringComparison.Ordinal));
            if (draft is null)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            if (Incomplete(draft))
                return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。");
            return null;
        }
    }

    /// <summary>
    /// 先對帳表名與欄名，再看已保存檢視的端點。通過才把這一份設成唯一啟用包並寫入 dbml。失敗不寫檔。
    /// </summary>
    public AnnotationWriteResult Confirm(ToolCatalog catalog, string connectionId, IReadOnlyList<ViewDocument>? views)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        AnnotationPackage snapshot;
        lock (_gate)
        {
            var draft = Load().FirstOrDefault(package => string.Equals(package.Status, "draft", StringComparison.Ordinal));
            if (draft is null)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            if (Incomplete(draft))
                return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。");
            snapshot = Clone(draft);
        }

        SchemaGraphResult read;
        try
        {
            read = SchemaGraph.Read(catalog, connectionId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return AnnotationWriteResult.Fail("execution_failed", "讀取資料表結構失敗。");
        }

        if (!read.Ok)
            return AnnotationWriteResult.Fail(read.Code, read.Message);

        var unmatched = Unmatched(snapshot, read);
        if (unmatched.Count > 0)
            return AnnotationWriteResult.Fail("confirm_unmatched", "無法確認。這些表或欄對不上來源：" + string.Join("、", unmatched) + "。");

        var blocked = Blocked(snapshot, views);
        if (blocked.Count > 0)
            return AnnotationWriteResult.Fail("confirm_blocked", "無法確認。這些檢視會落到新包之外：" + string.Join("、", blocked) + "。");

        lock (_gate)
        {
            var packages = Load();
            var index = packages.FindIndex(package =>
                string.Equals(package.Status, "draft", StringComparison.Ordinal)
                && string.Equals(package.Id, snapshot.Id, StringComparison.Ordinal));
            if (index < 0)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            var stored = packages[index];
            if (Incomplete(stored))
                return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。");

            unmatched = Unmatched(stored, read);
            if (unmatched.Count > 0)
                return AnnotationWriteResult.Fail("confirm_unmatched", "無法確認。這些表或欄對不上來源：" + string.Join("、", unmatched) + "。");

            blocked = Blocked(stored, views);
            if (blocked.Count > 0)
                return AnnotationWriteResult.Fail("confirm_blocked", "無法確認。這些檢視會落到新包之外：" + string.Join("、", blocked) + "。");

            stored.Status = "confirmed";
            stored.Dbml = Render(stored);
            foreach (var package in packages)
            {
                if (!ReferenceEquals(package, stored))
                    package.Active = false;
            }

            stored.Active = true;
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(stored));
        }
    }

    public static string Render(AnnotationPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(package.Purpose))
        {
            var title = string.IsNullOrWhiteSpace(package.Title) ? "annotation" : package.Title;
            builder.Append("Project ").Append(Ident(title)).Append(" {\n");
            builder.Append("  Note: ").Append(Quote(package.Purpose.Trim())).Append("\n}\n\n");
        }

        var tables = package.Tables ?? [];
        for (var index = 0; index < tables.Count; index++)
        {
            if (index > 0)
                builder.Append('\n');
            AppendTable(builder, tables[index]);
        }

        var links = package.Links ?? [];
        if (links.Count > 0 && tables.Count > 0)
            builder.Append('\n');
        foreach (var link in links)
        {
            if (link is null)
                continue;
            builder.Append("Ref: ")
                .Append(Ident(link.From)).Append('.').Append(Ident(link.FromColumn))
                .Append(" > ")
                .Append(Ident(link.To)).Append('.').Append(Ident(link.ToColumn));
            if (!string.IsNullOrWhiteSpace(link.Note))
                builder.Append(" [note: ").Append(Quote(link.Note)).Append(']');
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static AnnotationPackage FromSchema(SchemaGraphResult read, string title)
    {
        var known = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var table in read.Tables)
        {
            var columns = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in table.Columns)
                columns.Add(column.Name);
            known[table.Name] = columns;
        }

        var links = new List<AnnotationLink>();
        var tables = new List<AnnotationTable>();
        foreach (var table in read.Tables)
        {
            var columns = new List<AnnotationColumn>();
            foreach (var column in table.Columns)
            {
                columns.Add(new AnnotationColumn
                {
                    Name = column.Name,
                    Type = column.Type ?? "",
                    NotNull = false,
                    PrimaryKey = column.PrimaryKey,
                    Note = "",
                    Pii = "",
                    UniqueIndex = false,
                });

                var referencedTable = column.ReferencesTable ?? "";
                var referencedColumn = column.ReferencesColumn ?? "";
                if (referencedTable.Length == 0 || referencedColumn.Length == 0)
                    continue;
                if (!known.TryGetValue(referencedTable, out var target) || !target.Contains(referencedColumn))
                    continue;
                if (!known.TryGetValue(table.Name, out var source) || !source.Contains(column.Name))
                    continue;

                links.Add(new AnnotationLink
                {
                    From = table.Name,
                    FromColumn = column.Name,
                    To = referencedTable,
                    ToColumn = referencedColumn,
                    Origin = "foreign-key",
                    Note = "",
                });
            }

            tables.Add(new AnnotationTable
            {
                Name = table.Name,
                Note = "",
                Columns = columns,
                Indexes = [],
            });
        }

        return new AnnotationPackage
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title ?? "",
            Purpose = "",
            Status = "draft",
            Active = false,
            Dbml = "",
            Tables = tables,
            Links = links,
        };
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
        var temp = _path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(temp, JsonSerializer.Serialize(packages, Json));
            File.Move(temp, _path, overwrite: true);
            _packages = packages;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }

    private static void ApplyAnnotations(AnnotationPackage stored, AnnotationPackage incoming)
    {
        stored.Purpose = incoming.Purpose ?? "";
        var requestTables = incoming.Tables ?? [];
        foreach (var table in stored.Tables ?? [])
        {
            var match = requestTables.FirstOrDefault(item => string.Equals(item?.Name, table.Name, StringComparison.Ordinal));
            if (match is null)
                continue;
            table.Note = match.Note ?? "";
            table.Columns ??= [];
            foreach (var column in table.Columns)
            {
                var noted = match.Columns?.FirstOrDefault(item => string.Equals(item?.Name, column.Name, StringComparison.Ordinal));
                if (noted is null)
                    continue;
                var note = noted.Note ?? "";
                column.Note = string.IsNullOrWhiteSpace(note) ? "" : note;
                column.Pii = noted.Pii ?? "";
            }
        }

        stored.Links = MergeLinks(stored.Links, incoming.Links ?? []);
    }

    private static bool Incomplete(AnnotationPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.Purpose))
            return true;
        var tables = package.Tables ?? [];
        if (tables.Count == 0)
            return true;
        var columns = 0;
        foreach (var table in tables)
            columns += table.Columns?.Count ?? 0;
        return columns == 0;
    }

    private static List<string> Unmatched(AnnotationPackage package, SchemaGraphResult read)
    {
        var known = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var table in read.Tables)
        {
            var columns = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in table.Columns)
                columns.Add(column.Name);
            known[table.Name] = columns;
        }

        var missing = new List<string>();
        foreach (var table in package.Tables ?? [])
        {
            var tableName = table?.Name ?? "";
            if (!known.TryGetValue(tableName, out var columns))
            {
                missing.Add(tableName);
                continue;
            }

            foreach (var column in table?.Columns ?? [])
            {
                var columnName = column?.Name ?? "";
                if (!columns.Contains(columnName))
                    missing.Add(tableName + "." + columnName);
            }
        }

        return missing;
    }

    private static List<string> Blocked(AnnotationPackage package, IReadOnlyList<ViewDocument>? views)
    {
        var names = new List<string>();
        foreach (var view in views ?? [])
        {
            if (view is null || !Outside(package, view))
                continue;
            names.Add(view.Name ?? "");
        }

        return names;
    }

    private static bool Outside(AnnotationPackage package, ViewDocument view)
    {
        foreach (var source in view.Sources ?? [])
        {
            if (string.IsNullOrEmpty(source))
                continue;
            if (package.Tables?.Any(table => string.Equals(table.Name, source, StringComparison.Ordinal)) != true)
                return true;
        }

        foreach (var column in view.Columns ?? [])
        {
            if (column is null || (string.IsNullOrEmpty(column.Source) && string.IsNullOrEmpty(column.Name)))
                continue;
            if (!Inside(package, column.Source, column.Name))
                return true;
        }

        foreach (var link in view.Links ?? [])
        {
            if (link is null || !Inside(package, link.From, link.FromColumn) || !Inside(package, link.To, link.ToColumn))
                return true;
        }

        return false;
    }

    private static void AppendTable(StringBuilder builder, AnnotationTable? table)
    {
        builder.Append("Table ").Append(Ident(table?.Name)).Append(" {\n");
        foreach (var column in table?.Columns ?? [])
        {
            if (column is null)
                continue;
            builder.Append("  ").Append(Ident(column.Name)).Append(' ').Append(column.Type ?? "");
            var settings = new List<string>();
            if (column.PrimaryKey)
                settings.Add("pk");
            if (column.NotNull)
                settings.Add("not null");
            if (!string.IsNullOrWhiteSpace(column.Note))
                settings.Add("note: " + Quote(column.Note));
            if (settings.Count > 0)
                builder.Append(" [").Append(string.Join(", ", settings)).Append(']');
            builder.Append('\n');
        }

        var indexes = table?.Indexes ?? [];
        if (indexes.Count > 0)
        {
            builder.Append("\n  indexes {\n");
            foreach (var index in indexes)
            {
                if (index is null)
                    continue;
                builder.Append("    (").Append(string.Join(", ", (index.Columns ?? []).Select(Ident))).Append(") [name: ").Append(Quote(index.Name));
                if (!string.IsNullOrWhiteSpace(index.Note))
                    builder.Append(", note: ").Append(Quote(index.Note));
                builder.Append("]\n");
            }

            builder.Append("  }\n");
        }

        if (!string.IsNullOrWhiteSpace(table?.Note))
            builder.Append("\n  Note: ").Append(Quote(table.Note)).Append('\n');
        builder.Append("}\n");
    }

    private static string Ident(string? name)
    {
        var text = name ?? "";
        if (text.Length > 0
            && (char.IsAsciiLetter(text[0]) || text[0] == '_')
            && text.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_'))
            return text;
        return "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }

    private static string Quote(string? value)
    {
        var text = value ?? "";
        return "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "'";
    }

    private static bool Inside(AnnotationPackage package, string? table, string? column)
    {
        if (string.IsNullOrEmpty(table) || string.IsNullOrEmpty(column))
            return false;
        var found = package.Tables?.FirstOrDefault(item => string.Equals(item.Name, table, StringComparison.Ordinal));
        return found?.Columns?.Any(item => string.Equals(item.Name, column, StringComparison.Ordinal)) == true;
    }

    private static List<AnnotationLink> MergeLinks(List<AnnotationLink>? existing, List<AnnotationLink> incoming)
    {
        var prior = existing ?? [];
        var merged = new List<AnnotationLink>();
        foreach (var link in incoming)
        {
            var duplicate = merged.FirstOrDefault(item => SameLink(item, link));
            if (duplicate is not null)
            {
                duplicate.Note = link.Note ?? "";
                continue;
            }

            var origin = prior.FirstOrDefault(item => SameLink(item, link))?.Origin;
            if (string.IsNullOrWhiteSpace(origin))
                origin = "declared";
            merged.Add(new AnnotationLink
            {
                From = link.From ?? "",
                FromColumn = link.FromColumn ?? "",
                To = link.To ?? "",
                ToColumn = link.ToColumn ?? "",
                Origin = origin,
                Note = link.Note ?? "",
            });
        }

        return merged;
    }

    private static bool SameLink(AnnotationLink left, AnnotationLink right)
    {
        var straight = string.Equals(left.From, right.From, StringComparison.Ordinal)
            && string.Equals(left.FromColumn, right.FromColumn, StringComparison.Ordinal)
            && string.Equals(left.To, right.To, StringComparison.Ordinal)
            && string.Equals(left.ToColumn, right.ToColumn, StringComparison.Ordinal);
        var swapped = string.Equals(left.From, right.To, StringComparison.Ordinal)
            && string.Equals(left.FromColumn, right.ToColumn, StringComparison.Ordinal)
            && string.Equals(left.To, right.From, StringComparison.Ordinal)
            && string.Equals(left.ToColumn, right.FromColumn, StringComparison.Ordinal);
        return straight || swapped;
    }

    private static AnnotationPackage Clone(AnnotationPackage package)
        => JsonSerializer.Deserialize<AnnotationPackage>(JsonSerializer.Serialize(package, Json), Json)!;
}
