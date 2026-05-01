using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

public sealed class SecondaryIndexProjectionAutoSelectionTests(DynamoContainerFixture fixture)
    : SecondaryIndexProjectionTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.Conservative;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Conservative_WhereOnKeysOnlyGsiPk_RejectsCandidate_UsesBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(OrderItems.Items.Where(o => o.Status == "PENDING"));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id
                && e.Message.Contains("ByStatusKeysOnly")
                && e.Message.Contains("projection type is KeysOnly"));
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexProjectionOrders"
            WHERE "status" = 'PENDING'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Conservative_WhereOnIncludeGsiPk_RejectsCandidate_UsesBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.Region == "US-EAST").ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(OrderItems.Items.Where(o => o.Region == "US-EAST"));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id
                && e.Message.Contains("ByRegionInclude")
                && e.Message.Contains("projection type is Include"));
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id);

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexProjectionOrders"
            WHERE "region" = 'US-EAST'
            """);
    }
}
