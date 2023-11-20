using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DBService.Models
{
    public class TableBaseModel
    {
        public TableBaseModel() { }
        public TableBaseModel(string tableName)
        {
            TableName = tableName;
        }
        public DbConnection? Connection { get; set; }
        public string? TableName { get; set; }
        public IEnumerable<FieldBaseModel>? Fields { get; set; }
        public IEnumerable<RecordBaseModel>? Records { get; set; }

        public static TableBaseModel CreateTableBaseModel<T>() where T : TableDefBaseModel
        {
            string tableName = nameof(T);
            List<FieldBaseModel> fieldList = [];

            var properties = typeof(T).GetProperties();
            if (properties != null && properties.Any())
            {
                foreach (var p in properties)
                {
                    FieldBaseModel field = new();
                    if ( p.GetCustomAttribute<KeyAttribute>() != null)
                    {
                        field.IsPrimaryKey = true;
                    }
                    if (p.GetCustomAttribute<RequiredAttribute>() != null)
                    {
                        field.IsNotNull = true;
                    }
                    if(p.GetCustomAttribute<ForeignKeyAttribute>() is ForeignKeyAttribute fkey)
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
        public JsonObject? GetMetaJsonObject()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append($"\"ConnectString\": \"{Connection?.ConnectionString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");

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
            sb.Append('}');
            string result = sb.ToString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }
        public JsonArray? GetRecordsJsonArray()
        {
            StringBuilder sb = new();
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = new();
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
            var tempString = sb.ToString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(tempString));
            return (JsonArray?)JsonNode.Parse(stream);
        }
        public JsonObject? GetRecordsJsonObject()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append($"\"ConnectString\": \"{Connection?.ConnectionString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");
            sb.Append($"\"RecordData\": ");
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = new();
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

            sb.Append('}');
            MemoryStream stream = new(Encoding.UTF8.GetBytes(sb.ToString()));
            return (JsonObject?)JsonNode.Parse(stream);
        }
        public JsonObject? ToFullJsonObject()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append($"\"ConnectString\": \"{Connection?.ConnectionString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");

            sb.Append($"\"FieldDefine\": ");
            if (Fields != null && Fields.Any())
            {
                sb.Append('[');
                List<string> fs = new();
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
            

            if(Records != null && Records.Any())
            {
                sb.Append(',');
                sb.Append($"\"Data\": ");
                sb.Append('[');
                List<string> rs = [];
                foreach (var item in Records)
                {
                    rs.Add(item.ToJsonString(true));
                }
                sb.Append(string.Join(',', rs));

                sb.Append(']');
            }
            sb.Append('}');
            var result = sb.ToString();
            MemoryStream stream = new(Encoding.UTF8.GetBytes(result));
            return (JsonObject?)JsonNode.Parse(stream);
        }

        #region Private Method

        #endregion
    }
}
