using GraphQLService.Models;
using GraphQLService.Schema;

namespace GraphQLService.Services;

public static class SchemaTreeBuilder
{
    /// <summary>
    /// 最左欄：目前資料庫所有資料表（扁平清單）。
    /// </summary>
    public static IReadOnlyList<TreeNodeDto> BuildTablesTree(DbSchemaRegistry? registry)
    {
        if (registry?.Tables is null || registry.Tables.Count == 0)
            return [];

        return registry.Tables
            .OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TreeNodeDto
            {
                Id = $"table.{t.TableName}",
                Label = t.TableName,
                Kind = "table",
                TableName = t.TableName,
                GraphQlTypeName = t.GraphQlTypeName,
                ListFieldName = t.ListFieldName,
                SingularFieldName = t.SingularFieldName,
                ByFieldName = t.ByFieldName
            })
            .ToList();
    }

    /// <summary>
    /// 中欄：選定資料表的欄位與查詢範本。
    /// </summary>
    public static IReadOnlyList<TreeNodeDto> BuildTableDetailTree(
        DbSchemaRegistry? registry,
        string tableName)
    {
        var table = registry?.Tables.FirstOrDefault(
            t => string.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase));

        if (table is null)
            return [];

        var detail = BuildTableNode(table);
        return detail.Children ?? [];
    }

    public static IReadOnlyList<TreeNodeDto> BuildSchemaTree(DbSchemaRegistry? registry)
    {
        if (registry?.Tables is null || registry.Tables.Count == 0)
            return [];

        var tables = registry.Tables
            .Select(t => BuildTableNode(t))
            .OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
        [
            new TreeNodeDto
            {
                Id = "root.meta",
                Label = "meta",
                Kind = "query",
                Expanded = true,
                Children =
                [
                    new TreeNodeDto
                    {
                        Id = "query.meta",
                        Label = "meta { tables }",
                        Kind = "querySnippet",
                        ListFieldName = "meta"
                    }
                ]
            },
            ..tables
        ];
    }

    private static TreeNodeDto BuildTableNode(TableDescriptor table)
    {
        var fieldNodes = table.Fields
            .Select(f => new TreeNodeDto
            {
                Id = $"field.{table.TableName}.{f.GraphQlFieldName}",
                Label = $"{f.GraphQlFieldName} ({f.ScalarKind})",
                Kind = "field",
                TableName = table.TableName,
                FieldName = f.GraphQlFieldName,
                GraphQlTypeName = table.GraphQlTypeName,
                ScalarKind = f.ScalarKind.ToString()
            })
            .ToList();

        var queryChildren = new List<TreeNodeDto>
        {
            new()
            {
                Id = $"query.{table.ListFieldName}.list",
                Label = $"{table.ListFieldName} {{ ... }}",
                Kind = "querySnippet",
                TableName = table.TableName,
                GraphQlTypeName = table.GraphQlTypeName,
                ListFieldName = table.ListFieldName
            },
            new()
            {
                Id = $"query.{table.SingularFieldName}.byId",
                Label = $"{table.SingularFieldName}(id) {{ ... }}",
                Kind = "querySnippet",
                TableName = table.TableName,
                GraphQlTypeName = table.GraphQlTypeName,
                SingularFieldName = table.SingularFieldName
            },
            new()
            {
                Id = $"query.{table.ByFieldName}",
                Label = $"{table.ByFieldName}(key, value) {{ ... }}",
                Kind = "querySnippet",
                TableName = table.TableName,
                ListFieldName = table.ByFieldName
            },
            new()
            {
                Id = $"mutation.create.{table.CreateMutationName}",
                Label = $"{table.CreateMutationName}(input)",
                Kind = "mutationSnippet",
                TableName = table.TableName,
                GraphQlTypeName = table.GraphQlTypeName
            }
        };

        return new TreeNodeDto
        {
            Id = $"table.{table.TableName}",
            Label = table.TableName,
            Kind = "table",
            TableName = table.TableName,
            GraphQlTypeName = table.GraphQlTypeName,
            ListFieldName = table.ListFieldName,
            SingularFieldName = table.SingularFieldName,
            Expanded = false,
            Children =
            [
                new TreeNodeDto
                {
                    Id = $"table.{table.TableName}.queries",
                    Label = "查詢範本",
                    Kind = "folder",
                    Children = queryChildren
                },
                new TreeNodeDto
                {
                    Id = $"table.{table.TableName}.fields",
                    Label = "欄位",
                    Kind = "folder",
                    Children = fieldNodes
                }
            ]
        };
    }

    public static IReadOnlyList<TreeNodeDto> JsonToTree(object? value, string idPrefix = "root")
        => JsonValueToTree(value, idPrefix);

    private static IReadOnlyList<TreeNodeDto> JsonValueToTree(object? value, string idPrefix)
    {
        if (value is null)
        {
            return
            [
                new TreeNodeDto { Id = $"{idPrefix}.null", Label = "null", Kind = "value" }
            ];
        }

        if (value is IReadOnlyDictionary<string, object?> dict)
            return DictToTree(dict, idPrefix);

        if (value is Dictionary<string, object?> dict2)
            return DictToTree(dict2, idPrefix);

        if (value is System.Text.Json.JsonElement je)
            return JsonElementToTree(je, idPrefix);

        if (value is IEnumerable<object?> list && value is not string)
        {
            var items = list.ToList();
            var children = new List<TreeNodeDto>();
            for (var i = 0; i < items.Count; i++)
            {
                var childNodes = JsonValueToTree(items[i], $"{idPrefix}[{i}]");
                children.Add(new TreeNodeDto
                {
                    Id = $"{idPrefix}[{i}]",
                    Label = $"[{i}]",
                    Kind = "arrayItem",
                    Expanded = i == 0,
                    Children = childNodes
                });
            }
            return children;
        }

        return
        [
            new TreeNodeDto
            {
                Id = $"{idPrefix}.value",
                Label = FormatScalar(value),
                Kind = "value"
            }
        ];
    }

    private static IReadOnlyList<TreeNodeDto> DictToTree(
        IReadOnlyDictionary<string, object?> dict,
        string idPrefix)
    {
        return dict
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv =>
            {
                var childId = $"{idPrefix}.{kv.Key}";
                if (kv.Value is IReadOnlyDictionary<string, object?> nested)
                {
                    return new TreeNodeDto
                    {
                        Id = childId,
                        Label = kv.Key,
                        Kind = "object",
                        Expanded = true,
                        Children = DictToTree(nested, childId)
                    };
                }

                if (kv.Value is Dictionary<string, object?> nested2)
                {
                    return new TreeNodeDto
                    {
                        Id = childId,
                        Label = kv.Key,
                        Kind = "object",
                        Expanded = true,
                        Children = DictToTree(nested2, childId)
                    };
                }

                if (kv.Value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    return new TreeNodeDto
                    {
                        Id = childId,
                        Label = kv.Key,
                        Kind = "object",
                        Expanded = true,
                        Children = JsonElementToTree(je, childId)
                    };
                }

                if (kv.Value is IEnumerable<object?> list && kv.Value is not string)
                {
                    return new TreeNodeDto
                    {
                        Id = childId,
                        Label = $"{kv.Key} [{list.Count()}]",
                        Kind = "array",
                        Expanded = false,
                        Children = JsonValueToTree(kv.Value, childId)
                    };
                }

                return new TreeNodeDto
                {
                    Id = childId,
                    Label = $"{kv.Key}: {FormatScalar(kv.Value)}",
                    Kind = "value"
                };
            })
            .ToList();
    }

    private static IReadOnlyList<TreeNodeDto> JsonElementToTree(System.Text.Json.JsonElement element, string idPrefix)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .Select(p => JsonPropertyToNode(p, idPrefix))
                .ToList(),
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray()
                .Select((item, i) => new TreeNodeDto
                {
                    Id = $"{idPrefix}[{i}]",
                    Label = $"[{i}]",
                    Kind = "arrayItem",
                    Expanded = i == 0,
                    Children = JsonElementToTree(item, $"{idPrefix}[{i}]")
                })
                .ToList(),
            _ =>
            [
                new TreeNodeDto
                {
                    Id = $"{idPrefix}.value",
                    Label = element.ToString(),
                    Kind = "value"
                }
            ]
        };
    }

    private static TreeNodeDto JsonPropertyToNode(System.Text.Json.JsonProperty prop, string idPrefix)
    {
        var childId = $"{idPrefix}.{prop.Name}";
        return prop.Value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => new TreeNodeDto
            {
                Id = childId,
                Label = prop.Name,
                Kind = "object",
                Expanded = false,
                Children = JsonElementToTree(prop.Value, childId)
            },
            System.Text.Json.JsonValueKind.Array => new TreeNodeDto
            {
                Id = childId,
                Label = $"{prop.Name} [{prop.Value.GetArrayLength()}]",
                Kind = "array",
                Expanded = false,
                Children = JsonElementToTree(prop.Value, childId)
            },
            _ => new TreeNodeDto
            {
                Id = childId,
                Label = $"{prop.Name}: {prop.Value}",
                Kind = "value"
            }
        };
    }

    private static string FormatScalar(object? value)
        => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            _ => value.ToString() ?? ""
        };
}
