using DbServices.Core.Models;

namespace GraphQLService.Schema;

public sealed class TableDescriptor
{
    public required string TableName { get; init; }
    public required string GraphQlTypeName { get; init; }
    public required string InputTypeName { get; init; }
    public required string ListFieldName { get; init; }
    public required string SingularFieldName { get; init; }
    public required string ByFieldName { get; init; }
    public required string CreateMutationName { get; init; }
    public required string UpdateMutationName { get; init; }
    public required string DeleteMutationName { get; init; }
    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }

    public static TableDescriptor Create(string tableName, IEnumerable<FieldBaseModel>? fields, ILogger? logger = null)
    {
        var graphQlTypeName = GraphQlNaming.ToGraphQlTypeName(tableName);
        var fieldList = fields?
            .Select(f => GraphQlTypeMapper.ToFieldDescriptor(f, logger))
            .ToList() ?? [];

        if (fieldList.All(f => !f.IsPrimaryKey))
        {
            fieldList.Insert(0, new FieldDescriptor("Id", "id", GraphQlScalarKind.Int, true));
        }

        var listFieldName = GraphQlNaming.ToListFieldName(tableName);

        return new TableDescriptor
        {
            TableName = tableName,
            GraphQlTypeName = graphQlTypeName,
            InputTypeName = GraphQlNaming.ToInputTypeName(graphQlTypeName),
            ListFieldName = listFieldName,
            SingularFieldName = GraphQlNaming.ToSingularFieldName(tableName),
            ByFieldName = GraphQlNaming.ToByFieldName(listFieldName),
            CreateMutationName = GraphQlNaming.ToCreateMutationName(graphQlTypeName),
            UpdateMutationName = GraphQlNaming.ToUpdateMutationName(graphQlTypeName),
            DeleteMutationName = GraphQlNaming.ToDeleteMutationName(graphQlTypeName),
            Fields = fieldList
        };
    }
}
