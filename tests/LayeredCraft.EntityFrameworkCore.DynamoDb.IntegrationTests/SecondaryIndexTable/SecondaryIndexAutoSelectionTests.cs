using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Diagnostics;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure;
using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
/// Integration tests for conservative automatic index selection. Verifies that the provider
/// selects the correct secondary index based on WHERE-clause constraints without an explicit
/// <c>.WithIndex()</c> hint.
/// </summary>
public class SecondaryIndexAutoSelectionTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    /// Overrides the base options to enable <see cref="DynamoAutomaticIndexSelectionMode.Conservative"/>
    /// so that the auto-selection analyzer may rewrite query sources.
    /// </remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder = new DbContextOptionsBuilder<SecondaryIndexDbContext>(
            base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt =>
            opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    // ── GSI auto-selection ───────────────────────────────────────────────────

    [Fact]
    public async Task Conservative_WhereOnGsiPk_AutoSelects_ByStatusIndex()
    {
        // Status equality satisfies the ByStatus GSI PK gate — the analyzer should rewrite the
        // FROM clause to "SecondaryIndexOrders"."ByStatus" without an explicit .WithIndex() call.
        var results = await Db.Orders
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains("ByStatus"));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    [Fact]
    public async Task Conservative_WhereOnGsiPk_WithSkRange_AutoSelects_AndProducesCorrectResults()
    {
        // Region equality covers the ByRegion GSI PK; the CreatedAt range is a sort-key condition.
        var results = await Db.Orders
            .Where(o => o.Region == "US-EAST" && string.Compare(o.CreatedAt, "2024-01-14") >= 0)
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items
            .Where(o => o.Region == "US-EAST"
                && string.Compare(o.CreatedAt, "2024-01-14", StringComparison.Ordinal) >= 0)
            .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST' AND "CreatedAt" >= '2024-01-14'
            """);
    }

    // ── Base table stays on base table ───────────────────────────────────────

    [Fact]
    public async Task Conservative_WhereOnTablePk_DoesNotAutoSelectGsi_StaysOnBaseTable()
    {
        // CustomerId equality satisfies both LSI PKs (ByCreatedAt and ByPriority) but not any GSI
        // PK. The two LSI candidates tie at score 0 (no SK condition, no ordering), so the analyzer
        // emits IDX002 and falls back to the base table.
        var results = await Db.Orders
            .Where(o => o.CustomerId == "C#1")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    // ── LSI auto-selection ───────────────────────────────────────────────────

    [Fact]
    public async Task Conservative_WhereOnTablePk_LsiCandidates_AutoSelects_WhenOrderingMatches()
    {
        // CustomerId equality covers both LSI PKs (LSIs share the base-table PK).
        // An OrderBy(Priority) ordering aligns with ByPriority but not ByCreatedAt,
        // so ByPriority wins the score tie-break.
        List<OrderItem>? results = null;
        try
        {
            results = await Db.Orders
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
    public async Task Conservative_WhereOnTablePk_AmbiguousLsi_FallsBackToBaseTable()
    {
        // CustomerId equality satisfies both LSI PKs (ByCreatedAt and ByPriority) with equal
        // scores (no SK condition, no ordering). The analyzer emits IDX002 and returns base table.
        var results = await Db.Orders
            .Where(o => o.CustomerId == "C#1")
            .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                && e.LogLevel == LogLevel.Warning
                && e.Message.Contains("ByCreatedAt")
                && e.Message.Contains("ByPriority"));

        // No index in the FROM clause — base table is used.
        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    // ── Off mode override ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="DynamoAutomaticIndexSelectionMode.Off"/> suppresses auto-selection
    /// even when the predicate would satisfy a GSI partition key.
    /// </summary>
    [Fact]
    public async Task Off_WhereOnGsiPk_DoesNotAutoSelect()
    {
        // Re-create the context with Off mode to override the class-level Conservative setting.
        var loggerFactory = new TestPartiQlLoggerFactory();
        var offOptions = new DbContextOptionsBuilder<SecondaryIndexDbContext>(
                base.CreateOptions(loggerFactory))
            .UseDynamo(opt =>
                opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Off))
            .Options;

        await using var offDb = new SecondaryIndexDbContext(offOptions);

        _ = await offDb.Orders
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        // Off mode: base table is used regardless of the predicate.
        loggerFactory.AssertBaseline(
            """
            SELECT "CustomerId", "OrderId", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING'
            """);

        loggerFactory.Dispose();
    }
}
