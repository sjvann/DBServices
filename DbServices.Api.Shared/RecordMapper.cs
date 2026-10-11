using DbServices.Core.Models;
using System.Text.Json.Nodes;

namespace DbServices.Api.Shared;

/// <summary>
/// 將 <see cref="TableBaseModel"/> / <see cref="RecordBaseModel"/> 轉為 API 友善結構。
/// </summary>
public static class RecordMapper
{
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> ToRecordDictionaries(TableBaseModel? table)
    {
        if (table?.Records is null)
            return Array.Empty<IReadOnlyDictionary<string, object?>>();

        return table.Records
            .Select(ToRecordDictionary)
            .ToList();
    }

    public static IReadOnlyDictionary<string, object?> ToRecordDictionary(RecordBaseModel record)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = record.Id
        };

        if (record.FieldValue is not null)
        {
            foreach (var kv in record.FieldValue)
                dict[kv.Key] = kv.Value;
        }

        return dict;
    }

    public static JsonObject? ToRecordsJsonObject(TableBaseModel? table)
        => table?.GetRecordsJsonObject();

    public static JsonObject? ToMetaJsonObject(TableBaseModel? table)
        => table?.GetMetaJsonObject();

    public static IEnumerable<KeyValuePair<string, object?>> ParseInput(IReadOnlyDictionary<string, object?> input)
    {
        foreach (var kv in input)
        {
            if (string.Equals(kv.Key, "id", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return new KeyValuePair<string, object?>(kv.Key, kv.Value);
        }
    }

    public static IEnumerable<KeyValuePair<string, object?>> ParseJsonObject(JsonObject json)
        => json.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
}
