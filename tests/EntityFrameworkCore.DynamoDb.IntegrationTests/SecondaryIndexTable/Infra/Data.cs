using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public static class OrderItems
{
    public static readonly List<OrderItem> Items =
    [
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

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
        OrderItemMapper.ToItems(Items);
}
