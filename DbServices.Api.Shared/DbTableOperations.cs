using DbServices.Core.Exceptions;
using DbServices.Core.Models;
using DbServices.Core.Models.Interface;
using System.Text;
using System.Text.Json.Nodes;

namespace DbServices.Api.Shared;

/// <summary>
/// 封裝資料表 CRUD 與 metadata 操作，供 REST / GraphQL 共用。
/// </summary>
public sealed class DbTableOperations
{
    private readonly DbServiceHolder _holder;

    public DbTableOperations(DbServiceHolder holder)
    {
        _holder = holder;
    }

    private IDbService Db => _holder.Db;

    public async Task<string[]?> GetAllTableNamesAsync(CancellationToken cancellationToken = default)
    {
        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.GetAllTableNamesAsync(cancellationToken: cancellationToken);

        return Db.GetAllTableNames();
    }

    public async Task<IEnumerable<FieldBaseModel>?> GetFieldsAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.GetFieldsByTableNameAsync(tableName, cancellationToken);

        return Db.GetFieldsByTableName(tableName);
    }

    public async Task<IEnumerable<object>?> GetValueSetAsync(
        string tableName,
        string fieldName,
        CancellationToken cancellationToken = default)
    {
        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.GetValueSetByFieldNameAsync(fieldName, tableName, cancellationToken);

        return Db.GetValueSetByFieldName(fieldName, tableName);
    }

    public async Task<TableBaseModel?> GetRecordsAsync(
        string tableName,
        string? key = null,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
        {
            var kp = new KeyValuePair<string, object?>(key, value);
            if (Db is IDbServiceAsync asyncDb)
                return await asyncDb.GetRecordByKeyValueAsync(kp, tableName: tableName, cancellationToken: cancellationToken);

            return Db.GetRecordByKeyValue(kp, null, tableName);
        }

        return Db.GetRecordByTableName(tableName);
    }

    public async Task<TableBaseModel?> GetRecordByIdAsync(
        string tableName,
        long id,
        CancellationToken cancellationToken = default)
    {
        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.GetRecordByIdAsync(id, tableName, cancellationToken);

        return Db.GetRecordById(id, tableName);
    }

    public async Task<TableBaseModel?> GetMetadataAsync(string tableName)
    {
        var result = Db.GetRecordByTableName(tableName);
        return await Task.FromResult(result);
    }

    public async Task<TableBaseModel?> InsertAsync(
        string tableName,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var kps = RecordMapper.ParseInput(input).ToList();

        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.InsertRecordAsync(kps, tableName, cancellationToken);

        return Db.InsertRecord(kps, tableName);
    }

    public async Task<TableBaseModel?> InsertFromJsonAsync(
        string tableName,
        JsonObject payload,
        CancellationToken cancellationToken = default)
    {
        var kps = RecordMapper.ParseJsonObject(payload).ToList();

        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.InsertRecordAsync(kps, tableName, cancellationToken);

        Db.SetCurrentTableName(tableName);
        return Db.InsertRecord(kps);
    }

    public async Task<TableBaseModel?> UpdateAsync(
        string tableName,
        long id,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetRecordByIdAsync(tableName, id, cancellationToken);
        if (existing is null)
            return null;

        var kps = RecordMapper.ParseInput(input).ToList();

        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.UpdateRecordByIdAsync(id, kps, tableName, cancellationToken);

        Db.SetCurrentTableName(tableName);
        return Db.UpdateRecordById(id, kps);
    }

    public async Task<TableBaseModel?> UpdateFromJsonAsync(
        string tableName,
        long id,
        JsonObject payload,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetRecordByIdAsync(tableName, id, cancellationToken);
        if (existing is null)
            return null;

        var kps = RecordMapper.ParseJsonObject(payload).ToList();

        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.UpdateRecordByIdAsync(id, kps, tableName, cancellationToken);

        Db.SetCurrentTableName(tableName);
        return Db.UpdateRecordById(id, kps);
    }

    public async Task<bool> DeleteAsync(
        string tableName,
        long id,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetRecordByIdAsync(tableName, id, cancellationToken);
        if (existing is null)
            return false;

        if (Db is IDbServiceAsync asyncDb)
            return await asyncDb.DeleteRecordByIdAsync(id, tableName, cancellationToken);

        Db.SetCurrentTableName(tableName);
        return Db.DeleteRecordById(id);
    }

    public static JsonObject? ParseJsonPayload(object payload)
    {
        string json = payload switch
        {
            JsonObject jo => jo.ToJsonString(),
            System.Text.Json.JsonElement je => je.GetRawText(),
            _ => payload.ToString() ?? "{}"
        };

        MemoryStream ms = new(Encoding.UTF8.GetBytes(json));
        var result = JsonNode.Parse(ms);
        return result as JsonObject;
    }
}
