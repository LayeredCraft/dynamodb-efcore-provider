using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public sealed record UserEntity
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public sealed record OrderEntity
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public static class SharedTableItems
{
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
    [
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#U" },
            ["Sk"] = new AttributeValue { S = "USER#1" },
            ["Name"] = new AttributeValue { S = "Ada" },
            ["$type"] = new AttributeValue { S = "UserEntity" },
            ["$kind"] = new AttributeValue { S = "UserEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#U" },
            ["Sk"] = new AttributeValue { S = "USER#2" },
            ["Name"] = new AttributeValue { S = "Lin" },
            ["$type"] = new AttributeValue { S = "UserEntity" },
            ["$kind"] = new AttributeValue { S = "UserEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#O" },
            ["Sk"] = new AttributeValue { S = "ORDER#1" },
            ["Description"] = new AttributeValue { S = "order-one" },
            ["$type"] = new AttributeValue { S = "OrderEntity" },
            ["$kind"] = new AttributeValue { S = "OrderEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#O" },
            ["Sk"] = new AttributeValue { S = "ORDER#2" },
            ["Description"] = new AttributeValue { S = "order-two" },
            ["$type"] = new AttributeValue { S = "OrderEntity" },
            ["$kind"] = new AttributeValue { S = "OrderEntity" },
        },
    ];
}
