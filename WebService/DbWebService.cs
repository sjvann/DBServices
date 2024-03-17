using DBServices;
using DBServices.Models;
using DBServices.Models.Interface;
using Microsoft.AspNetCore.Mvc;
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
            IDbService? db = SetupServer(connectString, server);
            if(db == null)
            {
                return;
            }   
            app.MapGet("/meta", () => GetAllTableName(db));
            var resourceName = app.MapGroup("/{resourceName}");
            resourceName.MapGet("/fieldSet", ([FromRoute] string resourceName) => GetMetaData(resourceName, db));
            resourceName.MapGet("/valueSet", ([FromRoute] string resourceName, [FromQuery] string fieldName) => GetMetaValueSet(resourceName, fieldName, db));
            resourceName.MapGet("", ([FromRoute] string resourceName, [AsParameters] QueryModel query) => GetDataQuery(resourceName, query, db));
            resourceName.MapGet("/{id:int}", (string resourceName, int id) => GetDataById(resourceName, id, db));


            resourceName.MapPost("", ([FromRoute] string resourceName, object payload) => PostData(resourceName, payload, db));

            resourceName.MapPut("/{id:int}", ([FromRoute] string resourceName, [FromRoute] int id, object content) => PutData(resourceName, id, content, db));

            resourceName.MapDelete("/{id:int}", (string resourceName, int id) => DeleteData(resourceName, id, db));
        }

        #region Private Method
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

                "SqlServer" => MainService.UseMsSQL(connectString),
                "Sqlite" => MainService.UseSQLite(connectString),
                "MySQL" => MainService.UseMySQL(connectString),
                "Oracle" => MainService.UseOracle(connectString),
                _ => null
            };


        }
        #endregion
    }
}
