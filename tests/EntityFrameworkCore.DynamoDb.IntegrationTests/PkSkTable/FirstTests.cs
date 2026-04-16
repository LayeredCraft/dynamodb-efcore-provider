using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Integration tests for First* query behavior with the ADR-002 pagination model.</summary>
public class FirstTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    // ── Model smoke tests ────────────────────────────────────────────────────

    [Fact]
    public void PkAndSkProperlyConfiguredAsKeys()
    {
        var entityType = Db.Model.FindEntityType(typeof(PkSkItem))!;
        var pkName = entityType.GetPartitionKeyPropertyName();
        var skName = entityType.GetSortKeyPropertyName();
        var efPrimaryKey = entityType.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray();

        pkName.Should().Be("Pk");
        skName.Should().Be("Sk");
        efPrimaryKey.Should().Equal("Pk", "Sk");
    }

    // ── ToListAsync — baseline ───────────────────────────────────────────────

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(PkSkItems.Items);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            """);
    }

    // ── Key-only First* — safe path ──────────────────────────────────────────

    [Fact]
    public async Task OrderBy_Sk_FirstAsync_ReturnsLowestSkWithinPartition()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .FirstAsync(CancellationToken);

        var expected =
            PkSkItems.Items.Where(item => item.Pk == "P#1").OrderBy(item => item.Sk).First();

        result.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "sk" ASC
            """);
    }

    [Fact]
    public async Task FirstAsync_KeyOnly_PkAndSkEquality_ReturnsMatchingItem()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.Sk == "0002")
                .FirstAsync(CancellationToken);

        var expected = PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0002");

        result.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1' AND "sk" = '0002'
            """);
    }

    [Fact]
    public async Task FirstAsync_KeyOnly_SetsImplicitLimit1_OnRequest()
    {
        // Key-only First* sets implicit Limit=1 on the ExecuteStatement request.
        LoggerFactory.Clear();

        await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.Sk == "0001")
            .FirstAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1' AND "sk" = '0001'
            """);
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_WithUserLimit_ThrowsTranslationFailure()
    {
        // Limit(n) + First* is disallowed — use
        // .Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct).
        var act = async ()
            => await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .Limit(5)
                .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    [Fact]
    public async Task FirstAsync_KeyOnly_ThrowsWhenNoMatch()
    {
        // Key-only path, no match — First throws, no translation failure.
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#2" && item.Sk == "9999")
            .FirstAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no elements*");
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_ReturnsNullWhenNoMatch()
    {
        var result = await Db
            .Items
            .Where(item => item.Pk == "P#2" && item.Sk == "9999")
            .FirstOrDefaultAsync(CancellationToken);

        result.Should().BeNull();
    }

    // ── SK filter predicates — always throws ────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_SkIn_ThrowsTranslationFailure()
    {
        // SK IN (...) is a filter expression in DynamoDB — not a key condition. Limit=1 counts
        // scanned items (not matched), so it would silently miss matching rows in the partition.
        var skValues = new[] { "0002", "0003" };
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#1" && skValues.Contains(item.Sk))
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    [Fact]
    public async Task FirstOrDefault_SkOrEquality_ThrowsTranslationFailure()
    {
        // SK = A OR SK = B is a filter expression — not a key condition. Same Limit=1 hazard.
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#1" && (item.Sk == "0002" || item.Sk == "0003"))
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }

    // ── Non-key First* — always throws ──────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithoutOptIn_ThrowsTranslationFailure()
    {
        // IsTarget is a non-key attribute — unsafe path always throws.
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.IsTarget)
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
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#1")
            .Take(3)
            .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Limit(n)*");
    }
}
