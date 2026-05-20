using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind aggregate-operator specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindAggregateOperatorsQueryDynamoTest
    : NorthwindAggregateOperatorsQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindAggregateOperatorsQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture)
        : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindAggregateOperatorsQueryDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_All(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_no_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_no_data_cast_to_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_no_data_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_binary_expression(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_no_arg_empty(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_arg_expression(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_division_on_decimal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_division_on_decimal_no_significant_digits(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_with_coalesce(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_nested_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_min_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_scalar_returning_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_Any_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_uncorrelated_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_on_float_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_on_float_column_in_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_no_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_binary_expression(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_arg_expression(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_division_on_decimal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_division_on_decimal_no_significant_digits(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_coalesce(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_over_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_over_nested_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_over_max_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_on_float_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_on_float_column_in_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_on_float_column_in_subquery_with_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_with_no_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_with_arg(bool async)
        => Task.CompletedTask;

    public override Task Min_no_data(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Min_no_data(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_no_data_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_no_data_cast_to_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_no_data_subquery(bool async)
        => Task.CompletedTask;

    public override Task Max_no_data(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Max_no_data(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_no_data_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_no_data_cast_to_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_no_data_subquery(bool async)
        => Task.CompletedTask;

    public override Task Average_no_data(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Average_no_data(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_no_data_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_no_data_cast_to_nullable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_no_data_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_with_coalesce(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_over_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_over_nested_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_over_max_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_with_no_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_with_arg(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_with_coalesce(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_over_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_over_nested_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_over_sum_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Count_with_no_predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Count_with_predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Count_with_order_by(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_OrderBy_Count(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Where_Count(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Count_with_predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Where_Count_with_predicate(bool async)
        => Task.CompletedTask;

    public override Task Where_OrderBy_Count_client_eval(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_OrderBy_Count_client_eval(a);
            AssertSql();
        });

    public override Task OrderBy_Where_Count_client_eval(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Where_Count_client_eval(a);
            AssertSql();
        });

    public override Task OrderBy_Where_Count_client_eval_mixed(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Where_Count_client_eval_mixed(a);
            AssertSql();
        });

    public override Task OrderBy_Count_with_predicate_client_eval(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Count_with_predicate_client_eval(a);
            AssertSql();
        });

    public override Task OrderBy_Count_with_predicate_client_eval_mixed(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Count_with_predicate_client_eval_mixed(a);
            AssertSql();
        });

    public override Task OrderBy_Where_Count_with_predicate_client_eval(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Where_Count_with_predicate_client_eval(a);
            AssertSql();
        });

    public override Task OrderBy_Where_Count_with_predicate_client_eval_mixed(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.OrderBy_Where_Count_with_predicate_client_eval_mixed(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_client_Take(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct_Scalar(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Distinct(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct_OrderBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct_OrderBy2(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct_OrderBy3(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Distinct_Count(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_Select_Distinct_Count(bool async)
        => Task.CompletedTask;

    public override Task Single_Throws(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Single_Throws(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Single_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Single(bool async)
        => Task.CompletedTask;

    public override Task SingleOrDefault_Throws(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.SingleOrDefault_Throws(a);
            AssertSql();
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SingleOrDefault_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_SingleOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task First(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task First_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Where_First(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task FirstOrDefault_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Where_FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_inside_subquery_gets_server_evaluated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Multiple_collection_navigation_with_FirstOrDefault_chained(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Multiple_collection_navigation_with_FirstOrDefault_chained_projecting_scalar(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task First_inside_subquery_gets_client_evaluated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last_when_no_order_by(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault_when_no_order_by(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Last(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault_Predicate(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_LastOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_subquery(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_array_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_array_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_subquery_and_local_array_closure(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_uint_array_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_uint_array_closure(a);
            AssertSql(
            """
            SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
            FROM "Employees"
            WHERE "employeeID" IN [?, ?]
            """,
            """
            SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
            FROM "Employees"
            WHERE "employeeID" IN [?]
            """);
        });

    public override Task Contains_with_local_nullable_uint_array_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_nullable_uint_array_closure(a);
            AssertSql(
            """
            SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
            FROM "Employees"
            WHERE "employeeID" IN [?, ?]
            """,
            """
            SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
            FROM "Employees"
            WHERE "employeeID" IN [?]
            """);
        });

    public override Task Contains_with_local_array_inline(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_array_inline(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ABCDE', 'ALFKI']
            """);
        });

    public override Task Contains_with_local_list_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_list_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_object_list_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_object_list_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_list_closure_all_null(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_list_inline(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_list_inline(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ABCDE', 'ALFKI']
            """);
        });

    public override Task Contains_with_local_list_inline_closure_mix(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_list_inline_closure_mix(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_enumerable_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_enumerable_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task Contains_with_local_object_enumerable_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_object_enumerable_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_enumerable_closure_all_null(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_enumerable_closure_all_null(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE 1 = 0
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_enumerable_inline(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_enumerable_inline_closure_mix(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_ordered_enumerable_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_ordered_enumerable_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task Contains_with_local_object_ordered_enumerable_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_object_ordered_enumerable_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_ordered_enumerable_closure_all_null(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_ordered_enumerable_inline(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_ordered_enumerable_inline(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ABCDE', 'ALFKI']
            """);
        });

    public override Task Contains_with_local_ordered_enumerable_inline_closure_mix(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_ordered_enumerable_inline_closure_mix(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_read_only_collection_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_read_only_collection_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task Contains_with_local_object_read_only_collection_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_object_read_only_collection_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_ordered_read_only_collection_all_null(bool async)
        => Task.CompletedTask;

    public override Task Contains_with_local_read_only_collection_inline(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_read_only_collection_inline(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ABCDE', 'ALFKI']
            """);
        });

    public override Task Contains_with_local_read_only_collection_inline_closure_mix(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_read_only_collection_inline_closure_mix(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_non_primitive_list_inline_closure_mix(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_non_primitive_list_inline_closure_mix(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_non_primitive_list_closure_mix(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_non_primitive_list_closure_mix(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_collection_false(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_false(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE NOT ("customerID" IN [?, ?])
            """);
        });

    public override Task Contains_with_local_collection_complex_predicate_and(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_complex_predicate_and(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE ("customerID" = 'ALFKI' OR "customerID" = 'ABCDE') AND "customerID" IN [?, ?]
            """);
        });

    public override Task Contains_with_local_collection_complex_predicate_or(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_complex_predicate_or(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?] OR "customerID" = 'ALFKI' OR "customerID" = 'ABCDE'
            """);
        });

    public override Task Contains_with_local_collection_complex_predicate_not_matching_ins1(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_complex_predicate_not_matching_ins1(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" = 'ALFKI' OR "customerID" = 'ABCDE' OR NOT ("customerID" IN [?, ?])
            """);
        });

    public override Task Contains_with_local_collection_complex_predicate_not_matching_ins2(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_complex_predicate_not_matching_ins2(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?] AND "customerID" <> 'ALFKI' AND "customerID" <> 'ABCDE'
            """);
        });

    public override Task Contains_with_local_collection_sql_injection(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_sql_injection(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?] OR "customerID" = 'ALFKI' OR "customerID" = 'ABCDE'
            """);
        });

    public override Task Contains_with_local_collection_empty_closure(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Contains_with_local_collection_empty_closure(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE 1 = 0
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_collection_empty_inline(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_top_level(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_tuple_array_closure(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_local_anonymous_type_array_closure(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OfType_Select(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OfType_Select_OfType_Select(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_non_matching_types_in_projection_doesnt_produce_second_explicit_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_with_non_matching_types_in_projection_introduces_explicit_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_with_non_matching_types_in_projection_introduces_explicit_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Take_Last_gives_correct_result(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task OrderBy_Skip_Last_gives_correct_result(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_should_rewrite_to_identity_equality(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_Contains_over_entityType_should_rewrite_to_identity_equality(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_Contains_with_constant_list(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_Contains_with_parameter_list(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_parameter_list_value_type_id(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_constant_list_value_type_id(bool async)
        => Task.CompletedTask;

    public override Task IImmutableSet_Contains_with_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.IImmutableSet_Contains_with_parameter(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task IReadOnlySet_Contains_with_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.IReadOnlySet_Contains_with_parameter(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task HashSet_Contains_with_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.HashSet_Contains_with_parameter(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task ImmutableHashSet_Contains_with_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.ImmutableHashSet_Contains_with_parameter(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?]
            """);
        });

    public override Task Array_cast_to_IEnumerable_Contains_with_constant(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Array_cast_to_IEnumerable_Contains_with_constant(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ALFKI', 'WRONG']
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_keyless_entity_throws(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_with_null_should_rewrite_to_false(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_with_null_in_projection(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_scalar_with_null_should_rewrite_to_identity_equality_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_negated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_complex(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_nullable_scalar_with_null_in_subquery_translated_correctly(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_non_nullable_scalar_with_null_in_subquery_simplifies_to_false(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_should_materialize_when_composite(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_over_entityType_should_materialize_when_composite2(bool async)
        => Task.CompletedTask;

    public override Task String_FirstOrDefault_in_projection_does_not_do_client_eval(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.String_FirstOrDefault_in_projection_does_not_do_client_eval(a);
            AssertSql(
            """
            SELECT "customerID"
            FROM "Customers"
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_constant_Sum(bool async)
        => Task.CompletedTask;

    public override Task Where_subquery_any_equals_operator(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_any_equals_operator(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?, ?]
            """);
        });

    public override Task Where_subquery_any_equals(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_any_equals(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN ['ABCDE', 'ALFKI', 'ANATR']
            """);
        });

    public override Task Where_subquery_any_equals_static(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_any_equals_static(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "customerID" IN [?, ?, ?]
            """);
        });

    public override Task Where_subquery_where_any(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_where_any(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "city" = 'México D.F.' AND "customerID" IN [?, ?, ?]
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "city" = 'México D.F.' AND "customerID" IN [?, ?, ?]
            """);
        });

    public override Task Where_subquery_all_not_equals_operator(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_all_not_equals_operator(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE NOT ("customerID" IN [?, ?, ?])
            """);
        });

    public override Task Where_subquery_all_not_equals(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_all_not_equals(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE NOT ("customerID" IN ['ABCDE', 'ALFKI', 'ANATR'])
            """);
        });

    public override Task Where_subquery_all_not_equals_static(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_all_not_equals_static(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE NOT ("customerID" IN [?, ?, ?])
            """);
        });

    public override Task Where_subquery_where_all(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Where_subquery_where_all(a);
            AssertSql(
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "city" = 'México D.F.' AND NOT ("customerID" IN [?, ?, ?])
            """,
            """
            SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            WHERE "city" = 'México D.F.' AND NOT ("customerID" IN [?, ?, ?])
            """);
        });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Cast_to_same_Type_Count_works(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Cast_before_aggregate_is_preserved(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Enumerable_min_is_mapped_to_Queryable_1(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Enumerable_min_is_mapped_to_Queryable_2(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task DefaultIfEmpty_selects_only_required_columns(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_Last_member_access_in_projection_translated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_LastOrDefault_member_access_in_projection_translated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_explicit_cast_over_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Count_on_projection_with_client_eval(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_with_unmapped_property_access_throws_meaningful_exception(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Sum_over_empty_returns_zero(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_over_default_returns_default(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_over_default_returns_default(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_over_default_returns_default(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_after_DefaultIfEmpty_does_not_throw(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Max_after_DefaultIfEmpty_does_not_throw(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Min_after_DefaultIfEmpty_does_not_throw(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Average_on_nav_subquery_in_projection(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task All_true(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Count_after_client_projection(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Not_Any_false(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_aggregate_function_with_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_Average_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_Sum_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_Count_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_LongCount_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_Max_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_inside_Min_without_GroupBy(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Return_type_of_singular_operator_is_preserved(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Type_casting_inside_sum(bool async)
        => Task.CompletedTask;

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindAggregateOperatorsQueryDynamoTestDefault
        : NorthwindAggregateOperatorsQueryDynamoTest
    {
        public NorthwindAggregateOperatorsQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture)
            : base(fixture)
            => _ = containerFixture;
    }
}
