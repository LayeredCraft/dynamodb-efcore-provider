using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for the pagination model: Limit(n) and key-only First*.</summary>
#pragma warning disable EF9102
public class PaginationTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    // ── Limit(n) on ToListAsync ──────────────────────────────────────────────

    [Fact]
    public async Task Limit_SetsRequestLimit_OnToListAsync()
    {
        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(3);
    }

    [Fact]
    public async Task Limit_ChainedTwice_LastOneWins()
    {
        await Db.SimpleItems.Limit(10).Limit(20).ToListAsync(CancellationToken);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(20);
    }

    [Fact]
    public async Task Limit_SingleRequest_ToListAsync_NoNextTokenFollowUp()
    {
        // Limit(n) is always a single request — the provider must not follow NextToken.
        await Db.SimpleItems.Limit(3).ToListAsync(CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToPageAsync_ResumeFromToken_SeedsNextRequest()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);

        firstPage.NextToken.Should().NotBeNull();
        SqlCapture.ExecuteStatementCalls.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls[0].Limit.Should().Be(1);
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeFalse();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeFalse();

        SqlCapture.Clear();

        _ = await Db.SimpleItems.ToPageAsync(1, firstPage.NextToken, CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls[0].Limit.Should().Be(1);
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeTrue();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
    }

    [Fact]
    public async Task WithNextToken_AndLimit_PerformsSingleRequestFromSavedCursor()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        SqlCapture.Clear();

        _ = await Db
            .SimpleItems
            .WithNextToken(firstPage.NextToken!)
            .Limit(1)
            .ToListAsync(CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls[0].Limit.Should().Be(1);
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeTrue();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
    }

    [Fact]
    public async Task ToPageAsync_FinalPage_ReturnsNullNextToken()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(3, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        SqlCapture.Clear();

        var finalPage = await Db.SimpleItems.ToPageAsync(3, firstPage.NextToken, CancellationToken);

        finalPage.NextToken.Should().BeNull();
        finalPage.HasMoreResults.Should().BeFalse();
    }

    [Fact]
    public async Task WithNextToken_ToListAsync_ResumesFromSavedCursor()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        SqlCapture.Clear();

        var remaining = await Db
            .SimpleItems
            .WithNextToken(firstPage.NextToken!)
            .ToListAsync(CancellationToken);

        remaining.Should().HaveCount(SimpleItems.Items.Count - firstPage.Items.Count);
        SqlCapture.ExecuteStatementCalls.Should().NotBeEmpty();
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeTrue();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
        SqlCapture
            .ExecuteStatementCalls
            .Skip(1)
            .Should()
            .OnlyContain(call => call.SeedNextTokenPresent == false);
    }

    [Fact]
    public async Task WithNextToken_ThenToPageAsync_SeedsSingleRequest()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        SqlCapture.Clear();

        var resumedPage = await Db
            .SimpleItems
            .WithNextToken(firstPage.NextToken!)
            .ToPageAsync(1, null, CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls[0].Limit.Should().Be(1);
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeTrue();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
        resumedPage.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task WithNextToken_Reenumeration_ReseedsFirstRequestEachRun()
    {
        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        var resumedQuery = Db.SimpleItems.WithNextToken(firstPage.NextToken!);

        SqlCapture.Clear();
        _ = await resumedQuery.ToListAsync(CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().NotBeEmpty();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
        SqlCapture
            .ExecuteStatementCalls
            .Skip(1)
            .Should()
            .OnlyContain(call => call.SeedNextTokenPresent == false);

        SqlCapture.Clear();
        _ = await resumedQuery.ToListAsync(CancellationToken);

        SqlCapture.ExecuteStatementCalls.Should().NotBeEmpty();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
        SqlCapture
            .ExecuteStatementCalls
            .Skip(1)
            .Should()
            .OnlyContain(call => call.SeedNextTokenPresent == false);
    }

    [Fact]
    public async Task ToListAsync_MultiRequestWithoutSeed_KeepsSeedNextTokenFalse()
    {
        var marker = $"seed-next-token-{Guid.NewGuid():N}";
        var largePayload = new string('x', 20_000);

        var extraItems = Enumerable
            .Range(0, 80)
            .Select(i => new SimpleItem
            {
                Pk = $"EXTRA#{marker}#{i:D3}",
                BoolValue = i % 2 == 0,
                IntValue = 1_000 + i,
                LongValue = 10_000 + i,
                FloatValue = 1.25f,
                DoubleValue = 2.5,
                DecimalValue = 3.75m,
                StringValue = largePayload,
                GuidValue = Guid.NewGuid(),
                DateTimeOffsetValue = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
                NullableBoolValue = null,
                NullableIntValue = null,
                NullableStringValue = marker,
            })
            .ToList();

        Db.SimpleItems.AddRange(extraItems);
        await Db.SaveChangesAsync(CancellationToken);

        try
        {
            SqlCapture.Clear();

            var results = await Db
                .SimpleItems
                .Where(x => x.NullableStringValue == marker)
                .ToListAsync(CancellationToken);

            results.Should().HaveCount(extraItems.Count);
            SqlCapture.ExecuteStatementCalls.Should().HaveCountGreaterThan(1);
            SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeFalse();
            SqlCapture
                .ExecuteStatementCalls
                .Skip(1)
                .Should()
                .OnlyContain(call => call.RequestNextTokenPresent);
            SqlCapture
                .ExecuteStatementCalls
                .Should()
                .OnlyContain(call => call.SeedNextTokenPresent == false);
        }
        finally
        {
            Db.SimpleItems.RemoveRange(extraItems);
            await Db.SaveChangesAsync(CancellationToken);
        }
    }

    [Fact]
    public async Task ToPageAsync_Items_MatchInMemoryTakeSemantics()
    {
        // Fetch all seeded items in a single page and verify the returned entities match the
        // known seed data. DynamoDB scan order is nondeterministic, so comparison is
        // order-insensitive.
        var page =
            await Db.SimpleItems.ToPageAsync(SimpleItems.Items.Count, null, CancellationToken);

        page.Items.Should().HaveCount(SimpleItems.Items.Count);
        page.Items.Should().BeEquivalentTo(SimpleItems.Items);
    }

    // ── First* with key-only path ────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_KeyOnly_UsesImplicitLimit1()
    {
        // PK equality, no sort key → safe, implicit Limit=1.
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
#pragma warning restore EF9102
