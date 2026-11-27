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
    public List<AnnotationChange> Changes { get; set; } = [];
    public string SourceKind { get; set; } = "";
    public string SourceKey { get; set; } = "";
}

public sealed class AnnotationTable
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string NoteOrigin { get; set; } = "";
    public string NoteSource { get; set; } = "";
    public List<AnnotationColumn> Columns { get; set; } = [];
    public List<AnnotationIndex> Indexes { get; set; } = [];
}

public sealed class AnnotationColumn
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool NotNull { get; set; }
    public string Note { get; set; } = "";
    public string NoteOrigin { get; set; } = "";
    public string NoteSource { get; set; } = "";
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

public sealed class AnnotationChange
{
    public string Kind { get; set; } = "";
    public string Table { get; set; } = "";
    public string Column { get; set; } = "";
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
            var copy = Prepare(incoming);
            var index = DraftIndex(packages, copy.SourceKind, copy.SourceKey);
            if (index >= 0)
                return Merge(packages, packages[index], copy, NoteSourceFor(packages, copy));

            var seeded = NoteSourceFor(packages, copy);
            if (seeded is not null)
                Seed(copy, seeded);
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
            var index = packages.FindIndex(package =>
                string.Equals(package.Status, "draft", StringComparison.Ordinal)
                && string.Equals(package.Id, incoming.Id ?? "", StringComparison.Ordinal));
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
            if (cancellationToken.IsCancellationRequested)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            var index = DraftIndex(packages, "connection", connectionId);
            if (index < 0)
                index = packages.FindIndex(package => string.Equals(package.Status, "draft", StringComparison.Ordinal) && !Identified(package));
            if (index >= 0)
            {
                var merged = Merge(packages, packages[index], copy, NoteSourceFor(packages, copy));
                if (!merged.Ok || merged.Package is null)
                    return merged;
                return ApplyConnectionDefaults(packages, packages[index]);
            }

