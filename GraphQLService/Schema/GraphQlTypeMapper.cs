using DbServices.Core.Models;
using Microsoft.Extensions.Logging;

namespace GraphQLService.Schema;

public static class GraphQlTypeMapper
{
    public static GraphQlScalarKind MapScalarKind(string? fieldType, ILogger? logger = null)
    {
        var normalized = fieldType?.ToLowerInvariant() ?? string.Empty;

        if (normalized.Contains("bigint") || normalized.Contains("int64"))
            return GraphQlScalarKind.Long;

        if (normalized.Contains("int") || normalized.Contains("integer") || normalized.Contains("smallint"))
            return GraphQlScalarKind.Int;

        if (normalized.Contains("decimal") || normalized.Contains("numeric") || normalized.Contains("money"))
            return GraphQlScalarKind.Decimal;

        if (normalized.Contains("float") || normalized.Contains("double") || normalized.Contains("real"))
            return GraphQlScalarKind.Float;

        if (normalized.Contains("bool") || normalized.Contains("bit"))
            return GraphQlScalarKind.Boolean;

        if (normalized.Contains("date") || normalized.Contains("time"))
            return GraphQlScalarKind.DateTime;

        if (!string.IsNullOrEmpty(normalized) &&
            normalized is not ("text" or "varchar" or "nvarchar" or "char" or "string" or "json" or "blob"))
        {
            logger?.LogDebug("欄位型別 {FieldType} 使用 String fallback", fieldType);
        }

        return GraphQlScalarKind.String;
    }

    public static FieldDescriptor ToFieldDescriptor(FieldBaseModel field, ILogger? logger = null)
    {
        var graphQlFieldName = GraphQlNaming.ToGraphQlFieldName(field.FieldName ?? "field");
        return new FieldDescriptor(
            FieldName: field.FieldName ?? graphQlFieldName,
            GraphQlFieldName: graphQlFieldName,
            ScalarKind: MapScalarKind(field.FieldType, logger),
            IsPrimaryKey: field.IsPrimaryKey);
    }
}
