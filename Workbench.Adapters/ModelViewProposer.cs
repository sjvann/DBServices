using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

/// <summary>
/// 把啟用註記包與分析師的描述送給已設定的模型，再交給提案閘門。
/// </summary>
public sealed class ModelViewProposer : IViewProposer
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IModelProfile _profiles;

    public ModelViewProposer(IModelProfile profiles) => _profiles = profiles;

    public async Task<ViewProposalResult> ProposeAsync(ViewProposalRequest request, CancellationToken cancellationToken)
    {
        var status = _profiles.Read();
        if (!status.Ready || string.IsNullOrWhiteSpace(status.ModelId))
            return ViewProposalResult.Fail("proposal_rejected", "模型尚未設定。");

        var endpoint = string.IsNullOrWhiteSpace(status.Endpoint) ? OllamaCatalog.Address : status.Endpoint.Trim();
        var key = _profiles.ResolveKey(null);
        var payload = JsonSerializer.Serialize(new
        {
            model = status.ModelId,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = "只回傳一個 JSON 物件，不要解釋。鍵使用 sources、links、columns、filters、limit、reasons。links 每段含 from、fromColumn、to、toColumn、join、viewOnly、reason。表和欄只能來自提供的註記。每張表與每段關聯都要有理由。" },
                new { role = "user", content = Prompt(request) },
            },
        });

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var message = new HttpRequestMessage(HttpMethod.Post, ChatUrl(endpoint));
            if (!string.IsNullOrEmpty(key))
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            message.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ViewProposalResult.Fail("proposal_rejected", "提案沒有完成。");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = ReadContent(body);
            var json = Slice(text);
            if (json.Length == 0)
                return ViewProposalResult.Fail("proposal_rejected", "提案無法讀取。");
            var proposal = JsonSerializer.Deserialize<ViewProposalDto>(json, Json);
            return ViewProposal.Gate(proposal, request.Package);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ViewProposalResult.Fail("proposal_rejected", "提案沒有完成。");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            return ViewProposalResult.Fail("proposal_rejected", "提案沒有完成。");
        }
    }

    private static string Prompt(ViewProposalRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("想看的內容：").Append(request.Ask?.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(request.Grain))
            builder.Append("一列代表什麼：").Append(request.Grain.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(request.Wanted))
            builder.Append("希望出現的欄位或概念：").Append(request.Wanted.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(request.Filter))
            builder.Append("篩選：").Append(request.Filter.Trim()).Append('\n');
        if (!string.IsNullOrWhiteSpace(request.Limit))
            builder.Append("筆數：").Append(request.Limit.Trim()).Append('\n');
        builder.Append("註記：\n");
        foreach (var table in request.Package?.Tables ?? [])
        {
            builder.Append(table.Name).Append(' ').Append(table.Note).Append('\n');
            foreach (var column in table.Columns ?? [])
                builder.Append("  ").Append(column.Name).Append(' ').Append(column.Type).Append(' ').Append(column.Note).Append('\n');
        }

        foreach (var link in request.Package?.Links ?? [])
        {
            var origin = link.Origin?.Trim() ?? "";
            if (origin is not ("foreign-key" or "declared"))
                continue;
            builder.Append("關聯 ").Append(link.From).Append('.').Append(link.FromColumn)
                .Append(" -> ").Append(link.To).Append('.').Append(link.ToColumn).Append('\n');
        }

        return builder.ToString();
    }

    private static string ChatUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/models".Length].TrimEnd('/');
        return trimmed + "/chat/completions";
    }

    private static string ReadContent(string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (document.RootElement.TryGetProperty("choices", out var choices))
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                    return content.GetString() ?? "";
            }
        }

        if (document.RootElement.TryGetProperty("message", out var direct)
            && direct.TryGetProperty("content", out var text)
            && text.ValueKind == JsonValueKind.String)
            return text.GetString() ?? "";
        return body;
    }

    private static string Slice(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : "";
    }
}
