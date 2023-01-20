using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DBService.Models
{
    public class TableBaseModel
    {
        public TableBaseModel() { }
        public TableBaseModel(string tableName)
        {
            TableName = tableName;
        }
        public string? ConnectString { get; set; }
        public string? TableName { get; set; }
        public IEnumerable<string>? PkFieldList { get; set; }
        public IEnumerable<FieldBaseModel>? Fields { get; set; }
        public IEnumerable<RecordBaseModel>? Records { get; set; }

        public JsonObject? ToJsonObject(bool fullSet = false , bool ignoreNull = false)
        {
            StringBuilder sb = new();
            sb.Append('{');
            if (fullSet)
            {
                sb.Append($"\"ConnectString\": \"{ConnectString}\",");
                sb.Append($"\"TableName\": \"{TableName}\",");
                sb.Append("\"PrimaryKey\": ");
                if (PkFieldList != null && PkFieldList.Any())
                {
                    List<string> pkstring = new();
                   
                    foreach(var pk in PkFieldList)
                    {
                        pkstring.Add($"\"{pk}\"");
                    }
                    sb.Append($"[ {string.Join(',', pkstring)} ]");
                }
                else
                {
                    sb.Append("[]");
                }

                sb.Append(',');

                sb.Append($"\"FieldDefine\": ");
                if (Fields != null && Fields.Any())
                {
                    sb.Append('[');
                    List<string> fs = new();
                    foreach (var item in Fields)
                    {
                        
                        fs.Add(item.ToJsonString(ignoreNull));
                    }
                    sb.Append(string.Join(',', fs));
                    sb.Append(']');
                }
                else
                {
                    sb.Append("[]");
                }
                sb.Append(',');
            }
            sb.Append($"\"Data\": ");
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = new();
                foreach (var item in Records)
                {
                    rs.Add(item.ToJsonString(ignoreNull));
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
    }
}
