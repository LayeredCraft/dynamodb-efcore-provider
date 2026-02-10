using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public record PrimitiveCollectionsItem(
    string Pk,
    List<string> Tags,
    Dictionary<string, int> ScoresByCategory,
    HashSet<string> LabelSet,
    HashSet<int> RatingSet,
    Dictionary<string, string> Metadata,
    List<string>? OptionalTags);

public static class PrimitiveCollectionsItems
{
    public static readonly List<PrimitiveCollectionsItem> Items =
    [
        new(
            "ITEM#A",
            ["alpha", "beta"],
            new Dictionary<string, int> { ["math"] = 10, ["science"] = 20 },
            ["alpha", "common"],
            [1, 2],
            new Dictionary<string, string> { ["tier"] = "gold", ["region"] = "eu" },
            null),
        new(
            "ITEM#B",
            ["gamma"],
            new Dictionary<string, int> { ["math"] = 99 },
            ["gamma"],
            [3],
            new Dictionary<string, string>
            {
                ["tier"] = "silver", ["region"] = "us", ["flag_a"] = "1",
            },
            ["opt1", "opt2"]),
        new(
            "ITEM#C",
            [],
            new Dictionary<string, int>(),
            ["common"],
            [2],
            new Dictionary<string, string>(),
            ["only"]),
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => PrimitiveCollectionsItemMapper.ToItems(Items);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullStrings = false)]
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
