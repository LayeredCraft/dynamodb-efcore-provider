using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

/// <summary>
/// Integration tests that verify automatic index selection and explicit hints interact correctly
/// with EF Core discriminator predicates on a shared (multi-entity-type) indexed table.
/// </summary>
/// <remarks>
/// Table: <c>work-orders-indexed-table</c> — GSI <c>ByPriority</c> (PK=Priority) scoped to
/// <c>PriorityWorkOrderEntity</c>; LSI <c>ByStatus</c> (PK=Pk, SK=Status) inherited by all types.
/// Conservative auto-selection mode is active for all tests in this class.
/// </remarks>
public class SharedTableIndexAutoSelectionTests(SharedTableWithIndexesDynamoFixture fixture)
    : SharedTableWithIndexesTestBase(fixture)
{
    // ── Derived-type GSI auto-selection ──────────────────────────────────────

    /// <summary>
    /// Verifies that a derived-type query whose predicate covers the GSI partition key is
    /// auto-selected to that GSI. The single-value discriminator predicate
    /// (<c>"$type" = 'PriorityWorkOrderEntity'</c>) is a safe AND conjunction and must not
    /// block Gate 2 (unsafe-OR check).
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_DerivedTypeQuery_AutoSelects_ByPriorityGsi()
    {
        // Priority == 3 covers the ByPriority GSI PK. The discriminator adds a single equality,
        // which is a safe AND — not an OR — so HasUnsafeOr remains false.
        var results =
            await Db.PriorityWorkOrders.Where(o => o.Priority == 3).ToListAsync(CancellationToken);

        results
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(
                new PriorityWorkOrderEntity
                {
                    Pk = "WO#ALPHA", Sk = "WO#001", Status = "OPEN", Priority = 3,
                });

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' was auto-selected."));

        AssertSql(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"."ByPriority"
            WHERE "Priority" = 3 AND "$type" = 'PriorityWorkOrderEntity'
            """);
    }

    // ── Base-type OR discriminator safety ────────────────────────────────────

    /// <summary>
    /// Verifies that the OR discriminator predicate emitted for a base-type query
    /// (<c>"$type" = 'X' OR "$type" = 'Y'</c>) is classified as a safe filter-only OR by
    /// <c>DynamoConstraintExtractionVisitor</c>, because <c>$type</c> is not a partition key or
    /// sort key on any candidate index. The Pk equality constraint is still extracted and
    /// satisfies the ByStatus LSI Gate 1.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_BaseTypeQuery_OrDiscriminatorIsSafe_AutoSelects_ByStatusLsi()
    {
        // The base-type query injects: ("$type" = 'PriorityWorkOrderEntity' OR "$type" =
        // 'ArchivedWorkOrderEntity').
        // $type is not a PK/SK of any index, so the OR must be classified as safe (filter-only).
        // Pk = "WO#ALPHA" satisfies the ByStatus LSI partition-key gate.
        var results = await Db
            .WorkOrders
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        results
            .Should()
            .HaveCount(3)
            .And
            .Contain(o => o.Sk == "WO#001")
            .And
            .Contain(o => o.Sk == "WO#002")
            .And
            .Contain(o => o.Sk == "WO#003");

        // IDX003: ByStatus LSI auto-selected.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'work-orders-indexed-table' was auto-selected."));

        AssertSql(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"."ByStatus"
            WHERE "Pk" = 'WO#ALPHA' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    // ── No index PK constraint fallback ──────────────────────────────────────

    /// <summary>
    /// Verifies that a base-type query whose predicate covers only an LSI sort-key attribute
    /// (not any partition key) causes all candidates to fail Gate 1, and the query falls back to
    /// the base table with an IDX001 diagnostic.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_BaseTypeQuery_NoIndexPkConstraint_FallsBackToBaseTable()
    {
        // Status is the ByStatus LSI sort key — NOT a partition key on any index.
        // No GSI PK is constrained either. All candidates fail Gate 1.
        _ = await Db.WorkOrders.Where(o => o.Status == "OPEN").ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .Contain(e => e.EventId.Id == DynamoEventId.NoCompatibleSecondaryIndexFound.Id);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"
            WHERE "Status" = 'OPEN' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    // ── Index scoping: ByPriority not visible to ArchivedWorkOrders ──────────

    /// <summary>
    /// Verifies that the runtime model correctly scopes index candidates to the queried entity
    /// type. <c>ByPriority</c> is declared only on <c>PriorityWorkOrderEntity</c> and must not
    /// appear as a candidate for <c>ArchivedWorkOrderEntity</c> queries — neither auto-selected
    /// nor mentioned in any IDX005 rejection diagnostic.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Conservative_ArchivedWorkOrderQuery_ByPriorityGsiIsNotACandidate()
    {
        // Pk = "WO#ALPHA" would satisfy ByStatus LSI PK. ByPriority is not in the candidate list
        // for ArchivedWorkOrderEntity because it is scoped to PriorityWorkOrderEntity only.
        _ = await Db
            .ArchivedWorkOrders
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        // ByPriority must never appear in any diagnostic message for this query.
        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .NotContain(
                e => e.Message.Contains("ByPriority"),
                "ByPriority is scoped to PriorityWorkOrderEntity and must not be a candidate for ArchivedWorkOrderEntity queries");
    }

    // ── Explicit hint — IDX004 diagnostics ───────────────────────────────────

    /// <summary>
    /// Verifies that an explicit <c>.WithIndex("ByStatus")</c> hint on a base-type query emits
    /// an IDX004 diagnostic and routes the query to the LSI.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ExplicitHint_ByStatus_OnWorkOrders_EmitsIdx004()
    {
        _ = await Db
            .WorkOrders
            .WithIndex("ByStatus")
            .Where(o => o.Pk == "WO#ALPHA")
            .ToListAsync(CancellationToken);

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.LogLevel == LogLevel.Information
                && e.Message.Contains(
                    "Index 'ByStatus' on table 'work-orders-indexed-table' was explicitly selected via WithIndex()."));

        AssertSql(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"."ByStatus"
            WHERE "Pk" = 'WO#ALPHA' AND ("$type" = 'ArchivedWorkOrderEntity' OR "$type" = 'PriorityWorkOrderEntity')
            """);
    }

    /// <summary>
    /// Verifies that an explicit <c>.WithIndex("ByPriority")</c> hint on a derived-type query
    /// emits an IDX004 diagnostic and routes the query to the GSI.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ExplicitHint_ByPriority_OnPriorityWorkOrders_EmitsIdx004()
    {
        var results =
            await Db
                .PriorityWorkOrders
                .WithIndex("ByPriority")
                .Where(o => o.Priority == 5)
                .ToListAsync(CancellationToken);

        results
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeEquivalentTo(
                new PriorityWorkOrderEntity
                {
                    Pk = "WO#BETA", Sk = "WO#001", Status = "OPEN", Priority = 5,
                });

        LoggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.ExplicitIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' was explicitly selected via WithIndex()."));

        AssertSql(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"."ByPriority"
            WHERE "Priority" = 5 AND "$type" = 'PriorityWorkOrderEntity'
            """);
    }

    // ── Sibling index validation with real schema ─────────────────────────────

    /// <summary>
    /// Verifies that an explicit hint for a sibling entity type's index throws
    /// <c>InvalidOperationException</c> even when the physical index exists on the table.
    /// This confirms that candidate scoping enforced at compile time is independent of the
    /// physical DynamoDB schema.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task ExplicitHint_SiblingEntityIndex_OnArchivedWorkOrders_Throws()
    {
        // ByPriority is declared on PriorityWorkOrderEntity — not ArchivedWorkOrderEntity.
        // The postprocessor scopes candidates to ArchivedWorkOrderEntity and ByPriority is absent,
        // so the hint validator throws before any network call is made.
        var act = async ()
            => await Db.ArchivedWorkOrders.WithIndex("ByPriority").ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByPriority*");
    }

    // ── SuggestOnly mode on shared table ─────────────────────────────────────

    /// <summary>
    /// Verifies that <c>DynamoAutomaticIndexSelectionMode.SuggestOnly</c> emits an IDX003
    /// "would be selected" diagnostic for a shared-table derived-type query but does not rewrite
    /// the query source, leaving execution on the base table.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task SuggestOnly_DerivedTypeQuery_EmitsDiagnosticButStaysOnBaseTable()
    {
        var loggerFactory = new TestPartiQlLoggerFactory();
        var baseOptions = base.CreateOptions(loggerFactory);
        var suggestOptions =
            new DbContextOptionsBuilder<SharedTableWithIndexesDbContext>(baseOptions).UseDynamo(opt
                    => opt.UseAutomaticIndexSelection(
                        DynamoAutomaticIndexSelectionMode.SuggestOnly))
                .Options;

        await using var suggestDb = new SharedTableWithIndexesDbContext(suggestOptions);

        _ = await suggestDb
            .PriorityWorkOrders
            .Where(o => o.Priority == 3)
            .ToListAsync(CancellationToken);

        // IDX003 "would be selected" diagnostic emitted (checked before AssertBaseline which clears
        // state).
        loggerFactory
            .QueryDiagnosticEvents
            .Should()
            .ContainSingle(e
                => e.EventId.Id == DynamoEventId.SecondaryIndexSelected.Id
                && e.Message.Contains(
                    "Index 'ByPriority' on table 'work-orders-indexed-table' would be selected in Conservative mode."));

        // SuggestOnly: query executes on base table — no index in FROM clause.
        loggerFactory.AssertBaseline(
            """
            SELECT "Pk", "Sk", "Status", "Priority", "$type"
            FROM "work-orders-indexed-table"
            WHERE "Priority" = 3 AND "$type" = 'PriorityWorkOrderEntity'
            """);

        loggerFactory.Dispose();
    }
}
