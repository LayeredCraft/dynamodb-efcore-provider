using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Represents the UserEntity type.</summary>
public sealed record UserEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Name { get; set; } = null!;
}

/// <summary>Represents the OrderEntity type.</summary>
public sealed record OrderEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Description { get; set; } = null!;
}

/// <summary>Represents the PersonEntity type.</summary>
public abstract record PersonEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Name { get; set; } = null!;
}

/// <summary>Represents the EmployeeEntity type.</summary>
public sealed record EmployeeEntity : PersonEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public string Department { get; set; } = null!;
}

/// <summary>Represents the ManagerEntity type.</summary>
public sealed record ManagerEntity : PersonEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public int Level { get; set; }
}

/// <summary>Represents the WorkOrderEntity type.</summary>
public abstract record WorkOrderEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public string Pk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Sk { get; set; } = null!;

    /// <summary>Provides functionality for this member.</summary>
    public string Status { get; set; } = null!;
}

/// <summary>Represents the PriorityWorkOrderEntity type.</summary>
public sealed record PriorityWorkOrderEntity : WorkOrderEntity
{
    /// <summary>Provides functionality for this member.</summary>
    public int Priority { get; set; }
}

/// <summary>Represents the ArchivedWorkOrderEntity type.</summary>
public sealed record ArchivedWorkOrderEntity : WorkOrderEntity;

/// <summary>Represents the SharedTableItems type.</summary>
public static class SharedTableItems
{
    /// <summary>Provides functionality for this member.</summary>
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
