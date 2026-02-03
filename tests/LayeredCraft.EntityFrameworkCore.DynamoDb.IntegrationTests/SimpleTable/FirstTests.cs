using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class FirstTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task FirstAsync_WithPredicate_ReturnsMatchingItem()
    {
        var result =
            await Db.SimpleItems.FirstAsync(item => item.Pk == "ITEM#2", CancellationToken);

        result.Pk.Should().Be("ITEM#2");

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#2'
            """);
    }

    [Fact]
    public async Task FirstAsync_WithPredicate_ThrowsWhenNoMatch()
    {
        var act = async ()
            => await Db.SimpleItems.FirstAsync(item => item.Pk == "MISSING", CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'MISSING'
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithPredicate_ReturnsNullWhenNoMatch()
    {
        var result =
            await Db.SimpleItems.FirstOrDefaultAsync(
                item => item.Pk == "MISSING",
                CancellationToken);

        result.Should().BeNull();

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'MISSING'
            """);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithPredicate_ReturnsMatchingItem()
    {
        var result =
            await Db.SimpleItems.FirstOrDefaultAsync(
                item => item.Pk == "ITEM#3",
                CancellationToken);

        result.Should().NotBeNull();
        result!.Pk.Should().Be("ITEM#3");

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#3'
            """);
    }

    [Fact]
    public async Task FirstAsync_DoesNotEmitLimitClause()
    {
        var result =
            await Db.SimpleItems.FirstAsync(item => item.Pk == "ITEM#1", CancellationToken);

        result.Pk.Should().Be("ITEM#1");

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#1'
            """);
    }
}
