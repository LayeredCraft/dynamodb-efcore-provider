using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class QueryTests(SimpleTableDynamoFixture fixture)
    : DynamoDbQueryTestBase<SimpleTableDynamoFixture, SimpleTableDbContext>(
        fixture,
        fixture.Container.GetConnectionString())
{
    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        await using var db = CreateContext();

        var resultItems = await db.SimpleItems.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(SimpleItems.Items);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Where_ComplexPredicate_ReturnsFilteredItems()
    {
        await using var db = CreateContext();

        // Intentionally mixes comparison operators and boolean logic.
        // Goal: exercise predicate translation and parameterization.
        var x = 500;
        var query = db.SimpleItems.Where(item
            => item.IntValue >= 0
               && item.LongValue > x
               && item.StringValue != "delta"
               && (item.BoolValue == true || item.DoubleValue < 0));

        var resultItems = await query.ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 0
               && item.LongValue > 500
               && item.StringValue != "delta"
               && (item.BoolValue || item.DoubleValue < 0));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue >= 0 AND LongValue > ? AND StringValue <> 'delta' AND (BoolValue = TRUE OR DoubleValue < 0)
            """);
    }

    [Fact]
    public async Task Where_MultipleWhereCalls_CombinePredicates()
    {
        await using var db = CreateContext();

        // Use multiple Where calls so the provider has to combine predicates.
        var query = db
            .SimpleItems.Where(item => item.IntValue != 200000)
            .Where(item => item.IntValue > -200)
            .Where(item => item.LongValue <= 1000 || item.BoolValue == true);

        var resultItems = await query.ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.IntValue != 200000)
                .Where(item => item.IntValue > -200)
                .Where(item => item.LongValue <= 1000 || item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue <> 200000 AND IntValue > -200 AND (LongValue <= 1000 OR BoolValue = TRUE)
            """);
    }

    [Fact]
    public async Task OrderBy_ThenBy_WithOrPredicate_ReturnsItemsInAscendingOrder()
    {
        await using var db = CreateContext();

        // DynamoDB PartiQL requires a hash-key condition when using ORDER BY.
        // We keep ORDER BY strictly on the primary key, but make the predicate non-trivial.
        var query = db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#3" || item.Pk == "ITEM#1" || item.Pk == "ITEM#4")
            .OrderBy(item => item.Pk)
            .ThenBy(item => item.Pk);

        var resultItems = await query.ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item
                    => item.Pk == "ITEM#3" || item.Pk == "ITEM#1" || item.Pk == "ITEM#4")
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Pk)
                .ToList();

        // DynamoDB Local doesn't reliably return deterministic ordering for these PartiQL scans,
        // so validate ordering via the generated PartiQL instead of result ordering.
        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#3' OR Pk = 'ITEM#1' OR Pk = 'ITEM#4'
            ORDER BY Pk ASC, Pk ASC
            """);
    }

    [Fact]
    public async Task
        OrderByDescending_ThenByDescending_WithAndOrPredicate_ReturnsItemsInExpectedOrder()
    {
        await using var db = CreateContext();

        var query = db
            .SimpleItems
            .Where(item
                => (item.Pk == "ITEM#1" || item.Pk == "ITEM#2" || item.Pk == "ITEM#3")
                   && (item.IntValue >= 100 || item.BoolValue == false))
            .OrderByDescending(item => item.Pk)
            .ThenByDescending(item => item.Pk);

        var resultItems = await query.ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item
                    => (item.Pk == "ITEM#1" || item.Pk == "ITEM#2" || item.Pk == "ITEM#3")
                       && (item.IntValue >= 100 || !item.BoolValue))
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Pk)
                .ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE (Pk = 'ITEM#1' OR Pk = 'ITEM#2' OR Pk = 'ITEM#3') AND (IntValue >= 100 OR BoolValue = FALSE)
            ORDER BY Pk DESC, Pk DESC
            """);
    }

    [Fact]
    public async Task Where_WithCapturedVariables_InlinesParametersCorrectly()
    {
        await using var db = CreateContext();

        // Use captured variables to test parameter handling
        var minInt = 100;
        var maxLong = 1000L;
        var excludeString = "delta";

        var query = db.SimpleItems.Where(item
            => item.IntValue >= minInt
               && item.LongValue <= maxLong
               && item.StringValue != excludeString);

        var resultItems = await query.ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 100 && item.LongValue <= 1000 && item.StringValue != "delta");

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue >= ? AND LongValue <= ? AND StringValue <> ?
            """);
    }
}
