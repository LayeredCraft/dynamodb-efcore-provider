using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations;

/// <summary>GUID translation specification tests for the DynamoDB provider.</summary>
public abstract class GuidTranslationsDynamoTest
    : GuidTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected GuidTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(GuidTranslationsDynamoTest));

    public override async Task New_with_constant()
    {
        await base.New_with_constant();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "guid" = 'df36f493-463f-4123-83f9-6b135deeb7ba'
            """);
    }

    public override async Task New_with_parameter()
    {
        await base.New_with_parameter();

        AssertSql(
            """
            SELECT "id", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "guid" = ?
            """);
    }

    public override async Task ToString_projection()
    {
        await base.ToString_projection();

        AssertSql(
            """
            SELECT "guid"
            FROM "BasicTypesEntity"
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB provider does not translate Guid.NewGuid() in server-side predicates.")]
    public override async Task NewGuid() => await base.NewGuid();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class GuidTranslationsDynamoTestDefault : GuidTranslationsDynamoTest
    {
        public GuidTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
