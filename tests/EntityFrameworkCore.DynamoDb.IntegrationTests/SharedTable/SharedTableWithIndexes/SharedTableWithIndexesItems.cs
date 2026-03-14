using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

/// <summary>
/// Deterministic seed data for the shared-table-with-indexes integration tests. Items are
/// pre-built as <c>AttributeValue</c> maps because <c>WorkOrderEntity</c> is abstract
/// and cannot be used with a source-generated DynamoMapper.
/// </summary>
/// <remarks>
/// Seed layout (5 items across 2 partition keys):
/// <list type="table">
///   <listheader><term>Pk</term><term>Sk</term><term>Status</term><term>Priority</term><term>$type</term></listheader>
///   <item><term>WO#ALPHA</term><term>WO#001</term><term>OPEN</term><term>3</term><term>PriorityWorkOrderEntity</term></item>
///   <item><term>WO#ALPHA</term><term>WO#002</term><term>CLOSED</term><term>1</term><term>PriorityWorkOrderEntity</term></item>
///   <item><term>WO#BETA</term><term>WO#001</term><term>OPEN</term><term>5</term><term>PriorityWorkOrderEntity</term></item>
///   <item><term>WO#ALPHA</term><term>WO#003</term><term>OPEN</term><term>—</term><term>ArchivedWorkOrderEntity</term></item>
///   <item><term>WO#BETA</term><term>WO#002</term><term>CLOSED</term><term>—</term><term>ArchivedWorkOrderEntity</term></item>
/// </list>
/// Useful sub-sets: Priority=3 → Item 1 only; Pk="WO#ALPHA" → Items 1, 2, 4;
/// PriorityWorkOrders → Items 1–3; ArchivedWorkOrders → Items 4–5.
/// </remarks>
public static class SharedTableWithIndexesItems
{
    /// <summary>All seed items as DynamoDB attribute-value maps ready for batch write.</summary>
    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
    [
        // Item 1 — PriorityWorkOrderEntity, Pk=WO#ALPHA, Priority=3
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#001" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["Priority"] = new AttributeValue { N = "3" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },

        // Item 2 — PriorityWorkOrderEntity, Pk=WO#ALPHA, Priority=1
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#002" },
            ["Status"] = new AttributeValue { S = "CLOSED" },
            ["Priority"] = new AttributeValue { N = "1" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },

        // Item 3 — PriorityWorkOrderEntity, Pk=WO#BETA, Priority=5
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#BETA" },
            ["Sk"] = new AttributeValue { S = "WO#001" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["Priority"] = new AttributeValue { N = "5" },
            ["$type"] = new AttributeValue { S = "PriorityWorkOrderEntity" },
        },

        // Item 4 — ArchivedWorkOrderEntity, Pk=WO#ALPHA (no Priority attribute)
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#ALPHA" },
            ["Sk"] = new AttributeValue { S = "WO#003" },
            ["Status"] = new AttributeValue { S = "OPEN" },
            ["$type"] = new AttributeValue { S = "ArchivedWorkOrderEntity" },
        },

        // Item 5 — ArchivedWorkOrderEntity, Pk=WO#BETA (no Priority attribute)
        new()
        {
            ["Pk"] = new AttributeValue { S = "WO#BETA" },
            ["Sk"] = new AttributeValue { S = "WO#002" },
            ["Status"] = new AttributeValue { S = "CLOSED" },
            ["$type"] = new AttributeValue { S = "ArchivedWorkOrderEntity" },
        },
    ];
}
