using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class SelectTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact]
    public async Task Select_AnonymousObjectProjection()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.IntValue })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { item.Pk, item.IntValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_AnonymousObjectProjection_WithAlias()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { Foo = item.StringValue })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { Foo = item.StringValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "stringValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_NestedAnonymousObjectProjection_NonCollection()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.IntValue, Sub = new { item.Pk, item.BoolValue } })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.IntValue, Sub = new { item.Pk, item.BoolValue } })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "intValue", "pk", "boolValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_NestedClassProjection_NonCollection()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item
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
                .Items
                .Select(item
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
            SELECT "intValue", "pk", "boolValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_DtoProjection()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new SimpleItemDto { Pk = item.Pk, IntValue = item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
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
            SELECT "pk"
            FROM "SimpleItems"
            """);
    }

    // Combined Operations Tests

    [Fact]
    public async Task Select_WithWhere()
    {
        var results = await Db
            .SimpleItems
            .Where(item => item.IntValue > 100)
            .Select(item => new { item.Pk, item.IntValue })
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Where(item => item.IntValue > 100)
                .Select(item => new { item.Pk, item.IntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            WHERE "intValue" > 100
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
                .SimpleItems
                .Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderBy(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "stringValue"
            FROM "SimpleItems"
            WHERE "pk" = 'ITEM#1' OR "pk" = 'ITEM#2'
            ORDER BY "pk" ASC
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
                .SimpleItems
                .Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderByDescending(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Where(item => item.Pk == "ITEM#1" || item.Pk == "ITEM#2")
                .OrderByDescending(item => item.Pk)
                .Select(item => new { item.Pk, item.StringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "stringValue"
            FROM "SimpleItems"
            WHERE "pk" = 'ITEM#1' OR "pk" = 'ITEM#2'
            ORDER BY "pk" DESC
            """);
    }

    // Different Data Types Tests

    [Fact]
    public async Task Select_NumericTypes()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item
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
                .Items
                .Select(item
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
            SELECT "intValue", "longValue", "floatValue", "doubleValue", "decimalValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_BooleanAndTemporal()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.BoolValue, item.DateTimeOffsetValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.Pk, item.BoolValue, item.DateTimeOffsetValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateTimeOffsetValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_GuidType()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.GuidValue })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { item.Pk, item.GuidValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "guidValue"
            FROM "SimpleItems"
            """);
    }

    // Nullable Properties Test

    [Fact]
    public async Task Select_NullableProperty()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.NullableStringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, item.NullableStringValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "nullableStringValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_NullablePrimitiveProperties()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.NullableBoolValue, item.NullableIntValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.Pk, item.NullableBoolValue, item.NullableIntValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "nullableBoolValue", "nullableIntValue"
            FROM "SimpleItems"
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            """);
    }

    // Multiple Properties of Same Type Test

    [Fact]
    public async Task Select_MultipleStrings()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.StringValue, item.NullableStringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.Pk, item.StringValue, item.NullableStringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "stringValue", "nullableStringValue"
            FROM "SimpleItems"
            """);
    }

    // All Properties Test

    [Fact]
    public async Task Select_AllPropertyTypes()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new
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
                    item.NullableBoolValue,
                    item.NullableIntValue,
                    item.NullableStringValue,
                    item.DateOnlyValue,
                    item.TimeOnlyValue,
                    item.TimeSpanValue,
                })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new
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
                    item.NullableBoolValue,
                    item.NullableIntValue,
                    item.NullableStringValue,
                    item.DateOnlyValue,
                    item.TimeOnlyValue,
                    item.TimeSpanValue,
                })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "intValue", "longValue", "floatValue", "doubleValue", "decimalValue", "stringValue", "guidValue", "dateTimeOffsetValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "dateOnlyValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_DateOnlyType_RoundTrips()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.DateOnlyValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, item.DateOnlyValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "dateOnlyValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_TimeOnlyType_RoundTrips()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.TimeOnlyValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, item.TimeOnlyValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "timeOnlyValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_TimeSpanType_RoundTrips()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, item.TimeSpanValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, item.TimeSpanValue }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "timeSpanValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Where_DateOnlyParameter_FiltersCorrectly()
    {
        var cutoff = new DateOnly(2026, 1, 1);

        var results =
            await Db
                .SimpleItems
                .Where(item => item.DateOnlyValue == cutoff)
                .Select(item => new { item.Pk, item.DateOnlyValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Where(item => item.DateOnlyValue == cutoff)
                .Select(item => new { item.Pk, item.DateOnlyValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "dateOnlyValue"
            FROM "SimpleItems"
            WHERE "dateOnlyValue" = ?
            """);
    }

    [Fact]
    public async Task Where_TimeSpanParameter_FiltersCorrectly()
    {
        var cutoff = TimeSpan.FromHours(1);

        var results =
            await Db
                .SimpleItems
                .Where(item => item.TimeSpanValue == cutoff)
                .Select(item => new { item.Pk, item.TimeSpanValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Where(item => item.TimeSpanValue == cutoff)
                .Select(item => new { item.Pk, item.TimeSpanValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "timeSpanValue" = ?
            """);
    }

    // Error Cases Tests

    [Fact]
    public async Task Select_ComputedExpression_ProjectsAttributeOnly()
    {
        var results = await Db
            .SimpleItems
            .Select(item => new { item.Pk, Doubled = item.IntValue * 2 })
            .ToListAsync(CancellationToken);

        var expected =
            SimpleItems.Items.Select(item => new { item.Pk, Doubled = item.IntValue * 2 }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_MethodCall_ProjectsAttributeOnly()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, Upper = item.StringValue.ToUpper() })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.Pk, Upper = item.StringValue.ToUpper() })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "stringValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_MethodCallChain_NullReceiver_ThrowsNullReferenceException()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Select(item
                    => new
                    {
                        item.Pk,
                        Normalized = item.NullableStringValue!.Trim().ToUpperInvariant(),
                    })
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Select_MethodCall_NullReceiver_ThrowsNullReferenceException()
    {
        var act = async ()
            => await Db
                .SimpleItems
                .Select(item => item.NullableStringValue!.Length)
                .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task Select_StringConcat_ProjectsAttributeOnly()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { item.Pk, Combined = item.Pk + ":" + item.StringValue })
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new { item.Pk, Combined = item.Pk + ":" + item.StringValue })
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "stringValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_DtoProjection_WithComputedArgument()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new ConstructorItemDto(item.Pk, item.IntValue * 2))
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new ConstructorItemDto(item.Pk, item.IntValue * 2))
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_CapturedVariable_ComputedProjection()
    {
        var factor = 2;
        var results =
            await Db
                .SimpleItems
                .Select(item => item.IntValue * factor)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.IntValue * factor).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "intValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_ProjectionDeduplicatesLeafs()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new { A = item.Pk, B = item.Pk })
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => new { A = item.Pk, B = item.Pk }).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_ConstructorDtoProjection()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new ConstructorItemDto(item.Pk, item.IntValue))
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new ConstructorItemDto(item.Pk, item.IntValue))
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_ConstructorDtoProjection_WithNamedArguments()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => new ConstructorItemDto(pk: item.Pk, intValue: item.IntValue))
                .ToListAsync(CancellationToken);

        var expected =
            SimpleItems
                .Items
                .Select(item => new ConstructorItemDto(item.Pk, item.IntValue))
                .ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "intValue"
            FROM "SimpleItems"
            """);
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
            SELECT "intValue"
            FROM "SimpleItems"
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
            SELECT "boolValue"
            FROM "SimpleItems"
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
            SELECT "guidValue"
            FROM "SimpleItems"
            """);
    }

    [Fact]
    public async Task Select_ScalarDateTime()
    {
        var results =
            await Db
                .SimpleItems
                .Select(item => item.DateTimeOffsetValue)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Select(item => item.DateTimeOffsetValue).ToList();

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "dateTimeOffsetValue"
            FROM "SimpleItems"
            """);
    }

    private sealed class SimpleItemDto
    {
        public int IntValue { get; set; }

        public required string Pk { get; set; }
    }

    private sealed class NestedProjection
    {
        public int IntValue { get; set; }

        public required NestedSubProjection Sub { get; set; }
    }

    private sealed class NestedSubProjection
    {
        public bool BoolValue { get; set; }

        public required string Pk { get; set; }
    }

    private sealed class ConstructorItemDto
    {
        public ConstructorItemDto(string pk, int intValue)
        {
            Pk = pk + 1;
            IntValue = intValue + 1;
        }

        public int IntValue { get; }

        public string Pk { get; }
    }
}
