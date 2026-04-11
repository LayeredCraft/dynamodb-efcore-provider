using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Verifies that the logical NOT operator is translated to PartiQL NOT predicates.</summary>
public class NotOperatorTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_NotBoolColumn_TranslatesToPartiQlNot()
    {
        var resultItems =
            await Db.SimpleItems.Where(item => !item.BoolValue).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => !item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "$version", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE NOT ("BoolValue" = TRUE)
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_NotCompoundPredicate_TranslatesToPartiQlNotWithParentheses()
    {
        var resultItems =
            await Db
                .SimpleItems
                .Where(item => !(item.BoolValue && item.IntValue > 50))
                .ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => !(item.BoolValue && item.IntValue > 50));

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "Pk", "$version", "BoolValue", "DateTimeOffsetValue", "DecimalValue", "DoubleValue", "FloatValue", "GuidValue", "IntValue", "LongValue", "NullableBoolValue", "NullableIntValue", "NullableStringValue", "StringValue"
            FROM "SimpleItems"
            WHERE NOT ("BoolValue" = TRUE AND "IntValue" > 50)
            """);
    }
}
