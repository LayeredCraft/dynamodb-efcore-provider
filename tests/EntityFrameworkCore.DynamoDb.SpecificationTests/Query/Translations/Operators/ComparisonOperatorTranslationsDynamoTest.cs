using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations.Operators;

/// <summary>Comparison operator translation specification tests for the DynamoDB provider.</summary>
public abstract class ComparisonOperatorTranslationsDynamoTest
    : ComparisonOperatorTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected ComparisonOperatorTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) :
        base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ComparisonOperatorTranslationsDynamoTest));

    public override async Task Equal()
    {
        await base.Equal();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" = 8
            """);
    }

    public override async Task NotEqual()
    {
        await base.NotEqual();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" <> 8
            """);
    }

    public override async Task GreaterThan()
    {
        await base.GreaterThan();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" > 8
            """);
    }

    public override async Task GreaterThanOrEqual()
    {
        await base.GreaterThanOrEqual();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" >= 8
            """);
    }

    public override async Task LessThan()
    {
        await base.LessThan();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" < 8
            """);
    }

    public override async Task LessThanOrEqual()
    {
        await base.LessThanOrEqual();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" <= 8
            """);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class ComparisonOperatorTranslationsDynamoTestDefault
        : ComparisonOperatorTranslationsDynamoTest
    {
        public ComparisonOperatorTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
