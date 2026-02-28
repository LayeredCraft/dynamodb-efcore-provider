using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class FirstTests(PkSkTableDynamoFixture fixture) : PkSkTableTestBase(fixture)
{
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

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var results = await Db.Items.ToListAsync(CancellationToken);

        results.Should().HaveCount(PkSkItems.Items.Count);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            """);
    }

    [Fact]
    public async Task OrderBy_Sk_FirstAsync_ReturnsLowestSkWithinPartition()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .FirstAsync(CancellationToken);

        result.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0001");

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            ORDER BY Sk ASC
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithSelectivePredicate_PagesUntilMatch()
    {
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();

        result.Should().NotBeNull();
        result!.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0002");

        calls.Should().NotBeEmpty();

        // With Phase 4 changes: page size is null by default (DynamoDB scans up to 1MB)
        // This is more efficient than scanning 1 item at a time
        calls.Should().OnlyContain(call => call.Limit == null);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task FirstAsync_WithPredicate_ReturnsMatchingItem()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.Sk == "0002")
                .FirstAsync(CancellationToken);

        result.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0002");

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND Sk = '0002'
            """);
    }

    [Fact]
    public async Task FirstAsync_WithPredicate_ThrowsWhenNoMatch()
    {
        var act = async ()
            => await Db
                .Items
                .Where(item => item.Pk == "P#2" && item.IsTarget)
                .FirstAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#2' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithPredicate_ReturnsNullWhenNoMatch()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#2" && item.IsTarget)
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().BeNull();

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#2' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task FirstAsync_DoesNotEmitLimitClause()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.Sk == "0001")
                .FirstAsync(CancellationToken);

        result.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0001");

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND Sk = '0001'
            """);
    }

    [Fact]
    public async Task FirstAsync_WithPageSize_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .WithPageSize(10)
                .FirstAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();

        result.Should().NotBeNull();
        result.Pk.Should().Be("P#1");

        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 10);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithPageSizeOverload_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .FirstOrDefaultAsync(25, CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();

        result.Should().NotBeNull();
        result!.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0002");

        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 25);
        LoggerFactory.RowLimitingWarnings.Should().BeEmpty();

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task FirstAsync_WithoutPagination_StopsSinglePage()
    {
        LoggerFactory.Clear();

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .WithoutPagination()
                .FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();

        // With WithoutPagination, may or may not find result depending on first page
        // The key assertion is that only ONE request is made
        calls.Should().HaveCount(1);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task Take_WithSelectiveFilter_ContinuesPaging()
    {
        LoggerFactory.Clear();

        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1" && item.IsTarget)
                .Take(2)
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        // Should continue paging to get 2 results
        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1' AND IsTarget = TRUE
            """);
    }

    [Fact]
    public async Task Take_WithPageSize_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .WithPageSize(5)
                .Take(3)
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 5);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            """);
    }

    [Fact]
    public async Task Take_FirstAsync_UsesMinimumResultLimit()
    {
        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .Take(2)
                .FirstAsync(CancellationToken);

        result.Sk.Should().Be("0001");

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            ORDER BY Sk ASC
            """);
    }

    [Fact]
    public async Task Take_FirstOrDefaultAsync_UsesMinimumResultLimit()
    {
        var limit = 2;

        var result =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .Take(limit)
                .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();
        result!.Sk.Should().Be("0001");

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            ORDER BY Sk ASC
            """);
    }

    [Fact]
    public async Task Take_Take_UsesMinimumResultLimit()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .Take(3)
                .Take(5)
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            ORDER BY Sk ASC
            """);
    }

    [Fact]
    public async Task Take_Take_UsesMinimumResultLimit_WhenReversed()
    {
        var results =
            await Db
                .Items
                .Where(item => item.Pk == "P#1")
                .OrderBy(item => item.Sk)
                .Take(5)
                .Take(3)
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        AssertSql(
            """
            SELECT Pk, Sk, Category, IsTarget
            FROM PkSkItems
            WHERE Pk = 'P#1'
            ORDER BY Sk ASC
            """);
    }
}
