using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Workbench.Contracts;

namespace Workbench.Core;

/// <summary>
/// 讀第一張工作表的儲存格計算結果，轉成註記包。不讀業務列，不碰目錄檔。
/// </summary>
public static class DictionaryWorkbook
{
    private const string IndexNote = "S_唯一索引 以 V 勾記。此組合不是唯一鍵，因為異動為 append";

    private static readonly string[] Headers =
    [
        "H_檔案名稱",
        "H_欄位序號",
        "H_欄位名稱",
        "H_資料類型長度",
        "資料描述",
        "H_個資去識別化處理",
        "S_檔案名稱",
        "S_欄位序號",
        "S_欄位名稱",
        "S_資料類型",
        "S_資料長度",
        "S_唯一索引",
        "資料備註",
        "允許空白",
    ];

    public static OpResult<AnnotationPackageDto> Read(string? fileName, byte[]? content)
    {
        if (content is not { Length: > 0 })
            return Reject();

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var workbook = Load(zip, "xl/workbook.xml");
            if (workbook is null)
                return Reject();

            var sheets = workbook.Descendants()
                .Where(element => element.Name.LocalName == "sheet" && element.Parent?.Name.LocalName == "sheets")
                .ToList();
            if (sheets.Count != 1)
                return Reject();

            var sheetPath = SheetPath(zip, sheets[0]);
            if (sheetPath is null)
                return Reject();
            var sheet = Load(zip, sheetPath);
            if (sheet is null)
                return Reject();

            var shared = SharedStrings(zip);
            var ignored = IgnoredCells(sheet);
            var grid = Cells(sheet, shared, ignored);
            if (!HeaderMatches(grid))
                return Reject();

            var tables = Tables(grid);
            if (tables is null || tables.Count == 0)
                return Reject();

            var title = Path.GetFileNameWithoutExtension(fileName ?? "");
            return OpResult<AnnotationPackageDto>.Success(new AnnotationPackageDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Purpose = "",
                Status = "draft",
                Active = false,
                Tables = tables,
                Links = [],
                Dbml = "",
            });
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or XmlException or ArgumentException or FormatException)
        {
            return Reject();
        }
    }

    private static OpResult<AnnotationPackageDto> Reject()
        => OpResult<AnnotationPackageDto>.Fail(WorkbenchMessages.Text("import_rejected", WorkbenchMessages.ImportRejected));

    private static List<AnnotationTableDto>? Tables(Dictionary<int, Dictionary<int, string>> grid)
    {
        var max = grid.Count == 0 ? 1 : grid.Keys.Max();
        var tables = new List<AnnotationTableDto>();
        var files = new Dictionary<AnnotationTableDto, string>();
        var descriptions = new Dictionary<AnnotationTableDto, string>();
        AnnotationTableDto? current = null;
        var lastColumnRow = 0;

        for (var row = 2; row <= max; row++)
        {
            var hName = Cell(grid, row, 3);
            var sName = Cell(grid, row, 9);
            if (hName.Length == 0 && sName.Length == 0)
                continue;

            var sFile = Cell(grid, row, 7);
            if (sFile.Length == 0)
            {
                if (current is null)
                    return null;
            }
            else if (current is null || !string.Equals(current.Name, sFile, StringComparison.Ordinal))
            {
                current = tables.FirstOrDefault(table => string.Equals(table.Name, sFile, StringComparison.Ordinal));
                if (current is null)
                {
                    current = new AnnotationTableDto { Name = sFile };
                    tables.Add(current);
                }
            }

            var hFile = Cell(grid, row, 1);
            if (hFile.Length > 0 && !files.ContainsKey(current!))
                files[current!] = hFile;

            current!.Columns.Add(Column(grid, row, hFile));
            lastColumnRow = row;
        }

        if (current is not null)
        {
            for (var row = lastColumnRow + 1; row <= max; row++)
            {
                var description = TypeDescription(grid, row);
                if (description.Length == 0)
                    continue;
                descriptions[current] = description;
                break;
            }
        }

        foreach (var table in tables)
        {
            var source = files.TryGetValue(table, out var file) && file.Length > 0 ? file : table.Name;
            table.Note = "來源檔 " + source + "。";
            if (descriptions.TryGetValue(table, out var description) && description.Length > 0)
                table.Note += Polish(description);

            var indexed = table.Columns
                .Select((column, index) => (column, index))
                .Where(item => item.column.UniqueIndex)
                .OrderBy(item => Ordinal(item.column.SOrdinal))
                .ThenBy(item => item.index)
                .Select(item => item.column.Name)
                .ToList();
            if (indexed.Count == 0)
                continue;
            table.Indexes.Add(new AnnotationIndexDto
            {
                Name = "ix_" + table.Name.ToLowerInvariant() + "_append",
                Note = IndexNote,
                Columns = indexed,
            });
        }

        return tables;
    }

    private static AnnotationColumnDto Column(Dictionary<int, Dictionary<int, string>> grid, int row, string sourceFile)
    {
        var hName = Cell(grid, row, 3);
        var sName = Cell(grid, row, 9);
        var hType = Cell(grid, row, 4);
        var description = Cell(grid, row, 5);
        var pii = Cell(grid, row, 6);
        var format = Cell(grid, row, 10);
        var length = Cell(grid, row, 11);
        var parts = Segments(description).Concat(Segments(Cell(grid, row, 13)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (sName.Length > 0 && hName.Length > 0 && !string.Equals(sName, hName, StringComparison.Ordinal))
            parts.Add("H 欄名 " + hName);
        if (format.Length > 0)
            parts.Add("畫面格式 " + format);
        if (LengthDiffers(hType, length))
            parts.Add("S 資料長度 " + length);
        if (pii.Length > 0 && !parts.Contains(pii, StringComparer.Ordinal))
            parts.Add(pii);

        return new AnnotationColumnDto
        {
            Name = sName.Length > 0 ? sName : hName,
            Type = MapType(hType),
            NotNull = !string.Equals(Cell(grid, row, 14), "Y", StringComparison.Ordinal),
            Note = string.Join("；", parts),
            HName = hName,
            SName = sName,
            HType = hType,
            SType = format,
            SLength = length,
            Pii = pii,
            UniqueIndex = string.Equals(Cell(grid, row, 12), "V", StringComparison.Ordinal),
            SourceFile = sourceFile,
            HOrdinal = Cell(grid, row, 2),
            SOrdinal = Cell(grid, row, 8),
        };
    }

    private static string TypeDescription(Dictionary<int, Dictionary<int, string>> grid, int row)
    {
        foreach (var column in Headers.Select((header, index) => (header, index)).Where(item => item.header.Contains('類', StringComparison.Ordinal) && item.header.Contains("型", StringComparison.Ordinal)))
        {
            var text = Cell(grid, row, column.index + 1);
            if (text.Length > 0)
                return text;
        }

        return "";
    }

    private static bool HeaderMatches(Dictionary<int, Dictionary<int, string>> grid)
    {
        if (!grid.TryGetValue(1, out var header))
            return false;
        for (var index = 0; index < Headers.Length; index++)
        {
            if (!string.Equals(Cell(grid, 1, index + 1), Headers[index], StringComparison.Ordinal))
                return false;
        }

        return header.Keys.All(column => column <= Headers.Length || Cell(grid, 1, column).Length == 0);
    }

    private static string MapType(string hType)
    {
        var trimmed = hType.Trim();
        var name = BaseName(trimmed);
        if (name.Equals("STRING", StringComparison.OrdinalIgnoreCase))
            return "text";
        if (name.Equals("DATE", StringComparison.OrdinalIgnoreCase))
            return "date";
        return trimmed.ToLowerInvariant();
    }

    private static bool LengthDiffers(string hType, string sLength)
    {
        if (sLength.Length == 0)
            return false;
        var name = BaseName(hType);
        if (name.Equals("DATE", StringComparison.OrdinalIgnoreCase) || name.Equals("STRING", StringComparison.OrdinalIgnoreCase))
            return false;
        var open = hType.IndexOf('(');
        var close = hType.LastIndexOf(')');
        if (open < 0 || close <= open)
            return false;
        var inside = hType[(open + 1)..close].Trim();
        if (decimal.TryParse(inside, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected)
            && decimal.TryParse(sLength, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual))
            return expected != actual;
        return !string.Equals(inside, sLength, StringComparison.Ordinal);
    }

    private static string BaseName(string hType)
    {
        var trimmed = hType.Trim();
        var open = trimmed.IndexOf('(');
        return open < 0 ? trimmed : trimmed[..open].Trim();
    }

    private static IEnumerable<string> Segments(string text)
    {
        foreach (var part in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    private static string Polish(string text)
    {
        var spaced = new StringBuilder();
        char? previous = null;
        foreach (var character in text.Replace(',', '，'))
        {
            if (previous is char prior && Gap(prior, character))
                spaced.Append(' ');
            spaced.Append(character);
            previous = character;
        }

        var result = spaced.ToString().Trim();
        if (result.Length > 0 && !result.EndsWith('。'))
            result += "。";
        return result;
    }

    private static bool Gap(char left, char right)
        => (Han(left) && Latin(right)) || (Latin(left) && Han(right));

    private static bool Han(char character) => character is >= '\u4E00' and <= '\u9FFF';

    private static bool Latin(char character) => character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');

    private static int Ordinal(string text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : int.MaxValue;

    private static string Cell(Dictionary<int, Dictionary<int, string>> grid, int row, int column)
        => grid.TryGetValue(row, out var cells) && cells.TryGetValue(column, out var value) ? value : "";

    private static Dictionary<int, Dictionary<int, string>> Cells(XDocument sheet, IReadOnlyList<string>? shared, HashSet<string> ignored)
    {
        var formulas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var formula in sheet.Descendants().Where(element => element.Name.LocalName == "f"))
        {
            var key = formula.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "si")?.Value;
            if (!string.IsNullOrEmpty(key) && formula.Value.Length > 0)
                formulas[key] = formula.Value;
        }

        var grid = new Dictionary<int, Dictionary<int, string>>();
        var countedRows = new HashSet<int>();
        foreach (var cell in sheet.Descendants().Where(element => element.Name.LocalName == "c"))
        {
            var reference = cell.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "r")?.Value;
            if (string.IsNullOrEmpty(reference) || ignored.Contains(reference) || !TryAddress(reference, out var column, out var row))
                continue;
            var formula = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "f");
            var formulaText = formula?.Value ?? "";
            if (formulaText.Length == 0)
            {
                var key = formula?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "si")?.Value;
                if (!string.IsNullOrEmpty(key) && formulas.TryGetValue(key, out var sharedFormula))
                    formulaText = sharedFormula;
            }

            if (formulaText.Contains("COUNT", StringComparison.OrdinalIgnoreCase))
            {
                countedRows.Add(row);
                continue;
            }

            var text = Value(cell, shared);
            if (text.Length == 0)
                continue;
            if (!grid.TryGetValue(row, out var cells))
            {
                cells = [];
                grid[row] = cells;
            }

            cells[column] = text;
        }

        foreach (var row in countedRows)
            grid.Remove(row);
        return grid;
    }

    private static string Value(XElement cell, IReadOnlyList<string>? shared)
    {
        var kind = cell.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "t")?.Value ?? "";
        if (kind == "inlineStr")
            return Text(cell);

        var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? "";
        if (kind == "s")
        {
            if (shared is null || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= shared.Count)
                throw new FormatException();
            return shared[index].Trim();
        }

        if (kind is "" or "n")
            return NormalizeNumber(raw.Trim());
        return raw.Trim();
    }

    private static string NormalizeNumber(string raw)
    {
        if (raw.Length == 0)
            return "";
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && number == decimal.Truncate(number)
            && raw.IndexOfAny(['e', 'E']) < 0)
            return decimal.Truncate(number).ToString(CultureInfo.InvariantCulture);
        return raw;
    }

    private static string Text(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var node in element.Descendants().Where(item => item.Name.LocalName == "t"))
        {
            if (node.Ancestors().Any(ancestor => ancestor.Name.LocalName == "phoneticPr"))
                continue;
            builder.Append(node.Value);
        }

        return builder.ToString().Trim();
    }

    private static HashSet<string> IgnoredCells(XDocument sheet)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in sheet.Descendants().Where(element => element.Name.LocalName == "hyperlink"))
        {
            var location = link.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "location")?.Value ?? "";
            var external = link.Attributes().Any(attribute => attribute.Name.LocalName == "id");
            if (!location.Contains('!', StringComparison.Ordinal) && !external)
                continue;
            foreach (var reference in Expand(link.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "ref")?.Value))
                ignored.Add(reference);
        }

        return ignored;
    }

    private static IEnumerable<string> Expand(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            yield break;
        var parts = reference.Split(':');
        if (parts.Length == 1 || !TryAddress(parts[0], out var left, out var top) || !TryAddress(parts[1], out var right, out var bottom))
        {
            yield return parts[0];
            yield break;
        }

        var width = Math.Abs((long)right - left) + 1;
        var height = Math.Abs((long)bottom - top) + 1;
        if (width * height > 4096)
            throw new InvalidDataException();

        for (var row = Math.Min(top, bottom); row <= Math.Max(top, bottom); row++)
        {
            for (var column = Math.Min(left, right); column <= Math.Max(left, right); column++)
                yield return ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string ColumnName(int column)
    {
        var name = "";
        while (column > 0)
        {
            column--;
            name = (char)('A' + (column % 26)) + name;
            column /= 26;
        }

        return name;
    }

    private static bool TryAddress(string reference, out int column, out int row)
    {
        column = 0;
        row = 0;
        var index = 0;
        var value = 0L;
        while (index < reference.Length && char.IsLetter(reference[index]))
        {
            if (value > (long.MaxValue - 26) / 26)
                throw new InvalidDataException();
            value = (value * 26) + (char.ToUpperInvariant(reference[index]) - 'A' + 1);
            if (value > 16384)
                throw new InvalidDataException();
            index++;
        }

        column = (int)value;
        if (column == 0 || index == reference.Length)
            return false;
        return int.TryParse(reference[index..], NumberStyles.Integer, CultureInfo.InvariantCulture, out row) && row > 0;
    }

    private static IReadOnlyList<string>? SharedStrings(ZipArchive zip)
    {
        var document = Load(zip, "xl/sharedStrings.xml");
        if (document is null)
            return null;
        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(Text)
            .ToList();
    }

    private static string? SheetPath(ZipArchive zip, XElement sheet)
    {
        var id = sheet.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value;
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        var target = rels?.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Relationship" && element.Attribute("Id")?.Value == id)
            ?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
            return null;
        var path = target.Replace('\\', '/');
        if (path.StartsWith('/'))
            return path.TrimStart('/');
        if (path.StartsWith("xl/", StringComparison.Ordinal))
            return path;
        return "xl/" + path.TrimStart('/');
    }

    private static XDocument? Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? zip.GetEntry(path.Replace('/', '\\'));
        if (entry is null)
            return null;
        if (entry.Length > 8L * 1024 * 1024)
            throw new InvalidDataException();
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
