using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTableWithIndexes;

public static class SharedTableWithIndexesItems
{
    public static readonly List<PriorityWorkOrderSeedItem> PriorityWorkOrders =
    [
        new() { Pk = "WO#ALPHA", Sk = "WO#001", Status = "OPEN", Priority = 3 },
        new() { Pk = "WO#ALPHA", Sk = "WO#002", Status = "CLOSED", Priority = 1 },
        new() { Pk = "WO#BETA", Sk = "WO#001", Status = "OPEN", Priority = 5 },
    ];

    public static readonly List<ArchivedWorkOrderSeedItem> ArchivedWorkOrders =
    [
        new() { Pk = "WO#ALPHA", Sk = "WO#003", Status = "OPEN" },
        new() { Pk = "WO#BETA", Sk = "WO#002", Status = "CLOSED" },
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
    [
        ..PriorityWorkOrderSeedItemMapper.ToItems(PriorityWorkOrders),
        ..ArchivedWorkOrderSeedItemMapper.ToItems(ArchivedWorkOrders),
    ];
}
