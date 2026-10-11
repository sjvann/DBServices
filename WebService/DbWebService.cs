using DbServices.Api.Shared;
using DbServices.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebService.Models;

namespace WebService
{
    /// <summary>
    /// 微服務
    /// </summary>
    public static class DbWebService
    {
        /// <summary>
        /// 使用Web Service
        /// </summary>
        /// <param name="app">擴增WebApplication</param>
        /// <param name="connectString">資料庫連練字串</param>
        /// <param name="server">資料庫種類</param>
        public static void UseDbWebService(this WebApplication app, string connectString, string server)
        {
            var logger = app.Services
                .GetService<ILoggerFactory>()
                ?.CreateLogger(nameof(DbWebService))
                ?? app.Logger;

            var db = DbServiceFactory.Create(connectString, server);
            if (db is null)
            {
                logger?.LogError("無法初始化資料庫服務，伺服器類型: {ServerType}", server);
                return;
            }

            var holder = new DbServiceHolder();
            holder.Initialize(db);
            var operations = new DbTableOperations(holder);

            logger?.LogInformation("資料庫 Web 服務已啟動，伺服器類型: {ServerType}", server);

            app.MapGet("/meta", () => GetAllTableNameAsync(operations));
            var resourceName = app.MapGroup("/{resourceName}");
            resourceName.MapGet("/fieldSet", async ([FromRoute] string resourceName) => await GetMetaDataAsync(resourceName, operations));
            resourceName.MapGet("/valueSet", async ([FromRoute] string resourceName, [FromQuery] string fieldName) => await GetMetaValueSetAsync(resourceName, fieldName, operations));
            resourceName.MapGet("", async ([FromRoute] string resourceName, [AsParameters] QueryModel query) => await GetDataQueryAsync(resourceName, query, operations));
            resourceName.MapGet("/{id:int}", async (string resourceName, int id) => await GetDataByIdAsync(resourceName, id, operations));

            resourceName.MapPost("", async ([FromRoute] string resourceName, object payload) => await PostDataAsync(resourceName, payload, operations));

            resourceName.MapPut("/{id:int}", async ([FromRoute] string resourceName, [FromRoute] int id, object content) => await PutDataAsync(resourceName, id, content, operations));

            resourceName.MapDelete("/{id:int}", async (string resourceName, int id) => await DeleteDataAsync(resourceName, id, operations));
        }

        public static async Task<IResult> DeleteDataAsync(string resourceName, int id, DbTableOperations operations)
        {
            try
            {
                var deleted = await operations.DeleteAsync(resourceName, id);
                if (!deleted)
                {
                    var existing = await operations.GetRecordByIdAsync(resourceName, id);
                    if (existing is null)
                        return Results.NotFound();
                    return Results.NoContent();
                }
                return Results.Ok();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"刪除記錄時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> PutDataAsync(string resourceName, int id, dynamic payload, DbTableOperations operations)
        {
            try
            {
                var json = DbTableOperations.ParseJsonPayload(payload);
                if (json is null)
                    return Results.Problem("Not a Json Content");

                var updated = await operations.UpdateFromJsonAsync(resourceName, id, json);
                if (updated is null)
                    return Results.NotFound();

                return updated.Records is not null
                    ? Results.Ok(updated.GetRecordsJsonObject())
                    : Results.NoContent();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"更新記錄時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> PostDataAsync(string resourceName, dynamic payload, DbTableOperations operations)
        {
            try
            {
                var json = DbTableOperations.ParseJsonPayload(payload);
                if (json is null)
                    return Results.Problem("Not a Json Content");

                var r = await operations.InsertFromJsonAsync(resourceName, json);
                if (r is null)
                    return Results.NotFound();

                var newId = r.Records?.First().Id;
                return Results.Created($"/{resourceName}/{newId}", r.GetRecordsJsonObject());
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"新增記錄時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> GetDataByIdAsync(string resourceName, int id, DbTableOperations operations)
        {
            try
            {
                var result = await operations.GetRecordByIdAsync(resourceName, id);
                if (result is not null)
                    return Results.Ok(result.GetRecordsJsonObject());

                return Results.NotFound();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"取得記錄時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> GetDataQueryAsync(string resourceName, QueryModel query, DbTableOperations operations)
        {
            try
            {
                var result = await operations.GetRecordsAsync(resourceName, query.Key, query.Value);
                if (result is not null)
                {
                    if (!string.IsNullOrEmpty(query.Key) && !string.IsNullOrEmpty(query.Value))
                        return Results.Ok(result.GetRecordsJsonObjectString());

                    return Results.Ok(result.GetRecordsJsonObject());
                }

                return Results.NotFound();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"查詢記錄時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> GetAllTableNameAsync(DbTableOperations operations)
        {
            try
            {
                var result = await operations.GetAllTableNamesAsync();
                return result is not null ? Results.Ok(result) : Results.NotFound();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"取得資料表清單時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> GetMetaDataAsync(string resourceName, DbTableOperations operations)
        {
            try
            {
                var result = await operations.GetMetadataAsync(resourceName);
                if (result is not null)
                    return Results.Ok(result.GetMetaJsonObject());

                return Results.NotFound();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"取得資料表欄位資訊時發生錯誤: {ex.Message}");
            }
        }

        private static async Task<IResult> GetMetaValueSetAsync(string resourceName, string fieldName, DbTableOperations operations)
        {
            try
            {
                var valueSet = await operations.GetValueSetAsync(resourceName, fieldName);
                if (valueSet is not null && valueSet.Any())
                    return Results.Ok(valueSet);

                return Results.NotFound();
            }
            catch (DbServiceException ex)
            {
                return Results.Problem(ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem($"取得欄位值集合時發生錯誤: {ex.Message}");
            }
        }
    }
}
