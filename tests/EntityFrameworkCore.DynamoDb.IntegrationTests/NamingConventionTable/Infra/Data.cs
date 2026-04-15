using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>Seed data for naming convention integration tests.</summary>
public static class NamingConventionItems
{
    /// <summary>Provides functionality for this member.</summary>
    public static readonly List<NamingConventionItem> Items =
    [
        new()
        {
            Pk = "ITEM#1",
            FirstName = "Alice",
            LastName = "Smith",
            ItemCount = 10,
            IsActive = true,
            ExplicitOverride = "alpha",
        },
        new()
        {
            Pk = "ITEM#2",
            FirstName = "Bob",
            LastName = "Jones",
            ItemCount = 0,
            IsActive = false,
            ExplicitOverride = "beta",
        },
        new()
        {
            Pk = "ITEM#3",
            FirstName = "Carol",
            LastName = "White",
            ItemCount = -5,
            IsActive = true,
            ExplicitOverride = "gamma",
        },
    ];

    /// <summary>
    ///     Pre-mapped <see cref="Items" /> as DynamoDB attribute dictionaries using the same
    ///     snake_case naming convention applied by the EF provider.
    /// </summary>
    public static readonly List<Dictionary<string, AttributeValue>> AttributeValues =
        NamingConventionItemMapper.ToItems(Items);
}
