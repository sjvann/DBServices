namespace GraphQLService.Schema;

public sealed record FieldDescriptor(
    string FieldName,
    string GraphQlFieldName,
    GraphQlScalarKind ScalarKind,
    bool IsPrimaryKey);
