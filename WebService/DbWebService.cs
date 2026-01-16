using DbServices.Core.Models;
using DbServices.Core.Models.Interface;
using DbServices.Core.Exceptions;
using DBServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json.Nodes;
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

            IDbService? db = SetupServer(connectString, server);
            if(db == null)
            {
                logger?.LogError("無法初始化資料庫服務，伺服器類型: {ServerType}", server);
                return;
            }   

            logger?.LogInformation("資料庫 Web 服務已啟動，伺服器類型: {ServerType}", server);
            
            app.MapGet("/meta", async () => await GetAllTableNameAsync(db));
            var resourceName = app.MapGroup("/{resourceName}");
            resourceName.MapGet("/fieldSet", async ([FromRoute] string resourceName) => await GetMetaDataAsync(resourceName, db));
            resourceName.MapGet("/valueSet", async ([FromRoute] string resourceName, [FromQuery] string fieldName) => await GetMetaValueSetAsync(resourceName, fieldName, db));
            resourceName.MapGet("", async ([FromRoute] string resourceName, [AsParameters] QueryModel query) => await GetDataQueryAsync(resourceName, query, db));
            resourceName.MapGet("/{id:int}", async (string resourceName, int id) => await GetDataByIdAsync(resourceName, id, db));

            resourceName.MapPost("", async ([FromRoute] string resourceName, object payload) => await PostDataAsync(resourceName, payload, db));

            resourceName.MapPut("/{id:int}", async ([FromRoute] string resourceName, [FromRoute] int id, object content) => await PutDataAsync(resourceName, id, content, db));

            resourceName.MapDelete("/{id:int}", async (string resourceName, int id) => await DeleteDataAsync(resourceName, id, db));
        }

        #region 非同步 API 方法
        
        /// <summary>
        /// 非同步刪除資料表某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">該紀錄唯一編碼</param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static async Task<IResult> DeleteDataAsync(string resourceName, int id, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var existingRecord = await asyncDb.GetRecordByIdAsync(id, resourceName);
                    if (existingRecord == null) 
                        return Results.NotFound();

                    bool success = await asyncDb.DeleteRecordByIdAsync(id, resourceName);
                    return success ? Results.Ok() : Results.NoContent();
                }
                else
                {
                    // 降級到同步方法
                    db.SetCurrentTableName(resourceName);
                    if (db.GetRecordById(id) == null) return Results.NotFound();
                    return db.DeleteRecordById(id) ? Results.Ok() : Results.NoContent();
                }
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

        /// <summary>
        /// 非同步更新資料表某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">該紀錄唯一編碼</param>
        /// <param name="payload">該紀錄有更新的部分</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> PutDataAsync(string resourceName, int id, dynamic payload, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var existingRecord = await asyncDb.GetRecordByIdAsync(id, resourceName);
                    if (existingRecord == null) return Results.NotFound();

                    MemoryStream ms = new(Encoding.UTF8.GetBytes(payload.GetRawText()));
                    var result = JsonNode.Parse(ms);
                    if (result is JsonObject one)
                    {
                        IEnumerable<KeyValuePair<string, object?>> kps = one.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
                        var updated = await asyncDb.UpdateRecordByIdAsync(id, kps, resourceName);
                        if (updated != null)
                        {
                            return Results.Ok(updated.GetRecordsJsonObject());
                        }
                        else
                        {
                            return Results.NoContent();
                        }
                    }
                    else
                    {
                        return Results.Problem("Not a Json Content");
                    }
                }
                else
                {
                    // 降級到原有的同步方法
                    return PutData(resourceName, id, payload, db);
                }
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

        /// <summary>
        /// 非同步新增資料表紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="payload">新增資料別內容(JSON)</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> PostDataAsync(string resourceName, dynamic payload, IDbService db)
        {
            try
            {
                MemoryStream ms = new(Encoding.UTF8.GetBytes(payload.GetRawText()));
                var result = JsonNode.Parse(ms);
                if (result is JsonObject one)
                {
                    IEnumerable<KeyValuePair<string, object?>> kps = one.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
                    
                    if (db is IDbServiceAsync asyncDb)
                    {
                        var r = await asyncDb.InsertRecordAsync(kps, resourceName);
                        if (r != null)
                        {
                            var id = r.Records?.First().Id;
                            return Results.Created($"/{resourceName}/{id}", r.GetRecordsJsonObject());
                        }
                        else
                        {
                            return Results.NotFound();
                        }
                    }
                    else
                    {
                        // 降級到同步方法
                        return PostData(resourceName, payload, db);
                    }
                }
                else
                {
                    return Results.Problem("Not a Json Content");
                }
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

        /// <summary>
        /// 非同步由ID取得某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">某紀錄唯一編碼</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> GetDataByIdAsync(string resourceName, int id, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var result = await asyncDb.GetRecordByIdAsync(id, resourceName);
                    if (result != null)
                    {
                        var resultString = result.GetRecordsJsonObject();
                        return Results.Ok(resultString);
                    }
                    else
                    {
                        return Results.NotFound();
                    }
                }
                else
                {
                    // 降級到同步方法
                    return GetDataById(resourceName, id, db);
                }
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

        /// <summary>
        /// 非同步使用QueryModel建立Key-Values來查詢資料
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="query">查詢條件</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> GetDataQueryAsync(string resourceName, QueryModel query, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb && !string.IsNullOrEmpty(query.Key) && !string.IsNullOrEmpty(query.Value))
                {
                    KeyValuePair<string, object?> kp = new(query.Key, query.Value);
                    var result = await asyncDb.GetRecordByKeyValueAsync(kp, tableName: resourceName);
                    if (result != null)
                    {
                        var resultString = result.GetRecordsJsonObjectString();
                        return Results.Ok(resultString);
                    }
                    else
                    {
                        return Results.NotFound();
                    }
                }
                else
                {
                    // 降級到同步方法
                    return GetDataQuery(resourceName, query, db);
                }
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

        /// <summary>
        /// 非同步取回資料庫中所有資料表名稱
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> GetAllTableNameAsync(IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var result = await asyncDb.GetAllTableNamesAsync();
                    return Results.Ok(result);
                }
                else
                {
                    // 降級到同步方法
                    return GetAllTableName(db);
                }
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

        /// <summary>
        /// 非同步取得資料表欄位資訊
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> GetMetaDataAsync(string resourceName, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var result = await asyncDb.GetFieldsByTableNameAsync(resourceName);
                    return Results.Ok(result);
                }
                else
                {
                    // 降級到同步方法
                    return GetMetaData(resourceName, db);
                }
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

        /// <summary>
        /// 非同步取得欄位值集合
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static async Task<IResult> GetMetaValueSetAsync(string resourceName, string fieldName, IDbService db)
        {
            try
            {
                if (db is IDbServiceAsync asyncDb)
                {
                    var result = await asyncDb.GetValueSetByFieldNameAsync(fieldName, resourceName);
                    return Results.Ok(result);
                }
                else
                {
                    // 降級到同步方法
                    return GetMetaValueSet(resourceName, fieldName, db);
                }
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

        #endregion

        #region 原有的同步方法
        #region Delete Data
        /// <summary>
        /// 刪除資料表某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">該紀錄唯一編碼</param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static IResult DeleteData(string resourceName, int id, IDbService db)
        {  
            db.SetCurrentTableName(resourceName);
            if (db.GetRecordById(id) == null) return Results.NotFound();
            if (db.DeleteRecordById(id))
            {
                return Results.Ok();
            }
            else
            {
                return Results.NoContent();
            }
        }
        #endregion
        #region Put Data
        /// <summary>
        /// 更新資料表某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">該紀錄唯一編碼</param>
        /// <param name="payload">該紀錄有更新的部分</param>
        ///         /// <param name="db"></param>
        /// <returns></returns>
        private static IResult PutData(string resourceName, int id, dynamic payload, IDbService db)
        {
            db.SetCurrentTableName(resourceName);
            if (db.GetRecordById(id) == null) return Results.NotFound();

            MemoryStream ms = new(Encoding.UTF8.GetBytes(payload.GetRawText()));
            var result = JsonNode.Parse(ms);
            if (result is JsonObject one)
            {
                IEnumerable<KeyValuePair<string, object?>> kps = one.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
                var updated = db.UpdateRecordById(id, kps);
                if (updated != null)
                {
                    return Results.Ok(updated.GetRecordsJsonObject());
                }
                else
                {
                    return Results.NoContent();
                }
            }
            else
            {
                return Results.Problem("Not a Json Content");
            }
        }
        #endregion
        #region Post Data
        /// <summary>
        /// 新增資料表紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="payload">新增資料別內容(JSON)</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static IResult PostData(string resourceName, dynamic payload, IDbService db)
        {
            db.SetCurrentTableName(resourceName);
            MemoryStream ms = new(Encoding.UTF8.GetBytes(payload.GetRawText()));
            var result = JsonNode.Parse(ms);
            if (result is JsonObject one)
            {
                IEnumerable<KeyValuePair<string, object?>> kps = one.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
                var r = db.InsertRecord(kps);
                if (r != null)
                {
                    var id = r.Records?.First().Id;
                    return Results.Created($"/{resourceName}/{id}", r.GetRecordsJsonObject());
                }
                else
                {
                    return Results.NotFound();
                }
            }
            else
            {
                return Results.Problem("Not a Json Content");
            }
        }
        #endregion
        #region Get Data
        /// <summary>
        /// 由ID取得某紀錄
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="id">某紀錄唯一編碼</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static IResult GetDataById(string resourceName, int id, IDbService db)
        {   
            var result = db.GetRecordById(id, resourceName);
            if (result != null)
            {
                var resultString = result.GetRecordsJsonObject();

                return Results.Ok(resultString);

            }
            else
            {
                return Results.NotFound();
            }

        }
        /// <summary>
        /// 使用QueryModel建立Key-Values來查詢資料
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="query">查詢條件</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static IResult GetDataQuery(string resourceName, QueryModel query, IDbService db)
        {  
            TableBaseModel? result;
            if (query != null && query.Key != null && query.Value != null)
            {
                KeyValuePair<string, object?> queryKeyValue = new(query.Key, query.Value);
                result = db.GetRecordByKeyValue(queryKeyValue,null, resourceName);
            }
            else
            {
                result = db.GetRecordByTableName(resourceName);
            }

            if (result != null)
            {
                var resultString = result.GetRecordsJsonObject();
                
                return Results.Ok(resultString);
            }
            else
            {
                return Results.NotFound();
            }
        }
        #endregion
        #region Get MetaData
        /// <summary>
        /// 取回資料庫中所有資料表名稱
        /// </summary>
        /// <param name="db"></param>
        /// <returns>資料表名稱清單</returns>
        private static IResult GetAllTableName(IDbService db)
        {
            var tableNames = db.GetAllTableNames();
            if (tableNames != null)
            {
                return Results.Ok(tableNames);
            }
            else
            {
                return Results.NotFound();
            }
        }
        /// <summary>
        /// 取回資料表的欄位結構定義
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="db"></param>
        /// <returns>回傳TableBaseModel類別</returns>
        private static IResult GetMetaData(string resourceName, IDbService db)
        {
            TableBaseModel? result = db.GetRecordByTableName(resourceName);
            if (result != null)
            {

                return Results.Ok(result.GetMetaJsonObject());

            }
            else
            {
                return Results.NotFound();
            }
        }
        /// <summary>
        /// 回傳資料表鍾某欄位的可能值
        /// </summary>
        /// <param name="resourceName">資料表名稱</param>
        /// <param name="fieldName">欄位名稱</param>
        /// <param name="db"></param>
        /// <returns></returns>
        private static IResult GetMetaValueSet(string resourceName, string fieldName, IDbService db)
        {
           
            var valueSet = db.GetValueSetByFieldName(fieldName, resourceName);
            if (valueSet != null && valueSet.Any())
            {
                return Results.Ok(valueSet);
            }
            else
            {
                return Results.NotFound();
            }
        }
        #endregion

        private static IDbService? SetupServer(string connectString, string serverName)
        {
            return serverName switch
            { 

               // "SqlServer" => MainService.UseMsSQL(connectString),
                "Sqlite" => MainService.UseSQLite(connectString),
               // "MySQL" => MainService.UseMySQL(connectString),
                //"Oracle" => MainService.UseOracle(connectString),
                _ => null
            };


        }
        #endregion
    }
}
