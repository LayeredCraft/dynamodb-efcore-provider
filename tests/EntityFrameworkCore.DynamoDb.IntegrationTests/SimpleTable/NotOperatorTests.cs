using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Verifies that the logical NOT operator is translated to PartiQL NOT predicates.</summary>
public class NotOperatorTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_NotBoolColumn_TranslatesToPartiQlNot()
    {
        var resultItems =
            await Db.SimpleItems.Where(item => !item.BoolValue).ToListAsync(CancellationToken);

        var expected = SimpleItems.Items.Where(item => !item.BoolValue);

        resultItems.Should().BeEquivalentTo(expected);

        AssertSql(
            """
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE NOT ("boolValue" = TRUE)
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
            SELECT "pk", "boolValue", "dateOnlyValue", "dateTimeOffsetValue", "decimalValue", "doubleValue", "floatValue", "guidValue", "intValue", "longValue", "nullableBoolValue", "nullableIntValue", "nullableStringValue", "stringValue", "timeOnlyValue", "timeSpanValue"
            FROM "SimpleItems"
            WHERE NOT ("boolValue" = TRUE AND "intValue" > 50)
            """);
    }
}
