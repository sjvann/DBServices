using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models
{
    public class RecordBaseModel
    {
        public IEnumerable<string>? PkFieldValue { get; set; }
        public string? ParentKey { get; set; }
        public IEnumerable<KeyValuePair<string, object>>? FieldValue { get; set; }

        public string ToJsonString(bool ignorNull = false)
        {

            StringBuilder sb = new();
            sb.Append('{');
            if (FieldValue != null && FieldValue.Any())
            {
                List<string> oneRecord = new();
                foreach (var one in FieldValue)
                {
                    if (!ignorNull && one.Value != null)
                    {
                        oneRecord.Add(CheckType(one.Key, one.Value));
                    }
                    else
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
