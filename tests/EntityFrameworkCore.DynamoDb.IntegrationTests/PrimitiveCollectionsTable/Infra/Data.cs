using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

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
                ["tier"] = "silver", ["region"] = "us", ["flag_a"] = "1"
            },
            ["opt1", "opt2"]),
        new(
            "ITEM#C",
            [],
            new Dictionary<string, int>(),
            ["common"],
            [2],
            new Dictionary<string, string>(),
            ["only"])
    ];

    public static readonly List<MutablePrimitiveCollectionsItem> MutableItems =
    [
        new()
        {
            Pk = "ITEM#MUTABLE#A",
            ChargesByTier =
                new Dictionary<string, decimal> { ["gold"] = 1.25m, ["silver"] = 0.5m },
            RatingSet = [1, 2, 3],
            Tags = ["alpha", "beta"],
            OptionalScores = [7, null, 9]
        }
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues(PrimitiveCollectionsItemMapper.ToItems(Items));

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>>
        MutableAttributeValues =
            CreateAttributeValues(MutablePrimitiveCollectionsItemMapper.ToItems(MutableItems));

    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues(
        List<Dictionary<string, AttributeValue>> items)
        => items;
}
