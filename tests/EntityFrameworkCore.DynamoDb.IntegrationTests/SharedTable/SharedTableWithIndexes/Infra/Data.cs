using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

public static class SharedTableWithIndexesItems
{
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
    [
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#001" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["Priority"] = new AttributeValue { N = "3" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#002" },
            ["Status"] = new AttributeValue { S = "CLOSED" },
            ["Priority"] = new AttributeValue { N = "1" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#BETA" },
            ["Sk"] = new AttributeValue { S = "WO#001" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["Priority"] = new AttributeValue { N = "5" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#003" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["$type"] = new AttributeValue { S = "ArchivedWorkOrderEntity" },
        },
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#BETA" },
            ["Sk"] = new AttributeValue { S = "WO#002" },
            ["Status"] = new AttributeValue { S = "CLOSED" },
            ["$type"] = new AttributeValue { S = "ArchivedWorkOrderEntity" },
        },
    ];
}
