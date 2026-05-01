using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

public class SecondaryIndexWithoutIndexTests(DynamoContainerFixture fixture)
    : SecondaryIndexTableTestFixture(fixture)
{
    protected override DynamoAutomaticIndexSelectionMode AutomaticIndexSelectionMode
        => DynamoAutomaticIndexSelectionMode.On;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task WithoutIndex_OnMode_UsesBaseTable_AndEmitsDiagnosticIDX006()
    {
        var results = await Db
            .Orders
            .WithoutIndex()
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelectionDisabled.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(".WithoutIndex()"));

        AssertSql(
            """
            SELECT "customerId", "orderId", "createdAt", "priority", "region", "status"
            FROM "SecondaryIndexOrders"
            WHERE "status" = 'PENDING'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task WithoutIndex_DoesNotEmitAutoSelectionDiagnostics()
    {
        await Db
            .Orders
            .WithoutIndex()
            .Where(o => o.Status == "SHIPPED")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e
                => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id
                || e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                || e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                || e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task WithoutIndex_WithExplicitHint_ThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .Orders
                .WithIndex("ByStatus")
                .WithoutIndex()
                .Where(o => o.Status == "PENDING")
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*'.WithIndex()'*'.WithoutIndex()'*");
    }
}
