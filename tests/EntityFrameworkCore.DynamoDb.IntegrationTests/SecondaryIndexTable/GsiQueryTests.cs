using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>Integration tests for querying <c>OrderItem</c> via global secondary indexes.</summary>
public class GsiQueryTests(SecondaryIndexDynamoFixture fixture) : SecondaryIndexTestBase(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ByStatus_Gsi_WithOrderBy_EmitsOrderByClause()
    {
        // DynamoDB Local does not support ORDER BY on secondary index sort keys via PartiQL.
        // This test verifies that the provider generates the correct SQL; result ordering is
        // verified client-side if DynamoDB Local accepts the query.
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
        catch (AmazonDynamoDBException)
        {
            // DynamoDB Local limitation: ORDER BY on GSI sort keys is not supported.
        }

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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Verifies that Limit(n) sets the evaluation budget when querying a GSI.</summary>
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

        // Limit(2) evaluates at most 2 items. All seeded US-EAST items match, so 2 are returned.
        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST'
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
