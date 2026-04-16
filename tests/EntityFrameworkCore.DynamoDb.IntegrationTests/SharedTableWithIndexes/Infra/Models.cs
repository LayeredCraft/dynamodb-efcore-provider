namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

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

public sealed record PriorityWorkOrderSeedItem
{
    public string Pk { get; init; } = null!;
    public string Sk { get; init; } = null!;
    public string Status { get; init; } = null!;
    public int Priority { get; init; }
}

public sealed record ArchivedWorkOrderSeedItem
{
    public string Pk { get; init; } = null!;
    public string Sk { get; init; } = null!;
    public string Status { get; init; } = null!;
}
