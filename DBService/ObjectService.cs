using DBService.Models.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService
{
    public class ObjectService
    {
        private readonly IDbService? _db;
        public ObjectService(IDbService? db)
        {
            _db = db;
        }
        #region GET Data

        public T? GetData<T>(int id, string? tableName = null) where T : class
        {
            if (_db is null) return default;
            _db.SetCurrentTable(tableName ?? typeof(T).Name);
            var queryResult = _db.GetRecordById(id);
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {

                var jsonResult = queryResult.Records.FirstOrDefault();
                return jsonResult?.GetObject<T>();
            }
            return default;
        }
        public IEnumerable<T>? GetDatas<T>(KeyValuePair<string, object?>? query, string? tableName = null) where T : class
        {
            if (_db is null) return default;
         
            var queryResult = query != null ? _db.GetRecordByKeyValue(query.Value) : _db.GetRecordByTable(tableName ?? typeof(T).Name);
            List<T> targets = new();
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                foreach (var item in queryResult.Records)
                {
                    if (item.GetObject<T>() is T one) targets.Add(one);
                }
            }
            return targets;
        }
        public IEnumerable<T>? GetDatas<T>(IEnumerable<KeyValuePair<string, object?>>? query, string? tableName = null) where T : class
        {
            if (_db is null) return default;
          
            var queryResult = query != null ? _db.GetRecordByKeyValues(query) : _db.GetRecordByTable(tableName ?? typeof(T).Name);
            List<T> targets = new();
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                foreach (var item in queryResult.Records)
                {
                    if (item.GetObject<T>() is T one) targets.Add(one);
                }
            }
            return targets;
        }
        public IEnumerable<T>? GetDatas<T>(string? queryString, string? tableName = null) where T: class
        {
            if (_db is null) return default;
            var queryResult = queryString != null ? _db.GetRecordForAll(queryString) : _db.GetRecordByTable(tableName ?? typeof(T).Name);
            List<T> targets = new();
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                foreach (var item in queryResult.Records)
                {
                    if (item.GetObject<T>() is T one) targets.Add(one);
                }
            }
            return targets;

        }
        #endregion
        #region Set Data
        public T? SetData<T>(T data) where T : class
        {
            if (_db is null) return default;
           
            Dictionary<string, object?> dictionary = new();
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                if (property.Name == "Id" || property.Name == "id") continue;
                var value = property.GetValue(data);
                if (value is T valueT)
                {
                    dictionary.Add(property.Name, valueT);
                }
            }
            var targets = dictionary.ToList<KeyValuePair<string, object?>>();
            var result = _db.AddRecord(targets);
            var record = result?.Records?.FirstOrDefault();
            if (record?.Id is int id)
            {
                return GetData<T>(id);
            }
            else
            {
                return default;
            }
        }
        #endregion
    }
}