            var seeded = NoteSourceFor(packages, copy);
            if (seeded is not null)
                Seed(copy, seeded);
            ApplyExcelDefaults(copy, packages);
            packages.Add(copy);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return AnnotationWriteResult.Success(Clone(copy));
        }
    }

    /// <summary>只刪工作中的草稿。已確認包留在檔內。</summary>
    public AnnotationWriteResult Abandon(string? id = null)
    {
        lock (_gate)
        {
            var packages = Load();
            var index = string.IsNullOrEmpty(id)
                ? packages.FindIndex(package => string.Equals(package.Status, "draft", StringComparison.Ordinal))
                : packages.FindIndex(package =>
                    string.Equals(package.Status, "draft", StringComparison.Ordinal)
                    && string.Equals(package.Id, id, StringComparison.Ordinal));
            if (index < 0)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");

            packages.RemoveAt(index);
            if (!TryFlush(packages))
            {
                Reload();
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            }

            return new AnnotationWriteResult { Ok = true };
        }
    }

    public AnnotationActive Active()
    {
        lock (_gate)
        {
            var packages = Load();
            var active = packages.FirstOrDefault(package => package.Active && string.Equals(package.Status, "confirmed", StringComparison.Ordinal));
            var others = packages
                .Where(package => string.Equals(package.Status, "confirmed", StringComparison.Ordinal) && !ReferenceEquals(package, active))
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
                string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
                && string.Equals(package.Id, id, StringComparison.Ordinal)
                && (package.Active || string.Equals(package.SourceKind, "excel", StringComparison.Ordinal)));
        }
    }

    /// <summary>從啟用包另寫一份草稿。已有草稿、或關聯落在包外時不寫檔。</summary>
    public AnnotationWriteResult ForkFromActive(AnnotationPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        lock (_gate)
        {
            var packages = Load();
            if (packages.Any(package =>
                string.Equals(package.Status, "draft", StringComparison.Ordinal)
                && SameDraftIdentity(package, incoming)))
                return AnnotationWriteResult.Fail("draft_exists", "已有工作中的草稿，沒有改。");

            var stored = packages.FirstOrDefault(package =>
                string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
                && string.Equals(package.Id, incoming.Id ?? "", StringComparison.Ordinal)
                && (package.Active || string.Equals(package.SourceKind, "excel", StringComparison.Ordinal)));
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
    public AnnotationPackage? FindDraft(string? id = null)
    {
        lock (_gate)
        {
            var found = WorkingDraft(Load(), id);
            return found is null ? null : Clone(found);
        }
    }

    public AnnotationWriteResult? RefuseConfirm(string? draftId = null)
    {
        lock (_gate)
        {
            var draft = WorkingDraft(Load(), draftId);
            if (draft is null)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            if (string.Equals(draft.SourceKind, "excel", StringComparison.Ordinal))
            {
                if (ExcelIncomplete(draft))
                    return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。至少要有一張表、一個欄。");
                return null;
            }

            if (Incomplete(draft))
                return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。整包功能說明要有內容，而且至少要有一張表、一個欄。");
            return null;
        }
    }

    public List<AnnotationPackage> Bindable()
    {
        lock (_gate)
        {
            return Load()
                .Where(package => package.Active && string.Equals(package.Status, "confirmed", StringComparison.Ordinal) && !string.Equals(package.SourceKind, "excel", StringComparison.Ordinal))
                .Select(Clone)
                .ToList();
        }
    }

    /// <summary>
    /// 先對帳表名與欄名，再看已保存檢視的端點。通過才把這一份設成唯一啟用包並寫入 dbml。失敗不寫檔。
    /// </summary>
    public AnnotationWriteResult Confirm(ToolCatalog catalog, string connectionId, IReadOnlyList<ViewDocument>? views, string? draftId = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        AnnotationPackage snapshot;
        lock (_gate)
        {
            var draft = WorkingDraft(Load(), draftId);
            if (draft is null)
                return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
            if (string.Equals(draft.SourceKind, "excel", StringComparison.Ordinal))
            {
                if (ExcelIncomplete(draft))
                    return AnnotationWriteResult.Fail("confirm_incomplete", "無法確認。至少要有一張表、一個欄。");
                return CommitIdentified(draft.Id, views, schema: null);
            }

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

        var blocked = string.Equals(snapshot.SourceKind, "connection", StringComparison.Ordinal)
            ? []
            : Blocked(snapshot, views);
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

            blocked = string.Equals(stored.SourceKind, "connection", StringComparison.Ordinal)
                ? BlockedBound(stored, views, PreviousId(packages, stored))
                : Blocked(stored, views);
            if (blocked.Count > 0)
                return AnnotationWriteResult.Fail("confirm_blocked", "無法確認。這些檢視會落到新包之外：" + string.Join("、", blocked) + "。");

            if (string.Equals(stored.SourceKind, "connection", StringComparison.Ordinal))
                return FinishIdentified(packages, stored);

            stored.Status = "confirmed";
            stored.Dbml = Render(stored);
            stored.Changes = [];
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
            SourceKind = "connection",
            SourceKey = title ?? "",
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
            var previousNote = table.Note ?? "";
            var nextNote = match.Note ?? "";
            if (string.Equals(match.NoteOrigin, "excel", StringComparison.Ordinal))
            {
                table.Note = nextNote;
                table.NoteOrigin = "excel";
                table.NoteSource = match.NoteSource ?? "";
            }
            else if (!string.Equals(previousNote, nextNote, StringComparison.Ordinal) || string.Equals(match.NoteOrigin, "user", StringComparison.Ordinal))
            {
                table.Note = nextNote;
                table.NoteOrigin = string.IsNullOrWhiteSpace(nextNote) ? "" : "user";
                table.NoteSource = "";
            }
            table.Columns ??= [];
            foreach (var column in table.Columns)
            {
                var noted = match.Columns?.FirstOrDefault(item => string.Equals(item?.Name, column.Name, StringComparison.Ordinal));
                if (noted is null)
                    continue;
                var note = noted.Note ?? "";
                note = string.IsNullOrWhiteSpace(note) ? "" : note;
                if (string.Equals(noted.NoteOrigin, "excel", StringComparison.Ordinal))
                {
                    column.Note = note;
                    column.NoteOrigin = "excel";
                    column.NoteSource = noted.NoteSource ?? "";
                }
                else if (!string.Equals(column.Note ?? "", note, StringComparison.Ordinal) || string.Equals(noted.NoteOrigin, "user", StringComparison.Ordinal))
                {
                    column.Note = note;
                    column.NoteOrigin = note.Length == 0 ? "" : "user";
                    column.NoteSource = "";
                }

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
            if (column is null || (string.IsNullOrWhiteSpace(column.Source) && string.IsNullOrWhiteSpace(column.Name)))
                continue;
            if (!ColumnInside(package, view, column))
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

    private static bool ColumnInside(AnnotationPackage package, ViewDocument view, ViewColumn column)
        => Inside(package, column.Source, column.Name);

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

    private AnnotationWriteResult Merge(List<AnnotationPackage> packages, AnnotationPackage stored, AnnotationPackage source, AnnotationPackage? notes)
    {
        var changes = Diff(stored, source);
        var tables = Project(source, stored, notes);
        var links = Relate(stored, source, tables);
        stored.Tables = tables;
        stored.Links = links;
        stored.Changes = changes;
        stored.Status = "draft";
        stored.Active = false;
        if (string.Equals(source.SourceKind, "connection", StringComparison.Ordinal))
        {
            stored.SourceKind = "connection";
            stored.SourceKey = source.SourceKey ?? "";
            ApplyExcelDefaults(stored, packages);
        }

        if (!TryFlush(packages))
        {
            Reload();
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        }

        return AnnotationWriteResult.Success(Clone(stored));
    }

    private static void Seed(AnnotationPackage draft, AnnotationPackage active)
    {
        var tables = Project(draft, null, active);
        var links = Relate(active, draft, tables);
        draft.Changes = Diff(active, draft);
        draft.Tables = tables;
        draft.Links = links;
    }

    private static AnnotationPackage Prepare(AnnotationPackage incoming)
    {
        var copy = Clone(incoming);
        copy.Status = "draft";
        copy.Active = false;
        if (string.IsNullOrWhiteSpace(copy.Id))
            copy.Id = Guid.NewGuid().ToString("N");
        copy.Purpose ??= "";
        copy.Dbml ??= "";
        copy.Tables ??= [];
        copy.Links ??= [];
        copy.Changes = [];
        copy.SourceKind ??= "";
        copy.SourceKey ??= "";
        if (string.Equals(copy.SourceKind, "excel", StringComparison.Ordinal))
        {
            copy.SourceKey = FileKey(string.IsNullOrWhiteSpace(copy.SourceKey) ? copy.Title : copy.SourceKey);
            StampExcel(copy);
        }

        return copy;
    }

    private static AnnotationPackage? ActiveOf(List<AnnotationPackage> packages)
        => packages.FirstOrDefault(package => package.Active && string.Equals(package.Status, "confirmed", StringComparison.Ordinal));

    /// <summary>
    /// 註記先取草稿同鍵，再取啟用包同鍵，最後才用來來源。形狀一律用來來源。
    /// </summary>
    private static List<AnnotationTable> Project(AnnotationPackage source, AnnotationPackage? draft, AnnotationPackage? active)
    {
        var draftColumns = IndexColumns(draft);
        var activeColumns = IndexColumns(active);
        var tables = new List<AnnotationTable>();
        foreach (var table in source.Tables ?? [])
        {
            if (table is null)
                continue;
            var name = table.Name ?? "";
            var key = name.Trim();
            string note;
            string noteOrigin;
            string noteSource;
            var draftTable = FindTable(draft, key);
            var activeTable = FindTable(active, key);
            if (draft is not null && draftTable is not null)
            {
                note = draftTable.Note ?? "";
                noteOrigin = draftTable.NoteOrigin ?? "";
                noteSource = draftTable.NoteSource ?? "";
            }
            else if (activeTable is not null)
            {
                note = activeTable.Note ?? "";
                noteOrigin = activeTable.NoteOrigin ?? "";
                noteSource = activeTable.NoteSource ?? "";
            }
            else
            {
                note = table.Note ?? "";
                noteOrigin = table.NoteOrigin ?? "";
                noteSource = table.NoteSource ?? "";
            }

            var columns = new List<AnnotationColumn>();
            foreach (var column in table.Columns ?? [])
            {
                if (column is null)
                    continue;
                var copy = CopyColumn(column);
                var columnKey = Key(name, column.Name);
                if (draft is not null && draftColumns.TryGetValue(columnKey, out var fromDraft))
                {
                    copy.Note = fromDraft.Note ?? "";
                    copy.NoteOrigin = fromDraft.NoteOrigin ?? "";
                    copy.NoteSource = fromDraft.NoteSource ?? "";
                    copy.Pii = fromDraft.Pii ?? "";
                }
                else if (activeColumns.TryGetValue(columnKey, out var fromActive))
                {
                    copy.Note = fromActive.Note ?? "";
                    copy.NoteOrigin = fromActive.NoteOrigin ?? "";
                    copy.NoteSource = fromActive.NoteSource ?? "";
                    copy.Pii = fromActive.Pii ?? "";
                }

                columns.Add(copy);
            }

            tables.Add(new AnnotationTable
            {
                Name = name,
                Note = note,
                NoteOrigin = noteOrigin,
                NoteSource = noteSource,
                Columns = columns,
                Indexes = CopyIndexes(table.Indexes),
            });
        }

        return tables;
    }

    private static List<AnnotationLink> Relate(AnnotationPackage annotations, AnnotationPackage source, List<AnnotationTable> shape)
    {
        var links = new List<AnnotationLink>();
        foreach (var link in source.Links ?? [])
        {
            if (link is null || !string.Equals(link.Origin, "foreign-key", StringComparison.Ordinal) || !EndsIn(shape, link))
                continue;
            if (links.Any(item => SameEnds(item, link)))
                continue;
            var prior = (annotations.Links ?? []).FirstOrDefault(item =>
                item is not null
                && string.Equals(item.Origin, "foreign-key", StringComparison.Ordinal)
                && SameEnds(item, link));
            links.Add(Placed(shape, link, "foreign-key", prior?.Note ?? link.Note ?? ""));
        }

        foreach (var link in annotations.Links ?? [])
        {
            if (link is null || !string.Equals(link.Origin, "declared", StringComparison.Ordinal) || !EndsIn(shape, link))
                continue;
            if (links.Any(item => string.Equals(item.Origin, "declared", StringComparison.Ordinal) && SameEnds(item, link)))
                continue;
            links.Add(Placed(shape, link, "declared", link.Note ?? ""));
        }

        return links;
    }

    private static List<AnnotationChange> Diff(AnnotationPackage previous, AnnotationPackage source)
    {
        var changes = new List<AnnotationChange>();
        var oldColumns = IndexColumns(previous);
        var newColumns = IndexColumns(source);
        var oldFk = FkMap(previous);
        var newFk = FkMap(source);
        var oldNames = TableNames(previous);
        var newNames = TableNames(source);
        foreach (var table in previous.Tables ?? [])
        {
            if (table is null)
                continue;
            var columns = table.Columns ?? [];
            if (columns.Count == 0)
            {
                if (!newNames.Contains((table.Name ?? "").Trim()))
                    changes.Add(Change("removed", table.Name, ""));
                continue;
            }

            foreach (var column in columns)
            {
                if (column is null || newColumns.ContainsKey(Key(table.Name, column.Name)))
                    continue;
                changes.Add(Change("removed", table.Name, column.Name));
            }
        }

        foreach (var table in source.Tables ?? [])
        {
            if (table is null)
                continue;
            var columns = table.Columns ?? [];
            if (columns.Count == 0)
            {
                if (!oldNames.Contains((table.Name ?? "").Trim()))
                    changes.Add(Change("added", table.Name, ""));
                continue;
            }

            foreach (var column in columns)
            {
                if (column is null)
                    continue;
                var key = Key(table.Name, column.Name);
                if (!oldColumns.TryGetValue(key, out var prior))
                {
                    changes.Add(Change("added", table.Name, column.Name));
                    continue;
                }

                if (ShapeChanged(prior, column, FkOf(oldFk, key), FkOf(newFk, key)))
                    changes.Add(Change("changed", table.Name, column.Name));
            }
        }

        return changes;
    }

    private static bool ShapeChanged(AnnotationColumn prior, AnnotationColumn next, string oldFk, string newFk)
        => !string.Equals(prior.Type ?? "", next.Type ?? "", StringComparison.Ordinal)
            || prior.NotNull != next.NotNull
            || prior.PrimaryKey != next.PrimaryKey
            || prior.UniqueIndex != next.UniqueIndex
            || !string.Equals(oldFk, newFk, StringComparison.Ordinal);

    private static Dictionary<string, AnnotationColumn> IndexColumns(AnnotationPackage? package)
    {
        var map = new Dictionary<string, AnnotationColumn>(StringComparer.Ordinal);
        foreach (var table in package?.Tables ?? [])
        {
            if (table is null)
                continue;
            foreach (var column in table.Columns ?? [])
            {
                if (column is null)
                    continue;
                map[Key(table.Name, column.Name)] = column;
            }
        }

        return map;
    }

    private static Dictionary<string, string> TableNotes(AnnotationPackage? package)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in package?.Tables ?? [])
        {
            if (table is null)
                continue;
            var key = (table.Name ?? "").Trim();
            if (!map.ContainsKey(key))
                map[key] = table.Note ?? "";
        }

        return map;
    }

    private static Dictionary<string, string> FkMap(AnnotationPackage package)
    {
        var shape = new List<AnnotationTable>();
        foreach (var table in package.Tables ?? [])
        {
            if (table is not null)
                shape.Add(table);
        }

        var sets = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var link in package.Links ?? [])
        {
            if (link is null || !string.Equals(link.Origin, "foreign-key", StringComparison.Ordinal) || !EndsIn(shape, link))
                continue;
            var from = Key(link.From, link.FromColumn);
            var to = Key(link.To, link.ToColumn);
            AddEnd(sets, from, to);
            AddEnd(sets, to, from);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in sets)
            map[pair.Key] = string.Join("\u001e", pair.Value);
        return map;
    }

    private static void AddEnd(Dictionary<string, SortedSet<string>> sets, string column, string other)
    {
        if (!sets.TryGetValue(column, out var ends))
        {
            ends = new SortedSet<string>(StringComparer.Ordinal);
            sets[column] = ends;
        }

        ends.Add(other);
    }

    private static string FkOf(Dictionary<string, string> map, string key)
        => map.TryGetValue(key, out var value) ? value : "";

    private static HashSet<string> TableNames(AnnotationPackage package)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in package.Tables ?? [])
        {
            if (table is not null)
                names.Add((table.Name ?? "").Trim());
        }

        return names;
    }

    private static AnnotationLink Placed(List<AnnotationTable> shape, AnnotationLink link, string origin, string note)
    {
        var (from, fromColumn) = StoredEnd(shape, link.From, link.FromColumn);
        var (to, toColumn) = StoredEnd(shape, link.To, link.ToColumn);
        return new AnnotationLink
        {
            From = from,
            FromColumn = fromColumn,
            To = to,
            ToColumn = toColumn,
            Origin = origin,
            Note = note,
        };
    }

    private static (string Table, string Column) StoredEnd(List<AnnotationTable> shape, string? table, string? column)
    {
        var tableName = (table ?? "").Trim();
        var columnName = (column ?? "").Trim();
        var found = shape.FirstOrDefault(item => string.Equals((item.Name ?? "").Trim(), tableName, StringComparison.Ordinal));
        var foundColumn = found?.Columns?.FirstOrDefault(item => item is not null && string.Equals((item.Name ?? "").Trim(), columnName, StringComparison.Ordinal));
        return (found?.Name ?? table ?? "", foundColumn?.Name ?? column ?? "");
    }

    private static bool EndsIn(List<AnnotationTable> tables, AnnotationLink link)
        => HasColumn(tables, link.From, link.FromColumn) && HasColumn(tables, link.To, link.ToColumn);

    private static bool HasColumn(List<AnnotationTable> tables, string? table, string? column)
    {
        var tableName = (table ?? "").Trim();
        var columnName = (column ?? "").Trim();
        if (tableName.Length == 0 || columnName.Length == 0)
            return false;
        var found = tables.FirstOrDefault(item => string.Equals((item.Name ?? "").Trim(), tableName, StringComparison.Ordinal));
        return found?.Columns?.Any(item => string.Equals((item.Name ?? "").Trim(), columnName, StringComparison.Ordinal)) == true;
    }

    private static bool SameEnds(AnnotationLink left, AnnotationLink right)
    {
        var straight = string.Equals(Key(left.From, left.FromColumn), Key(right.From, right.FromColumn), StringComparison.Ordinal)
            && string.Equals(Key(left.To, left.ToColumn), Key(right.To, right.ToColumn), StringComparison.Ordinal);
        var swapped = string.Equals(Key(left.From, left.FromColumn), Key(right.To, right.ToColumn), StringComparison.Ordinal)
            && string.Equals(Key(left.To, left.ToColumn), Key(right.From, right.FromColumn), StringComparison.Ordinal);
        return straight || swapped;
    }

    private static string Key(string? table, string? column)
        => (table ?? "").Trim() + "\u001f" + (column ?? "").Trim();

    private static AnnotationChange Change(string kind, string? table, string? column)
        => new() { Kind = kind, Table = table ?? "", Column = column ?? "" };

    private static AnnotationColumn CopyColumn(AnnotationColumn column) => new()
    {
        Name = column.Name ?? "",
        Type = column.Type ?? "",
        NotNull = column.NotNull,
        Note = column.Note ?? "",
        NoteOrigin = column.NoteOrigin ?? "",
        NoteSource = column.NoteSource ?? "",
        HName = column.HName ?? "",
        SName = column.SName ?? "",
        HType = column.HType ?? "",
        SType = column.SType ?? "",
        SLength = column.SLength ?? "",
        Pii = column.Pii ?? "",
        PrimaryKey = column.PrimaryKey,
        UniqueIndex = column.UniqueIndex,
        SourceFile = column.SourceFile ?? "",
        HOrdinal = column.HOrdinal ?? "",
        SOrdinal = column.SOrdinal ?? "",
    };

    private static List<AnnotationIndex> CopyIndexes(List<AnnotationIndex>? indexes)
    {
        var copy = new List<AnnotationIndex>();
        foreach (var index in indexes ?? [])
        {
            if (index is null)
                continue;
            copy.Add(new AnnotationIndex
            {
                Name = index.Name ?? "",
                Note = index.Note ?? "",
                Columns = [..(index.Columns ?? [])],
            });
        }

        return copy;
    }

    private static AnnotationPackage Clone(AnnotationPackage package)
        => JsonSerializer.Deserialize<AnnotationPackage>(JsonSerializer.Serialize(package, Json), Json)!;

    private static string FileKey(string? fileName) => Path.GetFileName(fileName ?? "").Trim();

    private static bool SameKey(string? left, string? right)
        => string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Identified(AnnotationPackage package)
        => package.SourceKind is "excel" or "connection";

    private static bool SameIdentity(AnnotationPackage package, string? kind, string? key)
        => string.Equals(package.SourceKind, kind, StringComparison.Ordinal) && SameKey(package.SourceKey, key);

    private static bool SameDraftIdentity(AnnotationPackage draft, AnnotationPackage incoming)
        => Identified(incoming) ? SameIdentity(draft, incoming.SourceKind, incoming.SourceKey) : !Identified(draft);

    private static int DraftIndex(List<AnnotationPackage> packages, string? kind, string? key)
    {
        if (kind is "excel" or "connection")
            return packages.FindIndex(package => string.Equals(package.Status, "draft", StringComparison.Ordinal) && SameIdentity(package, kind, key));
        return packages.FindIndex(package => string.Equals(package.Status, "draft", StringComparison.Ordinal) && !Identified(package));
    }

    private static AnnotationPackage? NoteSourceFor(List<AnnotationPackage> packages, AnnotationPackage incoming)
    {
        if (incoming.SourceKind is "excel" or "connection")
        {
            if (string.Equals(incoming.SourceKind, "excel", StringComparison.Ordinal))
                return null;
            var same = packages.FirstOrDefault(package =>
                string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
                && SameIdentity(package, incoming.SourceKind, incoming.SourceKey));
            if (same is not null)
                return same;
            if (string.Equals(incoming.SourceKind, "connection", StringComparison.Ordinal))
            {
                var legacy = ActiveOf(packages);
                if (legacy is not null && !Identified(legacy))
                    return legacy;
            }

            return null;
        }

        return ActiveOf(packages);
    }

    private static AnnotationPackage? WorkingDraft(List<AnnotationPackage> packages, string? id)
    {
        var drafts = packages.Where(package => string.Equals(package.Status, "draft", StringComparison.Ordinal)).ToList();
        if (!string.IsNullOrEmpty(id))
            return drafts.FirstOrDefault(package => string.Equals(package.Id, id, StringComparison.Ordinal));
        if (drafts.Count == 1)
            return drafts[0];
        var untyped = drafts.Where(package => !Identified(package)).ToList();
        return untyped.Count == 1 && drafts.Count == 1 ? untyped[0] : untyped.Count == 1 && drafts.All(package => !Identified(package)) ? untyped[0] : null;
    }

    private static bool ExcelIncomplete(AnnotationPackage package)
    {
        var tables = package.Tables ?? [];
        if (tables.Count == 0)
            return true;
        var columns = 0;
        foreach (var table in tables)
            columns += table.Columns?.Count ?? 0;
        return columns == 0;
    }

    private AnnotationWriteResult CommitIdentified(string draftId, IReadOnlyList<ViewDocument>? views, SchemaGraphResult? schema)
    {
        var packages = Load();
        var index = packages.FindIndex(package =>
            string.Equals(package.Status, "draft", StringComparison.Ordinal)
            && string.Equals(package.Id, draftId, StringComparison.Ordinal));
        if (index < 0)
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        var stored = packages[index];
        if (schema is not null)
        {
            var unmatched = Unmatched(stored, schema);
            if (unmatched.Count > 0)
                return AnnotationWriteResult.Fail("confirm_unmatched", "無法確認。這些表或欄對不上來源：" + string.Join("、", unmatched) + "。");
        }

        var blocked = BlockedBound(stored, views, PreviousId(packages, stored));
        if (string.Equals(stored.SourceKind, "connection", StringComparison.Ordinal) && blocked.Count > 0)
            return AnnotationWriteResult.Fail("confirm_blocked", "無法確認。這些檢視會落到新包之外：" + string.Join("、", blocked) + "。");
        return FinishIdentified(packages, stored);
    }

    private AnnotationWriteResult ApplyConnectionDefaults(List<AnnotationPackage> packages, AnnotationPackage stored)
    {
        ApplyExcelDefaults(stored, packages);
        if (!TryFlush(packages))
        {
            Reload();
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        }

        return AnnotationWriteResult.Success(Clone(stored));
    }

    private static string PreviousId(List<AnnotationPackage> packages, AnnotationPackage draft)
    {
        var previous = packages.FirstOrDefault(package =>
            string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
            && SameIdentity(package, draft.SourceKind, draft.SourceKey));
        return previous?.Id ?? draft.Id;
    }

    private AnnotationWriteResult FinishIdentified(List<AnnotationPackage> packages, AnnotationPackage stored)
    {
        var previous = packages.FirstOrDefault(package =>
            !ReferenceEquals(package, stored)
            && string.Equals(package.Status, "confirmed", StringComparison.Ordinal)
            && SameIdentity(package, stored.SourceKind, stored.SourceKey));
        var target = previous ?? stored;
        if (!ReferenceEquals(target, stored))
        {
            target.Title = stored.Title ?? "";
            target.Purpose = stored.Purpose ?? "";
            target.Tables = stored.Tables ?? [];
            target.Links = stored.Links ?? [];
            target.SourceKind = stored.SourceKind ?? "";
            target.SourceKey = stored.SourceKey ?? "";
            packages.Remove(stored);
        }

        target.Status = "confirmed";
        target.Changes = [];
        target.Dbml = Render(target);
        target.Active = string.Equals(target.SourceKind, "connection", StringComparison.Ordinal);
        if (!TryFlush(packages))
        {
            Reload();
            return AnnotationWriteResult.Fail("execution_failed", "沒有保存。");
        }

        return AnnotationWriteResult.Success(Clone(target));
    }

    private static List<string> BlockedBound(AnnotationPackage package, IReadOnlyList<ViewDocument>? views, string boundId)
    {
        var names = new List<string>();
        foreach (var view in views ?? [])
        {
            if (view is null || !string.Equals(view.AnnotationId, boundId, StringComparison.Ordinal) || !Outside(package, view))
                continue;
            names.Add(view.Name ?? "");
        }

        return names;
    }

    private static void StampExcel(AnnotationPackage package)
    {
        foreach (var table in package.Tables ?? [])
        {
            if (table is null)
                continue;
            if (string.IsNullOrWhiteSpace(table.NoteOrigin) && !string.IsNullOrWhiteSpace(table.Note))
            {
                table.NoteOrigin = "excel";
                table.NoteSource = package.SourceKey;
            }

            foreach (var column in table.Columns ?? [])
            {
                if (column is null || !string.IsNullOrWhiteSpace(column.NoteOrigin) || string.IsNullOrWhiteSpace(column.Note))
                    continue;
                column.NoteOrigin = "excel";
                column.NoteSource = package.SourceKey;
            }
        }
    }

    private static void ApplyExcelDefaults(AnnotationPackage draft, List<AnnotationPackage> packages)
    {
        var excels = packages
            .Where(package => string.Equals(package.Status, "confirmed", StringComparison.Ordinal) && string.Equals(package.SourceKind, "excel", StringComparison.Ordinal))
            .ToList();
        foreach (var table in draft.Tables ?? [])
        {
            if (table is null)
                continue;
            ApplyExcelNote(excels, table.Name ?? "", null, table);
            foreach (var column in table.Columns ?? [])
            {
                if (column is not null)
                    ApplyExcelNote(excels, table.Name ?? "", column, null);
            }
        }
    }

    private static void ApplyExcelNote(List<AnnotationPackage> excels, string tableName, AnnotationColumn? column, AnnotationTable? table)
    {
        var origin = column?.NoteOrigin ?? table?.NoteOrigin ?? "";
        var source = column?.NoteSource ?? table?.NoteSource ?? "";
        var note = column?.Note ?? table?.Note ?? "";
        if (string.Equals(origin, "user", StringComparison.Ordinal))
            return;
        if (!string.IsNullOrWhiteSpace(note) && !string.Equals(origin, "excel", StringComparison.Ordinal))
            return;

        if (string.Equals(origin, "excel", StringComparison.Ordinal))
        {
            var file = excels.FirstOrDefault(package => SameKey(package.SourceKey, source));
            var updated = ColumnNote(file, tableName, column?.Name);
            if (updated is null)
                return;
            if (column is not null)
                column.Note = updated;
            else if (table is not null)
                table.Note = updated;
            return;
        }

        var hits = ColumnHits(excels, tableName, column?.Name);
        if (hits.Count != 1)
            return;
        if (column is not null)
        {
            column.Note = hits[0].Note;
            column.NoteOrigin = "excel";
            column.NoteSource = hits[0].File;
        }
        else if (table is not null)
        {
            table.Note = hits[0].Note;
            table.NoteOrigin = "excel";
            table.NoteSource = hits[0].File;
        }
    }

    private readonly record struct ExcelHit(string File, string Note);

    private static List<ExcelHit> ColumnHits(List<AnnotationPackage> excels, string tableName, string? columnName)
    {
        var hits = new List<ExcelHit>();
        foreach (var excel in excels)
        {
            var text = ColumnNote(excel, tableName, columnName);
            if (!string.IsNullOrWhiteSpace(text))
                hits.Add(new ExcelHit(excel.SourceKey ?? "", text));
        }

        return hits;
    }

    private static string? ColumnNote(AnnotationPackage? package, string tableName, string? columnName)
    {
        var table = package?.Tables?.FirstOrDefault(item => string.Equals(item?.Name, tableName, StringComparison.Ordinal));
        if (table is null)
            return null;
        if (columnName is null)
            return string.IsNullOrWhiteSpace(table.Note) ? null : table.Note.Trim();
        var column = table.Columns?.FirstOrDefault(item => string.Equals(item?.Name, columnName, StringComparison.Ordinal));
        return column is null || string.IsNullOrWhiteSpace(column.Note) ? null : column.Note.Trim();
    }

    private static AnnotationTable? FindTable(AnnotationPackage? package, string key)
        => package?.Tables?.FirstOrDefault(item => item is not null && string.Equals((item.Name ?? "").Trim(), key, StringComparison.Ordinal));
}
