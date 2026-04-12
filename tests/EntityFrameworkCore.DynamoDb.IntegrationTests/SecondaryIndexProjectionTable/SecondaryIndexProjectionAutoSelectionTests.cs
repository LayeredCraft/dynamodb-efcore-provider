using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

/// <summary>
///     Integration tests for auto-selection behavior when candidate indexes use partial
///     projections.
/// </summary>
public sealed class SecondaryIndexProjectionAutoSelectionTests(
    SecondaryIndexProjectionDynamoFixture fixture) : SecondaryIndexProjectionTestBase(fixture)
{
    /// <inheritdoc />
    protected override DbContextOptions<SecondaryIndexProjectionDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexProjectionDbContext>(
                base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    /// <summary>
    ///     Verifies that a KEYS_ONLY index candidate is rejected and the query remains on the base
    ///     table.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexProjectionOrders"
            WHERE "Status" = 'PENDING'
            """);
    }

    /// <summary>
    ///     Verifies that an INCLUDE index candidate is rejected and the query remains on the base
    ///     table.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexProjectionOrders"
            WHERE "Region" = 'US-EAST'
            """);
    }
}
