using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class WhereTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var resultItems = await Db.SimpleItems.ToListAsync(CancellationToken);

        resultItems.Should().BeEquivalentTo(SimpleItems.Items);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Where_ComplexPredicate_ReturnsFilteredItems()
    {
        // Intentionally mixes comparison operators and boolean logic.
        // Goal: exercise predicate translation and parameterization.
        var x = 500;
        var resultItems =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue >= 0
                    && item.LongValue > x
                    && item.StringValue != "delta"
                    && (item.BoolValue == true || item.DoubleValue < 0))
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 0
            && item.LongValue > 500
            && item.StringValue != "delta"
            && (item.BoolValue || item.DoubleValue < 0));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue >= 0 AND LongValue > ? AND StringValue <> 'delta' AND (BoolValue = TRUE OR DoubleValue < 0)
            """);
    }

    [Fact]
    public async Task Where_MultipleWhereCalls_CombinePredicates()
    {
        // Use multiple Where calls so the provider has to combine predicates.
        var resultItems = await Db
            .SimpleItems.Where(item => item.IntValue != 200000)
            .Where(item => item.IntValue > -200)
            .Where(item => item.LongValue <= 1000 || item.BoolValue == true)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.IntValue != 200000)
                .Where(item => item.IntValue > -200)
                .Where(item => item.LongValue <= 1000 || item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue <> 200000 AND IntValue > -200 AND (LongValue <= 1000 OR BoolValue = TRUE)
            """);
    }

    [Fact]
    public async Task Where_BoolColumnPredicate_RendersEqualsTrue()
    {
        var resultItems = await Db
            .SimpleItems.Where(item => item.BoolValue)
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task Where_BoolColumnPredicate_WithAnd_RendersEqualsTrue()
    {
        var resultItems = await Db
            .SimpleItems.Where(item => item.Pk == "ITEM#1" && item.BoolValue)
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.Pk == "ITEM#1" && item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#1' AND BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task Where_BoolColumn_EqualsCapturedBoolParameter_RendersColumnEqualsParameter()
    {
        var enabled = true;
        var resultItems = await Db
            .SimpleItems.Where(item => item.BoolValue == enabled)
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.BoolValue == enabled);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE BoolValue = ?
            """);
    }

    [Fact]
    public async Task OrderBy_ThenBy_WithOrPredicate_ReturnsItemsInAscendingOrder()
    {
        // DynamoDB PartiQL requires a hash-key condition when using ORDER BY.
        // We keep ORDER BY strictly on the primary key, but make the predicate non-trivial.
        var resultItems = await Db
            .SimpleItems
            .Where(item => item.Pk == "ITEM#3" || item.Pk == "ITEM#1" || item.Pk == "ITEM#4")
            .OrderBy(item => item.Pk)
            .ThenBy(item => item.Pk)
            .ToListAsync(CancellationToken);

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
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#3' OR Pk = 'ITEM#1' OR Pk = 'ITEM#4'
            ORDER BY Pk ASC, Pk ASC
            """);
    }

    [Fact]
    public async Task
        OrderByDescending_ThenByDescending_WithAndOrPredicate_ReturnsItemsInExpectedOrder()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item
                => (item.Pk == "ITEM#1" || item.Pk == "ITEM#2" || item.Pk == "ITEM#3")
                && (item.IntValue >= 100 || item.BoolValue == false))
            .OrderByDescending(item => item.Pk)
            .ThenByDescending(item => item.Pk)
            .ToListAsync(CancellationToken);

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
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE (Pk = 'ITEM#1' OR Pk = 'ITEM#2' OR Pk = 'ITEM#3') AND (IntValue >= 100 OR BoolValue = FALSE)
            ORDER BY Pk DESC, Pk DESC
            """);
    }

    [Fact]
    public async Task Where_WithCapturedVariables_InlinesParametersCorrectly()
    {
        // Use captured variables to test parameter handling
        var minInt = 100;
        var maxLong = 1000L;
        var excludeString = "delta";

        var resultItems =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue >= minInt
                    && item.LongValue <= maxLong
                    && item.StringValue != excludeString)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 100 && item.LongValue <= 1000 && item.StringValue != "delta");

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue >= ? AND LongValue <= ? AND StringValue <> ?
            """);
    }

    [Fact]
    public async Task Where_MethodCall_StillThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .SimpleItems.Where(item => item.StringValue.ToUpper() == "ALPHA")
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Where_CustomMethodCall_StillThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .SimpleItems.Where(item => NormalizeString(item.StringValue) == "ALPHA")
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Where_DelegateInvocation_StillThrowsInvalidOperationException()
    {
        var normalize = NormalizeString;

        var act = async ()
            => await Db
                .SimpleItems.Where(item => normalize(item.StringValue) == "ALPHA")
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static string NormalizeString(string value) => value.Trim().ToUpperInvariant();
}
