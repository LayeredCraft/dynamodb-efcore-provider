using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>
///     Integration tests for ORDER BY validation and execution against DynamoDB Local. These
///     complement the unit tests in <c>OrderByPartitionKeyValidationTests</c> by verifying that
///     validation is wired through the full LINQ → EF compilation pipeline and that valid queries
///     execute correctly against a real DynamoDB endpoint.
/// </summary>
public class OrderByTests(PkSkTableDynamoFixture fixture) : PkSkTableTestBase(fixture)
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
    ///     ORDER BY on a non-sort-key attribute must throw a provider error at query compilation
    ///     time, even when the WHERE clause has a valid partition key constraint.
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

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*sort key*");
    }

    // ── happy path ────────────────────────────────────────────────────────────

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

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1'
            ORDER BY "Sk" ASC
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

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Category", "IsTarget"
            FROM "PkSkItems"
            WHERE "Pk" = 'P#1'
            ORDER BY "Sk" DESC
            """);
    }
}
