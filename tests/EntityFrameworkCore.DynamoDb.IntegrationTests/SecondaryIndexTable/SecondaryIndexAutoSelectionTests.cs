using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

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
    /// Overrides the base options to enable <c>DynamoAutomaticIndexSelectionMode.Conservative</c>
    /// so that the auto-selection analyzer may rewrite query sources.
    /// </remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    // ── GSI auto-selection ───────────────────────────────────────────────────

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_WhereOnGsiPk_AutoSelects_ByStatusIndex()
    {
        // Status equality satisfies the ByStatus GSI PK gate — the analyzer should rewrite the
        // FROM clause to "SecondaryIndexOrders"."ByStatus" without an explicit .WithIndex() call.
        var results =
            await Db.Orders.Where(o => o.Status == "PENDING").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.Status == "PENDING").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_WhereOnGsiPk_WithSkRange_AutoSelects_AndProducesCorrectResults()
    {
        // Region equality covers the ByRegion GSI PK; the CreatedAt range is a sort-key condition.
        var results =
            await Db
                .Orders
                .Where(o => o.Region == "US-EAST" && string.Compare(o.CreatedAt, "2024-01-14") >= 0)
                .ToListAsync(CancellationToken);

        var expected =
            OrderItems
                .Items
                .Where(o => o.Region == "US-EAST"
                    && string.Compare(o.CreatedAt, "2024-01-14", StringComparison.Ordinal) >= 0)
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-EAST' AND "CreatedAt" >= '2024-01-14'
            """);
    }

    // ── Base table stays on base table ───────────────────────────────────────

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_WhereOnTablePk_DoesNotAutoSelectGsi_StaysOnBaseTable()
    {
        // CustomerId equality satisfies both LSI PKs (ByCreatedAt and ByPriority) but not any GSI
        // PK. The two LSI candidates tie at score 0 (no SK condition, no ordering), so the analyzer
        // emits IDX002 and falls back to the base table.
        var results =
            await Db.Orders.Where(o => o.CustomerId == "C#1").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    // ── LSI auto-selection ───────────────────────────────────────────────────

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_WhereOnTablePk_LsiCandidates_AutoSelects_WhenOrderingMatches()
    {
        // CustomerId equality covers both LSI PKs (LSIs share the base-table PK).
        // An OrderBy(Priority) ordering aligns with ByPriority but not ByCreatedAt,
        // so ByPriority wins the score tie-break.
        List<OrderItem>? results = null;
        try
        {
            results = await Db
                .Orders
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
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1'
            ORDER BY "Priority" ASC
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_WhereOnTablePk_AmbiguousLsi_FallsBackToBaseTable()
    {
        // CustomerId equality satisfies both LSI PKs (ByCreatedAt and ByPriority) with equal
        // scores (no SK condition, no ordering). The analyzer emits IDX002 and returns base table.
        var results =
            await Db.Orders.Where(o => o.CustomerId == "C#1").ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => o.CustomerId == "C#1").ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.MultipleCompatibleSecondaryIndexesFound.Id
                && e.LogLevel == LogLevel.Warning
                && e.Message.Contains("are equally suitable")
                && e.Message.Contains("ByCreatedAt")
                && e.Message.Contains("ByPriority"));

        // No index in the FROM clause — base table is used.
        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "CustomerId" = 'C#1'
            """);
    }

    // ── IN predicate auto-selection ──────────────────────────────────────────

    /// <summary>
    /// Verifies that a <c>Contains()</c> predicate on a GSI partition key generates an IN
    /// constraint that satisfies Gate 1, causing the analyzer to auto-select the GSI.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_ContainsOnGsiPk_AutoSelects_ByStatusIndex()
    {
        // Contains() on a GSI PK attribute is translated to an IN constraint by the
        // constraint extractor, which satisfies Gate 1 (partition-key coverage).
        var statusList = new List<string> { "PENDING", "SHIPPED" };

        var results =
            await Db
                .Orders
                .Where(o => statusList.Contains(o.Status))
                .ToListAsync(CancellationToken);

        var expected = OrderItems.Items.Where(o => statusList.Contains(o.Status)).ToList();
        results.Should().BeEquivalentTo(expected);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" IN [?, ?]
            """);
    }

    // ── Unsafe OR blocking ────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that an OR predicate spanning two different GSI partition-key attributes is
    /// classified as an unsafe OR, which causes all index candidates to be rejected (IDX005 per
    /// candidate) and the query to fall back to the base table with an IDX001 summary.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_UnsafeOrPredicate_BlocksAutoSelection_EmitsRejectionDiagnostics()
    {
        // OR across two different GSI PK attributes: Status (ByStatus PK) || Region (ByRegion PK).
        // The constraint extractor sees the top-level OR as unsafe and does not extract any partial
        // equality constraints from its branches. All candidates therefore fail Gate 1 (no PK
        // constraint) — Gate 2 (unsafe OR) is never reached because Gate 1 fails first.
        _ = await Db
            .Orders
            .Where(o => o.Status == "PENDING" || o.Region == "US-EAST")
            .ToListAsync(CancellationToken);

        // IDX005 per rejected candidate — Gate 1 failures (no PK constraint extracted from OR
        // branches).
        LoggerFactory
            .QueryDiagnosticEvents
            .Where(e => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id)
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(e
                => e.Message.Contains(
                    "was rejected: no equality or IN constraint on the index partition key."));

        // IDX001 summary — no index was selected.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Status" = 'PENDING' OR "Region" = 'US-EAST'
            """);
    }

    // ── All candidates rejected ───────────────────────────────────────────────

    /// <summary>
    /// Verifies that when no predicate covers any index partition key, all candidates are rejected
    /// at Gate 1 with IDX005 diagnostics and the query falls back to the base table with IDX001.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task
        Conservative_WhereOnNonIndexPkAttribute_AllCandidatesRejected_EmitsIdxDiagnostics()
    {
        // Priority is the ByPriority LSI sort key — not a partition key on any index.
        // All candidates fail Gate 1 (no equality or IN constraint on any index PK).
        _ = await Db.Orders.Where(o => o.Priority == 1).ToListAsync(CancellationToken);

        // IDX005 for every rejected candidate — all fail on missing PK constraint.
        LoggerFactory
            .QueryDiagnosticEvents
            .Where(e => e.EventId.Id == DynamoEventId.SecondaryIndexCandidateRejected.Id)
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(e
                => e.Message.Contains(
                    "was rejected: no equality or IN constraint on the index partition key."));

        // IDX001 summary.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            WHERE "Priority" = 1
            """);
    }

    // ── Explicit hint diagnostics ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that an explicit <c>.WithIndex()</c> hint emits an IDX004 diagnostic and that no
    /// IDX003 auto-selection event is raised (the explicit path short-circuits auto-selection).
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ExplicitHint_WithIndex_EmitsExplicitIndexSelectedDiagnostic()
    {
        _ = await Db
            .Orders
            .WithIndex("ByStatus")
            .Where(o => o.Status == "PENDING")
            .ToListAsync(CancellationToken);

        // IDX004: explicit hint applied.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was explicitly selected via WithIndex()."));

        // No IDX003 auto-selection event — explicit path short-circuits auto-selection entirely.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(e => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    // ── Ordering alignment scoring ────────────────────────────────────────────

    /// <summary>
    /// Verifies that when a GSI PK equality constraint is combined with an <c>OrderBy</c> on the
    /// GSI sort key, the ordering-alignment bonus raises the candidate score and it is selected.
    /// Uses ByStatus (PK=Status, SK=CreatedAt): Status equality + OrderBy(CreatedAt) → score 2.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_GsiPkWithOrderBySk_AutoSelects_ScoringWinner()
    {
        // ByStatus PK=Status, SK=CreatedAt. Status equality passes Gate 1.
        // OrderBy(CreatedAt) aligns with ByStatus SK — ordering bonus score +1.
        // DynamoDB Local does not support ORDER BY on GSI sort keys, so wrap execution.
        try
        {
            _ = await Db
                .Orders
                .Where(o => o.Status == "PENDING")
                .OrderBy(o => o.CreatedAt)
                .ToListAsync(CancellationToken);
        }
        catch (AmazonDynamoDBException)
        {
            // DynamoDB Local limitation: ORDER BY on GSI sort keys is not supported.
        }

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'SecondaryIndexOrders' was auto-selected."));

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            ORDER BY "CreatedAt" ASC
            """);
    }

    // ── No predicate fallback ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a query with no WHERE clause results in no index PK constraints, so all
    /// candidates fail Gate 1 and the provider emits IDX001 and falls back to the base table.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_NoPredicate_EmitsNoCompatibleIndex_StaysOnBaseTable()
    {
        _ = await Db.Orders.ToListAsync(CancellationToken);

        // All candidates fail Gate 1 — no PK constraints extracted.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"
            """);
    }
}
