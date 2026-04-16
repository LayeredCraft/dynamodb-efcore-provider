using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class PriorityWorkOrderSeedItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(
        PriorityWorkOrderSeedItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(
        List<PriorityWorkOrderSeedItem> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(
        PriorityWorkOrderSeedItem source,
        Dictionary<string, AttributeValue> item)
        => item["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" };
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class ArchivedWorkOrderSeedItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(
        ArchivedWorkOrderSeedItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(
        List<ArchivedWorkOrderSeedItem> sources)
        => sources.Select(ToItem).ToList();

    private static void AfterToItem(
        ArchivedWorkOrderSeedItem source,
        Dictionary<string, AttributeValue> item)
        => item["$type"] = new AttributeValue { S = "ArchivedWorkOrderEntity" };
}
