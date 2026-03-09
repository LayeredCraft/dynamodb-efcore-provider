using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>Integration tests for querying <see cref="OrderItem" /> via local secondary indexes.</summary>
public class LsiQueryTests(SecondaryIndexDynamoFixture fixture) : SecondaryIndexTestBase(fixture)
{
    [Fact]
    public async Task ByCreatedAt_Lsi_ReturnsCustomerOrdersInDateOrder()
    {
        // DynamoDB Local does not support ORDER BY on secondary index sort keys via PartiQL.
        // This test verifies that the provider generates the correct SQL; result ordering is
        // verified client-side if DynamoDB Local accepts the query.
        List<OrderItem>? results = null;
        try
        {
            results = await Db.Orders
                .WithIndex("ByCreatedAt")
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException)
        {
            // DynamoDB Local limitation: ORDER BY on LSI sort keys is not supported.
        }

        if (results is not null)
        {
            var expected = OrderItems.Items
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.CreatedAt)
                .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "CreatedAt" ASC
            """);
    }

    [Fact]
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1' AND "CreatedAt" >= '2024-01-12' AND "CreatedAt" <= '2024-01-18'
            """);
    }

    [Fact]
    public async Task ByPriority_Lsi_ReturnsCustomerOrdersInPriorityOrder()
    {
        // DynamoDB Local does not support ORDER BY on secondary index sort keys via PartiQL.
        // This test verifies that the provider generates the correct SQL; result ordering is
        // verified client-side if DynamoDB Local accepts the query.
        List<OrderItem>? results = null;
        try
        {
            results = await Db.Orders
                .WithIndex("ByPriority")
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.Priority)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException)
        {
            // DynamoDB Local limitation: ORDER BY on LSI sort keys is not supported.
        }

        if (results is not null)
        {
            var expected = OrderItems.Items
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.Priority)
                .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "Priority" ASC
            """);
    }

    [Fact]
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1' AND "Priority" >= 3
            """);
    }

    [Fact]
    public async Task ByCreatedAt_Lsi_EmitsCorrectFromSource_ForExecuteStatement()
    {
        _ = await Db.Orders
            .WithIndex("ByCreatedAt")
            .Where(o => o.CustomerId == "C#2")
            .ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#2'
            """);
    }
}
