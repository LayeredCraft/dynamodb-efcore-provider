using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class SelectTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task Select_AnonymousObjectProjection()
    {
        var results =
            await Db
                .SimpleItems.Select(item => new { item.Pk, item.IntValue })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { item.Pk, item.IntValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_NestedAnonymousObjectProjection_NonCollection()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new { item.IntValue, Sub = new { item.Pk, item.BoolValue } })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item => new { item.IntValue, Sub = new { item.Pk, item.BoolValue } })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT IntValue, Pk, BoolValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_NestedClassProjection_NonCollection()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new NestedProjection
                    {
                        IntValue = item.IntValue,
                        Sub = new NestedSubProjection
                        {
                            Pk = item.Pk, BoolValue = item.BoolValue,
                        },
                    })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item
                    => new NestedProjection
                    {
                        IntValue = item.IntValue,
                        Sub = new NestedSubProjection
                        {
                            Pk = item.Pk, BoolValue = item.BoolValue,
                        },
                    })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT IntValue, Pk, BoolValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_DtoProjection()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_ScalarProjection()
    {
        var results = await Db.SimpleItems.Select(item => item.Pk).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.Pk).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk
            FROM SimpleItems
            """);
    }

    // Combined Operations Tests
    [Fact]
    public async Task Select_WithWhere()
    {
        var results = await Db
            .SimpleItems.Where(item => item.IntValue > 100)
            .Select(item => new { item.Pk, item.IntValue })
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.IntValue > 100)
                .Select(item => new { item.Pk, item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, IntValue
            FROM SimpleItems
            WHERE IntValue > 100
            """);
    }

    [Fact]
    public async Task Select_WithWhereAndOrderBy()
    {
        // Note: DynamoDB PartiQL requires:
        // 1. Partition key (Pk) filter in WHERE when using ORDER BY
        // 2. ORDER BY column must be part of primary key
        var results =
            await Db
                .SimpleItems.Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#1' OR Pk = 'ITEM#2'
            ORDER BY Pk ASC
            """);
    }

    [Fact]
    public async Task Select_WithWhereAndOrderByDescending()
    {
        // Note: DynamoDB PartiQL requires:
        // 1. Partition key (Pk) filter in WHERE when using ORDER BY
        // 2. ORDER BY column must be part of primary key
        var results =
            await Db
                .SimpleItems.Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderByDescending(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderByDescending(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, StringValue
            FROM SimpleItems
            WHERE Pk = 'ITEM#1' OR Pk = 'ITEM#2'
            ORDER BY Pk DESC
            """);
    }

    // Different Data Types Tests
    [Fact]
    public async Task Select_NumericTypes()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new
                    {
                        item.IntValue,
                        item.LongValue,
                        item.FloatValue,
                        item.DoubleValue,
                        item.DecimalValue,
                    })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item
                    => new
                    {
                        item.IntValue,
                        item.LongValue,
                        item.FloatValue,
                        item.DoubleValue,
                        item.DecimalValue,
                    })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT IntValue, LongValue, FloatValue, DoubleValue, DecimalValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_BooleanAndTemporal()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new { item.Pk, item.BoolValue, item.DateTimeOffsetValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item => new { item.Pk, item.BoolValue, item.DateTimeOffsetValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_GuidType()
    {
        var results =
            await Db
                .SimpleItems.Select(item => new { item.Pk, item.GuidValue })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { item.Pk, item.GuidValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, GuidValue
            FROM SimpleItems
            """);
    }

    // Nullable Properties Test
    [Fact]
    public async Task Select_NullableProperty()
    {
        var results =
            await Db
                .SimpleItems.Select(item => new { item.Pk, item.NullableStringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, item.NullableStringValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, NullableStringValue
            FROM SimpleItems
            """);
    }

    // Identity Projection Test
    [Fact]
    public async Task Select_IdentityProjection()
    {
        var results = await Db.SimpleItems.Select(item => item).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, DateTimeOffsetValue, DecimalValue, DoubleValue, FloatValue, GuidValue, IntValue, LongValue, NullableStringValue, StringValue
            FROM SimpleItems
            """);
    }

    // Multiple Properties of Same Type Test
    [Fact]
    public async Task Select_MultipleStrings()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new { item.Pk, item.StringValue, item.NullableStringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item => new { item.Pk, item.StringValue, item.NullableStringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, StringValue, NullableStringValue
            FROM SimpleItems
            """);
    }

    // All Properties Test
    [Fact]
    public async Task Select_AllPropertyTypes()
    {
        var results =
            await Db
                .SimpleItems.Select(item
                    => new
                    {
                        item.Pk,
                        item.BoolValue,
                        item.IntValue,
                        item.LongValue,
                        item.FloatValue,
                        item.DoubleValue,
                        item.DecimalValue,
                        item.StringValue,
                        item.GuidValue,
                        item.DateTimeOffsetValue,
                        item.NullableStringValue,
                    })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items.Select(item => new
                {
                    item.Pk,
                    item.BoolValue,
                    item.IntValue,
                    item.LongValue,
                    item.FloatValue,
                    item.DoubleValue,
                    item.DecimalValue,
                    item.StringValue,
                    item.GuidValue,
                    item.DateTimeOffsetValue,
                    item.NullableStringValue,
                })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT Pk, BoolValue, IntValue, LongValue, FloatValue, DoubleValue, DecimalValue, StringValue, GuidValue, DateTimeOffsetValue, NullableStringValue
            FROM SimpleItems
            """);
    }

    // Error Cases Tests
    [Fact]
    public async Task Select_ComputedExpression_ThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .SimpleItems.Select(item => new { item.Pk, Doubled = item.IntValue * 2 })
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*DynamoDB PartiQL does not support computed expressions*");
    }

    [Fact]
    public async Task Select_MethodCall_ThrowsInvalidOperationException()
    {
        var act = async ()
            => await Db
                .SimpleItems.Select(item => new { item.Pk, Upper = item.StringValue.ToUpper() })
                .ToListAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*DynamoDB PartiQL does not support*method call*");
    }

    // Scalar Variations Tests
    [Fact]
    public async Task Select_ScalarInt()
    {
        var results =
            await Db.SimpleItems.Select(item => item.IntValue).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.IntValue).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT IntValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_ScalarBool()
    {
        var results =
            await Db.SimpleItems.Select(item => item.BoolValue).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.BoolValue).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT BoolValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_ScalarGuid()
    {
        var results =
            await Db.SimpleItems.Select(item => item.GuidValue).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.GuidValue).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT GuidValue
            FROM SimpleItems
            """);
    }

    [Fact]
    public async Task Select_ScalarDateTime()
    {
        var results =
            await Db
                .SimpleItems.Select(item => item.DateTimeOffsetValue)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.DateTimeOffsetValue).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT DateTimeOffsetValue
            FROM SimpleItems
            """);
    }

    private sealed class SimpleItemDto
    {
        public required string Pk { get; set; }

        public int IntValue { get; set; }
    }

    private sealed class NestedProjection
    {
        public int IntValue { get; set; }

        public required NestedSubProjection Sub { get; set; }
    }

    private sealed class NestedSubProjection
    {
        public required string Pk { get; set; }

        public bool BoolValue { get; set; }
    }
}
