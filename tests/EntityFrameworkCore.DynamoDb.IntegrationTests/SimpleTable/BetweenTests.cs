using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>
///     Verifies that inclusive range predicates are translated to PartiQL BETWEEN expressions,
///     and that mixed (exclusive) bounds fall back to separate comparison operators.
/// </summary>
public class BetweenTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    // ------------------------------------------------------------------
    // Inclusive range → BETWEEN translation
    // ------------------------------------------------------------------

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_IntBetween_BothBoundsInclusive_TranslatesToBetween()
    {
        var low = 100;
        var high = 200000;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.IntValue >= low && item.IntValue <= high)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => item.IntValue >= low && item.IntValue <= high);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" BETWEEN ? AND ?
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_IntBetween_NoMatchingItems_ReturnsEmpty()
    {
        var low = 300000;
        var high = 900000;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.IntValue >= low && item.IntValue <= high)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => item.IntValue >= low && item.IntValue <= high).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" BETWEEN ? AND ?
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_LongBetween_BothBoundsInclusive_TranslatesToBetween()
    {
        var low = 1000L;
        var high = 9_000_000_000L;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.LongValue >= low && item.LongValue <= high)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => item.LongValue >= low && item.LongValue <= high);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "LongValue" BETWEEN ? AND ?
            """);
    }

    // ------------------------------------------------------------------
    // Mixed / exclusive bounds → no BETWEEN rewrite
    // ------------------------------------------------------------------

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_IntRange_ExclusiveLowerBound_DoesNotUseBetween()
    {
        var low = 100;
        var high = 200000;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.IntValue > low && item.IntValue <= high)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => item.IntValue > low && item.IntValue <= high);

        resultItems.Should().BeEquivalentTo(expected);

        // > and <= must NOT be collapsed into BETWEEN
        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" > ? AND "IntValue" <= ?
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_IntRange_ExclusiveUpperBound_DoesNotUseBetween()
    {
        var low = 100;
        var high = 987654;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.IntValue >= low && item.IntValue < high)
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => item.IntValue >= low && item.IntValue < high);

        resultItems.Should().BeEquivalentTo(expected);

        // >= and < must NOT be collapsed into BETWEEN
        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" >= ? AND "IntValue" < ?
            """);
    }

    // ------------------------------------------------------------------
    // BETWEEN composed with other predicates
    // ------------------------------------------------------------------

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_BetweenComposedWithOtherPredicate_UsesBetweenAndAddsClause()
    {
        var low = 100;
        var high = 987654;

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.IntValue >= low && item.IntValue <= high && item.BoolValue == true)
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= low && item.IntValue <= high && item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" BETWEEN ? AND ? AND "BoolValue" = TRUE
            """);
    }
}
