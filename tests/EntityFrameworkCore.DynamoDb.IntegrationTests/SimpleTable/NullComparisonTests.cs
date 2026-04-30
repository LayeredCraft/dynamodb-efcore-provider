using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>
///     Verifies that null comparisons in LINQ predicates are translated to the correct IS NULL /
///     IS MISSING PartiQL predicates.
/// </summary>
public class NullComparisonTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS NULL OR "nullableStringValue" IS MISSING
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS NOT NULL AND "nullableStringValue" IS NOT MISSING
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE ("nullableStringValue" IS NULL OR "nullableStringValue" IS MISSING) AND "boolValue" = TRUE
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE NOT ("nullableStringValue" IS NULL OR "nullableStringValue" IS MISSING)
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS NULL
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS NOT NULL
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS MISSING
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE "nullableStringValue" IS NOT MISSING
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_Functions_IsMissing_FindsAbsentAttribute()
    {
        // Insert an item via the SDK with NullableStringValue absent from the attribute map.
        const string missingItemPk = "ITEM#MISSING-STRING";
        var template = new Dictionary<string, AttributeValue>(SimpleItems.AttributeValues[0])
        {
            ["pk"] = new() { S = missingItemPk },
        };
        template.Remove("nullableStringValue");

        await Client.PutItemAsync(
            new PutItemRequest { TableName = SimpleItemTable.TableName, Item = template },
            CancellationToken);

        try
        {
            var results =
                await Db
                    .SimpleItems
                    .Where(x => EF.Functions.IsMissing(x.NullableStringValue))
                    .ToListAsync(CancellationToken);

            results.Should().HaveCount(1);
            results[0].Pk.Should().Be(missingItemPk);
            results[0].NullableStringValue.Should().BeNull();

            AssertSql(
                """
                SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
                FROM "SimpleItems"
                WHERE "nullableStringValue" IS MISSING
                """);
        }
        finally
        {
            // Delete the item to avoid contaminating other tests in the shared table.
            await Client.DeleteItemAsync(
                new DeleteItemRequest
                {
                    TableName = SimpleItemTable.TableName,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new() { S = missingItemPk },
                    },
                },
                CancellationToken);
        }
    }
}
