using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DbServices.Core.Models
{
    public class FieldBaseModel
    {
        public string? FieldName { get; set; }
        public string? FieldType { get; set; }
        public bool IsNotNull { get; set; } = false;
        public bool IsPrimaryKey { get; set; } = false;
        public bool IsForeignKey { get; set; } = false;
        public ForeignBaseModel? ForeignInfo { get; set; }


        public static FieldBaseModel ParseOneField(JsonNode? source)
        {
            FieldBaseModel result = new();
            if (source is JsonObject oneField)
            {

                result.FieldName = oneField["FieldName"]?.AsValue().GetValue<string>();
                result.FieldType = oneField["FieldType"]?.AsValue().GetValue<string>();
                result.IsNotNull = oneField["IsNotNull"]?.AsValue().GetValue<bool>() ?? false;
                result.IsPrimaryKey = oneField["IsPrimaryKey"]?.AsValue().GetValue<bool>() ?? false;
                result.IsForeignKey = oneField["IsForeignKey"]?.AsValue().GetValue<bool>() ?? false;
            }
            return result;
        }
        public static IEnumerable<FieldBaseModel> Parses(JsonNode? source)
        {
            List<FieldBaseModel> result = new();
            if (source is JsonArray manyFields)
            {
                foreach (var item in manyFields)
                {
                    result.Add(ParseOneField(item));
                }
            }
            return result;
        }

        public string ToJsonString(bool ignoreNull = false, bool pretty = true)
        {
            StringBuilder sb = new();
            string newLine = pretty ? Environment.NewLine : string.Empty;
            sb.Append('{').Append(newLine);
            if (ignoreNull)
            {
                if (!string.IsNullOrEmpty(FieldName)) { sb.Append($"\"FieldName\": \"{FieldName}\",").Append(newLine); }
                if (!string.IsNullOrEmpty(FieldType)) { sb.Append($"\"FieldType\": \"{FieldType}\",").Append(newLine); }
                sb.Append($"\"IsNotNull\": \"{IsNotNull}\",").Append(newLine);
                sb.Append($"\"IsPK\": \"{IsPrimaryKey}\",").Append(newLine);

                if (ForeignInfo != null)
                {
                    sb.Append($"\"IsFK\": \"{IsForeignKey}\",").Append(newLine);
                    sb.Append($"\"ForeignInfo\": {ForeignInfo.ToJsonString()}").Append(newLine);
                }
                else
                {
                    sb.Append($"\"IsFK\": \"{IsForeignKey}\"").Append(newLine);
                }
            }
            else
            {
                sb.Append($"\"FieldName\": \"{FieldName}\",").Append(newLine);
                sb.Append($"\"FieldType\": \"{FieldType}\",").Append(newLine);
                sb.Append($"\"IsNotNull\": \"{IsNotNull}\",").Append(newLine);
                sb.Append($"\"IsPK\": \"{IsPrimaryKey}\",").Append(newLine);
                sb.Append($"\"IsFK\": \"{IsForeignKey}\",").Append(newLine);
                sb.Append($"\"ForeignInfo\": {ForeignInfo?.ToJsonString()}").Append(newLine);
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
