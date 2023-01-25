
using DBService;
using DBService.Models;
using DBService.Models.Enum;
using DBService.Models.Interface;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WebService.Models;
using WebService.Models.EnumModel;

namespace WebService
{
    public static class DbWebService
    {

        public static void UseDbWebService(this WebApplication app, string connectString, string server)
        {
            IDbService db = SetupServer(connectString, server);

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
        private static IResult DeleteData(string resourceName, int id, IDbService db)
        {
            db.SetCurrent(resourceName);
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
        private static IResult PutData(string resourceName, int id,  dynamic payload, IDbService db)
        {
            db.SetCurrent(resourceName);
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
        private static IResult PostData(string resourceName, dynamic payload, IDbService db)
        {
            db.SetCurrent(resourceName);
            MemoryStream ms = new(Encoding.UTF8.GetBytes(payload.GetRawText()));
            var result = JsonNode.Parse(ms);
            if (result is JsonObject one)
            {
                IEnumerable<KeyValuePair<string, object?>> kps = one.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));
                var r = db.AddRecord(kps);
                if(r != null)
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
        private static IResult GetDataById(string resourceName, int id, IDbService db)
        {
            db.SetCurrent(resourceName);
            var result = db.GetRecordById(id);
            if (result != null)
            {
                var resultstring = result.GetRecordsJsonObject();

                return Results.Ok(resultstring);

            }
            else
            {
                return Results.NotFound();
            }

        }

        private static IResult GetDataQuery(string resourceName, QueryModel query, IDbService db)
        {
            db.SetCurrent(resourceName);
            TableBaseModel? result;
            if (query != null && query.Key != null && query.Value != null)
            {
                KeyValuePair<string, object?> querykv = new(query.Key, query.Value);
                result = db.GetRecordByKeyValue(querykv);
            }
            else
            {
                result = db.GetRecordByTable(resourceName);
            }

            if (result != null)
            {
                var resultstring = result.GetRecordsJsonObject();

                return Results.Ok(resultstring);
            }
            else
            {
                return Results.NotFound();
            }
        }
        #endregion
        #region Get MetaData
        private static IResult GetAllTableName(IDbService db)
        {
            var tableNames = db.GetTableNameList();
            if (tableNames != null)
            {
                return Results.Ok(tableNames);
            }
            else
            {
                return Results.NotFound();
            }
        }
        private static IResult GetMetaData(string resourceName, IDbService db)
        {
            db.SetCurrent(resourceName);
            TableBaseModel? result = db.GetCurrent();
            if (result != null)
            {

                return Results.Ok(result.GetMetaJsonObject());

            }
            else
            {
                return Results.NotFound();
            }
        }
        private static IResult GetMetaValueSet(string resourceName, string fieldName, IDbService db)
        {
            db.SetCurrent(resourceName);
            var valueSet = db.GetValueSetByFieldName(fieldName);
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

        private static IDbService SetupServer(string connectString, string serverName)
        {
            var db = new MainService(connectString);
            var result = Enum.TryParse(serverName, out EnumSupportedServer server);
            if (result)
            {
                switch (server)
                {
                    case EnumSupportedServer.SqlServer:
                        db.UseMsSQL();
                        break;
                    case EnumSupportedServer.Sqlite:
                        db.UseSQLite();
                        break;
                }
            }
            return db;
        }
        private static bool CheckToken(string? source)
        {
            string answer = "basso";
            if (!string.IsNullOrEmpty(source) && source == answer)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static ResultMessage CreateResultMessage(EnumResultCode? code, string? method, string? message, object? data)
        {
            return new ResultMessage
            {
                Code = code.ToString(),
                Method = method,
                Message = message,
                Data = data
            };
        }
        #endregion
    }
}
