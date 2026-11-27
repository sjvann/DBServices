using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Workbench.Contracts;
using Workbench.Core;
using Workbench.Core.Ports;

namespace Workbench.Adapters;

/// <summary>
/// 模型先用工具讀啟用註記包，再交提案閘門。不會呼叫工具時，改送表名，再補它點名的欄位。
/// </summary>
public sealed class ModelViewProposer : IViewProposer
{
    private const int MaxRounds = 6;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions Wire = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IModelProfile _profiles;
    private readonly HttpMessageHandler? _handler;

    public ModelViewProposer(IModelProfile profiles, HttpMessageHandler? handler = null)
    {
        _profiles = profiles;
        _handler = handler;
    }

    public async Task<ViewProposalResult> ProposeAsync(ViewProposalRequest request, CancellationToken cancellationToken)
    {
        var status = _profiles.Read();
        if (!status.Ready || string.IsNullOrWhiteSpace(status.ModelId))
            return ViewProposalResult.Fail("proposal_rejected", "模型尚未設定。");

        var endpoint = string.IsNullOrWhiteSpace(status.Endpoint) ? OllamaCatalog.Address : status.Endpoint.Trim();
        var key = _profiles.ResolveKey(null);
        var messages = new JsonArray
        {
            Message("system", SystemPrompt),
            Message("user", Ask(request)),
        };
        var sentIndex = false;
        var sentColumns = false;
        var sawList = false;
        var sawDetails = false;

        try
        {
            using var client = Open();
            for (var round = 0; round < MaxRounds; round++)
            {
                var body = await CompleteAsync(client, endpoint, status.ModelId, key, messages, cancellationToken);
                var message = ReadAssistant(body);
                if (message is null)
                    return ViewProposalResult.Fail("proposal_rejected", "提案無法讀取。");

                var calls = ReadCalls(message);
                if (calls.Count > 0)
                {
                    if (message["content"] is null || message["content"]!.GetValueKind() == JsonValueKind.Null)
                        message["content"] = "";
                    messages.Add(message);
                    foreach (var call in calls)
                    {
                        var text = Dispatch(call.Name, call.Arguments, request, ref sawList, ref sawDetails);
                        messages.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = text,
                        });
                    }

                    continue;
                }

                var content = ContentOf(message);
                var proposal = TryProposal(content);
                if (proposal is not null && (sawDetails || sentColumns))
                    return ViewProposal.Gate(proposal, request.Package);

                if (!sentIndex && !sawDetails && !sawList)
                {
                    sentIndex = true;
                    Remember(messages, message);
                    messages.Add(Message("user", "這次範圍裡的表名與註解：\n" + AnnotationProposalTools.ListTables(request.Package, request.Scope)));
                    continue;
                }

                if (!sentColumns && !sawDetails && proposal is { Sources.Count: > 0 })
                {
                    sentColumns = true;
                    Remember(messages, message);
                    var names = string.Join(',', proposal.Sources);
                    var details = AnnotationProposalTools.TableDetails(request.Package, request.Scope, names, includeLinks: true);
                    messages.Add(Message("user", "你點名的表的欄位與連接：\n" + details + "\n請依這些欄位完成 JSON。"));
                    continue;
                }

                if (proposal is not null)
                    return ViewProposal.Gate(proposal, request.Package);
                return ViewProposalResult.Fail("proposal_rejected", "提案無法讀取。");
            }

            return ViewProposalResult.Fail("proposal_rejected", "提案沒有完成。");
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

