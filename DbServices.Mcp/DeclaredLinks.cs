namespace DbServices.Mcp;

/// <summary>
/// 兩張以上來源表時，每一段都要對上啟用包裡的已聲明關聯。對不上就不改檢視檔。
/// </summary>
public static class DeclaredLinks
{
    public const string Code = "link_undeclared";
    public const string Message = "這段關聯不在註記包裡。";

    public static void TrimEnds(ViewDocument document)
    {
        foreach (var link in document.Links ?? [])
        {
            if (link is null)
                continue;
            link.From = link.From?.Trim() ?? "";
            link.FromColumn = link.FromColumn?.Trim() ?? "";
            link.To = link.To?.Trim() ?? "";
            link.ToColumn = link.ToColumn?.Trim() ?? "";
        }
    }

    public static bool Applies(ViewDocument document)
    {
        var sources = (document.Sources ?? [])
            .Select(source => source?.Trim() ?? "")
            .Where(source => source.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return sources > 1 && (document.Links ?? []).Count > 0;
    }

    public static bool TryPrepare(ViewDocument document, AnnotationPackage? package)
    {
        var sources = (document.Sources ?? [])
            .Select(source => source?.Trim() ?? "")
            .Where(source => source.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var links = document.Links ?? [];
        if (sources <= 1 || links.Count == 0)
            return true;

        var declared = Eligible(package);
        var kinds = new string[links.Count];
        for (var index = 0; index < links.Count; index++)
        {
            var origin = Match(links[index], declared);
            if (origin is null)
                return false;
            kinds[index] = origin == "foreign-key" ? "foreign-key" : "accepted";
        }

        for (var index = 0; index < links.Count; index++)
            links[index].Kind = kinds[index];
        return true;
    }

    private static List<Declared> Eligible(AnnotationPackage? package)
    {
        var declared = new List<Declared>();
        foreach (var link in package?.Links ?? [])
        {
            if (link is null)
                continue;
            var origin = link.Origin?.Trim() ?? "";
            if (origin is not ("foreign-key" or "declared"))
                continue;
            var from = link.From?.Trim() ?? "";
            var fromColumn = link.FromColumn?.Trim() ?? "";
            var to = link.To?.Trim() ?? "";
            var toColumn = link.ToColumn?.Trim() ?? "";
            if (from.Length == 0 || fromColumn.Length == 0 || to.Length == 0 || toColumn.Length == 0)
                continue;
            declared.Add(new Declared(from, fromColumn, to, toColumn, origin));
        }

        return declared;
    }

    private static string? Match(ViewLink? link, List<Declared> declared)
    {
        if (link is null)
            return null;
        var from = link.From?.Trim() ?? "";
        var fromColumn = link.FromColumn?.Trim() ?? "";
        var to = link.To?.Trim() ?? "";
        var toColumn = link.ToColumn?.Trim() ?? "";
        foreach (var item in declared)
        {
            var straight = string.Equals(from, item.From, StringComparison.Ordinal)
                && string.Equals(fromColumn, item.FromColumn, StringComparison.Ordinal)
                && string.Equals(to, item.To, StringComparison.Ordinal)
                && string.Equals(toColumn, item.ToColumn, StringComparison.Ordinal);
            var swapped = string.Equals(from, item.To, StringComparison.Ordinal)
                && string.Equals(fromColumn, item.ToColumn, StringComparison.Ordinal)
                && string.Equals(to, item.From, StringComparison.Ordinal)
                && string.Equals(toColumn, item.FromColumn, StringComparison.Ordinal);
            if (straight || swapped)
                return item.Origin;
        }

        return null;
    }

    private sealed record Declared(string From, string FromColumn, string To, string ToColumn, string Origin);
}
