using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace GraphQLService.Schema;

internal static class GraphQlTypeDescriptorHelper
{
    public static void ConfigureObjectField(IObjectTypeDescriptor descriptor, FieldDescriptor field)
    {
        var fd = descriptor.Field(field.GraphQlFieldName);
        ApplyScalar(fd, field.ScalarKind);
        fd.Resolve(ctx => ResolveFieldValue(ctx, field));
    }

    public static void ConfigureInputField(IInputObjectTypeDescriptor descriptor, FieldDescriptor field)
    {
        var fd = descriptor.Field(field.GraphQlFieldName);
        ApplyScalar(fd, field.ScalarKind);
    }

    private static void ApplyScalar(IObjectFieldDescriptor descriptor, GraphQlScalarKind kind)
    {
        switch (kind)
        {
            case GraphQlScalarKind.Int: descriptor.Type<IntType>(); break;
            case GraphQlScalarKind.Long: descriptor.Type<LongType>(); break;
            case GraphQlScalarKind.Float: descriptor.Type<FloatType>(); break;
            case GraphQlScalarKind.Decimal: descriptor.Type<DecimalType>(); break;
            case GraphQlScalarKind.Boolean: descriptor.Type<BooleanType>(); break;
            case GraphQlScalarKind.DateTime: descriptor.Type<DateTimeType>(); break;
            default: descriptor.Type<StringType>(); break;
        }
    }

    private static void ApplyScalar(IInputFieldDescriptor descriptor, GraphQlScalarKind kind)
    {
        switch (kind)
        {
            case GraphQlScalarKind.Int: descriptor.Type<IntType>(); break;
            case GraphQlScalarKind.Long: descriptor.Type<LongType>(); break;
            case GraphQlScalarKind.Float: descriptor.Type<FloatType>(); break;
            case GraphQlScalarKind.Decimal: descriptor.Type<DecimalType>(); break;
            case GraphQlScalarKind.Boolean: descriptor.Type<BooleanType>(); break;
            case GraphQlScalarKind.DateTime: descriptor.Type<DateTimeType>(); break;
            default: descriptor.Type<StringType>(); break;
        }
    }

    private static object? ResolveFieldValue(IResolverContext context, FieldDescriptor field)
    {
        if (context.Parent<object?>() is not IReadOnlyDictionary<string, object?> parent)
            return null;

        if (parent.TryGetValue(field.GraphQlFieldName, out var value) && value is not null)
            return value;

        if (parent.TryGetValue(field.FieldName, out value))
            return value;

        return null;
    }
}
