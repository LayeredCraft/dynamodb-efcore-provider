using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Integration tests for querying <see cref="OrderItem" /> via local secondary indexes.
///     These tests require the PartiQL generator to emit <c>FROM "Table"."Index"</c> and are
///     skipped until that feature is implemented (TODO §4).
/// </summary>
public class LsiQueryTests(SecondaryIndexDynamoFixture fixture) : SecondaryIndexTestBase(fixture)
{
    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByCreatedAt_Lsi_ReturnsCustomerOrdersInDateOrder()
    {
        // LSI shares the table partition key, so a partition-key equality filter is required.
        var results = await Db.Orders
            .WithIndex("ByCreatedAt")
            .Where(o => o.CustomerId == "C#1")
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.CustomerId == "C#1")
            .OrderBy(o => o.CreatedAt)
            .ToList();

        results.Should().ContainInOrder(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "CreatedAt" ASC
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByCreatedAt_Lsi_WithSortKeyBetween_ReturnsDateRangeOrders()
    {
        var results = await Db.Orders
            .WithIndex("ByCreatedAt")
            .Where(o => o.CustomerId == "C#1"
                        && string.Compare(o.CreatedAt, "2024-01-12", StringComparison.Ordinal) >= 0
                        && string.Compare(o.CreatedAt, "2024-01-18", StringComparison.Ordinal) <= 0)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.CustomerId == "C#1"
                        && string.Compare(o.CreatedAt, "2024-01-12", StringComparison.Ordinal) >= 0
                        && string.Compare(o.CreatedAt, "2024-01-18", StringComparison.Ordinal) <= 0)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1' AND "CreatedAt" >= '2024-01-12' AND "CreatedAt" <= '2024-01-18'
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByPriority_Lsi_ReturnsCustomerOrdersInPriorityOrder()
    {
        // Demonstrates that an LSI sort key can use the DynamoDB N (number) type.
        var results = await Db.Orders
            .WithIndex("ByPriority")
            .Where(o => o.CustomerId == "C#1")
            .OrderBy(o => o.Priority)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.CustomerId == "C#1")
            .OrderBy(o => o.Priority)
            .ToList();

        results.Should().ContainInOrder(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "Priority" ASC
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByPriority_Lsi_WithNumericSortKeyFilter_ReturnsHighPriorityOrders()
    {
        var results = await Db.Orders
            .WithIndex("ByPriority")
            .Where(o => o.CustomerId == "C#1" && o.Priority >= 3)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.CustomerId == "C#1" && o.Priority >= 3)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1' AND "Priority" >= 3
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByCreatedAt_Lsi_EmitsCorrectFromSource_ForExecuteStatement()
    {
        _ = await Db.Orders
            .WithIndex("ByCreatedAt")
            .Where(o => o.CustomerId == "C#2")
            .ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#2'
            """);
    }
}
