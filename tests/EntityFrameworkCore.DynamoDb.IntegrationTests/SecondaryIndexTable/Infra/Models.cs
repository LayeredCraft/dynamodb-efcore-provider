namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public sealed record OrderItem
{
    public string CustomerId { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string Region { get; set; } = null!;
    public int Priority { get; set; }
}
