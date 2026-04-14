namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable;

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
