using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class OrderItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<OrderItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<OrderItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();

    private static void AfterToItem(OrderItem source, Dictionary<string, AttributeValue> item)
        => item["$type"] = new AttributeValue { S = nameof(OrderItem) };
}
