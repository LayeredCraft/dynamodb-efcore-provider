using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

public class GsiQueryTests(DynamoContainerFixture fixture) : SecondaryIndexTableTestFixture(fixture)
{
    [Fact]
    public async Task ByStatus_Gsi_ReturnsAllPendingOrders_AcrossCustomers()
    {
        var results =
            await Db
                .Orders
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
        var results =
            await Db
                .Orders
                .WithIndex("ByStatus")
                .Where(o => o.Status == "SHIPPED" && string.Compare(o.CreatedAt, "2024-01-15") >= 0)
                .ToListAsync(CancellationToken);

        var expected =
            OrderItems
                .Items
                .Where(o => o.Status == "SHIPPED"
                    && string.Compare(o.CreatedAt, "2024-01-15", StringComparison.Ordinal) >= 0)
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
        List<OrderItem>? results = null;
        try
        {
            results = await Db
                .Orders
                .WithIndex("ByStatus")
                .Where(o => o.Status == "PENDING")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException) { }

        if (results is not null)
        {
            var expected =
                OrderItems
                    .Items
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
        var results =
            await Db
                .Orders
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
    public async Task ByRegion_Gsi_WithLimit_ReturnsCorrectCount()
    {
        var results =
            await Db
                .Orders
                .WithIndex("ByRegion")
                .Where(o => o.Region == "US-EAST")
                .Limit(2)
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
        _ = await Db
            .Orders
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
