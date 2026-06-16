using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations.Operators;

/// <summary>Logical operator translation specification tests for the DynamoDB provider.</summary>
public abstract class LogicalOperatorTranslationsDynamoTest
    : LogicalOperatorTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected LogicalOperatorTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) :
        base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(LogicalOperatorTranslationsDynamoTest));

    public override async Task And()
    {
        await base.And();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" = 8 AND "string" = 'Seattle'
            """);
    }

    public override async Task And_with_bool_property()
    {
        await base.And_with_bool_property();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "bool" = TRUE AND "string" = 'Seattle'
            """);
    }

    public override async Task Or()
    {
        await base.Or();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "int" = 999 OR "string" = 'Seattle'
            """);
    }

    public override async Task Or_with_bool_property()
    {
        await base.Or_with_bool_property();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "bool" = TRUE OR "string" = 'Seattle'
            """);
    }

    public override async Task Not()
    {
        await base.Not();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE NOT ("int" = 999)
            """);
    }

    public override async Task Not_with_bool_property()
    {
        await base.Not_with_bool_property();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE NOT ("bool" = TRUE)
            """);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class LogicalOperatorTranslationsDynamoTestDefault
        : LogicalOperatorTranslationsDynamoTest
    {
        public LogicalOperatorTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
