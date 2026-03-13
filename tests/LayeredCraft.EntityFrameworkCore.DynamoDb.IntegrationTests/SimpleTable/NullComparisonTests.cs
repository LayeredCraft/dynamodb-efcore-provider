using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>
///     Verifies that null comparisons in LINQ predicates are translated to the correct IS NULL /
///     IS MISSING PartiQL predicates.
/// </summary>
public class NullComparisonTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_EqualNull_TranslatesToIsNullOrIsMissing()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => x.NullableStringValue == null)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(x => x.NullableStringValue == null);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS NULL OR "NullableStringValue" IS MISSING
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_NotEqualNull_TranslatesToIsNotNullAndIsNotMissing()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => x.NullableStringValue != null)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(x => x.NullableStringValue != null);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS NOT NULL AND "NullableStringValue" IS NOT MISSING
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_EqualNull_ComposedWithAnd_AddsParentheses()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => x.NullableStringValue == null && x.BoolValue)
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(x => x.NullableStringValue == null && x.BoolValue);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE ("NullableStringValue" IS NULL OR "NullableStringValue" IS MISSING) AND "BoolValue" = TRUE
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_NotOnEqualNull_WrapsWithNot()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => !(x.NullableStringValue == null))
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(x => !(x.NullableStringValue == null));

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE NOT ("NullableStringValue" IS NULL OR "NullableStringValue" IS MISSING)
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_Functions_IsNull_TranslatesToIsNull()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => EF.Functions.IsNull(x.NullableStringValue))
                .ToListAsync(CancellationToken);

        // All seeded items with NullableStringValue == null are stored as NULL type
        // (OmitNullStrings = false)
        var expected = SimpleItems.Items.Where(x => x.NullableStringValue == null);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS NULL
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_Functions_IsNotNull_TranslatesToIsNotNull()
    {
        var results =
            await Db
                .SimpleItems
                .Where(x => EF.Functions.IsNotNull(x.NullableStringValue))
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(x => x.NullableStringValue != null);

        results.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS NOT NULL
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_Functions_IsMissing_ReturnsNoResults_WhenAllStoredAsNull()
    {
        // All seeded items store null NullableStringValue as {NULL:true} (OmitNullStrings = false),
        // so IS MISSING finds nothing among the seeded data.
        var results =
            await Db
                .SimpleItems
                .Where(x => EF.Functions.IsMissing(x.NullableStringValue))
                .ToListAsync(CancellationToken);

        results.Should().BeEmpty();

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS MISSING
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_Functions_IsNotMissing_ReturnsAllItems_WhenAllAttributesPresent()
    {
        // All seeded items have NullableStringValue present in the attribute map
        // (either as NULL type or a string value), so IS NOT MISSING returns all of them.
        var results =
            await Db
                .SimpleItems
                .Where(x => EF.Functions.IsNotMissing(x.NullableStringValue))
                .ToListAsync(CancellationToken);

        results.Should().BeEquivalentTo(SimpleItems.Items);

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS NOT MISSING
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_Functions_IsMissing_FindsAbsentAttribute()
    {
        // Insert an item via the SDK with NullableStringValue absent from the attribute map.
        var template = new Dictionary<string, AttributeValue>(SimpleItems.AttributeValues[0])
        {
            ["Pk"] = new() { S = "ITEM#MISSING-STRING" },
        };
        template.Remove("NullableStringValue");

        await Client.PutItemAsync(
            new PutItemRequest { TableName = SimpleTableDynamoFixture.TableName, Item = template },
            CancellationToken);

        var results =
            await Db
                .SimpleItems
                .Where(x => EF.Functions.IsMissing(x.NullableStringValue))
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Pk.Should().Be("ITEM#MISSING-STRING");
        results[0].NullableStringValue.Should().BeNull();

        AssertSql(
            """
            SELECT "Pk", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE "NullableStringValue" IS MISSING
            """);
    }
}
