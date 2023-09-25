using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DBService.Models
{
    public class RecordBaseModel
    {
        public IEnumerable<string>? PkFieldValue { get; set; }
        public string? ParentKeyValue { get; set; }
        public int Id { get; set; }
        public IEnumerable<KeyValuePair<string, object>>? FieldValue { get; set; }
        public T? GetObject<T>() where T : class
        {
            if (FieldValue == null) return default;
            List<KeyValuePair<string, JsonNode?>> targets = new();
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
            sb.Append('{');
            if (FieldValue != null && FieldValue.Any())
            {
                List<string> oneRecord = new();
                foreach (var one in FieldValue)
                {
                    if (!ignoreNull && one.Value != null)
                    {
                        oneRecord.Add(CheckType(one.Key, one.Value));
                    }
                }
                sb.Append(string.Join(',', oneRecord));
            }
            sb.Append('}');

            return sb.ToString();
        }
        private string CheckType(string key, object? value)
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
