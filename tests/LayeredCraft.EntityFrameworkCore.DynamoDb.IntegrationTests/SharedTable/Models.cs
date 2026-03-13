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

public abstract record PersonEntity
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public sealed record EmployeeEntity : PersonEntity
{
    public string Department { get; set; } = null!;
}

public sealed record ManagerEntity : PersonEntity
{
    public int Level { get; set; }
}

public abstract record WorkOrderEntity
{
    public string Pk { get; set; } = null!;
    public string Sk { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public sealed record PriorityWorkOrderEntity : WorkOrderEntity
{
    public int Priority { get; set; }
}

public sealed record ArchivedWorkOrderEntity : WorkOrderEntity;

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
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#H" },
            ["Sk"] = new AttributeValue { S = "PERSON#EMP-1" },
            ["Name"] = new AttributeValue { S = "Eve" },
            ["Department"] = new AttributeValue { S = "Engineering" },
            ["$type"] = new AttributeValue { S = "EmployeeEntity" },
            ["$kind"] = new AttributeValue { S = "EmployeeEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "TENANT#H" },
            ["Sk"] = new AttributeValue { S = "PERSON#MGR-1" },
            ["Name"] = new AttributeValue { S = "Max" },
            ["ManagerLevel"] = new AttributeValue { N = "7" },
            ["$type"] = new AttributeValue { S = "ManagerEntity" },
            ["$kind"] = new AttributeValue { S = "ManagerEntity" },
        },
    ];
}
