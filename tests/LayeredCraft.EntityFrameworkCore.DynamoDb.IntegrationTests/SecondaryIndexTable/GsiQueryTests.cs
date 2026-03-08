using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Integration tests for querying <see cref="OrderItem" /> via global secondary indexes.
///     These tests require the PartiQL generator to emit <c>FROM "Table"."Index"</c> and are
///     skipped until that feature is implemented (TODO §4).
/// </summary>
public class GsiQueryTests(SecondaryIndexDynamoFixture fixture) : SecondaryIndexTestBase(fixture)
{
    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByStatus_Gsi_ReturnsAllPendingOrders_AcrossCustomers()
    {
        var results = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByStatus_Gsi_WithSortKeyFilter_ReturnsOrdersInDateRange()
    {
        var results = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "SHIPPED"
                        && string.Compare(o.CreatedAt, "2024-01-15", StringComparison.Ordinal) >= 0)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.Status == "SHIPPED" && string.Compare(o.CreatedAt, "2024-01-15", StringComparison.Ordinal) >= 0)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'SHIPPED' AND "CreatedAt" >= '2024-01-15'
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByStatus_Gsi_WithOrderBy_ReturnsResultsInDateOrder()
    {
        var results = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING")
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.Status == "PENDING")
            .OrderBy(o => o.CreatedAt)
            .ToList();

        results.Should().ContainInOrder(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            ORDER BY "CreatedAt" ASC
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByRegion_Gsi_ReturnsAllUsEastOrders()
    {
        var results = await Db.Orders
            .WithIndex("ByRegion")
            .Where(o => o.Region == "US-EAST")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Region == "US-EAST").ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST'
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByRegion_Gsi_WithTake_ReturnsCorrectCount()
    {
        var results = await Db.Orders
            .WithIndex("ByRegion")
            .Where(o => o.Region == "US-EAST")
            .Take(2)
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST'
            """);
    }

    [Fact(Skip = "Requires FROM \"Table\".\"Index\" SQL generation (TODO §4)")]
    public async Task ByStatus_Gsi_EmitsCorrectFromSource_ForExecuteStatement()
    {
        _ = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "DELIVERED")
            .ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "Status", "CreatedAt", "Region", "Priority"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'DELIVERED'
            """);
    }
}
