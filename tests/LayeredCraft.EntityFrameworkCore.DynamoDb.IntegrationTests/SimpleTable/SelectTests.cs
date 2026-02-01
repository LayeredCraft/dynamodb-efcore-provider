using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class SelectTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task Select_AnonymousObjectProjection()
    {
        var results =
            await Db
                .SimpleItems.OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.IntValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue
            FROM SimpleItems
            ORDER BY Pk
            """);
    }

    [Fact]
    public async Task Select_DtoProjection()
    {
        var results =
            await Db
                .SimpleItems.OrderBy(item => item.Pk)
                .Select(item => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.OrderBy(item => item.Pk)
                .Select(item => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue
            FROM SimpleItems
            ORDER BY Pk
            """);
    }

    [Fact]
    public async Task Select_ScalarProjection()
    {
        var results =
            await Db
                .SimpleItems.OrderBy(item => item.Pk)
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.OrderBy(item => item.Pk).Select(item => item.Pk).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk
            FROM SimpleItems
            ORDER BY Pk
            """);
    }

    private sealed class SimpleItemDto
    {
        public required string Pk { get; set; }

        public int IntValue { get; set; }
    }
}
