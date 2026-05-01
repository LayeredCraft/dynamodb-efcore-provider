using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class ComplexTypesItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ComplexShapeItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<ComplexShapeItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial ComplexShapeItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<ComplexShapeItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
