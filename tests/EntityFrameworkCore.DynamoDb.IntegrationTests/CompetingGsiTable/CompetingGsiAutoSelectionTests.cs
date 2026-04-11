using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

/// <summary>Integration tests for auto-selection behavior when multiple GSIs are eligible.</summary>
public sealed class CompetingGsiAutoSelectionTests(CompetingGsiDynamoFixture fixture)
    : CompetingGsiTestBase(fixture)
{
    /// <inheritdoc />
    protected override DbContextOptions<CompetingGsiDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<CompetingGsiDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    /// <summary>Verifies that when two GSIs tie, the provider emits IDX002 and uses the base table.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_CompetingGsis_WithEqualScore_EmitsIdx002_UsesBaseTable()
    {
        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(OrderItems.Items.Where(o => o.Status == "PENDING"));

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e
                => e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                && e.Message.Contains("ByStatusCreatedAt")
                && e.Message.Contains("ByStatusPriority"));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "CompetingGsiOrders"
            WHERE "Status" = 'PENDING'
            """);
    }

    /// <summary>Verifies that ordering on one candidate sort key breaks the tie and selects that index.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_CompetingGsis_OrderBySortKey_SelectsScoringWinner()
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
                && e.Message.Contains("ByStatusCreatedAt")
                && e.Message.Contains("was auto-selected"));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "CompetingGsiOrders"."ByStatusCreatedAt"
            WHERE "Status" = 'PENDING'
            ORDER BY "CreatedAt" ASC
            """);
    }
}
