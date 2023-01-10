using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models
{
    public class FieldBaseModel
    {
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }
        public bool? IsKey { get; set; }

        public string ToJsonString(bool ignoreNull = false)
        {
            StringBuilder sb = new();
            sb.Append('{');
            if (ignoreNull)
            {
                if (!string.IsNullOrEmpty(FieldName)) { sb.Append($"\"FieldName\": \"{FieldName}\","); }
                if (!string.IsNullOrEmpty(FieldType)) { sb.Append($"\"FieldType\": \"{FieldType}\","); }
                if (IsKey != null) { sb.Append($"\"IsKey\": \"{IsKey}\""); }
            }
            else
            {
                sb.Append($"\"FieldName\": \"{FieldName}\",");
                sb.Append($"\"FieldType\": \"{FieldType}\",");
                sb.Append($"\"IsKey\": \"{IsKey}\"");
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
