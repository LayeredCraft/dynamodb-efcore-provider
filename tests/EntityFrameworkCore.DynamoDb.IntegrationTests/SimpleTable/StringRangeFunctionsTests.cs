using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Verifies DynamoDB string range EF.Functions helpers.</summary>
public class StringRangeFunctionsTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_FunctionsGreaterThan_ReturnsLexicographicStringRange()
    {
        var bound = "bravo";

        var resultItems = await Db
            .SimpleItems
            .Where(item => EF.Functions.GreaterThan(item.StringValue, bound))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => string.Compare(item.StringValue, bound, StringComparison.Ordinal) > 0);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableDateTimeOffsetValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "stringValue" > ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_FunctionsLessThan_ReturnsLexicographicStringRange()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => EF.Functions.LessThan(item.StringValue, "delta"))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => string.Compare(item.StringValue, "delta", StringComparison.Ordinal) < 0);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableDateTimeOffsetValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "stringValue" < 'delta'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_FunctionsBetween_ReturnsInclusiveLexicographicStringRange()
    {
        var low = "bravo";
        var high = "delta";

        var resultItems = await Db
            .SimpleItems
            .Where(item => EF.Functions.Between(item.StringValue, low, high))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => string.Compare(item.StringValue, low, StringComparison.Ordinal) >= 0
            && string.Compare(item.StringValue, high, StringComparison.Ordinal) <= 0);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableDateTimeOffsetValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "stringValue" BETWEEN ? AND ?
            """);
    }
}
