using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DBServices.Util
{
    public class JsonUtil
    {
        public static T? MappingJson<T>(JsonNode? source)
        {
            if (source is null) return default;
            var properties = typeof(T).GetProperties();
            var temp = Activator.CreateInstance<T>();
            foreach (var property in properties)
            {
                var value = source[property.Name]?.AsValue();
                if (value != null)
                {
                    if (value.GetType().Name == property.PropertyType.Name)
                    {
                        property.SetValue(temp, value);

                    }
                    else
                    {
                        switch (property.PropertyType.Name)
                        {
                            case "Int32":
                                if (CheckNullValue<int>(value) is int intvalue)
                                {
                                    property.SetValue(temp, intvalue);
                                }
                                break;
                            case "Int64":
                                if (CheckNullValue<long>(value) is long longvalue)
                                {
                                    property.SetValue(temp, longvalue);
                                }
                                break;
                            case "Decimal":
                                if (CheckNullValue<decimal>(value) is decimal decimalvalue)
                                {
                                    property.SetValue(temp, decimalvalue);
                                }
                                break;
                            case "String":
                                if (CheckNullValue<string>(value) is string stringvalue)
                                {
                                    property.SetValue(temp, stringvalue);
                                }

                                break;
                            case "Boolean":
                                if (CheckNullValue<bool>(value) is bool boolvalue)
                                {
                                    property.SetValue(temp, boolvalue);
                                }
                                else if (CheckNullValue<int>(value) is int intboolvalue)
                                {
                                    property.SetValue(temp, intboolvalue == 1);
                                }
                                break;
                            case "DateTime":
                                if (CheckNullValue<DateTime>(value) is DateTime datetimevalue)
                                {
                                    property.SetValue(temp, datetimevalue);
                                }
                                else if (CheckNullValue<string>(value) is string stringdatetimevalue)
                                {
                                    if (DateTime.TryParse(stringdatetimevalue, out DateTime dateTime))
                                    {
                                        property.SetValue(temp, dateTime);
                                    }
                                }
                                break;
                            case "Double":
                                if (CheckNullValue<double>(value) is double doublevalue)
                                {
                                    property.SetValue(temp, doublevalue);
                                }
                                break;
                            default:

                                object? obj = CheckNullValueForNull(property.PropertyType, value);

                                property.SetValue(temp, obj);
                                break;
                        }

                    }

                }

            }
            return temp;
        }
        public static object? CheckNullValueForNull(Type name, JsonValue value)
        {
            string typeName = name.GenericTypeArguments[0].Name;

            switch (typeName)
            {
                case "Int32":
                    if (CheckNullValue<int>(value) is int intvalue)
                    {
                        return intvalue;
                    }
                    return default;
                case "Int64":
                    if (CheckNullValue<long>(value) is long longvalue)
                    {
                        return longvalue;
                    }
                    return default;
                case "Decimal":
                    if (CheckNullValue<decimal>(value) is decimal decimalvalue)
                    {
                        return decimalvalue;
                    }
                    return default;
                case "String":
                    if (CheckNullValue<string>(value) is string stringvalue)
                    {
                        return stringvalue;
                    }
                    return default;
                case "Boolean":
                    if (CheckNullValue<bool>(value) is bool boolvalue)
                    {
                        return boolvalue;
                    }
                    else if (CheckNullValue<int>(value) is int intboolvalue)
                    {
                        return intboolvalue == 1;
                    }
                    return default;
                case "DateTime":
                    if (CheckNullValue<DateTime>(value) is DateTime datetimevalue)
                    {
                        return datetimevalue;
                    }
                    else if (CheckNullValue<string>(value) is string stringdatetimevalue)
                    {
                        if (DateTime.TryParse(stringdatetimevalue, out DateTime dateTime))
                        {
                            return dateTime;
                        }
                    }
                    return default;
                case "Double":
                    if (CheckNullValue<double>(value) is double doublevalue)
                    {
                        return doublevalue;
                    }
                    return default;
                default:
                    return default;
            }

        }

        public static T? CheckNullValue<T>(JsonValue value)
        {
            value.TryGetValue(out T? result);

            return result;
        }
    }
}
