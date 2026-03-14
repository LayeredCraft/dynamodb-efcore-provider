using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Represents the PkSkItem type.</summary>
public sealed record PkSkItem
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public bool IsTarget { get; set; }

    /// <summary>Provides functionality for this member.</summary>
    public string Category { get; set; } = null!;
}

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
