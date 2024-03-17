
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DBServices.Models
{
    public class TableBaseModel
    {
        public TableBaseModel() { }
        public TableBaseModel(string tableName)
        {
            TableName = tableName;
        }
        public string? ConnectionString { get; set; }
        public string? TableName { get; set; }
        public IEnumerable<FieldBaseModel>? Fields { get; set; }
        public IEnumerable<RecordBaseModel>? Records { get; set; }

        public ForeignBaseModel? GetForeignKeyTable(string fieldName)
        {
            if (Fields == null || !Fields.Any()) return null;
            ForeignBaseModel? result = Fields.Where(x => x.IsForeignKey && x.FieldName == fieldName).Select(x => x.ForeignInfo).FirstOrDefault();
            return result;
        }
        public T? GetObject<T>(int index = 0) where T : TableDefBaseModel
        {
            if (Records == null || !Records.Any()) return null;
            if (index < 0 || index >= Records.Count()) return null;
            return Records.ElementAt(index).GetObject<T>() as T;
        }
        public IEnumerable<T> GetObjects<T>() where T : TableDefBaseModel
        {
            List<T> result = [];
            if (Records == null || !Records.Any()) return result;
            foreach (var item in Records)
            {
                if (item.GetObject<T>() is T one)
                {
                    result.Add(one);
                }
            }
            return result;

        }

        public static TableBaseModel CreateTableBaseModel<T>() where T : TableDefBaseModel
        {
            string tableName = typeof(T).Name;
            List<FieldBaseModel> fieldList = [];

            var properties = typeof(T).GetProperties();
            if (properties != null && properties.Length > 0)
            {
                foreach (var p in properties)
                {
                    FieldBaseModel field = new();
                    if (p.GetCustomAttribute<KeyAttribute>() != null)
                    {
                        field.IsPrimaryKey = true;
                    }
                    if (p.GetCustomAttribute<RequiredAttribute>() != null)
                    {
                        field.IsNotNull = true;
                    }
                    if (p.GetCustomAttribute<ForeignKeyAttribute>() is ForeignKeyAttribute fkey)
                    {
                        field.IsForeignKey = true;
                        field.ForeignInfo = new ForeignBaseModel()
                        {
                            ComeFromTableName = fkey.Name,
                            ComeFromFieldName = "Id",
                            CurrentFieldName = p.Name
                        };
                    }
                    field.FieldName = p.Name;
                    field.FieldType = p.PropertyType.Name;
                    fieldList.Add(field);
                }
            }
            return new TableBaseModel()
            {
                TableName = tableName,
                Fields = fieldList
            };
        }
        public string GetMetaJsonString()
        {
            StringBuilder sb = new();
            sb.Append('{');
            sb.Append(GetBaseJsonString());
            if (Fields != null && Fields.Any())
            {
                sb.Append(',');
                sb.Append(GetFieldJsonString());
            }
            sb.Append('}');
            return sb.ToString();
        }
        public JsonObject? GetMetaJsonObject()
        {  
            string result = GetMetaJsonString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }

        public string GetRecordOnlyJsonString()
        {
            return GetRecordsJsonString(false);
        }
        public JsonArray? GetRecordOnlyJsonArray()
        {

            var tempString = GetRecordsJsonString(false);
            MemoryStream stream = new(Encoding.UTF8.GetBytes(tempString));
            return (JsonArray?)JsonNode.Parse(stream);
        }
        public string GetRecordsJsonObjectString()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append(GetBaseJsonString());
            if (Records != null && Records.Any())
            {
                sb.Append(',');
                sb.Append(GetRecordsJsonString());
            }
            sb.Append('}');
            return sb.ToString();
        }
        public JsonObject? GetRecordsJsonObject()
        {
            var result = GetRecordsJsonObjectString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }
        public string GetFullJsonString()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append(GetBaseJsonString());
            if (Fields != null && Fields.Any())
            {
                sb.Append(',');
                sb.Append(GetFieldJsonString());
            }

            if (Records != null && Records.Any())
            {
                sb.Append(',');
                sb.Append(GetRecordsJsonString());
            }
            sb.Append('}');
            return sb.ToString();
        }

        public JsonObject? ToFullJsonObject()
        {
           
            var result = GetFullJsonString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }

        #region Private Method
        private string GetBaseJsonString()
        {
            StringBuilder sb = new();
            sb.Append($"\"ConnectString\": \"{ConnectionString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");
            sb.Append($"\"FieldCount\": {Fields?.Count() ?? 0},");
            sb.Append($"\"RecordCount\": {Records?.Count() ?? 0}");
            return sb.ToString();
        }

        private string GetFieldJsonString()
        {
            StringBuilder sb = new();
            sb.Append($"\"FieldDefine\": ");
            if (Fields != null && Fields.Any())
            {
                sb.Append('[');
                List<string> fs = [];
                foreach (var item in Fields)
                {
                    fs.Add(item.ToJsonString(true));
                }
                sb.Append(string.Join(',', fs));
                sb.Append(']');
            }
            else
            {
                sb.Append("[]");
            }

            return sb.ToString();
        }

        private string GetRecordsJsonString(bool withHeadTag = true)
        {
            StringBuilder sb = new();
            if (withHeadTag) { sb.Append($"\"RecordData\": "); }
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = [];
                foreach (var item in Records)
                {
                    rs.Add(item.ToJsonString());
                }
                sb.Append(string.Join(',', rs));

                sb.Append(']');
            }
            else
            {
                sb.Append("[]");
            }
            return sb.ToString();
        }
        #endregion
    }
}
