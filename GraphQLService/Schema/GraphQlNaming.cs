using System.Text;
using System.Text.RegularExpressions;

namespace GraphQLService.Schema;

public static partial class GraphQlNaming
{
    [GeneratedRegex(@"[^_A-Za-z0-9]")]
    private static partial Regex InvalidNameRegex();

    public static string ToGraphQlTypeName(string tableName)
    {
        var sanitized = Sanitize(tableName);
        if (string.IsNullOrEmpty(sanitized))
            return "Table";
        return char.ToUpperInvariant(sanitized[0]) + sanitized[1..];
    }

    public static string ToInputTypeName(string graphQlTypeName) => $"{graphQlTypeName}Input";

    public static string ToListFieldName(string tableName)
    {
        var camel = ToCamelCase(Sanitize(tableName));
        if (camel.EndsWith('s'))
            return camel;
        return camel + "s";
    }

    public static string ToSingularFieldName(string tableName)
    {
        var camel = ToCamelCase(Sanitize(tableName));
        if (camel.EndsWith('s') && camel.Length > 1)
            return camel[..^1];
        return camel;
    }

    public static string ToByFieldName(string listFieldName) => $"{listFieldName}ByField";

    public static string ToCreateMutationName(string graphQlTypeName) => $"create{graphQlTypeName}";

    public static string ToUpdateMutationName(string graphQlTypeName) => $"update{graphQlTypeName}";

    public static string ToDeleteMutationName(string graphQlTypeName) => $"delete{graphQlTypeName}";

    public static string ToGraphQlFieldName(string fieldName)
    {
        var sanitized = Sanitize(fieldName);
        return ToCamelCase(sanitized);
    }

    private static string Sanitize(string name)
    {
        var replaced = InvalidNameRegex().Replace(name, "_");
        if (string.IsNullOrEmpty(replaced))
            return "field";
        if (char.IsDigit(replaced[0]))
            return "_" + replaced;
        return replaced;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (value.Length == 1)
            return value.ToLowerInvariant();
        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
