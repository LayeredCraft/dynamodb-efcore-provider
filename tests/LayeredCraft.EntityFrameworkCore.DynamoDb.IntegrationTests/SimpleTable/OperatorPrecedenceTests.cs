using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>
/// Tests that validate operator precedence and order of operations in generated PartiQL.
/// These tests ensure that parentheses optimization doesn't break query semantics.
/// </summary>
public class OperatorPrecedenceTests(SimpleTableDynamoFixture fixture)
    : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task AndHasHigherPrecedenceThanOr()
    {
        // Test: a OR b AND c should be evaluated as a OR (b AND c)
        // NOT as (a OR b) AND c
        var results =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue == 100 || (item.IntValue == 200 && item.BoolValue == true))
                .ToListAsync(CancellationToken);

        // Expected: IntValue=100 OR (IntValue=200 AND BoolValue=TRUE)
        // This should match items with IntValue=100 regardless of BoolValue
        // AND items with IntValue=200 only if BoolValue=true
        var expected = SimpleItems.Items.Where(item
            => item.IntValue == 100 || (item.IntValue == 200 && item.BoolValue));

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue = 100 OR IntValue = 200 AND BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task ExplicitParenthesesOverridePrecedence()
    {
        // Test: (a OR b) AND c should keep explicit parentheses
        var results =
            await Db
                .SimpleItems.Where(item
                    => (item.IntValue == 100 || item.IntValue == 200) && item.BoolValue == true)
                .ToListAsync(CancellationToken);

        // Expected: Items with IntValue in (100, 200) AND BoolValue=true
        var expected = SimpleItems.Items.Where(item
            => (item.IntValue == 100 || item.IntValue == 200) && item.BoolValue);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE (IntValue = 100 OR IntValue = 200) AND BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task ComparisonHasHigherPrecedenceThanLogical()
    {
        // Test: a > 5 AND b < 10 should not add extra parentheses around comparisons
        var results =
            await Db
                .SimpleItems.Where(item => item.IntValue > 100 && item.LongValue < 500)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.IntValue > 100 && item.LongValue < 500);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue > 100 AND LongValue < 500
            """);
    }

    [Fact]
    public async Task AssociativeAndChain()
    {
        // Test: a AND b AND c AND d should not add any parentheses
        var results =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue > 0
                       && item.LongValue > 0
                       && item.DoubleValue > 0
                       && item.BoolValue == true)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue > 0 && item.LongValue > 0 && item.DoubleValue > 0 && item.BoolValue);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue > 0 AND LongValue > 0 AND DoubleValue > 0 AND BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task AssociativeOrChain()
    {
        // Test: a OR b OR c OR d should not add any parentheses
        var results =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue == 100
                       || item.IntValue == 200
                       || item.IntValue == 300
                       || item.IntValue == 400)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue == 100
               || item.IntValue == 200
               || item.IntValue == 300
               || item.IntValue == 400);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue = 100 OR IntValue = 200 OR IntValue = 300 OR IntValue = 400
            """);
    }

    [Fact]
    public async Task ComplexMixedPrecedence()
    {
        // Test: (a OR b) AND (c OR d) AND e
        // Validates: OR inside AND needs parentheses, multiple AND is associative
        var results =
            await Db
                .SimpleItems.Where(item
                    => (item.IntValue == 100 || item.IntValue == 200)
                       && (item.LongValue < 500 || item.LongValue > 1000)
                       && item.BoolValue == true)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => (item.IntValue == 100 || item.IntValue == 200)
               && (item.LongValue < 500 || item.LongValue > 1000)
               && item.BoolValue);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE (IntValue = 100 OR IntValue = 200) AND (LongValue < 500 OR LongValue > 1000) AND BoolValue = TRUE
            """);
    }

    [Fact]
    public async Task NestedOrInsideAndPreservesSemantics()
    {
        // Critical test: Validates that AND containing OR keeps OR parentheses
        // and produces the same results as the current implementation
        var results =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue > 100
                       && item.StringValue != "test"
                       && (item.BoolValue == true || item.DoubleValue < 0)
                       && item.LongValue < 1000)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue > 100
               && item.StringValue != "test"
               && (item.BoolValue || item.DoubleValue < 0)
               && item.LongValue < 1000);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue > 100 AND StringValue <> 'test' AND (BoolValue = TRUE OR DoubleValue < 0) AND LongValue < 1000
            """);
    }

    [Fact]
    public async Task AllComparisonOperatorsHaveSamePrecedence()
    {
        // Test: Multiple comparison operators should not need parentheses
        // when combined with same-precedence logical operators
        var results =
            await Db
                .SimpleItems.Where(item
                    => item.IntValue >= 100
                       && item.IntValue <= 500
                       && item.LongValue != 0
                       && item.DoubleValue < 100)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 100
               && item.IntValue <= 500
               && item.LongValue != 0
               && item.DoubleValue < 100);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue >= 100 AND IntValue <= 500 AND LongValue <> 0 AND DoubleValue < 100
            """);
    }
}
