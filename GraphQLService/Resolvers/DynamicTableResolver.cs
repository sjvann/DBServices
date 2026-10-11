using DbServices.Api.Shared;
using GraphQLService.Schema;
using GraphQLService.Services;
using HotChocolate;
using HotChocolate.Resolvers;
using System.Collections;

namespace GraphQLService.Resolvers;

public sealed class DynamicTableResolver
{
    private readonly DbConnectionSession _session;

    public DynamicTableResolver(DbConnectionSession session)
    {
        _session = session;
    }

    private DbTableOperations Operations =>
        _session.Operations ?? throw new GraphQLException("尚未連線資料庫。請先在操作介面選擇資料庫並連線。");

    public async Task<MetaPayload> GetMetaAsync(CancellationToken cancellationToken = default)
    {
        var tables = await Operations.GetAllTableNamesAsync(cancellationToken);
        return new MetaPayload
        {
            Tables = tables ?? Array.Empty<string>()
        };
    }

    public async Task<IReadOnlyList<string>> GetFieldValueSetAsync(
        string table,
        string field,
        CancellationToken cancellationToken = default)
    {
        var values = await Operations.GetValueSetAsync(table, field, cancellationToken);
        return values?.Select(v => v?.ToString() ?? string.Empty).ToList() ?? [];
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetRecordsAsync(
        TableDescriptor table,
        CancellationToken cancellationToken = default)
    {
        var result = await Operations.GetRecordsAsync(table.TableName, cancellationToken: cancellationToken);
        return RecordMapper.ToRecordDictionaries(result);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> GetRecordsByFieldAsync(
        TableDescriptor table,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        var result = await Operations.GetRecordsAsync(table.TableName, key, value, cancellationToken);
        return RecordMapper.ToRecordDictionaries(result);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetRecordByIdAsync(
        TableDescriptor table,
        int id,
        CancellationToken cancellationToken = default)
    {
        var result = await Operations.GetRecordByIdAsync(table.TableName, id, cancellationToken);
        return RecordMapper.ToRecordDictionaries(result).FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<string, object?>?> CreateRecordAsync(
        TableDescriptor table,
        IResolverContext context,
        CancellationToken cancellationToken = default)
    {
        var input = ParseInput(context, "input");
        var created = await Operations.InsertAsync(table.TableName, input, cancellationToken);
        return RecordMapper.ToRecordDictionaries(created).FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<string, object?>?> UpdateRecordAsync(
        TableDescriptor table,
        int id,
        IResolverContext context,
        CancellationToken cancellationToken = default)
    {
        var input = ParseInput(context, "input");
        var updated = await Operations.UpdateAsync(table.TableName, id, input, cancellationToken);
        return RecordMapper.ToRecordDictionaries(updated).FirstOrDefault();
    }

    public async Task<bool> DeleteRecordAsync(
        TableDescriptor table,
        int id,
        CancellationToken cancellationToken = default)
    {
        return await Operations.DeleteAsync(table.TableName, id, cancellationToken);
    }

    private static IReadOnlyDictionary<string, object?> ParseInput(IResolverContext context, string argumentName)
    {
        if (context.ArgumentValue<IReadOnlyDictionary<string, object?>>(argumentName) is { } dict1)
            return dict1;

        if (context.ArgumentValue<Dictionary<string, object?>>(argumentName) is { } dict2)
            return dict2;

        if (context.ArgumentValue<IReadOnlyDictionary<string, object>>(argumentName) is { } dict3)
            return dict3.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

        var raw = context.ArgumentValue<object?>(argumentName);
        if (raw is IReadOnlyDictionary<string, object?> ro)
            return ro;

        if (raw is IDictionary dict)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is string key)
                    result[key] = entry.Value;
            }
            return result;
        }

        throw new GraphQLException($"無法解析參數 {argumentName}。");
    }
}
