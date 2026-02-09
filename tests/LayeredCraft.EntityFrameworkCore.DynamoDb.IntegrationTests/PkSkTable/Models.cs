using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public sealed record PkSkItem
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public bool IsTarget { get; set; }
    public string Category { get; set; } = null!;
}

public static class PkSkItems
{
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

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => PkSkItemMapper.ToItems(Items);
}

[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullStrings = false)]
internal static partial class PkSkItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(PkSkItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<PkSkItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial PkSkItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<PkSkItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
