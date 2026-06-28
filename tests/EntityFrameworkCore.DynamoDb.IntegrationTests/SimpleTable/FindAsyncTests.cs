using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for <c>FindAsync</c> on a partition-key-only table.</summary>
public class FindAsyncTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    private TestPartiQlLoggerFactory LoggerFactory => SqlCapture;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_PkOnly_ReturnsMatchingItem_AndSetsLimit1()
    {
        LoggerFactory.Clear();

        var result = await Db.SimpleItems.FindAsync(["ITEM#1"], CancellationToken);

        result.Should().BeEquivalentTo(SimpleItems.Items.Single(item => item.Pk == "ITEM#1"));

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);

        AssertSql(
            """
            SELECT "pk", "$type", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableDateTimeOffsetValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "pk" = ?
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_PkOnly_ReturnsNullWhenMissing()
    {
        LoggerFactory.Clear();

        var result = await Db.SimpleItems.FindAsync(["ITEM#missing"], CancellationToken);

        result.Should().BeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);
    }
}
