using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class FirstTests(PkSkTableDynamoFixture fixture) : PkSkTableTestBase(fixture)
{
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
                .Items.Where(item => item.Pk == "P#1")
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
                .Items.Where(item => item.Pk == "P#1" && item.IsTarget)
                .FirstOrDefaultAsync(CancellationToken);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();

        result.Should().NotBeNull();
        result!.Pk.Should().Be("P#1");
        result.Sk.Should().Be("0002");

        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 1);

        if (calls.Any(call => call.ItemsCount == 0 && call.ResponseNextTokenPresent == true))
            calls.Count.Should().BeGreaterThan(1);

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
                .Items.Where(item => item.Pk == "P#1" && item.Sk == "0002")
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
                .Items.Where(item => item.Pk == "P#2" && item.IsTarget)
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
                .Items.Where(item => item.Pk == "P#2" && item.IsTarget)
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
                .Items.Where(item => item.Pk == "P#1" && item.Sk == "0001")
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
}
