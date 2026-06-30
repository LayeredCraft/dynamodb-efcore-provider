using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Primitive collection query specification tests for the DynamoDB provider.</summary>
public abstract class PrimitiveCollectionsQueryDynamoTest
    : PrimitiveCollectionsQueryTestBase<
        PrimitiveCollectionsQueryDynamoTest.PrimitiveCollectionsQueryDynamoFixture>
{
    private const string UnsupportedPrimitiveCollectionQueryShape =
        "DynamoDB provider does not yet support this primitive collection query shape.";

    private const string OutOfBoundsListIndexReturnsNull =
        "DynamoDB returns NULL for out-of-bounds list index access; upstream test expects an exception.";

    protected PrimitiveCollectionsQueryDynamoTest(PrimitiveCollectionsQueryDynamoFixture fixture) :
        base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(PrimitiveCollectionsQueryDynamoTest));

    [ConditionalFact]
    public override async Task Inline_collection_of_ints_Contains()
    {
        await base.Inline_collection_of_ints_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [10, 999]
            """);
    }

    [ConditionalFact]
    public override Task Inline_collection_of_nullable_ints_Contains()
        => base.Inline_collection_of_nullable_ints_Contains();

    [ConditionalFact]
    public override Task Inline_collection_of_nullable_ints_Contains_null()
        => base.Inline_collection_of_nullable_ints_Contains_null();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Count_with_zero_values()
        => base.Inline_collection_Count_with_zero_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Count_with_one_value()
        => base.Inline_collection_Count_with_one_value();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Count_with_two_values()
        => base.Inline_collection_Count_with_two_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Count_with_three_values()
        => base.Inline_collection_Count_with_three_values();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_zero_values()
        => base.Inline_collection_Contains_with_zero_values();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_one_value()
        => base.Inline_collection_Contains_with_one_value();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_two_values()
        => base.Inline_collection_Contains_with_two_values();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_three_values()
        => base.Inline_collection_Contains_with_three_values();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_all_parameters()
        => base.Inline_collection_Contains_with_all_parameters();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_constant_and_parameter()
        => base.Inline_collection_Contains_with_constant_and_parameter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Contains_with_mixed_value_types()
        => base.Inline_collection_Contains_with_mixed_value_types();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_Contains_with_mixed_value_types()
        => base.Inline_collection_List_Contains_with_mixed_value_types();

    [ConditionalFact]
    public override async Task Inline_collection_Contains_as_Any_with_predicate()
    {
        await base.Inline_collection_Contains_as_Any_with_predicate();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "id" IN [2, 999]
            """);
    }

    [ConditionalFact]
    public override async Task Inline_collection_negated_Contains_as_All()
    {
        await base.Inline_collection_negated_Contains_as_All();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("id" IN [2, 999])
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Min_with_two_values()
        => base.Inline_collection_Min_with_two_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_Min_with_two_values()
        => base.Inline_collection_List_Min_with_two_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Max_with_two_values()
        => base.Inline_collection_Max_with_two_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_Max_with_two_values()
        => base.Inline_collection_List_Max_with_two_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Min_with_three_values()
        => base.Inline_collection_Min_with_three_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_Min_with_three_values()
        => base.Inline_collection_List_Min_with_three_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Max_with_three_values()
        => base.Inline_collection_Max_with_three_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_Max_with_three_values()
        => base.Inline_collection_List_Max_with_three_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_of_nullable_value_type_Min()
        => base.Inline_collection_of_nullable_value_type_Min();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_of_nullable_value_type_Max()
        => base.Inline_collection_of_nullable_value_type_Max();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_of_nullable_value_type_with_null_Min()
        => base.Inline_collection_of_nullable_value_type_with_null_Min();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_of_nullable_value_type_with_null_Max()
        => base.Inline_collection_of_nullable_value_type_with_null_Max();

    [ConditionalFact]
    public override Task Inline_collection_with_single_parameter_element_Contains()
        => base.Inline_collection_with_single_parameter_element_Contains();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_with_single_parameter_element_Count()
        => base.Inline_collection_with_single_parameter_element_Count();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_EF_Parameter()
        => base.Inline_collection_Contains_with_EF_Parameter();

    [ConditionalFact]
    public override Task Inline_collection_Contains_with_IEnumerable_EF_Parameter()
        => base.Inline_collection_Contains_with_IEnumerable_EF_Parameter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_Count_with_column_predicate_with_EF_Parameter()
        => base.Inline_collection_Count_with_column_predicate_with_EF_Parameter();

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_in_query_filter()
        => base.Inline_collection_in_query_filter();
#endif

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Count() => base.Parameter_collection_Count();

    [ConditionalFact]
    public override async Task Parameter_collection_of_ints_Contains_int()
    {
        await base.Parameter_collection_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_HashSet_of_ints_Contains_int()
    {
        await base.Parameter_collection_HashSet_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }

#if NET11_0_OR_GREATER
    [ConditionalFact]
    public override async Task Parameter_collection_FrozenSet_of_ints_Contains_int()
    {
        await base.Parameter_collection_FrozenSet_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }
#endif

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_ImmutableArray_of_ints_Contains_int()
        => base.Parameter_collection_ImmutableArray_of_ints_Contains_int();

#if NET11_0_OR_GREATER
    [ConditionalFact]
    public override async Task Parameter_collection_IReadOnlySet_of_ints_Contains_int()
    {
        await base.Parameter_collection_IReadOnlySet_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_ReadOnlyCollectionWithContains_of_ints_Contains_int()
    {
        await base.Parameter_collection_ReadOnlyCollectionWithContains_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }
#endif

#if NET11_0_OR_GREATER
    [ConditionalFact]
    public override async Task Static_readonly_collection_List_of_ints_Contains_int()
    {
        await base.Static_readonly_collection_List_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [10, 999]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [10, 999])
            """);
    }

    [ConditionalFact]
    public override async Task Static_readonly_collection_FrozenSet_of_ints_Contains_int()
    {
        await base.Static_readonly_collection_FrozenSet_of_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [10, 999]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [10, 999])
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Static_readonly_collection_ImmutableArray_of_ints_Contains_int()
        => base.Static_readonly_collection_ImmutableArray_of_ints_Contains_int();
#endif

    [ConditionalFact]
    public override async Task Parameter_collection_of_ints_Contains_nullable_int()
    {
        await base.Parameter_collection_of_ints_Contains_nullable_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableInt" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableInt" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_nullable_ints_Contains_int()
    {
        await base.Parameter_collection_of_nullable_ints_Contains_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("int" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_nullable_ints_Contains_nullable_int()
    {
        await base.Parameter_collection_of_nullable_ints_Contains_nullable_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableInt" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableInt" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task
        Parameter_collection_of_nullable_ints_Contains_nullable_int_with_EF_Parameter()
    {
        await base.Parameter_collection_of_nullable_ints_Contains_nullable_int_with_EF_Parameter();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableInt" IN [?, ?]
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_structs_Contains_struct()
    {
        await base.Parameter_collection_of_structs_Contains_struct();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "wrappedId" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("wrappedId" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_structs_Contains_nullable_struct()
    {
        await base.Parameter_collection_of_structs_Contains_nullable_struct();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableWrappedId" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableWrappedId" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task
        Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        await base
            .Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableWrappedIdWithNullableComparer" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableWrappedId" IN [?, ?])
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_of_nullable_structs_Contains_struct()
        => base.Parameter_collection_of_nullable_structs_Contains_struct();

    [ConditionalFact]
    public override async Task Parameter_collection_of_nullable_structs_Contains_nullable_struct()
    {
        await base.Parameter_collection_of_nullable_structs_Contains_nullable_struct();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableWrappedId" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableWrappedId" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task
        Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        await base
            .Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableWrappedIdWithNullableComparer" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableWrappedIdWithNullableComparer" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_strings_Contains_string()
    {
        await base.Parameter_collection_of_strings_Contains_string();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "string" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("string" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_strings_Contains_nullable_string()
    {
        await base.Parameter_collection_of_strings_Contains_nullable_string();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableString" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableString" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_nullable_strings_Contains_string()
    {
        await base.Parameter_collection_of_nullable_strings_Contains_string();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "string" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("string" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_nullable_strings_Contains_nullable_string()
    {
        await base.Parameter_collection_of_nullable_strings_Contains_nullable_string();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "nullableString" IN [?, ?]
            """,
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE NOT ("nullableString" IN [?, ?])
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_DateTimes_Contains()
    {
        await base.Parameter_collection_of_DateTimes_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "dateTime" IN [?, ?]
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_bools_Contains()
    {
        await base.Parameter_collection_of_bools_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "bool" IN [?]
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_of_enums_Contains()
    {
        await base.Parameter_collection_of_enums_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "enum" IN [?, ?]
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_null_Contains()
    {
        await base.Parameter_collection_null_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE 1 = 0
            """);
    }

    [ConditionalFact]
    public override async Task Parameter_collection_empty_Contains()
    {
        await base.Parameter_collection_empty_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE 1 = 0
            """);
    }

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override Task Parameter_collection_empty_Join()
        => base.Parameter_collection_empty_Join();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Contains_with_EF_Constant()
        => base.Parameter_collection_Contains_with_EF_Constant();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Where_with_EF_Constant_Where_Any()
        => base.Parameter_collection_Where_with_EF_Constant_Where_Any();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Count_with_column_predicate_with_EF_Constant()
        => base.Parameter_collection_Count_with_column_predicate_with_EF_Constant();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Count_with_huge_number_of_values()
        => base.Parameter_collection_Count_with_huge_number_of_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_Count_with_huge_number_of_values_over_5_operations()
        => base.Parameter_collection_Count_with_huge_number_of_values_over_5_operations();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_Count_with_huge_number_of_values_over_5_operations_same_parameter()
        => base
            .Parameter_collection_Count_with_huge_number_of_values_over_5_operations_same_parameter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_Count_with_huge_number_of_values_over_2_operations_same_parameter_different_type_mapping()
        => base
            .Parameter_collection_Count_with_huge_number_of_values_over_2_operations_same_parameter_different_type_mapping();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_Count_with_huge_number_of_values_over_5_operations_forced_constants()
        => base
            .Parameter_collection_Count_with_huge_number_of_values_over_5_operations_forced_constants();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_Count_with_huge_number_of_values_over_5_operations_mixed_parameters_constants()
        => base
            .Parameter_collection_Count_with_huge_number_of_values_over_5_operations_mixed_parameters_constants();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_of_ints_Contains_int_with_huge_number_of_values()
        => base.Parameter_collection_of_ints_Contains_int_with_huge_number_of_values();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations()
        => base
            .Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_same_parameter()
        => base
            .Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_same_parameter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_2_operations_same_parameter_different_type_mapping()
        => base
            .Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_2_operations_same_parameter_different_type_mapping();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_forced_constants()
        => base
            .Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_forced_constants();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_mixed_parameters_constants()
        => base
            .Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_5_operations_mixed_parameters_constants();

    [ConditionalFact]
    public override async Task Column_collection_of_ints_Contains()
    {
        await base.Column_collection_of_ints_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE contains("ints", 10)
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_of_nullable_ints_Contains()
        => base.Column_collection_of_nullable_ints_Contains();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_of_nullable_ints_Contains_null()
        => base.Column_collection_of_nullable_ints_Contains_null();

#if NET11_0_OR_GREATER
    [ConditionalFact]
    public override async Task Column_collection_of_strings_Contains()
    {
        await base.Column_collection_of_strings_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE contains("strings", '10')
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_of_strings_Contains_null()
        => base.Column_collection_of_strings_Contains_null();
#else
    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_of_strings_contains_null()
        => base.Column_collection_of_strings_contains_null();
#endif

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_of_nullable_strings_contains_null()
        => base.Column_collection_of_nullable_strings_contains_null();

    [ConditionalFact]
    public override async Task Column_collection_of_bools_Contains()
    {
        await base.Column_collection_of_bools_Contains();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE contains("bools", TRUE)
            """);
    }

    [ConditionalFact]
    public override Task Contains_on_Enumerable() => base.Contains_on_Enumerable();

    [ConditionalFact]
    public override async Task Contains_on_MemoryExtensions()
    {
        await base.Contains_on_MemoryExtensions();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "int" IN [10, 999]
            """);
    }

    [ConditionalFact]
    public override Task Contains_with_MemoryExtensions_with_null_comparer()
        => base.Contains_with_MemoryExtensions_with_null_comparer();

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_with_custom_converter()
        => base.Column_with_custom_converter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Constant_with_inferred_value_converter()
        => base.Constant_with_inferred_value_converter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_with_inferred_value_converter()
        => base.Parameter_with_inferred_value_converter();

    [ConditionalFact]
    public override async Task Multidimensional_array_is_not_supported()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => InitializeNonSharedTest<TestContext>(
                onModelCreating: mb => mb.Entity<TestEntity>().Property(typeof(int[,]), "MultidimensionalArray")));

        Assert.Contains("MultidimensionalArray", exception.Message, StringComparison.Ordinal);
        Assert.Contains("int[,]", exception.Message, StringComparison.Ordinal);
    }
#endif

    [ConditionalFact]
    public override async Task Column_collection_Count_method()
    {
        await base.Column_collection_Count_method();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE size("ints") = 2
            """);
    }

    [ConditionalFact]
    public override async Task Column_collection_Length()
    {
        await base.Column_collection_Length();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE size("ints") = 2
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Count_with_predicate()
        => base.Column_collection_Count_with_predicate();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_Count() => base.Column_collection_Where_Count();

    [ConditionalFact]
    public override async Task Column_collection_index_int()
    {
        await base.Column_collection_index_int();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "ints"[1] = 10
            """);
    }

    [ConditionalFact]
    public override Task Column_collection_index_string() => base.Column_collection_index_string();

    [ConditionalFact]
    public override Task Column_collection_index_datetime()
        => base.Column_collection_index_datetime();

    [ConditionalFact(Skip = OutOfBoundsListIndexReturnsNull)]
    public override Task Column_collection_index_beyond_end()
        => base.Column_collection_index_beyond_end();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Nullable_reference_column_collection_index_equals_nullable_column()
        => base.Nullable_reference_column_collection_index_equals_nullable_column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Non_nullable_reference_column_collection_index_equals_nullable_column()
        => base.Non_nullable_reference_column_collection_index_equals_nullable_column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_index_Column() => base.Inline_collection_index_Column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_index_Column_with_EF_Constant()
        => base.Inline_collection_index_Column_with_EF_Constant();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_value_index_Column()
        => base.Inline_collection_value_index_Column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Inline_collection_List_value_index_Column()
        => base.Inline_collection_List_value_index_Column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_index_Column_equal_Column()
        => base.Parameter_collection_index_Column_equal_Column();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_index_Column_equal_constant()
        => base.Parameter_collection_index_Column_equal_constant();

    [ConditionalFact]
    public override async Task Column_collection_ElementAt()
    {
        await base.Column_collection_ElementAt();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "ints"[1] = 10
            """);
    }

    [ConditionalFact]
    public override async Task Column_collection_First()
    {
        await base.Column_collection_First();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "ints"[0] = 1
            """);
    }

    [ConditionalFact]
    public override async Task Column_collection_FirstOrDefault()
    {
        await base.Column_collection_FirstOrDefault();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE "ints"[0] = 1
            """);
    }

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Single() => base.Column_collection_Single();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_SingleOrDefault()
        => base.Column_collection_SingleOrDefault();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Skip() => base.Column_collection_Skip();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Take() => base.Column_collection_Take();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Skip_Take() => base.Column_collection_Skip_Take();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_Skip() => base.Column_collection_Where_Skip();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_Take() => base.Column_collection_Where_Take();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_Skip_Take()
        => base.Column_collection_Where_Skip_Take();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Contains_over_subquery()
        => base.Column_collection_Contains_over_subquery();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_OrderByDescending_ElementAt()
        => base.Column_collection_OrderByDescending_ElementAt();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_ElementAt()
        => base.Column_collection_Where_ElementAt();

    [ConditionalFact]
    public override async Task Column_collection_Any()
    {
        await base.Column_collection_Any();

        AssertSql(
            """
            SELECT "id", "$type", "bool", "bools", "dateTime", "dateTimes", "enum", "enums", "int", "ints", "nullableInt", "nullableInts", "nullableString", "nullableStrings", "nullableWrappedId", "nullableWrappedIdWithNullableComparer", "string", "strings", "wrappedId"
            FROM "PrimitiveCollections"
            WHERE size("ints") > 0
            """);
    }

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_Distinct() => base.Column_collection_Distinct();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_SelectMany() => base.Column_collection_SelectMany();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_SelectMany_with_filter()
        => base.Column_collection_SelectMany_with_filter();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_SelectMany_with_Select_to_anonymous_type()
        => base.Column_collection_SelectMany_with_Select_to_anonymous_type();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_projection_from_top_level()
        => base.Column_collection_projection_from_top_level();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override Task Column_collection_Join_parameter_collection()
        => base.Column_collection_Join_parameter_collection();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override Task Inline_collection_Join_ordered_column_collection()
        => base.Inline_collection_Join_ordered_column_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Parameter_collection_Concat_column_collection()
        => base.Parameter_collection_Concat_column_collection();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Parameter_collection_with_type_inference_for_JsonScalarExpression()
        => base.Parameter_collection_with_type_inference_for_JsonScalarExpression();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_Union_parameter_collection()
        => base.Column_collection_Union_parameter_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_Intersect_inline_collection()
        => base.Column_collection_Intersect_inline_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Inline_collection_Except_column_collection()
        => base.Inline_collection_Except_column_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_Where_Union() => base.Column_collection_Where_Union();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_equality_parameter_collection()
        => base.Column_collection_equality_parameter_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_Concat_parameter_collection_equality_inline_collection()
        => base.Column_collection_Concat_parameter_collection_equality_inline_collection();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_equality_inline_collection()
        => base.Column_collection_equality_inline_collection();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_equality_inline_collection_with_parameters()
        => base.Column_collection_equality_inline_collection_with_parameters();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Column_collection_Where_equality_inline_collection()
        => base.Column_collection_Where_equality_inline_collection();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Parameter_collection_in_subquery_Count_as_compiled_query()
        => base.Parameter_collection_in_subquery_Count_as_compiled_query();

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Compiled_query_with_uncorrelated_parameter_collection_expression()
        => base.Compiled_query_with_uncorrelated_parameter_collection_expression();
#endif

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Parameter_collection_in_subquery_Union_column_collection_as_compiled_query()
        => base.Parameter_collection_in_subquery_Union_column_collection_as_compiled_query();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Parameter_collection_in_subquery_Union_column_collection()
        => base.Parameter_collection_in_subquery_Union_column_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Parameter_collection_in_subquery_Union_column_collection_nested()
        => base.Parameter_collection_in_subquery_Union_column_collection_nested();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Parameter_collection_in_subquery_and_Convert_as_compiled_query()
        => base.Parameter_collection_in_subquery_and_Convert_as_compiled_query();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query()
        => base
            .Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Column_collection_in_subquery_Union_parameter_collection()
        => base.Column_collection_in_subquery_Union_parameter_collection();

    [ConditionalFact(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Project_collection_of_ints_simple()
        => base.Project_collection_of_ints_simple();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_ints_ordered()
        => base.Project_collection_of_ints_ordered();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_datetimes_filtered()
        => base.Project_collection_of_datetimes_filtered();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_nullable_ints_with_paging()
        => base.Project_collection_of_nullable_ints_with_paging();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_nullable_ints_with_paging2()
        => base.Project_collection_of_nullable_ints_with_paging2();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_nullable_ints_with_paging3()
        => base.Project_collection_of_nullable_ints_with_paging3();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_ints_with_distinct()
        => base.Project_collection_of_ints_with_distinct();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_nullable_ints_with_distinct()
        => base.Project_collection_of_nullable_ints_with_distinct();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_collection_of_ints_with_ToList_and_FirstOrDefault()
        => base.Project_collection_of_ints_with_ToList_and_FirstOrDefault();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task
        Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => base.Project_empty_collection_of_nullables_and_collection_only_containing_nulls();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_multiple_collections() => base.Project_multiple_collections();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_primitive_collections_element()
        => base.Project_primitive_collections_element();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Project_inline_collection() => base.Project_inline_collection();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Project_inline_collection_with_Union()
        => base.Project_inline_collection_with_Union();

    [ConditionalFact(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Project_inline_collection_with_Concat()
        => base.Project_inline_collection_with_Concat();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Nested_contains_with_Lists_and_no_inferred_type_mapping()
        => base.Nested_contains_with_Lists_and_no_inferred_type_mapping();

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Nested_contains_with_arrays_and_no_inferred_type_mapping()
        => base.Nested_contains_with_arrays_and_no_inferred_type_mapping();

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Project_collection_from_entity_type_with_owned()
        => base.Project_collection_from_entity_type_with_owned();
#endif

    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Values_of_enum_casted_to_underlying_value()
        => base.Values_of_enum_casted_to_underlying_value();

#if NET11_0_OR_GREATER
    [ConditionalFact(Skip = UnsupportedPrimitiveCollectionQueryShape)]
    public override Task Subquery_over_primitive_collection_on_inheritance_derived_type()
        => base.Subquery_over_primitive_collection_on_inheritance_derived_type();
#endif

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    public class PrimitiveCollectionsQueryDynamoFixture
        : PrimitiveCollectionsQueryFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool UsePooling => false;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings
                    => warnings
                        .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                        .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options
                        .DynamoDbClient(DynamoTestStoreFactory.Instance.Client)
                        .TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<PrimitiveCollectionsEntity>(entity =>
            {
                entity.ToTable("PrimitiveCollections").HasPartitionKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class PrimitiveCollectionsQueryDynamoTestDefault
        : PrimitiveCollectionsQueryDynamoTest
    {
        public PrimitiveCollectionsQueryDynamoTestDefault(
            PrimitiveCollectionsQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
