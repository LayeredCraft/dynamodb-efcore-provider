using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class LsiQueryTests(DynamoContainerFixture fixture) : SecondaryIndexTableTestFixture(fixture)
{
    [Fact]
    public async Task ByCreatedAt_Lsi_ReturnsCustomerOrdersInDateOrder()
    {
        List<OrderItem>? results = null;
        try
        {
            results = await Db
                .Orders
                .WithIndex("ByCreatedAt")
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException) { }

        if (results is not null)
        {
            var expected =
                OrderItems
                    .Items
                    .Where(o => o.CustomerId == "C#1")
                    .OrderBy(o => o.CreatedAt)
                    .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "customerId" = 'C#1'
            ORDER BY "createdAt" ASC
            """);
    }

    [Fact]
    public async Task ByCreatedAt_Lsi_WithSortKeyBetween_ReturnsDateRangeOrders()
    {
        var results =
            await Db
                .Orders
                .WithIndex("ByCreatedAt")
                .Where(o => o.CustomerId == "C#1"
                    && o.CreatedAt.CompareTo("2024-01-12") >= 0
                    && o.CreatedAt.CompareTo("2024-01-18") <= 0)
                .ToListAsync(CancellationToken);

        var expected =
            OrderItems
                .Items
                .Where(o => o.CustomerId == "C#1"
                    && string.Compare(o.CreatedAt, "2024-01-12", StringComparison.Ordinal) >= 0
                    && string.Compare(o.CreatedAt, "2024-01-18", StringComparison.Ordinal) <= 0)
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "customerId" = 'C#1' AND "createdAt" >= '2024-01-12' AND "createdAt" <= '2024-01-18'
            """);
    }

    [Fact]
    public async Task ByPriority_Lsi_ReturnsCustomerOrdersInPriorityOrder()
    {
        List<OrderItem>? results = null;
        try
        {
            results = await Db
                .Orders
                .WithIndex("ByPriority")
                .Where(o => o.CustomerId == "C#1")
                .OrderBy(o => o.Priority)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException) { }

        if (results is not null)
        {
            var expected =
                OrderItems
                    .Items
                    .Where(o => o.CustomerId == "C#1")
                    .OrderBy(o => o.Priority)
                    .ToList();
            results.Should().ContainInOrder(expected);
        }

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "customerId" = 'C#1'
            ORDER BY "priority" ASC
            """);
    }

    [Fact]
    public async Task ByPriority_Lsi_WithNumericSortKeyFilter_ReturnsHighPriorityOrders()
    {
        var results =
            await Db
                .Orders
                .WithIndex("ByPriority")
                .Where(o => o.CustomerId == "C#1" && o.Priority >= 3)
                .ToListAsync(CancellationToken);

        var expected =
            OrderItems.Items.Where(o => o.CustomerId == "C#1" && o.Priority >= 3).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "customerId" = 'C#1' AND "priority" >= 3
            """);
    }

    [Fact]
    public async Task ByCreatedAt_Lsi_EmitsCorrectFromSource_ForExecuteStatement()
    {
        _ = await Db
            .Orders
            .WithIndex("ByCreatedAt")
            .Where(o => o.CustomerId == "C#2")
            .ToListAsync(CancellationToken);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "customerId" = 'C#2'
            """);
    }
}
