using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations.Temporal;

/// <summary>DateTime translation specification tests for the DynamoDB provider.</summary>
public abstract class DateTimeTranslationsDynamoTest
    : DateTimeTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected DateTimeTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(DateTimeTranslationsDynamoTest));

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Now() => base.Now();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task UtcNow() => base.UtcNow();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Today() => base.Today();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Date() => base.Date();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task AddYear() => base.AddYear();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Year() => base.Year();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Month() => base.Month();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task DayOfYear() => base.DayOfYear();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Day() => base.Day();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Hour() => base.Hour();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Minute() => base.Minute();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Second() => base.Second();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task Millisecond() => base.Millisecond();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task TimeOfDay() => base.TimeOfDay();

    [ConditionalFact(Skip = SkipReason.TemporalFunctionNotSupported)]
    public override Task subtract_and_TotalDays() => base.subtract_and_TotalDays();

    public override async Task Parse_with_constant()
    {
        await base.Parse_with_constant();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "dateTime" = '1998-05-04 15:30:10'
            """);
    }

    public override async Task Parse_with_parameter()
    {
        await base.Parse_with_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "dateTime" = ?
            """);
    }

    public override async Task New_with_constant()
    {
        await base.New_with_constant();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "dateTime" = '1998-05-04 15:30:10'
            """);
    }

    public override async Task New_with_parameters()
    {
        await base.New_with_parameters();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "dateTime" = ?
            """);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class DateTimeTranslationsDynamoTestDefault : DateTimeTranslationsDynamoTest
    {
        public DateTimeTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
