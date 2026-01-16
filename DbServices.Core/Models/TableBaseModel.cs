using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DbServices.Core.Models
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
        public string GetMetaJsonString(bool isPretty = false)
        {
            return GetMetaJsonObject()?.ToJsonString(new System.Text.Json.JsonSerializerOptions()
            {
                WriteIndented = isPretty
            }) ?? string.Empty;
        }
        public JsonObject? GetMetaJsonObject()
        {
            string result = GetMetaString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }


        public string GetRecordOnlyJsonString(bool isPretty = false)
        {
            return GetRecordsJsonObject()?.ToJsonString(new System.Text.Json.JsonSerializerOptions()
            {
                WriteIndented = isPretty
            }) ?? string.Empty;
        }
        public JsonArray? GetRecordOnlyJsonArray()
        {
            var tempString = GetRecordsString(false);
            MemoryStream stream = new(Encoding.UTF8.GetBytes(tempString));
            return (JsonArray?)JsonNode.Parse(stream);
        }
        public string GetRecordsJsonObjectString(bool isPretty = false)
        {
            return GetRecordsJsonObject()?.ToJsonString(new System.Text.Json.JsonSerializerOptions()
            {
                WriteIndented = isPretty
            }) ?? string.Empty;
        }
        public JsonObject? GetRecordsJsonObject()
        {
            var result = GetRecordObjectString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }
        public string GetFullyJsonString(bool isPretty = false)
        {
            return GetFullyJsonObject()?.ToJsonString(new System.Text.Json.JsonSerializerOptions()
            {
                WriteIndented = isPretty
            }) ?? string.Empty;
        }

        public JsonObject? GetFullyJsonObject()
        {
            var result = GetFullyString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }

        #region Private Method

        private string GetMetaString()
        {
            StringBuilder sb = new();
            sb.Append('{');
            sb.Append(GetBaseString());
            if (Fields != null && Fields.Any())
            {
                sb.Append(',');
                sb.Append(GetFieldString());
            }
            sb.Append('}');
            return sb.ToString();
        }
        private string GetBaseString()
        {
            StringBuilder sb = new();

			// NOTE: Use JsonSerializer to properly escape backslashes, quotes, etc.
			sb.Append("\"ConnectString\": ")
			  .Append(JsonSerializer.Serialize(ConnectionString ?? string.Empty))
			  .Append(',');
			sb.Append("\"TableName\": ")
			  .Append(JsonSerializer.Serialize(TableName ?? string.Empty))
			  .Append(',');
            sb.Append($"\"FieldCount\": {Fields?.Count() ?? 0},");
            sb.Append($"\"RecordCount\": {Records?.Count() ?? 0}");
            return sb.ToString();
        }

        private string GetFieldString()
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

        private string GetRecordObjectString()
        {
            StringBuilder sb = new();
            sb.Append('{');
            sb.Append(GetBaseString());
            if (Records != null && Records.Any())
            {
                sb.Append(',');
                sb.Append(GetRecordsString());
            }
            sb.Append('}');
            return sb.ToString();
        }
        private string GetRecordsString(bool withHeadTag = true)
        {
            StringBuilder sb = new();
            if (withHeadTag) { sb.Append($"\"RecordData\": "); }
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = [];
                foreach (var item in Records)
                {
                    rs.Add(item.ToJsonString(true));
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
        private string GetFullyString()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append(GetBaseString());
            if (Fields != null && Fields.Any())
            {
                sb.Append(',');
                sb.Append(GetFieldString());
            }

            if (Records != null && Records.Any())
            {
                sb.Append(',');
                sb.Append(GetRecordsString());
            }
            sb.Append('}');
            return sb.ToString();
        }
        #endregion
    }
}
