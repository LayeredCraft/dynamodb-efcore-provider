using System.Globalization;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
[DynamoIgnore("DateOnlyValue")]
[DynamoIgnore("TimeOnlyValue")]
internal static partial class SimpleItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(SimpleItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<SimpleItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial SimpleItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<SimpleItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();

    static void AfterToItem(SimpleItem source, Dictionary<string, AttributeValue> item)
    {
        item["dateOnlyValue"] =
            new AttributeValue { S = source.DateOnlyValue.ToString("yyyy-MM-dd") };
        item["timeOnlyValue"] =
            new AttributeValue { S = source.TimeOnlyValue.ToString("HH:mm:ss") };
    }

    static void AfterFromItem(Dictionary<string, AttributeValue> item, ref SimpleItem entity)
    {
        if (item.TryGetValue("dateOnlyValue", out var d) && d.S is { } dateStr)
            entity.DateOnlyValue = DateOnly.Parse(dateStr, CultureInfo.InvariantCulture);
        if (item.TryGetValue("timeOnlyValue", out var t) && t.S is { } timeStr)
            entity.TimeOnlyValue = TimeOnly.Parse(timeStr, CultureInfo.InvariantCulture);
    }
}
