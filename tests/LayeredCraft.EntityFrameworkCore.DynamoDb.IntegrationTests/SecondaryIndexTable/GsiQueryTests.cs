using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>Integration tests for querying <see cref="OrderItem" /> via global secondary indexes.</summary>
public class GsiQueryTests(SecondaryIndexDynamoFixture fixture) : SecondaryIndexTestBase(fixture)
{
    [Fact]
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact]
    public async Task ByStatus_Gsi_WithSortKeyFilter_ReturnsOrdersInDateRange()
    {
        var results = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "SHIPPED" && string.Compare(o.CreatedAt, "2024-01-15") >= 0)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.Status == "SHIPPED" && string.Compare(o.CreatedAt, "2024-01-15", StringComparison.Ordinal) >= 0)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'SHIPPED' AND "CreatedAt" >= '2024-01-15'
            """);
    }

    [Fact]
    public async Task ByStatus_Gsi_WithOrderBy_EmitsOrderByClause()
    {
        // DynamoDB Local does not support ORDER BY on secondary index sort keys via PartiQL.
        // This test verifies that the provider generates the correct SQL; result ordering is
        // verified client-side if DynamoDB Local accepts the query.
        List<OrderItem>? results = null;
        try
        {
            results = await Db.Orders
                .WithIndex("ByStatus")
                .Where(o => o.Status == "PENDING")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException)
        {
            // DynamoDB Local limitation: ORDER BY on GSI sort keys is not supported.
        }

        if (results is not null)
        {
            var expected = OrderItems.Items
                .Where(o => o.Status == "PENDING")
                .OrderBy(o => o.CreatedAt)
                .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            ORDER BY "CreatedAt" ASC
            """);
    }

    [Fact]
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST'
            """);
    }

    [Fact]
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST'
            """);
    }

    [Fact]
    public async Task ByStatus_Gsi_EmitsCorrectFromSource_ForExecuteStatement()
    {
        _ = await Db.Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "DELIVERED")
            .ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'DELIVERED'
            """);
    }
}
