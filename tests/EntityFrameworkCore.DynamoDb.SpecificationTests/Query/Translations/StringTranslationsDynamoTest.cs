using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations;

/// <summary>String translation specification tests for the DynamoDB provider.</summary>
public abstract class StringTranslationsDynamoTest
    : StringTranslationsTestBase<BasicTypesQueryDynamoFixture>
{
    protected StringTranslationsDynamoTest(BasicTypesQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(StringTranslationsDynamoTest));

    public override async Task Equals()
    {
        await base.Equals();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" = 'Seattle'
            """);
    }

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Equals_with_OrdinalIgnoreCase() => base.Equals_with_OrdinalIgnoreCase();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Equals_with_Ordinal() => base.Equals_with_Ordinal();

    [ConditionalFact(
        Skip = "DynamoDB provider does not translate static string.Equals in predicates.")]
    public override Task Static_Equals() => base.Static_Equals();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Static_Equals_with_OrdinalIgnoreCase()
        => base.Static_Equals_with_OrdinalIgnoreCase();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Static_Equals_with_Ordinal() => base.Static_Equals_with_Ordinal();

    public override async Task Length()
    {
        await base.Length();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE size("string") = 7
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task ToUpper() => base.ToUpper();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task ToLower() => base.ToLower();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf() => base.IndexOf();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_Char() => base.IndexOf_Char();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_empty_string() => base.IndexOf_with_empty_string();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_one_parameter_arg() => base.IndexOf_with_one_parameter_arg();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_one_parameter_arg_char()
        => base.IndexOf_with_one_parameter_arg_char();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_constant_starting_position()
        => base.IndexOf_with_constant_starting_position();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_constant_starting_position_char()
        => base.IndexOf_with_constant_starting_position_char();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_parameter_starting_position()
        => base.IndexOf_with_parameter_starting_position();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_with_parameter_starting_position_char()
        => base.IndexOf_with_parameter_starting_position_char();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_after_ToString() => base.IndexOf_after_ToString();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IndexOf_over_ToString() => base.IndexOf_over_ToString();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Replace() => base.Replace();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Replace_Char() => base.Replace_Char();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Replace_with_empty_string() => base.Replace_with_empty_string();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Replace_using_property_arguments()
        => base.Replace_using_property_arguments();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring() => base.Substring();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_one_arg_with_zero_startIndex()
        => base.Substring_with_one_arg_with_zero_startIndex();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_one_arg_with_constant()
        => base.Substring_with_one_arg_with_constant();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_one_arg_with_parameter()
        => base.Substring_with_one_arg_with_parameter();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_two_args_with_zero_startIndex()
        => base.Substring_with_two_args_with_zero_startIndex();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_two_args_with_zero_length()
        => base.Substring_with_two_args_with_zero_length();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_two_args_with_parameter()
        => base.Substring_with_two_args_with_parameter();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Substring_with_two_args_with_IndexOf()
        => base.Substring_with_two_args_with_IndexOf();

    public override async Task IsNullOrEmpty()
    {
        await base.IsNullOrEmpty();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE "string" IS NULL OR "string" IS MISSING OR "string" = ''
            """,
            """
            SELECT "string"
            FROM "NullableBasicTypesEntity"
            """);
    }

    public override async Task IsNullOrEmpty_negated()
    {
        await base.IsNullOrEmpty_negated();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "NullableBasicTypesEntity"
            WHERE NOT ("string" IS NULL OR "string" IS MISSING OR "string" = '')
            """,
            """
            SELECT "string"
            FROM "NullableBasicTypesEntity"
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task IsNullOrWhiteSpace() => base.IsNullOrWhiteSpace();

    public override async Task StartsWith_Literal()
    {
        await base.StartsWith_Literal();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE begins_with("string", 'Se')
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB provider only translates string.StartsWith(string), not char overloads.")]
    public override Task StartsWith_Literal_Char() => base.StartsWith_Literal_Char();

    public override async Task StartsWith_Parameter()
    {
        await base.StartsWith_Parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE begins_with("string", ?)
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB provider only translates string.StartsWith(string), not char overloads.")]
    public override Task StartsWith_Parameter_Char() => base.StartsWith_Parameter_Char();

    [ConditionalFact(
        Skip = "DynamoDB rejects begins_with when both operands reference the same attribute.")]
    public override Task StartsWith_Column() => base.StartsWith_Column();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task StartsWith_with_StringComparison_Ordinal()
        => base.StartsWith_with_StringComparison_Ordinal();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task StartsWith_with_StringComparison_OrdinalIgnoreCase()
        => base.StartsWith_with_StringComparison_OrdinalIgnoreCase();

    public override Task StartsWith_with_StringComparison_unsupported()
        => base.StartsWith_with_StringComparison_unsupported();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no ends_with function.")]
    public override Task EndsWith_Literal() => base.EndsWith_Literal();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no ends_with function.")]
    public override Task EndsWith_Literal_Char() => base.EndsWith_Literal_Char();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no ends_with function.")]
    public override Task EndsWith_Parameter() => base.EndsWith_Parameter();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no ends_with function.")]
    public override Task EndsWith_Parameter_Char() => base.EndsWith_Parameter_Char();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no ends_with function.")]
    public override Task EndsWith_Column() => base.EndsWith_Column();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task EndsWith_with_StringComparison_Ordinal()
        => base.EndsWith_with_StringComparison_Ordinal();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task EndsWith_with_StringComparison_OrdinalIgnoreCase()
        => base.EndsWith_with_StringComparison_OrdinalIgnoreCase();

    public override Task EndsWith_with_StringComparison_unsupported()
        => base.EndsWith_with_StringComparison_unsupported();

    public override async Task Contains_Literal()
    {
        await base.Contains_Literal();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE contains("string", 'eattl')
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB provider only translates string.Contains(string), not char overloads.")]
    public override Task Contains_Literal_Char() => base.Contains_Literal_Char();

    [ConditionalFact(
        Skip = "DynamoDB rejects contains when both operands reference the same attribute.")]
    public override Task Contains_Column() => base.Contains_Column();

    public override async Task Contains_negated()
    {
        await base.Contains_negated();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE NOT (contains("string", 'eattle'))
            """,
            """
            SELECT "string"
            FROM "BasicTypesEntity"
            """);
    }

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Contains_with_StringComparison_Ordinal()
        => base.Contains_with_StringComparison_Ordinal();

    [ConditionalFact(
        Skip =
            "DynamoDB provider only translates case-sensitive string operations without StringComparison overloads.")]
    public override Task Contains_with_StringComparison_OrdinalIgnoreCase()
        => base.Contains_with_StringComparison_OrdinalIgnoreCase();

    public override Task Contains_with_StringComparison_unsupported()
        => base.Contains_with_StringComparison_unsupported();

    public override async Task Contains_constant_with_whitespace()
    {
        await base.Contains_constant_with_whitespace();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE contains("string", '     ')
            """);
    }

    public override async Task Contains_parameter_with_whitespace()
    {
        await base.Contains_parameter_with_whitespace();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE contains("string", ?)
            """);
    }

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimStart_without_arguments() => base.TrimStart_without_arguments();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimStart_with_char_argument() => base.TrimStart_with_char_argument();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimStart_with_char_array_argument()
        => base.TrimStart_with_char_array_argument();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimEnd_without_arguments() => base.TrimEnd_without_arguments();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimEnd_with_char_argument() => base.TrimEnd_with_char_argument();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task TrimEnd_with_char_array_argument()
        => base.TrimEnd_with_char_array_argument();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Trim_without_argument_in_predicate()
        => base.Trim_without_argument_in_predicate();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Trim_with_char_argument_in_predicate()
        => base.Trim_with_char_argument_in_predicate();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Trim_with_char_array_argument_in_predicate()
        => base.Trim_with_char_array_argument_in_predicate();

    public override async Task Compare_simple_zero()
    {
        await base.Compare_simple_zero();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" = 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <> 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """);
    }

    public override async Task Compare_simple_one()
    {
        await base.Compare_simple_one();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" < 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle'
            """);
    }

    public override async Task Compare_with_parameter()
    {
        await base.Compare_with_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" < ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= ?
            """);
    }

    [ConditionalFact(
        Skip =
            "DynamoDB provider translates string.Compare/CompareTo only for sign constants -1, 0, or 1.")]
    public override Task Compare_simple_more_than_one() => base.Compare_simple_more_than_one();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Compare_nested() => base.Compare_nested();

    public override async Task Compare_multi_predicate()
    {
        await base.Compare_multi_predicate();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle' AND "string" < 'Toronto'
            """);
    }

    public override async Task CompareTo_simple_zero()
    {
        await base.CompareTo_simple_zero();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" = 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <> 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """);
    }

    public override async Task CompareTo_simple_one()
    {
        await base.CompareTo_simple_one();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" < 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle'
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle'
            """);
    }

    public override async Task CompareTo_with_parameter()
    {
        await base.CompareTo_with_parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" > ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" < ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" <= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= ?
            """,
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= ?
            """);
    }

    [ConditionalFact(
        Skip =
            "DynamoDB provider translates string.Compare/CompareTo only for sign constants -1, 0, or 1.")]
    public override Task CompareTo_simple_more_than_one() => base.CompareTo_simple_more_than_one();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task CompareTo_nested() => base.CompareTo_nested();

    public override async Task Compare_to_multi_predicate()
    {
        await base.Compare_to_multi_predicate();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
            FROM "BasicTypesEntity"
            WHERE "string" >= 'Seattle' AND "string" < 'Toronto'
            """);
    }

    [ConditionalFact(Skip = "DynamoDB PartiQL has no GROUP BY or string aggregate support.")]
    public override Task Join_over_non_nullable_column() => base.Join_over_non_nullable_column();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no GROUP BY or string aggregate support.")]
    public override Task Join_over_nullable_column() => base.Join_over_nullable_column();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no GROUP BY or string aggregate support.")]
    public override Task Join_with_predicate() => base.Join_with_predicate();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no GROUP BY or string aggregate support.")]
    public override Task Join_with_ordering() => base.Join_with_ordering();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Join_non_aggregate() => base.Join_non_aggregate();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_operator() => base.Concat_operator();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no GROUP BY or string aggregate support.")]
    public override Task Concat_aggregate() => base.Concat_aggregate();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_string_int_comparison1() => base.Concat_string_int_comparison1();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_string_int_comparison2() => base.Concat_string_int_comparison2();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_string_int_comparison3() => base.Concat_string_int_comparison3();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_string_int_comparison4() => base.Concat_string_int_comparison4();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_string_string_comparison()
        => base.Concat_string_string_comparison();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_method_comparison() => base.Concat_method_comparison();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_method_comparison_2() => base.Concat_method_comparison_2();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task Concat_method_comparison_3() => base.Concat_method_comparison_3();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task FirstOrDefault() => base.FirstOrDefault();

    [ConditionalFact(
        Skip = "DynamoDB PartiQL has no corresponding string function for this operation.")]
    public override Task LastOrDefault() => base.LastOrDefault();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no regex support.")]
    public override Task Regex_IsMatch() => base.Regex_IsMatch();

    [ConditionalFact(Skip = "DynamoDB PartiQL has no regex support.")]
    public override Task Regex_IsMatch_constant_input() => base.Regex_IsMatch_constant_input();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    protected override void ClearLog() => Fixture.ClearSql();

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class StringTranslationsDynamoTestDefault : StringTranslationsDynamoTest
    {
        public StringTranslationsDynamoTestDefault(
            BasicTypesQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
