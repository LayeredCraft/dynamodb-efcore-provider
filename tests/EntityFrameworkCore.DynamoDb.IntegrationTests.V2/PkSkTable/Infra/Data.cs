using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PkSkTable;

/// <summary>Represents the PkSkItems type.</summary>
public static class PkSkItems
{
    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<PkSkItem> Items =
    [
        new()
        {
            Pk = "P#1", Sk = "0001", IsTarget = false, Category = "alpha",
        },
        new()
        {
            Pk = "P#1", Sk = "0002", IsTarget = true, Category = "bravo",
        },
        new()
        {
            Pk = "P#1", Sk = "0003", IsTarget = false, Category = "charlie",
        },
        new()
        {
            Pk = "P#1", Sk = "0004", IsTarget = true, Category = "delta",
        },
        new()
        {
            Pk = "P#2", Sk = "0001", IsTarget = false, Category = "echo",
        },
    ];

    /// <summary>Provides functionality for this member.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => PkSkItemMapper.ToItems(Items);
}
