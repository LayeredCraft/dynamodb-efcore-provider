using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations;

/// <summary>Byte-array translation specification tests for the DynamoDB provider.</summary>
public abstract class ByteArrayTranslationsDynamoTest
    : ByteArrayTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected ByteArrayTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ByteArrayTranslationsDynamoTest));

    public override async Task Length()
    {
        await base.Length();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE size("byteArray") = 4
            """);
    }

    [ConditionalFact(Skip = SkipReason.ByteArrayElementAccessNotSupported)]
    public override Task Index() => base.Index();

    [ConditionalFact(Skip = SkipReason.ByteArrayElementAccessNotSupported)]
    public override Task First() => base.First();

    [ConditionalFact(Skip = SkipReason.ByteArrayContainsElementNotSupported)]
    public override Task Contains_with_constant() => base.Contains_with_constant();

    [ConditionalFact(Skip = SkipReason.ByteArrayContainsElementNotSupported)]
    public override Task Contains_with_parameter() => base.Contains_with_parameter();

    [ConditionalFact(Skip = SkipReason.ByteArrayContainsElementNotSupported)]
    public override Task Contains_with_column() => base.Contains_with_column();

#if NET11_0_OR_GREATER
    public override async Task Any()
    {
        await base.Any();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE size("byteArray") > 0
            """);
    }

#endif
    public override async Task SequenceEqual()
    {
        await base.SequenceEqual();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "byteArray" = ?
            """);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class ByteArrayTranslationsDynamoTestDefault : ByteArrayTranslationsDynamoTest
    {
        public ByteArrayTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
