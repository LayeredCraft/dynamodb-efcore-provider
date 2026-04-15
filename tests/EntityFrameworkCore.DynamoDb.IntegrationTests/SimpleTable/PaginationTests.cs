using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for the pagination model: Limit(n) and key-only First*.</summary>
public class PaginationTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    // ── Limit(n) on ToListAsync ──────────────────────────────────────────────

    [Fact]
    public async Task Limit_SetsRequestLimit_OnToListAsync()
    {
        SqlCapture.Clear();

        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(3);
    }

    [Fact]
    public async Task Limit_ChainedTwice_LastOneWins()
    {
        SqlCapture.Clear();

        await Db.SimpleItems.Limit(10).Limit(20).ToListAsync(CancellationToken);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(20);
    }

    [Fact]
    public async Task Limit_SingleRequest_ToListAsync_NoNextTokenFollowUp()
    {
        // Limit(n) is always a single request — the provider must not follow NextToken.
        SqlCapture.Clear();

        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().HaveCount(1);
    }

    // ── First* with key-only path ────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_KeyOnly_UsesImplicitLimit1()
    {
        // PK equality, no sort key → safe, implicit Limit=1.
        SqlCapture.Clear();

        var result =
            await Db
                .SimpleItems
                .Where(x => x.Pk == "ITEM#1")
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_WithUserLimit_ThrowsTranslationFailure()
    {
        // Limit(n) + First* is disallowed — use
        // .Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct).
        var act = async () => await Db
            .SimpleItems
            .Where(x => x.Pk == "ITEM#1")
            .Limit(5)
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    // ── Take is removed — must throw ─────────────────────────────────────────

    [Fact]
    public async Task Take_ThrowsTranslationFailurePointingToLimit()
    {
        var act = async () => await Db.SimpleItems.Take(3).ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Limit(n)*");
    }
}
