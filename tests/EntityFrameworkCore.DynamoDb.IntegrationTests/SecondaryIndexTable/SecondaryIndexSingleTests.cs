using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexSingleTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_ManualGsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .SingleOrDefaultAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(2);

        AssertSql(
            """
            SELECT "customerId", "orderId", "$type", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING' AND "createdAt" = '2024-01-10'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_AutoGsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .SingleOrDefaultAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(2);

        AssertSql(
            """
            SELECT "customerId", "orderId", "$type", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING' AND "createdAt" = '2024-01-10'
            """);
    }
}
