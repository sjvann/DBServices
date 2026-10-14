using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Workbench.Contracts;

var stdin = await Console.In.ReadToEndAsync();
JsonElement root;
try
{
    using var document = JsonDocument.Parse(stdin);
    if (document.RootElement.ValueKind != JsonValueKind.Object)
    {
        Write(new { code = ModelCodes.Unreachable });
        return;
    }

    root = document.RootElement.Clone();
}
catch (JsonException)
{
    Write(new { code = ModelCodes.Unreachable });
    return;
}

var kind = root.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String
    ? kindElement.GetString()
    : "";
if (kind is not ("probe" or "parse"))
{
    Write(new { code = ModelCodes.Unreachable });
    return;
}

var endpoint = Read(root, "endpoint");
var modelId = Read(root, "modelId");
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(modelId))
{
    Write(new { code = ModelCodes.InvalidSettings });
    return;
}

var apiKey = Environment.GetEnvironmentVariable("DBSERVICES_MODEL_API_KEY");
if (string.IsNullOrEmpty(apiKey))
    apiKey = "local";

try
{
    var credential = new System.ClientModel.ApiKeyCredential(apiKey);
    var options = new OpenAI.OpenAIClientOptions { Endpoint = uri };
    var chat = new ChatClient(modelId, credential, options);
    IChatClient client = chat.AsIChatClient();
    var prompt = kind == "probe"
        ? "ping"
        : "只回傳一個 JSON 物件，鍵只有 candidates。每個候選只有 connection、templateId、parameters。不要輸出 SQL 或規則命令。輸入：" + root.GetRawText();
    var response = await client.GetResponseAsync([new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt)]);
    var text = response.Text ?? "";
    if (kind == "probe")
    {
        Write(new { code = ModelCodes.Ok });
        return;
    }

    var parsed = ExtractObject(text);
    if (parsed is null)
    {
        Write(new { code = ModelCodes.Unreachable });
        return;
    }

    Console.Write(parsed);
}
catch (Exception exception)
{
    var detail = exception.Message;
    if (detail.Contains(apiKey, StringComparison.Ordinal))
        detail = "無法連線模型。";
    Console.Error.WriteLine(detail);
    Write(new { code = ModelCodes.Unreachable });
}

static string Read(JsonElement element, string name)
    => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
        ? property.GetString() ?? ""
        : "";

static string? ExtractObject(string text)
{
    var start = text.IndexOf('{');
    var end = text.LastIndexOf('}');
    if (start < 0 || end <= start)
        return null;
    var slice = text[start..(end + 1)];
    try
    {
        using var document = JsonDocument.Parse(slice);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return null;
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            return null;
        return slice;
    }
    catch (JsonException)
    {
        return null;
    }
}

static void Write(object payload) => Console.Write(JsonSerializer.Serialize(payload));
