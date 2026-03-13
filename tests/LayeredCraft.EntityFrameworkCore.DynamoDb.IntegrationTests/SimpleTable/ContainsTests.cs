using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Represents the ContainsTests type.</summary>
public class ContainsTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE contains("StringValue", ?)
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "Pk" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "StringValue" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "IntValue" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_CollectionContains_WithEmptyCollection_RendersFalsePredicate()
    {
        var keys = Array.Empty<string>();

        var resultItems = await Db
            .SimpleItems
            .Where(item => keys.Contains(item.Pk))
            .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => keys.Contains(item.Pk)).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE 1 = 0
            """);
    }

    /// <summary>Verifies inline Array.Empty values translate to an always-false predicate.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_CollectionContains_WithInlineArrayEmpty_RendersFalsePredicate()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => Array.Empty<string>().Contains(item.Pk))
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => Array.Empty<string>().Contains(item.Pk)).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE 1 = 0
            """);
    }

    /// <summary>Verifies inline Enumerable.Empty values translate to an always-false predicate.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_CollectionContains_WithInlineEnumerableEmpty_RendersFalsePredicate()
    {
        var resultItems = await Db
            .SimpleItems
            .Where(item => Enumerable.Empty<string>().Contains(item.Pk))
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => Enumerable.Empty<string>().Contains(item.Pk)).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE 1 = 0
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_CollectionContains_WithNullElement_ExecutesWithInPredicate()
    {
        string?[] values = [null, "Null String"];

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.NullableStringValue))
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => values.Contains(item.NullableStringValue)).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task
        Where_CollectionContains_OnNullableIntProperty_WithNullElement_ExecutesWithInPredicate()
    {
        int?[] values = [null, 42];

        var resultItems = await Db
            .SimpleItems
            .Where(item => values.Contains(item.NullableIntValue))
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Where(item => values.Contains(item.NullableIntValue)).ToList();

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableIntValue" IN [?, ?]
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_InlineCollectionContains_OnPartitionKey_ThrowsWhenListExceedsLimit()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Where(item => new[]
                {
                    "ITEM#1",
                    "ITEM#2",
                    "ITEM#3",
                    "ITEM#4",
                    "ITEM#5",
                    "ITEM#6",
                    "ITEM#7",
                    "ITEM#8",
                    "ITEM#9",
                    "ITEM#10",
                    "ITEM#11",
                    "ITEM#12",
                    "ITEM#13",
                    "ITEM#14",
                    "ITEM#15",
                    "ITEM#16",
                    "ITEM#17",
                    "ITEM#18",
                    "ITEM#19",
                    "ITEM#20",
                    "ITEM#21",
                    "ITEM#22",
                    "ITEM#23",
                    "ITEM#24",
                    "ITEM#25",
                    "ITEM#26",
                    "ITEM#27",
                    "ITEM#28",
                    "ITEM#29",
                    "ITEM#30",
                    "ITEM#31",
                    "ITEM#32",
                    "ITEM#33",
                    "ITEM#34",
                    "ITEM#35",
                    "ITEM#36",
                    "ITEM#37",
                    "ITEM#38",
                    "ITEM#39",
                    "ITEM#40",
                    "ITEM#41",
                    "ITEM#42",
                    "ITEM#43",
                    "ITEM#44",
                    "ITEM#45",
                    "ITEM#46",
                    "ITEM#47",
                    "ITEM#48",
                    "ITEM#49",
                    "ITEM#50",
                    "ITEM#51",
                }.Contains(item.Pk))
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IN limit of 50 values for partition key comparisons*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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
