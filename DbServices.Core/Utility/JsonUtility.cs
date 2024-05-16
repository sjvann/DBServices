using System.Text.Json.Nodes;

namespace DbServices.Core.Utility
{
    public static class JsonUtility
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
                                if (CheckNullValue<int>(value) is int intValue)
                                {
                                    property.SetValue(temp, intValue);
                                }
                                break;
                            case "Int64":
                                if (CheckNullValue<long>(value) is long longValue)
                                {
                                    property.SetValue(temp, longValue);
                                }
                                break;
                            case "Decimal":
                                if (CheckNullValue<decimal>(value) is decimal decimalValue)
                                {
                                    property.SetValue(temp, decimalValue);
                                }
                                break;
                            case "String":
                                if (CheckNullValue<string>(value) is string stringValue)
                                {
                                    property.SetValue(temp, stringValue);
                                }

                                break;
                            case "Boolean":
                                if (CheckNullValue<bool>(value) is bool boolValue)
                                {
                                    property.SetValue(temp, boolValue);
                                }
                                else if (CheckNullValue<int>(value) is int intBoolValue)
                                {
                                    property.SetValue(temp, intBoolValue == 1);
                                }
                                break;
                            case "DateTime":
                                if (CheckNullValue<DateTime>(value) is DateTime datetimeValue)
                                {
                                    property.SetValue(temp, datetimeValue);
                                }
                                else if (CheckNullValue<string>(value) is string stringDatetimeValue)
                                {
                                   
                                        property.SetValue(temp, DateTime.Parse(stringDatetimeValue));
                                    
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
                    if (CheckNullValue<int>(value) is int intValue)
                    {
                        return intValue;
                    }
                    return default;
                case "Int64":
                    if (CheckNullValue<long>(value) is long longValue)
                    {
                        return longValue;
                    }
                    return default;
                case "Decimal":
                    if (CheckNullValue<decimal>(value) is decimal decimalValue)
                    {
                        return decimalValue;
                    }
                    return default;
                case "String":
                    if (CheckNullValue<string>(value) is string stringValue)
                    {
                        return stringValue;
                    }
                    return default;
                case "Boolean":
                    if (CheckNullValue<bool>(value) is bool boolValue)
                    {
                        return boolValue;
                    }
                    else if (CheckNullValue<int>(value) is int intboolValue)
                    {
                        return intboolValue == 1;
                    }
                    return default;
                case "DateTime":
                    if (CheckNullValue<DateTime>(value) is DateTime datetimeValue)
                    {
                        return datetimeValue;
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
