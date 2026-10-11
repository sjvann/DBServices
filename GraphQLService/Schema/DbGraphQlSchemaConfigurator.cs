using GraphQLService.Resolvers;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace GraphQLService.Schema;

public static class DbGraphQlSchemaConfigurator
{
    public static IRequestExecutorBuilder AddDynamicDbSchema(
        IRequestExecutorBuilder builder,
        DbSchemaRegistry registry)
    {
        builder.AddType(new ObjectType<MetaPayload>(d =>
        {
            d.Name("MetaPayload");
            d.Field(t => t.Tables).Type<NonNullType<ListType<NonNullType<StringType>>>>();
        }));

        builder
            .AddQueryType(ConfigureQuery(registry))
            .AddMutationType(ConfigureMutation(registry));

        foreach (var table in registry.Tables)
        {
            builder
                .AddType(CreateTableObjectType(table))
                .AddType(CreateInputObjectType(table));
        }

        return builder;
    }

    private static Action<IObjectTypeDescriptor> ConfigureQuery(DbSchemaRegistry registry)
        => descriptor =>
        {
            descriptor.Name(OperationTypeNames.Query);

            descriptor.Field("meta")
                .Type("MetaPayload!")
                .Resolve(ctx => ctx.Service<DynamicTableResolver>().GetMetaAsync(ctx.RequestAborted));

            descriptor.Field("fieldValueSet")
                .Argument("table", a => a.Type<NonNullType<StringType>>())
                .Argument("field", a => a.Type<NonNullType<StringType>>())
                .Type<NonNullType<ListType<StringType>>>()
                .Resolve(ctx =>
                {
                    var table = ctx.ArgumentValue<string>("table");
                    var field = ctx.ArgumentValue<string>("field");
                    return ctx.Service<DynamicTableResolver>()
                        .GetFieldValueSetAsync(table, field, ctx.RequestAborted);
                });

            foreach (var table in registry.Tables)
            {
                var captured = table;

                descriptor.Field(captured.ListFieldName)
                    .Type($"[{captured.GraphQlTypeName}!]!")
                    .Resolve(ctx => ctx.Service<DynamicTableResolver>()
                        .GetRecordsAsync(captured, ctx.RequestAborted));

                descriptor.Field(captured.SingularFieldName)
                    .Argument("id", a => a.Type<NonNullType<IntType>>())
                    .Type(captured.GraphQlTypeName)
                    .Resolve(ctx =>
                    {
                        var id = ctx.ArgumentValue<int>("id");
                        return ctx.Service<DynamicTableResolver>()
                            .GetRecordByIdAsync(captured, id, ctx.RequestAborted);
                    });

                descriptor.Field(captured.ByFieldName)
                    .Argument("key", a => a.Type<NonNullType<StringType>>())
                    .Argument("value", a => a.Type<NonNullType<StringType>>())
                    .Type($"[{captured.GraphQlTypeName}!]!")
                    .Resolve(ctx =>
                    {
                        var key = ctx.ArgumentValue<string>("key");
                        var value = ctx.ArgumentValue<string>("value");
                        return ctx.Service<DynamicTableResolver>()
                            .GetRecordsByFieldAsync(captured, key, value, ctx.RequestAborted);
                    });
            }
        };

    private static Action<IObjectTypeDescriptor> ConfigureMutation(DbSchemaRegistry registry)
        => descriptor =>
        {
            descriptor.Name(OperationTypeNames.Mutation);

            // 如果沒有表，添加一個佔位字段以滿足 HotChocolate 的要求
            if (registry.Tables.Count == 0)
            {
                descriptor.Field("_placeholder")
                    .Type<StringType>()
                    .Resolve(_ => "No tables available. Please create tables in your database.");
            }

            foreach (var table in registry.Tables)
            {
                var captured = table;

                descriptor.Field(captured.CreateMutationName)
                    .Argument("input", a => a.Type($"{captured.InputTypeName}!"))
                    .Type(captured.GraphQlTypeName)
                    .Resolve(ctx => ctx.Service<DynamicTableResolver>()
                        .CreateRecordAsync(captured, ctx, ctx.RequestAborted));

                descriptor.Field(captured.UpdateMutationName)
                    .Argument("id", a => a.Type<NonNullType<IntType>>())
                    .Argument("input", a => a.Type($"{captured.InputTypeName}!"))
                    .Type(captured.GraphQlTypeName)
                    .Resolve(ctx =>
                    {
                        var id = ctx.ArgumentValue<int>("id");
                        return ctx.Service<DynamicTableResolver>()
                            .UpdateRecordAsync(captured, id, ctx, ctx.RequestAborted);
                    });

                descriptor.Field(captured.DeleteMutationName)
                    .Argument("id", a => a.Type<NonNullType<IntType>>())
                    .Type<NonNullType<BooleanType>>()
                    .Resolve(ctx =>
                    {
                        var id = ctx.ArgumentValue<int>("id");
                        return ctx.Service<DynamicTableResolver>()
                            .DeleteRecordAsync(captured, id, ctx.RequestAborted);
                    });
            }
        };

    private static ObjectType CreateTableObjectType(TableDescriptor table)
    {
        return new ObjectType(descriptor =>
        {
            descriptor.Name(table.GraphQlTypeName);
            foreach (var field in table.Fields)
                GraphQlTypeDescriptorHelper.ConfigureObjectField(descriptor, field);
        });
    }

    private static InputObjectType CreateInputObjectType(TableDescriptor table)
    {
        return new InputObjectType(descriptor =>
        {
            descriptor.Name(table.InputTypeName);
            foreach (var field in table.Fields.Where(f => !f.IsPrimaryKey))
                GraphQlTypeDescriptorHelper.ConfigureInputField(descriptor, field);
        });
    }
}
