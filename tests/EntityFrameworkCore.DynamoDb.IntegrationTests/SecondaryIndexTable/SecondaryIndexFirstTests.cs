using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     Integration tests for <c>First*</c> query behavior when the active query source is
///     resolved via automatic index selection. Verifies that the safe-path contract (key-only
///     predicate, implicit <c>Limit=1</c>, correct results) and unsafe-path rejection hold when the
///     provider selects a GSI or LSI without an explicit <c>.WithIndex()</c> hint.
/// </summary>
/// <remarks>
///     All tests run in <c>Conservative</c> auto-selection mode. One explicit-hint test is
///     included as a SQL-baseline anchor to confirm the generated FROM clause shape.
/// </remarks>
public class SecondaryIndexFirstTests(SecondaryIndexDynamoFixture fixture)
    : SecondaryIndexTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>Enables Conservative auto-selection so queries are rewritten to secondary indexes.</remarks>
    protected override DbContextOptions<SecondaryIndexDbContext> CreateOptions(
        TestPartiQlLoggerFactory loggerFactory)
    {
        var builder =
            new DbContextOptionsBuilder<SecondaryIndexDbContext>(base.CreateOptions(loggerFactory));
        builder.UseDynamo(opt
            => opt.UseAutomaticIndexSelection(DynamoAutomaticIndexSelectionMode.Conservative));
        return builder.Options;
    }

    // ── GSI auto-selection + First* ──────────────────────────────────────────

    /// <summary>
    ///     Status equality auto-selects <c>ByStatus</c>. The GSI partition for PENDING contains two
    ///     items; <c>FirstAsync</c> with only the GSI PK constrained is key-only and safe. Verifies that
    ///     the provider sets <c>Limit=1</c> on the wire and returns one matching item.
    /// </summary>
    [Fact]
    public async Task FirstAsync_AutoSelects_ByStatusGsi_PkEquality_SetsLimit1_ReturnsOneItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.Status == "PENDING")
            .FirstAsync(CancellationToken);

        result.Status.Should().Be("PENDING");

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING'
            """);
    }

    /// <summary>
    ///     Status + CreatedAt equality auto-selects <c>ByStatus</c> and uniquely identifies one item.
    ///     Verifies correct data is returned and that <c>Limit=1</c> is set on the wire.
    /// </summary>
    [Fact]
    public async Task FirstAsync_AutoSelects_ByStatusGsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        var result = await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10")
            .FirstAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.Status == "PENDING" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'PENDING' AND "CreatedAt" = '2024-01-10'
            """);
    }

    /// <summary>
    ///     <c>FirstOrDefaultAsync</c> with auto-selected GSI should return <c>null</c> when no item
    ///     in the index partition satisfies the key conditions.
    /// </summary>
    [Fact]
    public async Task FirstOrDefaultAsync_AutoSelects_ByStatusGsi_ReturnsNullWhenNoMatch()
    {
        var result = await Db
            .Orders
            .Where(o => o.Status == "CANCELLED" && o.CreatedAt == "2024-01-01")
            .FirstOrDefaultAsync(CancellationToken);

        result.Should().BeNull();

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByStatus"
            WHERE "Status" = 'CANCELLED' AND "CreatedAt" = '2024-01-01'
            """);
    }

    // ── LSI auto-selection + First* ──────────────────────────────────────────

    /// <summary>
    ///     CustomerId + CreatedAt equality: the SK condition on <c>CreatedAt</c> gives
    ///     <c>ByCreatedAt</c> a score bonus over <c>ByPriority</c>, so it is auto-selected. Verifies that
    ///     <c>First*</c> uses the LSI's key schema for safe-path validation and that <c>Limit=1</c> is set
    ///     on the wire.
    /// </summary>
    [Fact]
    public async Task FirstAsync_AutoSelects_ByCreatedAtLsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        // C#1 has three orders; CreatedAt 2024-01-10 uniquely identifies C#1/O#001.
        var result = await Db
            .Orders
            .Where(o => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10")
            .FirstAsync(CancellationToken);

        var expected =
            OrderItems.Items.Single(o => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10");

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByCreatedAt"
            WHERE "CustomerId" = 'C#1' AND "CreatedAt" = '2024-01-10'
            """);
    }

    /// <summary>
    ///     CustomerId + Priority equality auto-selects <c>ByPriority</c> (SK condition bonus).
    ///     Verifies that <c>First*</c> uses the LSI's key schema and that <c>Limit=1</c> is set.
    /// </summary>
    [Fact]
    public async Task FirstAsync_AutoSelects_ByPriorityLsi_PkAndSkEquality_ReturnsExactItem()
    {
        LoggerFactory.Clear();

        // C#1 has Priority values 1, 5, 3; Priority=5 uniquely identifies C#1/O#002.
        var result = await Db
            .Orders
            .Where(o => o.CustomerId == "C#1" && o.Priority == 5)
            .FirstAsync(CancellationToken);

        var expected = OrderItems.Items.Single(o => o.CustomerId == "C#1" && o.Priority == 5);

        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByPriority"
            WHERE "CustomerId" = 'C#1' AND "Priority" = 5
            """);
    }

    // ── Unsafe-path rejection with auto-selected index ───────────────────────

    /// <summary>
    ///     <c>Region</c> is not a key attribute of the auto-selected <c>ByStatus</c> GSI. The
    ///     safe-path validator must use the GSI's effective key schema (Status, CreatedAt) and reject the
    ///     query — the non-key predicate on Region must not slip through.
    /// </summary>
    [Fact]
    public async Task
        FirstOrDefaultAsync_AutoSelects_ByStatusGsi_NonKeyFilter_ThrowsTranslationFailure()
    {
        // Region is a non-key attribute relative to ByStatus (keys: Status, CreatedAt).
        var act = async () => await Db
            .Orders
            .Where(o => o.Status == "PENDING" && o.Region == "US-EAST")
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    /// <summary>
    ///     <c>OrderId</c> is not a key attribute of the auto-selected <c>ByCreatedAt</c> LSI (keys:
    ///     CustomerId, CreatedAt). A non-key predicate must be rejected even when the LSI was chosen
    ///     automatically rather than via <c>.WithIndex()</c>.
    /// </summary>
    [Fact]
    public async Task
        FirstOrDefaultAsync_AutoSelects_ByCreatedAtLsi_NonKeyFilter_ThrowsTranslationFailure()
    {
        // OrderId is the base-table SK but a non-key attribute on the ByCreatedAt LSI.
        var act = async () => await Db
            .Orders
            .Where(o
                => o.CustomerId == "C#1" && o.CreatedAt == "2024-01-10" && o.OrderId == "O#001")
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    // ── Explicit-hint baseline ───────────────────────────────────────────────

    /// <summary>
    ///     Explicit <c>.WithIndex("ByRegion")</c> anchor test. US-WEST has one seeded item, so the
    ///     result is deterministic. Confirms the FROM clause shape and that <c>Limit=1</c> is set on the
    ///     wire when the index source is chosen via an explicit hint.
    /// </summary>
    [Fact]
    public async Task FirstAsync_ExplicitHint_ByRegionGsi_PkEquality_SetsLimit1_ReturnsItem()
    {
        LoggerFactory.Clear();

        // US-WEST has exactly one item (C#1/O#003), making the result deterministic.
        var result = await Db
            .Orders
            .WithIndex("ByRegion")
            .Where(o => o.Region == "US-WEST")
            .FirstAsync(CancellationToken);

        var expected = OrderItems.Items.Single(o => o.Region == "US-WEST");
        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "CustomerId", "OrderId", "$version", "CreatedAt", "Priority", "Region", "Status"
            FROM "SecondaryIndexOrders"."ByRegion"
            WHERE "Region" = 'US-WEST'
            """);
    }
}
