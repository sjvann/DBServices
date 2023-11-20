using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DBService.Models
{
    public class RecordBaseModel
    {
        public int Id { get; set; }
        public IEnumerable<KeyValuePair<string, object>>? FieldValue { get; set; }
        public T? GetObject<T>() where T : class
        {
            if (FieldValue == null) return default;
            List<KeyValuePair<string, JsonNode?>> targets = [];
            foreach (var item in FieldValue)
            {
                JsonNode? newValue = JsonValue.Create(item.Value);
                if (newValue != null)
                {
                    targets.Add(new KeyValuePair<string, JsonNode?>(item.Key, newValue));
                }
            }
            JsonObject result = new(targets);

            var objectResult = JsonSerializer.Deserialize<T>(result);
            return objectResult;
        }
        public string ToJsonString(bool ignoreNull = false)
        {

            StringBuilder sb = new();
            if(FieldValue != null && FieldValue.Any())
            {
                sb.Append('{');
                if (FieldValue != null && FieldValue.Any())
                {
                    List<string> oneRecord = [];
                    foreach (var one in FieldValue)
                    {
                        if(ignoreNull && one.Value == null)
                        {
                            continue;
                        }
                        else
                        {
                             oneRecord.Add(CheckType(one.Key, one.Value));
                        }
                    }
                    sb.Append(string.Join(',', oneRecord));
                }
                sb.Append('}');
            }
            return sb.ToString();
        }
        private static string CheckType(string key, object? value)
        {
            if (value == null)
            {
                return $"\"{key}\": null";
            }
            else if (value is Int16 or Int32 or Int64 or long)
            {
                return $"\"{key}\": {value}";
            }
            else if (value is decimal)
            {
                return $"\"{key}\": {value}";
            }
            else
            {
                return $"\"{key}\": \"{value}\"";
            }
        }
    }
}
