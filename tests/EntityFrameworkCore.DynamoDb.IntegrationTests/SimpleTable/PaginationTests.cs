using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for the ADR-002 pagination model: Limit(n), First*, WithNonKeyFilter.</summary>
public class PaginationTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    // ── Limit(n) on ToListAsync ──────────────────────────────────────────────

    [Fact]
    public async Task Limit_SetsRequestLimit_OnToListAsync()
    {
        LoggerFactory.Clear();

        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(3);
    }

    [Fact]
    public async Task Limit_ChainedTwice_LastOneWins()
    {
        LoggerFactory.Clear();

        await Db.SimpleItems.Limit(10).Limit(20).ToListAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(20);
    }

    [Fact]
    public async Task Limit_SingleRequest_ToListAsync_NoNextTokenFollowUp()
    {
        // Limit(n) is always a single request — the provider must not follow NextToken.
        LoggerFactory.Clear();

        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        LoggerFactory.ExecuteStatementCalls.Should().HaveCount(1);
    }

    // ── First* with key-only path ────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_KeyOnly_UsesImplicitLimit1()
    {
        // PK equality, no sort key → safe, implicit Limit=1.
        LoggerFactory.Clear();

        var result =
            await Db
                .SimpleItems
                .Where(x => x.Pk == "ITEM#1")
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_WithExplicitLimit_UsesExplicitLimit()
    {
        // User Limit(n) overrides the implicit Limit=1 set by First*.
        LoggerFactory.Clear();

        var result =
            await Db
                .SimpleItems
                .Where(x => x.Pk == "ITEM#1")
                .Limit(5)
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(5);
    }

    [Fact]
    public async Task Limit_OnFirstOrDefault_SetsRequestLimit()
    {
        LoggerFactory.Clear();

        await Db.SimpleItems.Limit(3).FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(3);
    }

    // ── First* with WithNonKeyFilter opt-in ─────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_WithNonKeyFilter_OptIn_NoUserLimit_LimitIsNull()
    {
        // Scan-like (no PK equality) + opt-in: ClearImplicitLimit is called → Limit=null (1MB
        // default).
        LoggerFactory.Clear();

        // Any result is fine — we're asserting the request Limit is null.
        await Db
            .SimpleItems
            .Where(x => x.IntValue > 0)
            .WithNonKeyFilter()
            .FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().BeNull();
    }

    [Fact]
    public async Task FirstOrDefault_WithNonKeyFilter_OptIn_WithUserLimit_LimitIsPreserved()
    {
        // WithNonKeyFilter + Limit(n): user limit is preserved (ClearImplicitLimit is no-op when
        // HasUserLimit=true).
        LoggerFactory.Clear();

        await Db
            .SimpleItems
            .Where(x => x.IntValue > 0)
            .WithNonKeyFilter()
            .Limit(50)
            .FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(50);
    }

    [Fact]
    public async Task FirstOrDefault_WithNonKeyFilter_OptIn_SingleRequest()
    {
        // First* is always a single request, regardless of opt-in.
        LoggerFactory.Clear();

        await Db
            .SimpleItems
            .Where(x => x.IntValue > 0)
            .WithNonKeyFilter()
            .FirstOrDefaultAsync(CancellationToken);

        LoggerFactory.ExecuteStatementCalls.Should().HaveCount(1);
    }

    // ── Take is removed — must throw ─────────────────────────────────────────

    [Fact]
    public async Task Take_ThrowsTranslationFailurePointingToLimit()
    {
        var act = async () => await Db.SimpleItems.Take(3).ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Limit(n)*");
    }
}
