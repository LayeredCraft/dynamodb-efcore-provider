using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class PrimitiveCollectionsItemMapper
{
    internal static List<Dictionary<string, AttributeValue>> ToItems(
        List<PrimitiveCollectionsItem> source)
        => source.Select(ToItem).ToList();

    internal static List<PrimitiveCollectionsItem> FromItems(
        List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();

    internal static partial Dictionary<string, AttributeValue> ToItem(
        PrimitiveCollectionsItem source);

    internal static partial PrimitiveCollectionsItem FromItem(
        Dictionary<string, AttributeValue> item);
}
