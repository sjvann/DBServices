using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBService.Models
{
    public class ForeignBaseModel
    {
        public string? ComeFromTableName { get; set; }
        public string? ComeFromFieldName { get; set; }
        public string? CurrentFieldName { get; set; }


        public string ToJsonString(bool ignoreNull = false)
        {
            StringBuilder sb = new();
            sb.Append('{');
            if (ignoreNull)
            {
                if (!string.IsNullOrEmpty(ComeFromTableName)) { sb.Append($"\"ComeFromTableName\": \"{ComeFromTableName}\","); }
                if (!string.IsNullOrEmpty(ComeFromFieldName)) { sb.Append($"\"ComeFromFieldName\": \"{ComeFromFieldName}\","); }
                if (!string.IsNullOrEmpty(CurrentFieldName)) { sb.Append($"\"CurrentFieldName\": \"{CurrentFieldName}\""); }
            }
            else
            {
                sb.Append($"\"ComeFromTableName\": \"{ComeFromTableName}\",");
                sb.Append($"\"ComeFromFieldName\": \"{ComeFromFieldName}\",");
                sb.Append($"\"CurrentFieldName\": \"{CurrentFieldName}\"");
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
