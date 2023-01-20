
using DBService;
using DBService.Models.Interface;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using WebService.Models;
using WebService.Models.EnumModel;

namespace WebService
{
    public static class DbWebService
    {

        public static void UseDbWebService(this WebApplication app, string connectString)
        {   
            IDbService db = new MainService(connectString).UseSQLite();
            app.MapGet("/api/{resourceName}", ([FromRoute]string resourceName) => GetAllData(resourceName, db));
            app.MapGet("/api/{resourceName}/{id:int}", (string resourceName, int id) => GetDataById(resourceName, id, db));
            app.MapGet("/api/KeyValue/{resourceName}", ([FromRoute]string resourceName, [FromQuery]QueryModel query) => GetDataByKeyValue(resourceName,query, db));
            //app.MapGet("/api/KeyValue/{resourceName}", ([FromRoute]string resourceName, [FromQuery(Name = "Query")] IEnumerable<QueryModel> query  ) => GetDataByKeyValues(resourceName, query, db));
         
        }


        private static IResult GetAllData(string resourceName, IDbService db)
        {
            db.SetCurrent(resourceName);
            var result = db.GetRecordForAll();
            if (result != null)
            {
                var resultstring = result.ToJsonObject(false, false);
                
                return Results.Ok(resultstring);

            }
            else
            {
                return Results.NotFound();
            }

        }
        private static IResult GetDataById(string resourceName, int id, IDbService db)
        {
            db.SetCurrent(resourceName);
            var result = db.GetRecordById(id);
            if (result != null)
            {
                var resultstring = result.ToJsonObject(false, false);

                return Results.Ok(resultstring);

            }
            else
            {
                return Results.NotFound();
            }

        }
        private static IResult GetDataByKeyValue(string resourceName, QueryModel query, IDbService db)
        {
            db.SetCurrent(resourceName);
            var result = db.GetRecordByKeyValue(new KeyValuePair<string, object>(query.Key,query.Value));
            if (result != null)
            {
                var resultstring = result.ToJsonObject(false, false);

                return Results.Ok(resultstring);

            }
            else
            {
                return Results.NotFound();
            } 
        }

        private static IResult GetDataByKeyValues(string resourceName, IEnumerable<QueryModel> query, IDbService db)
        {
            db.SetCurrent(resourceName);
            IEnumerable<KeyValuePair<string, object>> queries = from q in query
                                                                where q.Key != null && q.Value != null
                                                                select new KeyValuePair<string, object>(q.Key!, q.Value!);
            var result =db.GetRecordByKeyValues(queries);
            if (result != null)
            {
                var resultstring = result.ToJsonObject(false, false);

                return Results.Ok(resultstring);

            }
            else
            {
                return Results.NotFound();
            }
        }
        #region Private
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
