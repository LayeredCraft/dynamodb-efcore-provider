using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullValues = false)]
internal static partial class SimpleItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SimpleItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<SimpleItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial SimpleItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<SimpleItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
