using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind select-query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindSelectQueryDynamoTest
    : NorthwindSelectQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindSelectQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(NorthwindSelectQueryDynamoTest));

    public override Task Select_into(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_into(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    public override Task Projection_when_arithmetic_expression_precedence(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Projection_when_arithmetic_expression_precedence(a);
                AssertSql(
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    """);
            });

    public override Task Projection_when_arithmetic_expressions(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Projection_when_arithmetic_expressions(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_when_arithmetic_mixed(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_when_arithmetic_mixed_subqueries(bool async)
        => Task.CompletedTask;

    public override Task Projection_when_null_value(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Projection_when_null_value(a);
                AssertSql(
                    """
                    SELECT "region"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_when_client_evald_subquery(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_to_object_array(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_of_entity_type_into_object_array(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_of_multiple_entity_types_into_object_array(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_of_entity_type_into_object_list(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_to_int_array(bool async) => Task.CompletedTask;

    public override Task Select_bool_closure(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_bool_closure(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """,
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_bool_closure_with_order_by_property_with_cast_to_nullable(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_bool_closure_with_order_parameter_with_cast_to_nullable(bool async)
        => Task.CompletedTask;

    public override Task Select_scalar(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_scalar(a);
                AssertSql(
                    """
                    SELECT "city"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_one(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_one(a);
                AssertSql(
                    """
                    SELECT "city"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_two(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_two(a);
                AssertSql(
                    """
                    SELECT "city", "phone"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_three(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_three(a);
                AssertSql(
                    """
                    SELECT "city", "phone", "country"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_bool_constant_true(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_bool_constant_true(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_constant_in_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_constant_in_expression(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_conditional_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_conditional_expression(a);
                AssertSql(
                    """
                    SELECT "productID", "unitsInStock"
                    FROM "Products"
                    """);
            });

    public override Task Select_customer_table(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_customer_table(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    """);
            });

    public override Task Select_customer_identity(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_customer_identity(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_anonymous_with_object(bool async) => Task.CompletedTask;

    public override Task Select_anonymous_nested(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_nested(a);
                AssertSql(
                    """
                    SELECT "city", "country"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_empty(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_empty(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_anonymous_literal(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_anonymous_literal(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_constant_int(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_constant_int(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_constant_null_string(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_constant_null_string(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_local(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_local(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_scalar_primitive(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_scalar_primitive(a);
                AssertSql(
                    """
                    SELECT "employeeID"
                    FROM "Employees"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_scalar_primitive_after_take(bool async) => Task.CompletedTask;

    public override Task Select_project_filter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_project_filter(a);
                AssertSql(
                    """
                    SELECT "companyName"
                    FROM "Customers"
                    WHERE "city" = 'London'
                    """);
            });

    public override Task Select_project_filter2(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_project_filter2(a);
                AssertSql(
                    """
                    SELECT "city"
                    FROM "Customers"
                    WHERE "city" = 'London'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level2(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level3(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level4(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level5(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_multi_level6(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_count_using_anonymous_type(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_deep(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
        => Task.CompletedTask;

    public override Task New_date_time_in_anonymous_type_works(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.New_date_time_in_anonymous_type_works(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    WHERE begins_with("customerID", 'A')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Select_non_matching_value_types_int_to_long_introduces_explicit_cast(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_nullable_int_to_long_introduces_explicit_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_nullable_int_to_int_doesnt_introduce_explicit_cast(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_int_to_nullable_int_doesnt_introduce_explicit_cast(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_from_binary_expression_introduces_explicit_cast(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_from_binary_expression_nested_introduces_top_level_explicit_cast(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast1(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast2(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Select_non_matching_value_types_from_length_introduces_explicit_cast(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Select_non_matching_value_types_from_method_call_introduces_explicit_cast(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task
        Select_non_matching_value_types_from_anonymous_type_introduces_explicit_cast(bool async)
        => Task.CompletedTask;

    public override Task Select_conditional_with_null_comparison_in_test(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_conditional_with_null_comparison_in_test(a);
                AssertSql(
                    """
                    SELECT "customerID", "orderID"
                    FROM "Orders"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    public override Task Select_over_10_nested_ternary_condition(bool isAsync)
        => NoSyncTest(
            isAsync,
            async a =>
            {
                await base.Select_over_10_nested_ternary_condition(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_conditional_drops_false(bool async) => Task.CompletedTask;

    public override Task Select_conditional_terminates_at_true(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_conditional_terminates_at_true(a);
                AssertSql(
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_conditional_flatten_nested_results(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_conditional_flatten_nested_tests(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_in_a_subquery_should_be_liftable(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_changes_asc_order_to_desc(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_changes_desc_order_to_asc(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_without_explicit_ordering(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_after_multiple_orderbys(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_after_orderby_thenby(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_subquery_via_pushdown(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_after_orderBy_and_take(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_join_outer(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_join_outer_with_take(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_join_inner(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_join_inner_with_skip(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_SelectMany(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_SelectMany_with_Take(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_projection_subquery(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_projection_subquery_single_result(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Reverse_in_projection_scalar_subquery(bool async) => Task.CompletedTask;

    public override Task Projection_containing_DateTime_subtraction(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Projection_containing_DateTime_subtraction(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    WHERE "orderID" < 10300
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Take_OrderBy_and_FirstOrDefault(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Skip_and_FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault_followed_by_projecting_length(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Take_and_SingleOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault_with_parameter(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_followed_by_projection_of_length_property(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_2(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault_2(
            bool async)
        => Task.CompletedTask;

    public override Task Select_datetime_year_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_year_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_month_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_month_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_day_of_year_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_day_of_year_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_day_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_day_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_hour_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_hour_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_minute_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_minute_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_second_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_second_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_millisecond_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_millisecond_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_DayOfWeek_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_DayOfWeek_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_Ticks_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_Ticks_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_datetime_TimeOfDay_component(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_datetime_TimeOfDay_component(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    public override Task Select_byte_constant(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_byte_constant(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_short_constant(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_short_constant(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Select_bool_constant(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_bool_constant(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    """);
            });

    public override Task Anonymous_projection_AsNoTracking_Selector(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Anonymous_projection_AsNoTracking_Selector(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Anonymous_projection_with_repeated_property_being_ordered(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Anonymous_projection_with_repeated_property_being_ordered_2(bool async)
        => Task.CompletedTask;

    public override Task Select_GetValueOrDefault_on_DateTime(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Select_GetValueOrDefault_on_DateTime(a);
                AssertSql(
                    """
                    SELECT "orderDate"
                    FROM "Orders"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_GetValueOrDefault_on_DateTime_with_null_values(bool async)
        => Task.CompletedTask;

    public override Task Cast_on_top_level_projection_brings_explicit_Cast(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Cast_on_top_level_projection_brings_explicit_Cast(a);
                AssertSql(
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    """);
            });

    public override Task Client_method_in_projection_requiring_materialization_1(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Client_method_in_projection_requiring_materialization_1(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE begins_with("customerID", 'A')
                    """);
            });

    public override Task Client_method_in_projection_requiring_materialization_2(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Client_method_in_projection_requiring_materialization_2(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE begins_with("customerID", 'A')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_nullable_struct(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Multiple_select_many_with_predicate(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_without_result_selector_naked_collection_navigation(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_without_result_selector_collection_navigation_composed(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_1(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_2(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_3(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_4(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_5(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_6(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_correlated_with_outer_7(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_with_multiple_Take(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_with_nested_DefaultIfEmpty(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_with_multiple_Take(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_over_empty_collection_of_value_type_returns_correct_results(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_non_nullable_value_after_FirstOrDefault_on_empty_collection(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Member_binding_after_ctor_arguments_fails_with_client_eval(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Filtered_collection_projection_is_tracked(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Filtered_collection_projection_with_to_list_is_tracked(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_with_complex_expression_that_can_be_funcletized(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_chained_entity_navigation_doesnt_materialize_intermittent_entities(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_entity_compared_to_null(bool async) => Task.CompletedTask;

    public override Task Explicit_cast_in_arithmetic_operation_is_preserved(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Explicit_cast_in_arithmetic_operation_is_preserved(a);
                AssertSql(
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    WHERE "orderID" = 10250
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SelectMany_whose_selector_references_outer_source(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_FirstOrDefault_with_entity_equality_check_in_projection(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_FirstOrDefault_with_nullable_unsigned_int_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task ToList_Count_in_projection_works(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault_member_access_in_projection_translates_to_server(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_with_parameterized_constructor(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_with_parameterized_constructor_with_member_assignment(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_projection_AsNoTracking_OrderBy(bool async)
        => Task.CompletedTask;

    public override Task Coalesce_over_nullable_uint(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Coalesce_over_nullable_uint(a);
                AssertSql(
                    """
                    SELECT "employeeID"
                    FROM "Orders"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_uint_through_collection_FirstOrDefault(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Project_keyless_entity_FirstOrDefault_without_orderby(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_AsEnumerable_projection(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_custom_type_in_both_sides_of_ternary(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_multiple_collection_with_same_constant_works(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_after_navigation_and_distinct(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Correlated_collection_after_distinct_not_containing_original_identifier(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Custom_projection_reference_navigation_PK_to_FK_optimization(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Projecting_Length_of_a_string_property_after_FirstOrDefault_on_correlated_collection(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_count_of_navigation_which_is_generic_list(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_count_of_navigation_which_is_generic_collection(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projecting_count_of_navigation_which_is_generic_collection_using_convert(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_take_projection_doesnt_project_intermittent_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Do_not_erase_projection_mapping_when_adding_single_projection(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_skip_projection_doesnt_project_intermittent_column(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Projection_Distinct_projection_preserves_columns_used_for_distinct_in_subquery(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Projection_take_predicate_projection(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Ternary_in_client_eval_assigns_correct_types(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task VisitLambda_should_not_be_visited_trivially(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(
            bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_include_over_result_of_single_non_scalar(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Take_on_correlated_collection_in_first(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Client_projection_via_ctor_arguments(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Client_projection_with_string_initialization_with_scalar_subquery(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task MemberInit_in_projection_without_arguments(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_of_list_of_anonymous_type(bool async) => Task.CompletedTask;

    public override Task Using_enumerable_parameter_in_projection(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Using_enumerable_parameter_in_projection(a);
                AssertSql(
                    """
                    SELECT "customerID"
                    FROM "Customers"
                    WHERE begins_with("customerID", 'F')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_from_result_of_single_result(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_from_result_of_single_result_2(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task List_from_result_of_single_result_3(bool async) => Task.CompletedTask;

    public override Task Entity_passed_to_DTO_constructor_works(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Entity_passed_to_DTO_constructor_works(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Set_operation_in_pending_collection(bool async) => Task.CompletedTask;

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindSelectQueryDynamoTestDefault : NorthwindSelectQueryDynamoTest
    {
        public NorthwindSelectQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
