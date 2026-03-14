using Amazon.DynamoDBv2.Model;
using DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Order entity that maps to a DynamoDB table with a composite primary key and multiple
///     secondary indexes. Used to exercise GSI and LSI query paths in integration tests.
/// </summary>
public sealed record OrderItem
{
    /// <summary>Partition key for the base table. Also the shared PK for all LSIs.</summary>
    public string CustomerId { get; set; } = null!;

    /// <summary>Sort key for the base table.</summary>
    public string OrderId { get; set; } = null!;

    /// <summary>
    ///     Order status. Used as the partition key for the <c>ByStatus</c> GSI so that all orders
    ///     with a given status can be queried together.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    ///     ISO-8601 date string. Used as the sort key for the <c>ByStatus</c> and <c>ByRegion</c>
    ///     GSIs, and as the sort key for the <c>ByCreatedAt</c> LSI.
    /// </summary>
    public string CreatedAt { get; set; } = null!;

    /// <summary>
    ///     Fulfillment region. Used as the partition key for the <c>ByRegion</c> GSI so that all
    ///     orders from a given region can be queried together.
    /// </summary>
    public string Region { get; set; } = null!;

    /// <summary>
    ///     Numeric dispatch priority. Used as the sort key for the <c>ByPriority</c> LSI, which
    ///     demonstrates that LSI sort keys can use the DynamoDB <c>N</c> (number) type.
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>Static test data and DynamoDB attribute-value conversions for <c>OrderItem</c>.</summary>
public static class OrderItems
{
    /// <summary>
    ///     Deterministic seed data that covers multiple customers, statuses, regions, and priority
    ///     values so that GSI and LSI queries can be verified against known sub-sets.
    /// </summary>
    public static readonly List<OrderItem> Items =
    [
        // Customer C#1 — three orders across different statuses, regions, and priorities.
        new()
        {
            CustomerId = "C#1",
            OrderId = "O#001",
            Status = "PENDING",
            CreatedAt = "2024-01-10",
            Region = "US-EAST",
            Priority = 1,
        },
        new()
        {
            CustomerId = "C#1",
            OrderId = "O#002",
            Status = "SHIPPED",
            CreatedAt = "2024-01-15",
            Region = "US-EAST",
            Priority = 5,
        },
        new()
        {
            CustomerId = "C#1",
            OrderId = "O#003",
            Status = "DELIVERED",
            CreatedAt = "2024-01-20",
            Region = "US-WEST",
            Priority = 3,
        },

        // Customer C#2 — two orders; one PENDING to verify GSI returns across customers.
        new()
        {
            CustomerId = "C#2",
            OrderId = "O#001",
            Status = "PENDING",
            CreatedAt = "2024-01-12",
            Region = "EU-WEST",
            Priority = 2,
        },
        new()
        {
            CustomerId = "C#2",
            OrderId = "O#002",
            Status = "SHIPPED",
            CreatedAt = "2024-01-18",
            Region = "US-EAST",
            Priority = 4,
        },

        // Customer C#3 — single delivered order in EU-WEST.
        new()
        {
            CustomerId = "C#3",
            OrderId = "O#001",
            Status = "DELIVERED",
            CreatedAt = "2024-01-05",
            Region = "EU-WEST",
            Priority = 1,
        },
    ];

    /// <summary>All seed items pre-converted to DynamoDB attribute-value maps ready for batch write.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        CreateAttributeValues();

    private static IReadOnlyList<Dictionary<string, AttributeValue>> CreateAttributeValues()
        => OrderItemMapper.ToItems(Items);
}

/// <summary>
///     Source-generated DynamoDB mapper for <c>OrderItem</c> using exact property-name
///     convention.
/// </summary>
[DynamoMapper(Convention = DynamoNamingConvention.Exact, OmitNullStrings = false)]
internal static partial class OrderItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);

    internal static List<Dictionary<string, AttributeValue>> ToItems(List<OrderItem> sources)
        => sources.Select(ToItem).ToList();

    internal static partial OrderItem FromItem(Dictionary<string, AttributeValue> item);

    internal static List<OrderItem> FromItems(List<Dictionary<string, AttributeValue>> items)
        => items.Select(FromItem).ToList();
}
