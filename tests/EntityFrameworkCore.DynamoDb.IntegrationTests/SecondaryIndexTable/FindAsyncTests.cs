using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>Integration tests for <c>FindAsync</c> on a table with secondary indexes.</summary>
public class FindAsyncTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_UsesBaseTable_AndDoesNotEmitIndexSelectionDiagnostics()
    {
        LoggerFactory.Clear();

        var result = await Db.Orders.FindAsync(["C#1", "O#001"], CancellationToken);

        result
            .Should()
            .BeEquivalentTo(
                OrderItems.Items.Single(item
                    => item.CustomerId == "C#1" && item.OrderId == "O#001"));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                || e.EventId.Id == DynamoEventId.ExplicitIndexSelectionDisabled.Id);

        AssertSql(
            """
            SELECT "customerId", "orderId", "$type", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "customerId" = ? AND "orderId" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task NormalQuery_StillUsesAutomaticIndexSelection()
    {
        LoggerFactory.Clear();

        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(OrderItems.Items.Where(item => item.Status == "PENDING"));

        AssertSql(
            """
            SELECT "customerId", "orderId", "$type", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "status" = 'PENDING'
            """);
    }
}
