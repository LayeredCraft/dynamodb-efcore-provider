using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

/// <summary>Represents the PrimitiveCollectionsItems type.</summary>
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
