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
        public string? ConnectString { get; set; }
        public string? TableName { get; set; }
        public IEnumerable<string>? PkFieldList { get; set; }
        public IEnumerable<FieldBaseModel>? Fields { get; set; }
        public IEnumerable<RecordBaseModel>? Records { get; set; }

        public JsonObject? GetMetaJsonObject()
        {
            StringBuilder sb = new();
            sb.Append('{');
            sb.Append($"\"ConnectString\": \"{ConnectString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");
            sb.Append($"\"MetaData\": ");
            if (Fields != null && Fields.Any())
            {
                sb.Append('[');
                List<string> fs = new();
                foreach (var item in Fields)
                {
                    fs.Add(item.ToJsonString());
                }
                sb.Append(string.Join(',', fs));
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
        public JsonObject? GetRecordsJsonObject()
        {
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append($"\"ConnectString\": \"{ConnectString}\",");
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

            sb.Append($"\"ConnectString\": \"{ConnectString}\",");
            sb.Append($"\"TableName\": \"{TableName}\",");
            sb.Append("\"PrimaryKey\": ");
            if (PkFieldList != null && PkFieldList.Any())
            {
                List<string> pkstring = new();

                foreach (var pk in PkFieldList)
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

                    fs.Add(item.ToJsonString(true));
                }
                sb.Append(string.Join(',', fs));
                sb.Append(']');
            }
            else
            {
                sb.Append("[]");
            }
            sb.Append(',');

            sb.Append($"\"Data\": ");
            if (Records != null && Records.Any())
            {
                sb.Append('[');
                List<string> rs = new();
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

            sb.Append('}');

            MemoryStream stream = new(Encoding.UTF8.GetBytes(sb.ToString()));
            return (JsonObject?)JsonNode.Parse(stream);
        }

        #region Private Method

        #endregion
    }
}
