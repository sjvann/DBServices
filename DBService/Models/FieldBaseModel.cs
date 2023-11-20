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
        public bool IsNotNull { get; set; } = false;
        public bool IsPrimaryKey { get; set; } = false;
        public bool IsForeignKey { get; set; } = false;
        public ForeignBaseModel? ForeignInfo { get; set; }


        public string ToJsonString(bool ignoreNull = false)
        {
            StringBuilder sb = new();
            sb.Append('{');
            if (ignoreNull)
            {
                if (!string.IsNullOrEmpty(FieldName)) { sb.Append($"\"FieldName\": \"{FieldName}\","); }
                if (!string.IsNullOrEmpty(FieldType)) { sb.Append($"\"FieldType\": \"{FieldType}\","); }
                sb.Append($"\"IsNotNull\": \"{IsNotNull}\",");
                sb.Append($"\"IsPK\": \"{IsPrimaryKey}\",");
               
                if(ForeignInfo != null)
                {
                     sb.Append($"\"IsFK\": \"{IsForeignKey}\",");
                    sb.Append($"\"ForeignInfo\": {ForeignInfo.ToJsonString()}");
                }
                else
                {
                     sb.Append($"\"IsFK\": \"{IsForeignKey}\"");
                }
            }
            else
            {
                sb.Append($"\"FieldName\": \"{FieldName}\",");
                sb.Append($"\"FieldType\": \"{FieldType}\",");
                sb.Append($"\"IsNotNull\": \"{IsNotNull}\",");
                sb.Append($"\"IsPK\": \"{IsPrimaryKey}\",");
                sb.Append($"\"IsFK\": \"{IsForeignKey}\",");
                sb.Append($"\"ForeignInfo\": {ForeignInfo?.ToJsonString()}");
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
