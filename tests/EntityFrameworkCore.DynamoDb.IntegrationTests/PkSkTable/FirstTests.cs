using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Integration tests for First* query behavior with the ADR-002 pagination model.</summary>
public class FirstTests(PkSkTableDynamoFixture fixture) : PkSkTableTestBase(fixture)
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
            SELECT "Pk", "Sk", "Category", "IsTarget"
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
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1'
            ORDER BY "Sk" ASC
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
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1' AND "Sk" = '0002'
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
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1' AND "Sk" = '0001'
            """);
    }

    [Fact]
    public async Task FirstAsync_KeyOnly_WithExplicitLimit_UsesExplicitLimit()
    {
        // User Limit(n) takes precedence over the implicit Limit=1 set by First*.
        LoggerFactory.Clear();

        await Db.Items.Where(item => item.Pk == "P#1").Limit(5).FirstAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(5);
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

    // ── Non-key First* — must opt in ────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithoutOptIn_ThrowsTranslationFailure()
    {
        // IsTarget is a non-key attribute — requires WithNonKeyFilter() opt-in.
        var act = async () => await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.IsTarget)
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*WithNonKeyFilter*");
    }

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithOptIn_ReturnsMatch()
    {
        // P#1 has two IsTarget=true items; WithNonKeyFilter lets the query through.
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .WithNonKeyFilter()
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();
        result!.Pk.Should().Be("P#1");
        result.IsTarget.Should().BeTrue();

        // Limit is null (1MB default) because no explicit Limit(n) was set.
        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().BeNull();

        AssertSql(
            """
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1' AND "IsTarget" = TRUE
            """);
    }

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithOptIn_WithLimit_UsesLimit()
    {
        // WithNonKeyFilter + Limit(n): user limit is preserved on the request.
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .WithNonKeyFilter()
                .Limit(50)
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(50);
    }

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithOptIn_ReturnsNullWhenNoMatch()
    {
        // P#2 has no IsTarget=true items; WithNonKeyFilter lets the query through → null.
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#2" && item.IsTarget)
                .WithNonKeyFilter()
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().BeNull();

        AssertSql(
            """
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#2' AND "IsTarget" = TRUE
            """);
    }

    [Fact]
    public async Task FirstOrDefault_WithNonKeyFilter_IsAlwaysSingleRequest()
    {
        // Regardless of opt-in, First* must be a single request (never pages).
        LoggerFactory.Clear();

        await Db
            .Items
            .Where(item => item.Pk == "P#1" && item.IsTarget)
            .WithNonKeyFilter()
            .FirstOrDefaultAsync(CancellationToken);

        LoggerFactory.ExecuteStatementCalls.Should().HaveCount(1);
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
