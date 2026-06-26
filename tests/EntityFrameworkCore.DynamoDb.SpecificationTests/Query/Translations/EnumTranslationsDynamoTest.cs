using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations;

/// <summary>Enum translation specification tests for the DynamoDB provider.</summary>
public abstract class EnumTranslationsDynamoTest
    : EnumTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    private const string BitwiseSkipReason =
        "DynamoDB PartiQL does not support bitwise enum operators or Enum.HasFlag translation.";

    protected EnumTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(EnumTranslationsDynamoTest));

    public override async Task Equality_to_constant()
    {
        await base.Equality_to_constant();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "enum" = 0
            """);
    }

    public override async Task Equality_to_parameter()
    {
        await base.Equality_to_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "enum" = ?
            """);
    }

    public override async Task Equality_nullable_enum_to_constant()
    {
        await base.Equality_nullable_enum_to_constant();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" = 0
            """);
    }

    public override async Task Equality_nullable_enum_to_parameter()
    {
        await base.Equality_nullable_enum_to_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" = ?
            """);
    }

    public override async Task Equality_nullable_enum_to_null_constant()
    {
        await base.Equality_nullable_enum_to_null_constant();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" IS NULL OR "enum" IS MISSING
            """);
    }

    public override async Task Equality_nullable_enum_to_null_parameter()
    {
        await base.Equality_nullable_enum_to_null_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" = ?
            """);
    }

    public override async Task Equality_nullable_enum_to_nullable_parameter()
    {
        await base.Equality_nullable_enum_to_nullable_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" = ?
            """);
    }

    [ConditionalFact]
    public virtual async Task Nullable_enum_has_value()
    {
        await AssertQuery(ss => ss.Set<NullableBasicTypesEntity>().Where(b => b.Enum.HasValue));

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "enum" IS NOT NULL AND "enum" IS NOT MISSING
            """);
    }

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Bitwise_and_enum_constant() => base.Bitwise_and_enum_constant();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Bitwise_and_integral_constant() => base.Bitwise_and_integral_constant();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Bitwise_and_nullable_enum_with_constant()
        => base.Bitwise_and_nullable_enum_with_constant();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Where_bitwise_and_nullable_enum_with_null_constant()
        => base.Where_bitwise_and_nullable_enum_with_null_constant();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Where_bitwise_and_nullable_enum_with_non_nullable_parameter()
        => base.Where_bitwise_and_nullable_enum_with_non_nullable_parameter();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Where_bitwise_and_nullable_enum_with_nullable_parameter()
        => base.Where_bitwise_and_nullable_enum_with_nullable_parameter();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Bitwise_or() => base.Bitwise_or();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task Bitwise_projects_values_in_select()
        => base.Bitwise_projects_values_in_select();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task HasFlag() => base.HasFlag();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task HasFlag_with_non_nullable_parameter()
        => base.HasFlag_with_non_nullable_parameter();

    [ConditionalFact(Skip = BitwiseSkipReason)]
    public override Task HasFlag_with_nullable_parameter()
        => base.HasFlag_with_nullable_parameter();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class EnumTranslationsDynamoTestDefault : EnumTranslationsDynamoTest
    {
        public EnumTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
