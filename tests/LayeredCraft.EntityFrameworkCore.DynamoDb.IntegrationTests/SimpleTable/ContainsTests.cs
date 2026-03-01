using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class ContainsTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task Where_StringContains_WithCapturedParameter_TranslatesToPartiQlContains()
    {
        var term = "ha";

        var resultItems = await Db
            .SimpleItems
            .Where(item => item.StringValue.Contains(term))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => item.StringValue.Contains(term));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE contains(StringValue, ?)
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_OnPartitionKey_TranslatesToInPredicate()
    {
        var keys = new[] { "ITEM#1", "ITEM#3" };

        var resultItems = await Db
            .SimpleItems
            .Where(item => keys.Contains(item.Pk))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => keys.Contains(item.Pk));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE Pk IN [?, ?]
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_OnNonKeyProperty_TranslatesToInPredicate()
    {
        var values = new[] { "alpha", "delta" };

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.StringValue))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => values.Contains(item.StringValue));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE StringValue IN [?, ?]
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_OnIntProperty_WithArray_TranslatesToInPredicate()
    {
        var values = new[] { 100, -100 };

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.IntValue))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => values.Contains(item.IntValue));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue IN [?, ?]
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_OnIntProperty_WithList_TranslatesToInPredicate()
    {
        IReadOnlyList<int> values = new List<int> { 100, 987654 };

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.IntValue))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => values.Contains(item.IntValue));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE IntValue IN [?, ?]
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_WithEmptyCollection_RendersFalsePredicate()
    {
        var keys = Array.Empty<string>();

        var resultItems = await Db
            .SimpleItems
            .Where(item => keys.Contains(item.Pk))
            .ToListAsync(CancellationToken);

        resultItems.Should().BeEmpty();

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE 1 = 0
            """);
    }

    /// <summary>Verifies inline Array.Empty values translate to an always-false predicate.</summary>
    [Fact]
    public async Task Where_CollectionContains_WithInlineArrayEmpty_RendersFalsePredicate()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => Array.Empty<string>().Contains(item.Pk))
            .ToListAsync(CancellationToken);

        resultItems.Should().BeEmpty();

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE 1 = 0
            """);
    }

    /// <summary>Verifies inline Enumerable.Empty values translate to an always-false predicate.</summary>
    [Fact]
    public async Task Where_CollectionContains_WithInlineEnumerableEmpty_RendersFalsePredicate()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => Enumerable.Empty<string>().Contains(item.Pk))
            .ToListAsync(CancellationToken);

        resultItems.Should().BeEmpty();

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE 1 = 0
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_WithNullElement_ExecutesWithInPredicate()
    {
        string?[] values = [null, "Null String"];

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.NullableStringValue))
            .ToListAsync(CancellationToken);

        resultItems.Should().Contain(item => item.Pk == "ITEM#2");

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE NullableStringValue IN [?, ?]
            """);
    }

    [Fact]
    public async Task
        Where_CollectionContains_OnNullableIntProperty_WithNullElement_ExecutesWithInPredicate()
    {
        int?[] values = [null, 42];

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.NullableIntValue))
            .ToListAsync(CancellationToken);

        resultItems.Should().Contain(item => item.Pk == "ITEM#2");

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableBoolValue, NullableIntValue, NullableStringValue, StringValue
            FROM SimpleItems
            WHERE NullableIntValue IN [?, ?]
            """);
    }

    [Fact]
    public async Task Where_CollectionContains_OnPartitionKey_ThrowsWhenListExceedsLimit()
    {
        var keys = Enumerable.Range(1, 51).Select(i => $"ITEM#{i}").ToArray();

        var act = async ()
            => await Db
                .SimpleItems
                .Where(item => keys.Contains(item.Pk))
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IN limit of 50 values for partition key comparisons*");
    }

    [Fact]
    public async Task Where_CollectionContains_OnNonKeyProperty_ThrowsWhenListExceedsLimit()
    {
        var values = Enumerable.Range(1, 101).Select(i => $"value-{i}").ToArray();

        var act = async ()
            => await Db
                .SimpleItems
                .Where(item => values.Contains(item.StringValue))
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IN limit of 100 values for non-key comparisons*");
    }

    [Fact]
    public async Task Where_StringContains_WithStringComparisonOverload_StillThrows()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Where(item => item.StringValue.Contains("a", StringComparison.Ordinal))
                .Select(item => item.Pk)
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only string.Contains(string) is supported*");
    }

    [Fact]
    public async Task QueryableContains_StillThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Select(item => item.Pk)
                .ContainsAsync("ITEM#1", CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*LINQ operator 'Contains'*not currently supported*");
    }
}