    private HttpClient Open()
    {
        var client = _handler is null ? new HttpClient() : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static async Task<string> CompleteAsync(HttpClient client, string endpoint, string modelId, string key, JsonArray messages, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["model"] = modelId,
            ["temperature"] = 0,
            ["messages"] = messages.DeepClone(),
            ["tools"] = Tools(),
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, ChatUrl(endpoint));
        if (!string.IsNullOrEmpty(key))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        message.Content = new StringContent(payload.ToJsonString(Wire), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("提案沒有完成。");
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string Dispatch(string name, string arguments, ViewProposalRequest request, ref bool sawList, ref bool sawDetails)
    {
        if (string.Equals(name, "listTables", StringComparison.Ordinal))
        {
            sawList = true;
            return AnnotationProposalTools.ListTables(request.Package, request.Scope);
        }

        if (string.Equals(name, "getTableDetails", StringComparison.Ordinal))
        {
            sawDetails = true;
            var (tables, links) = ReadDetailArgs(arguments);
            return AnnotationProposalTools.TableDetails(request.Package, request.Scope, tables, links);
        }

        return "沒有這個工具。";
    }

    private static (string Tables, bool Links) ReadDetailArgs(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return ("", false);
        try
        {
            using var document = JsonDocument.Parse(arguments);
            var root = document.RootElement;
            var tables = root.TryGetProperty("tableNames", out var names) && names.ValueKind == JsonValueKind.String
                ? names.GetString() ?? ""
                : "";
            var links = false;
            if (root.TryGetProperty("includeLinks", out var flag))
            {
                links = flag.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => string.Equals(flag.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                    _ => false,
                };
            }

            return (tables, links);
        }
        catch (JsonException)
        {
            return ("", false);
        }
    }

    private static void Remember(JsonArray messages, JsonObject message)
    {
        if (ContentOf(message).Length > 0)
            messages.Add(message);
    }

    private static JsonObject Message(string role, string content) => new()
    {
        ["role"] = role,
        ["content"] = content,
    };

    private const string SystemPrompt =
        "你是模組設計助理。用繁體中文思考後，只回傳一個 JSON 物件，不要解釋。" +
        "鍵使用 sources、links、columns、filters、limit、reasons。" +
        "links 每段含 from、fromColumn、to、toColumn、join、viewOnly、reason。" +
        "表和欄只能來自工具回傳的註記。每張表與每段連接都要有理由。" +
        "需要表名時呼叫 listTables。需要欄位時呼叫 getTableDetails。需要表與表的連接時把 includeLinks 設為 true。不准發明註記裡沒有的表或欄。";

    private static string Ask(ViewProposalRequest request)
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
        var title = request.Package?.Title?.Trim();
        if (!string.IsNullOrEmpty(title))
            builder.Append("啟用包：").Append(title).Append('\n');
        return builder.ToString();
    }

    private static JsonArray Tools() => new(
        Tool("listTables", "列出這次範圍內的資料表名稱與表註解。", new JsonObject()),
        Tool("getTableDetails", "取得指定資料表的欄位、型別與欄註解。includeLinks 為 true 時才附上這些表之間已聲明的連接。", new JsonObject
        {
            ["tableNames"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "以逗號分隔的表名",
            },
            ["includeLinks"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "是否附上已聲明的連接。預設 false。",
            },
        }, "tableNames"));

    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required)
    {
        var parameters = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
        };
        if (required.Length > 0)
            parameters["required"] = new JsonArray(required.Select(item => JsonValue.Create(item)!).ToArray());
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = parameters,
            },
        };
    }

    private static string ChatUrl(string endpoint)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^"/models".Length].TrimEnd('/');
        return trimmed + "/chat/completions";
    }

    private static JsonObject? ReadAssistant(string body)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (!document.RootElement.TryGetProperty("choices", out var choices))
            return null;
        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message))
                return JsonNode.Parse(message.GetRawText()) as JsonObject;
        }

        return null;
    }

    private sealed record ToolCall(string Id, string Name, string Arguments);

    private static List<ToolCall> ReadCalls(JsonObject message)
    {
        if (message["tool_calls"] is not JsonArray calls)
            return [];
        var list = new List<ToolCall>();
        var index = 0;
        foreach (var call in calls)
        {
            var function = call?["function"];
            var name = function?["name"]?.GetValue<string>() ?? "";
            if (name.Length == 0)
                continue;
            var id = call?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
                id = "call-" + index;
            list.Add(new ToolCall(id, name, ArgumentsOf(function)));
            index++;
        }

        return list;
    }

    private static string ArgumentsOf(JsonNode? function)
    {
        var arguments = function?["arguments"];
        if (arguments is null)
            return "";
        if (arguments is JsonValue value && value.TryGetValue<string>(out var text))
            return text ?? "";
        return arguments.ToJsonString();
    }

    private static string ContentOf(JsonObject message)
    {
        if (message["content"] is JsonValue value && value.TryGetValue<string>(out var text))
            return text ?? "";
        return "";
    }

    private static ViewProposalDto? TryProposal(string content)
    {
        var json = Slice(content);
        if (json.Length == 0)
            return null;
        try
        {
            return JsonSerializer.Deserialize<ViewProposalDto>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Slice(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : "";
    }
}
