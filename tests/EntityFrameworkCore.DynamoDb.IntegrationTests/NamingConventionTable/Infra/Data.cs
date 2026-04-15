using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>Seed data for naming convention integration tests.</summary>
public static class NamingConventionData
{
    /// <summary>Snake_case entity seed items.</summary>
    public static readonly List<SnakeCaseItem> SnakeCaseItems =
    [
        new()
        {
            Pk = "SNAKE#1", FirstName = "Alice", ItemCount = 10, ExplicitOverride = "alpha",
        },
        new() { Pk = "SNAKE#2", FirstName = "Bob", ItemCount = 0, ExplicitOverride = "beta" },
    ];

    /// <summary>Kebab-case entity seed items.</summary>
    public static readonly List<KebabCaseItem> KebabCaseItems =
    [
        new() { Pk = "KEBAB#1", DisplayName = "Widget", TotalCount = 5 },
        new() { Pk = "KEBAB#2", DisplayName = "Gadget", TotalCount = 3 },
    ];

    /// <summary>
    ///     Pre-mapped <see cref="SnakeCaseItems" /> as DynamoDB attribute dictionaries using
    ///     snake_case naming to match what the EF provider emits.
    /// </summary>
    public static readonly List<Dictionary<string, AttributeValue>> SnakeCaseAttributeValues =
        SnakeCaseItemMapper.ToItems(SnakeCaseItems);

    /// <summary>
    ///     Pre-mapped <see cref="KebabCaseItems" /> as DynamoDB attribute dictionaries using
    ///     kebab-case naming to match what the EF provider emits.
    /// </summary>
    public static readonly List<Dictionary<string, AttributeValue>> KebabCaseAttributeValues =
        KebabCaseItemMapper.ToItems(KebabCaseItems);
}
