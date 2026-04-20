using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class StartsWithTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact]
    public async Task Where_StringStartsWith_WithCapturedParameter_TranslatesToPartiQlBeginsWith()
    {
        var prefix = "al";

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.StringValue.StartsWith(prefix))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.StringValue.StartsWith(prefix));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE begins_with("stringValue", ?)
            """);
    }

    [Fact]
    public async Task Where_StringStartsWith_WithInlineLiteral_TranslatesToPartiQlBeginsWith()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => item.StringValue.StartsWith("ch"))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.StringValue.StartsWith("ch"));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE begins_with("stringValue", 'ch')
            """);
    }

    [Fact]
    public async Task Where_StringStartsWith_WithStringComparisonOverload_StillThrows()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Where(item => item.StringValue.StartsWith("a", StringComparison.Ordinal))
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only string.StartsWith(string)*");
    }
}
