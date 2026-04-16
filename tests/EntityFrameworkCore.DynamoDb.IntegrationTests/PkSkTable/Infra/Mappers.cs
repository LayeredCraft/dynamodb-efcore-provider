using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal static partial class PkSkItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(PkSkItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<PkSkItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial PkSkItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<PkSkItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
