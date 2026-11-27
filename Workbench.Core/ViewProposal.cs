using Workbench.Contracts;

namespace Workbench.Core;

public interface IViewProposer
{
    Task<ViewProposalResult> ProposeAsync(ViewProposalRequest request, CancellationToken cancellationToken);
}

public sealed class ViewProposalResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public ViewProposalDto? Proposal { get; init; }

    public static ViewProposalResult Success(ViewProposalDto proposal) => new() { Ok = true, Proposal = proposal };

    public static ViewProposalResult Fail(string code, string message) => new() { Code = code, Message = message };
}

/// <summary>
/// 固定提案，測試與尚未接上模型時使用。不改註記包。
/// </summary>
public sealed class FixedViewProposer : IViewProposer
{
    public ViewProposalDto? Script { get; set; }

    public Task<ViewProposalResult> ProposeAsync(ViewProposalRequest request, CancellationToken cancellationToken)
    {
        if (Script is null)
            return Task.FromResult(ViewProposalResult.Fail("proposal_rejected", "沒有提案。"));
        return Task.FromResult(ViewProposal.Gate(Script, request.Package));
    }
}

public static class ViewProposal
{
    public static ViewProposalResult Gate(ViewProposalDto? proposal, AnnotationPackageDto? package)
    {
        if (package is not { Active: true } || string.Equals(package.Status, "draft", StringComparison.Ordinal))
            return ViewProposalResult.Fail("proposal_no_active", "沒有啟用中的註記包。");
        var sources = proposal?.Sources ?? [];
        if (proposal is null || sources.Count == 0)
            return ViewProposalResult.Fail("proposal_rejected", "沒有提案。");

        var tables = new Dictionary<string, AnnotationTableDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in package.Tables ?? [])
        {
            var name = table.Name?.Trim() ?? "";
            if (name.Length > 0)
                tables[name] = table;
        }

        foreach (var source in sources)
        {
            var name = source?.Trim() ?? "";
            if (!tables.ContainsKey(name))
                return ViewProposalResult.Fail("proposal_outside", "提案用了註記包以外的表或欄。");
            if (!proposal.Reasons.TryGetValue(name, out var reason) || string.IsNullOrWhiteSpace(reason))
                return ViewProposalResult.Fail("proposal_rejected", "要說明為什麼選這張表。");
        }

        foreach (var column in proposal.Columns ?? [])
        {
            if (!ColumnInside(tables, column.Source, column.Name))
                return ViewProposalResult.Fail("proposal_outside", "提案用了註記包以外的表或欄。");
        }

        foreach (var link in proposal.Links ?? [])
        {
            if (!ColumnInside(tables, link.From, link.FromColumn) || !ColumnInside(tables, link.To, link.ToColumn))
                return ViewProposalResult.Fail("proposal_outside", "提案用了註記包以外的表或欄。");
            var declared = Declared(package, link);
            if (declared is null)
            {
                if (!link.ViewOnly || string.IsNullOrWhiteSpace(link.Reason))
                    return ViewProposalResult.Fail("link_reason_required", "僅此檢視的關聯要寫理由。");
                link.Kind = "accepted";
                continue;
            }

            link.ViewOnly = false;
            link.Kind = declared == "foreign-key" ? "foreign-key" : "accepted";
            if (string.IsNullOrWhiteSpace(link.Reason))
                return ViewProposalResult.Fail("proposal_rejected", "要說明這段關聯為什麼入選。");
        }

        return ViewProposalResult.Success(proposal);
    }

    private static bool ColumnInside(Dictionary<string, AnnotationTableDto> tables, string? table, string? column)
    {
        var tableName = table?.Trim() ?? "";
        var columnName = column?.Trim() ?? "";
        if (!tables.TryGetValue(tableName, out var found))
            return false;
        return (found.Columns ?? []).Any(item => string.Equals(item.Name?.Trim(), columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Declared(AnnotationPackageDto package, ViewLinkDto link)
    {
        foreach (var item in package.Links ?? [])
        {
            var origin = item.Origin?.Trim() ?? "";
            if (origin is not ("foreign-key" or "declared"))
                continue;
            var straight = Same(link.From, item.From) && Same(link.FromColumn, item.FromColumn) && Same(link.To, item.To) && Same(link.ToColumn, item.ToColumn);
            var swapped = Same(link.From, item.To) && Same(link.FromColumn, item.ToColumn) && Same(link.To, item.From) && Same(link.ToColumn, item.FromColumn);
            if (straight || swapped)
                return origin;
        }

        return null;
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
