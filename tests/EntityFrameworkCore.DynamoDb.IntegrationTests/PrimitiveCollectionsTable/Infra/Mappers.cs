using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class MutablePrimitiveCollectionsItemMapper
{
    internal static List<Dictionary<string, AttributeValue>> ToItems(
        List<MutablePrimitiveCollectionsItem> source)
        => source.Select(ToItem).ToList();

    internal static partial Dictionary<string, AttributeValue> ToItem(
        MutablePrimitiveCollectionsItem source);

    internal static partial MutablePrimitiveCollectionsItem FromItem(
        Dictionary<string, AttributeValue> item);

    private static void AfterToItem(
        MutablePrimitiveCollectionsItem source,
        Dictionary<string, AttributeValue> item)
        => item["$type"] = new AttributeValue { S = nameof(MutablePrimitiveCollectionsItem) };
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
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

    private static void AfterToItem(
        PrimitiveCollectionsItem source,
        Dictionary<string, AttributeValue> item)
        => item["$type"] = new AttributeValue { S = nameof(PrimitiveCollectionsItem) };
}
