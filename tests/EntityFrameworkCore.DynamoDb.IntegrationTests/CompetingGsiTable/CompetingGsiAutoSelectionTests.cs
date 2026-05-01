using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

public sealed class CompetingGsiAutoSelectionTests(DynamoContainerFixture fixture)
    : CompetingGsiTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_CompetingGsis_WithEqualScore_EmitsIdx002_UsesBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(OrderItems.Items.Where(o => o.Status == "PENDING"));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e
                => e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                && e.LogLevel == LogLevel.Warning
                && e.Message.Contains("ByStatusCreatedAt")
                && e.Message.Contains("ByStatusPriority"));

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "CompetingGsiOrders"
            WHERE "status" = 'PENDING'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task On_CompetingGsis_OrderBySortKey_SelectsScoringWinner()
    {
        _ = await Db
            .Orders
            .Where(o => o.Status == "PENDING")
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatusCreatedAt' on table 'CompetingGsiOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "CompetingGsiOrders"."ByStatusCreatedAt"
            WHERE "status" = 'PENDING'
            ORDER BY "createdAt" ASC
            """);
    }
}
