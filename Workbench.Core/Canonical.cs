using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Workbench.Contracts;
using Workbench.Core.Ports;

namespace Workbench.Core;

public static class Scrub
{
    private static readonly Regex Markers = new(
        @"(?i)(data\s*source|server|address|password|pwd|user\s*id|uid|connect\s*string)\s*=\s*[^;,\s""']+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Message(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return Markers.Replace(text, "$1=[已省略]");
    }
}

public static class Canonical
{
    public static string RoleHash(Profile profile)
    {
        var work = profile.WorkRoles.OrderBy(item => item, StringComparer.Ordinal);
        var groups = profile.Groups
            .OrderBy(item => item.GroupId, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .Select(item => item.GroupId + ":" + item.Role);
        var text = (profile.IsAdmin ? "admin" : "user") + "|" + string.Join(",", work) + "|" + string.Join(",", groups);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public static string CsvCell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            text = "'" + text;

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        return text;
    }

    public static string Csv(Snapshot snapshot)
    {
        var lines = new List<string>();
        foreach (var block in snapshot.Blocks)
        {
            if (block.Type == ComponentKinds.Table)
            {
                lines.Add(string.Join(',', block.Columns.Select(CsvCell)));
                foreach (var row in block.Rows)
                {
                    var cells = new string[block.Columns.Count];
                    for (var index = 0; index < cells.Length; index++)
                        cells[index] = CsvCell(index < row.Count ? row[index] : "");
                    lines.Add(string.Join(',', cells));
                }
            }
            else if (block.Type == ComponentKinds.Count)
            {
                lines.Add(CsvCell(block.Count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0"));
            }
            else if (!string.IsNullOrEmpty(block.Text))
            {
                lines.Add(CsvCell(block.Text));
            }
        }

        return string.Join("\r\n", lines);
    }

    public static string PrintHtml(Snapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"zh-Hant\"><head><meta charset=\"utf-8\"><title>");
        builder.Append(System.Net.WebUtility.HtmlEncode("預覽"));
        builder.Append("</title></head><body>");
        foreach (var block in snapshot.Blocks)
        {
            if (block.Type is ComponentKinds.Title or ComponentKinds.Text)
            {
                builder.Append("<p>");
                builder.Append(System.Net.WebUtility.HtmlEncode(block.Text ?? ""));
                builder.Append("</p>");
            }
            else if (block.Type == ComponentKinds.Count)
            {
                builder.Append("<p>");
                builder.Append(System.Net.WebUtility.HtmlEncode(block.Count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0"));
                builder.Append("</p>");
            }
            else if (block.Type == ComponentKinds.Table)
            {
                builder.Append("<table><thead><tr>");
                foreach (var column in block.Columns)
                {
                    builder.Append("<th>");
                    builder.Append(System.Net.WebUtility.HtmlEncode(column));
                    builder.Append("</th>");
                }

                builder.Append("</tr></thead><tbody>");
                foreach (var row in block.Rows)
                {
                    builder.Append("<tr>");
                    for (var index = 0; index < block.Columns.Count; index++)
                    {
                        builder.Append("<td>");
                        builder.Append(System.Net.WebUtility.HtmlEncode(index < row.Count ? row[index] : ""));
                        builder.Append("</td>");
                    }

                    builder.Append("</tr>");
                }

                builder.Append("</tbody></table>");
            }
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    public static bool ArgumentsMatch(IReadOnlyList<ScalarFilter> sent, string argumentsJson, string connection, string table)
    {
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var expected = new HashSet<string>(StringComparer.Ordinal) { "connectionId", "table" };
            if (sent.Count > 0)
                expected.Add("filters");
            if (!names.SetEquals(expected))
                return false;

            if (!document.RootElement.TryGetProperty("connectionId", out var connectionElement)
                || connectionElement.ValueKind != JsonValueKind.String
                || connectionElement.GetString() != connection)
                return false;

            if (!document.RootElement.TryGetProperty("table", out var tableElement)
                || tableElement.ValueKind != JsonValueKind.String
                || tableElement.GetString() != table)
                return false;

            if (sent.Count == 0)
                return true;

            if (!document.RootElement.TryGetProperty("filters", out var filters) || filters.ValueKind != JsonValueKind.Object)
                return false;

            var filterNames = filters.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (!filterNames.SetEquals(sent.Select(item => item.Key)))
                return false;

            foreach (var filter in sent)
            {
                if (!ScalarEquals(filters.GetProperty(filter.Key), filter.Value))
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool ScalarEquals(JsonElement element, object? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return value is string text && element.GetString() == text;
            case JsonValueKind.Number:
                if (value is long or int)
                    return element.TryGetInt64(out var integer) && integer == Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
                if (value is decimal or double or float)
                    return element.TryGetDecimal(out var number) && number == Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
                return false;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return value is bool flag && element.GetBoolean() == flag;
            case JsonValueKind.Null:
                return value is null;
            default:
                return false;
        }
    }

    public static object? CoerceToken(string token)
    {
        if (token.Length > 0 && token.All(char.IsDigit))
            return long.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
        return token;
    }

    public static List<ComponentDto> Normalize(IReadOnlyList<ComponentDto> components)
    {
        var ordered = components.OrderBy(item => item.Order).ToList();
        for (var index = 0; index < ordered.Count; index++)
            ordered[index].Order = index;
        return ordered;
    }
}
