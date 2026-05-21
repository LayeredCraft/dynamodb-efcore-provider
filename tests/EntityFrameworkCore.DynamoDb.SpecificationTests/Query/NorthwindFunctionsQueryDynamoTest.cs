using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind function-query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindFunctionsQueryDynamoTest
    : NorthwindFunctionsQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindFunctionsQueryDynamoTest(NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture)
        : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(NorthwindFunctionsQueryDynamoTest));

    [ConditionalTheory(Skip = SkipReason.EntityTypeNotMappedInFixture)]
    public override Task Client_evaluation_of_uncorrelated_method_call(bool async)
        => base.Client_evaluation_of_uncorrelated_method_call(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Order_by_length_twice(bool async)
        => base.Order_by_length_twice(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Order_by_length_twice_followed_by_projection_of_naked_collection_navigation(bool async)
        => base.Order_by_length_twice_followed_by_projection_of_naked_collection_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Sum_over_round_works_correctly_in_projection(bool async)
        => base.Sum_over_round_works_correctly_in_projection(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Sum_over_round_works_correctly_in_projection_2(bool async)
        => base.Sum_over_round_works_correctly_in_projection_2(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Sum_over_truncate_works_correctly_in_projection(bool async)
        => base.Sum_over_truncate_works_correctly_in_projection(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Sum_over_truncate_works_correctly_in_projection_2(bool async)
        => base.Sum_over_truncate_works_correctly_in_projection_2(async);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_functions_nested(bool async)
        => base.Where_functions_nested(async);

    public override Task Static_equals_nullable_datetime_compared_to_non_nullable(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Static_equals_nullable_datetime_compared_to_non_nullable(a);
            AssertSql(
            """
            SELECT "orderID", "customerID", "employeeID", "orderDate"
            FROM "Orders"
            WHERE "orderDate" = ?
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Static_equals_int_compared_to_long(bool async)
        => base.Static_equals_int_compared_to_long(async);

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindFunctionsQueryDynamoTestDefault : NorthwindFunctionsQueryDynamoTest
    {
        public NorthwindFunctionsQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture)
            : base(fixture)
            => _ = containerFixture;
    }
}
