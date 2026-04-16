using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class OwnedTypesItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OwnedShapeItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<OwnedShapeItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial OwnedShapeItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<OwnedShapeItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
