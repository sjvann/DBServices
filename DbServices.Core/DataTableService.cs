using DbServices.Core.Models;
using DbServices.Core.Models.Enum;
using DbServices.Core.Models.Interface;
using System.ComponentModel.DataAnnotations;

namespace DbServices.Core
{
    public abstract class DataTableService<T>(IDbService db) : IDataTableService<T> where T : TableDefBaseModel
    {

        public int CreateNewTable()
        {
            return db.CreateNewTable<T>();

        }

        public bool DeleteRecordById(long id)
        {
            return db.DeleteRecordById(id, typeof(T).Name);
        }

        public bool DeleteRecordByKeyValue(KeyValuePair<string, object?> criteria)
        {

            return db.DeleteRecordByKeyValue(criteria, typeof(T).Name);
        }

        public int DropTable()
        {
            return db.DropTable(typeof(T).Name);
        }

        public int ExecuteSQL(string sql)
        {
            return db.ExecuteSQL(sql);
        }

        public int ExecuteStoredProcedure(string spName, IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            return db.ExecuteStoredProcedure(spName, parameters);
        }

        public IEnumerable<T>? GetAllRecord()
        {
            var result = db.GetRecordByTableName(typeof(T).Name);
            return ConvertTableBaseModels(result);
        }

        public string[] GetFieldsByTableName()
        {
            var result = db.GetFieldsByTableName(typeof(T).Name);
            return (from item in result
                    where item != null && item.FieldName != null
                    select item.FieldName).ToArray();
        }



        public T? GetRecordById(int id)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;

            var queryResult = db.GetRecordById(id, tableName);
            return ConvertTableBaseModel(queryResult);
        }

        public IEnumerable<T>? GetRecordByKeyValue(KeyValuePair<string, object?> query, EnumQueryOperator? opertor = null)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.GetRecordByKeyValue(query, opertor, tableName);

            return ConvertTableBaseModels(queryResult);


        }

        public IEnumerable<T>? GetRecordByKeyValues(IEnumerable<KeyValuePair<string, object?>> query, IEnumerable<EnumQueryOperator>? operators = null)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.GetRecordByKeyValues(query, tableName);
            return ConvertTableBaseModels(queryResult);
        }

        public IEnumerable<T>? GetRecordForField(string[] fields, string? whereStr = null)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.GetRecordForField(fields, whereStr, tableName);
            return ConvertTableBaseModels(queryResult);
        }

        public IEnumerable<T>? GetRecordWithWhere(string whereStr)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.GetRecordWithWhere(whereStr, tableName);
            return ConvertTableBaseModels(queryResult);
        }

        public string[]? GetValueSetByFieldName(string fieldName)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var result = db.GetValueSetByFieldName(fieldName, tableName);

            return (from item in result
                    where item != null && item.ToString() != null
                    select item.ToString()).ToArray();

        }

        public bool HasRecord()
        {
            return db?.HasRecord(typeof(T).Name) ?? false;
        }

        public T? InsertRecord(T source)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            List<KeyValuePair<string, object?>> target = [];
            foreach (var item in source.GetType().GetProperties())
            {
                if (item.Name == "Id" || item.Name == "id" || item.GetCustomAttributes(typeof(KeyAttribute), false) != null) continue;

                target.Add(new KeyValuePair<string, object?>(item.Name, item.GetValue(source)));
            }
            return InsertRecord(target);

        }
        public T? InsertRecord(IEnumerable<KeyValuePair<string, object?>> source)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.InsertRecord(source, tableName);
            return ConvertTableBaseModel(queryResult);

        }

        public T? UpdateRecordById(long id, IEnumerable<KeyValuePair<string, object?>> source)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.UpdateRecordById(id, source, tableName);
            return ConvertTableBaseModel(queryResult);
        }

        public bool UpdateRecordByKeyValue(KeyValuePair<string, object?> query, IEnumerable<KeyValuePair<string, object?>> source)
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            return db.UpdateRecordByKeyValue(query, source, tableName);

        }

        public IEnumerable<T2>? GetRecordByForeignKey<T2>(string fieldName, int id) where T2 : TableDefBaseModel
        {
            string tableName = typeof(T).Name;
            if (db is null || string.IsNullOrEmpty(tableName)) return default;
            var queryResult = db.GetRecordByForeignKeyForOneSide(fieldName, id, tableName);
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                return from item in queryResult.Records
                       where item.GetObject<T2>() is not null
                       select item.GetObject<T2>();
            }
            else
            {
                return default;
            }

        }


        #region Private Method
        private static IEnumerable<T>? ConvertTableBaseModels(TableBaseModel? queryResult)
        {
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                return from item in queryResult.Records
                       where item.GetObject<T>() is not null
                       select item.GetObject<T>();
            }
            else
            {
                return default;
            }
        }
        private static T? ConvertTableBaseModel(TableBaseModel? queryResult)
        {
            if (queryResult != null && queryResult.Records != null && queryResult.Records.Any())
            {
                var jsonResult = queryResult.Records.FirstOrDefault();
                return jsonResult?.GetObject<T>();
            }
            else
            {
                return default;
            }
        }
        #endregion
    }
}