using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>
///     Integration tests for ORDER BY validation and execution against DynamoDB Local. These
///     complement the unit tests in <c>OrderByPartitionKeyValidationTests</c> by verifying that
///     validation is wired through the full LINQ → EF compilation pipeline and that valid queries
///     execute correctly against a real DynamoDB endpoint.
/// </summary>
public class OrderByTests(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    // ── error cases ───────────────────────────────────────────────────────────

    /// <summary>
    ///     ORDER BY without a WHERE clause must throw a provider error at query compilation time,
    ///     before any request is sent to DynamoDB.
    /// </summary>
    [Fact]
    public async Task OrderBy_WithNoPkConstraint_ThrowsProviderError()
    {
        var act = async ()
            => await Db.Items.OrderBy(item => item.Sk).ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*partition key*");
    }

    /// <summary>
    ///     ORDER BY on a non-key attribute must throw a provider error at query compilation time,
    ///     even when the WHERE clause has a valid partition key constraint.
    /// </summary>
    [Fact]
    public async Task OrderBy_OnNonSortKeyAttribute_ThrowsProviderError()
    {
        var act = async ()
            => await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Category)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*key attribute*");
    }

    // ── happy path: SK ordering ───────────────────────────────────────────────

    /// <summary>
    ///     ORDER BY sort key with a partition key equality constraint must execute successfully and
    ///     return items in ascending sort key order as DynamoDB delivers them.
    /// </summary>
    [Fact]
    public async Task OrderBy_Sk_WithPkConstraint_ReturnsItemsInAscendingSortKeyOrder()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems.Items.Where(item => item.Pk == "P#1").OrderBy(item => item.Sk).ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "sk" ASC
            """);
    }

    /// <summary>
    ///     ORDER BY sort key descending with a partition key equality constraint must return items in
    ///     descending sort key order.
    /// </summary>
    [Fact]
    public async Task OrderByDescending_Sk_WithPkConstraint_ReturnsItemsInDescendingSortKeyOrder()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderByDescending(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderByDescending(item => item.Sk)
                .ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "sk" DESC
            """);
    }

    // ── happy path: PK ordering ───────────────────────────────────────────────

    /// <summary>
    ///     ORDER BY partition key with a partition key equality constraint is valid and returns items
    ///     in ascending partition key order.
    /// </summary>
    [Fact]
    public async Task OrderBy_Pk_WithPkEqualityConstraint_Valid()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Pk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems.Items.Where(item => item.Pk == "P#1").OrderBy(item => item.Pk).ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "pk" ASC
            """);
    }

    /// <summary>ORDER BY PK ASC then SK ASC with equality PK constraint returns items in PK then SK order.</summary>
    [Fact]
    public async Task OrderBy_PkThenSk_WithPkEqualityConstraint_ReturnsItemsInExactOrder()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Sk)
                .ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "pk" ASC, "sk" ASC
            """);
    }

    /// <summary>
    ///     ORDER BY PK DESC then SK DESC with equality PK constraint returns items in reverse PK then
    ///     reverse SK order.
    /// </summary>
    [Fact]
    public async Task OrderByDescending_PkThenSkDescending_WithPkEqualityConstraint_Valid()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Sk)
                .ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            ORDER BY "pk" DESC, "sk" DESC
            """);
    }

    // ── happy path: multi-partition PK IN ordering ────────────────────────────

    /// <summary>
    ///     ORDER BY PK ASC then SK ASC with IN PK constraint (multi-partition) returns items ordered
    ///     first by PK then by SK across partitions.
    /// </summary>
    [Fact]
    public async Task OrderBy_PkThenSk_WithPkInConstraint_ReturnsItemsInExactOrder()
    {
        var pks = new[] { "P#1", "P#2" };

        var results =
            await Db
                .Items
                .Where(item => pks.Contains(item.Pk))
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems
                .Items
                .Where(item => pks.Contains(item.Pk))
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Sk)
                .ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" IN [?, ?]
            ORDER BY "pk" ASC, "sk" ASC
            """);
    }

    /// <summary>
    ///     ORDER BY PK DESC then SK DESC with IN PK constraint returns items in descending PK then
    ///     descending SK order across partitions.
    /// </summary>
    [Fact]
    public async Task OrderByDescending_PkThenByDescendingSk_WithPkInConstraint_Valid()
    {
        var pks = new[] { "P#1", "P#2" };

        var results =
            await Db
                .Items
                .Where(item => pks.Contains(item.Pk))
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Sk)
                .ToListAsync(CancellationToken);

        var expected =
            PkSkItems
                .Items
                .Where(item => pks.Contains(item.Pk))
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Sk)
                .ToList();

        results.Should().Equal(expected);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" IN [?, ?]
            ORDER BY "pk" DESC, "sk" DESC
            """);
    }

    // ── error case: multi-partition SK-first ordering ─────────────────────────

    /// <summary>
    ///     ORDER BY SK (not PK) with a multi-partition IN constraint must throw a provider error
    ///     because DynamoDB requires the partition key to lead the ORDER BY chain.
    /// </summary>
    [Fact]
    public async Task OrderBy_Sk_WithPkInConstraint_ThrowsProviderError()
    {
        var pks = new[] { "P#1", "P#2" };

        var act = async ()
            => await Db
                .Items
                .Where(item => pks.Contains(item.Pk))
                .OrderBy(item => item.Sk)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*partition key*");
    }
}
