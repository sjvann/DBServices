using System.Text.Json;
using Workbench.Contracts;

namespace Workbench.Core;

public sealed class MapParseResult
{
    public bool Parsed { get; init; }
    public List<IntentCandidateDto> Candidates { get; init; } = [];

    public static MapParseResult None { get; } = new();
}

public static class MapParse
{
    public static MapParseResult Map(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return MapParseResult.None;

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return MapParseResult.None;

            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
                return MapParseResult.None;

            var list = new List<IntentCandidateDto>();
            foreach (var item in candidates.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return MapParseResult.None;

                var candidate = new IntentCandidateDto
                {
                    Connection = ReadString(item, "connection"),
                    TemplateId = ReadString(item, "templateId"),
                    TemplateName = ReadString(item, "templateName"),
                };
                if (item.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in parameters.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                            candidate.Parameters[property.Name] = property.Value.GetString() ?? "";
                        else if (property.Value.ValueKind == JsonValueKind.Number)
                            candidate.Parameters[property.Name] = property.Value.GetRawText();
                        else
                            return MapParseResult.None;
                    }
                }

                list.Add(candidate);
            }

            return new MapParseResult { Parsed = true, Candidates = list };
        }
        catch (JsonException)
        {
            return MapParseResult.None;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        return property.GetString();
    }
}
