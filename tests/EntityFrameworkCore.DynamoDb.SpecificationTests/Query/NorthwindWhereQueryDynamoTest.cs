using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind where-query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindWhereQueryDynamoTest
    : NorthwindWhereQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindWhereQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(NorthwindWhereQueryDynamoTest));

    public override Task Where_simple(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = 'London'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_as_queryable_expression(bool async) => Task.CompletedTask;

    public override async Task<string> Where_simple_closure(bool async)
    {
        string? queryString = null;

        await NoSyncTest(
            async,
            async a =>
            {
                queryString = await base.Where_simple_closure(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

        return queryString ?? string.Empty;
    }

    public override Task Where_indexer_closure(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_indexer_closure(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_dictionary_key_access_closure(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_dictionary_key_access_closure(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_tuple_item_closure(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_tuple_item_closure(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_named_tuple_item_closure(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_named_tuple_item_closure(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_simple_closure_constant(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_closure_constant(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE ? = TRUE
                    """);
            });

    public override Task Where_simple_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_method_call_nullable_type_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_method_call_nullable_type_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """);
            });

    public override Task Where_method_call_nullable_type_reverse_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_method_call_nullable_type_reverse_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "employeeID" > ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "employeeID" > ?
                    """);
            });

    public override Task Where_method_call_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_method_call_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_field_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_field_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_property_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_property_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_static_field_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_static_field_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_static_property_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_static_property_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_nested_field_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_nested_field_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_nested_property_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_nested_property_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_nested_field_access_closure_via_query_cache_error_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_nested_field_access_closure_via_query_cache_error_null(a);
                AssertSql();
            });

    public override Task Where_nested_field_access_closure_via_query_cache_error_method_null(
        bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_nested_field_access_closure_via_query_cache_error_method_null(a);
                AssertSql();
            });

    public override Task Where_new_instance_field_access_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_new_instance_field_access_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_new_instance_field_access_closure_via_query_cache(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_new_instance_field_access_closure_via_query_cache(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """,
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = ?
                    """);
            });

    public override Task Where_simple_closure_via_query_cache_nullable_type(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_closure_via_query_cache_nullable_type(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """);
            });

    public override Task Where_simple_closure_via_query_cache_nullable_type_reverse(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_closure_via_query_cache_nullable_type_reverse(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """,
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = ?
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_subquery_closure_via_query_cache(bool async) => Task.CompletedTask;

    public override Task Where_simple_shadow(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_shadow(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "title" = 'Sales Representative'
                    """);
            });

    public override Task Where_simple_shadow_projection(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_shadow_projection(a);
                AssertSql(
                    """
                    SELECT "title"
                    FROM "Employees"
                    WHERE "title" = 'Sales Representative'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_simple_shadow_projection_mixed(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_simple_shadow_subquery(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_shadow_subquery_FirstOrDefault(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_client(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_subquery_correlated(bool async) => Task.CompletedTask;

    public override Task Where_subquery_correlated_client_eval(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_subquery_correlated_client_eval(a);
                AssertSql();
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_client_and_server_top_level(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_client_or_server_top_level(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_client_and_server_non_top_level(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_client_deep_inside_predicate_and_server_top_level(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_method_int(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_using_object_overload_on_mismatched_types(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_using_int_overload_on_mismatched_types(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_on_mismatched_types_nullable_int_long(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_on_mismatched_types_int_nullable_int(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_on_mismatched_types_nullable_long_nullable_int(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_on_matched_nullable_int_types(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_equals_on_null_nullable_int_types(bool async) => Task.CompletedTask;

    public override Task Where_comparison_nullable_type_not_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_comparison_nullable_type_not_null(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" = 2
                    """);
            });

    public override Task Where_comparison_nullable_type_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_comparison_nullable_type_null(a);
                AssertSql(
                    """
                    SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "reportsTo" IS NULL OR "reportsTo" IS MISSING
                    """);
            });

    public override Task Where_simple_reversed(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_reversed(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE 'London' = "city"
                    """);
            });

    public override Task Where_is_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_is_null(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" IS NULL OR "region" IS MISSING
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_null_is_null(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_constant_is_null(bool async) => Task.CompletedTask;

    public override Task Where_is_not_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_is_not_null(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" IS NOT NULL AND "city" IS NOT MISSING
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_null_is_not_null(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_constant_is_not_null(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_identity_comparison(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_in_optimization_multiple(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_not_in_optimization1(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_not_in_optimization2(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_not_in_optimization3(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_not_in_optimization4(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_select_many_and(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_primitive(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_primitive_tracked(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_primitive_tracked2(bool async) => Task.CompletedTask;

    public override Task Where_bool_member(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "discontinued" = TRUE
                    """);
            });

    public override Task Where_bool_member_false(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_false(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE)
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_bool_client_side_negated(bool async) => Task.CompletedTask;

    public override Task Where_bool_member_negated_twice(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_negated_twice(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT (NOT ("discontinued" = TRUE))
                    """);
            });

    public override Task Where_bool_member_shadow(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_shadow(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "discontinued" = TRUE
                    """);
            });

    public override Task Where_bool_member_false_shadow(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_false_shadow(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE)
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_bool_member_equals_constant(bool async) => Task.CompletedTask;

    public override Task Where_bool_member_in_complex_predicate(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_in_complex_predicate(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "productID" > 100 AND "discontinued" = TRUE OR "discontinued" = TRUE
                    """);
            });

    public override Task Where_bool_member_compared_to_binary_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_compared_to_binary_expression(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "discontinued" = ("productID" > 50)
                    """);
            });

    public override Task Where_not_bool_member_compared_to_not_bool_member(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_not_bool_member_compared_to_not_bool_member(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE) = NOT ("discontinued" = TRUE)
                    """);
            });

    public override Task
        Where_negated_boolean_expression_compared_to_another_negated_boolean_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Where_negated_boolean_expression_compared_to_another_negated_boolean_expression(
                        a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("productID" > 50) = NOT ("productID" > 20)
                    """);
            });

    public override Task Where_not_bool_member_compared_to_binary_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_not_bool_member_compared_to_binary_expression(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE) = ("productID" > 50)
                    """);
            });

    public override Task Where_bool_parameter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_parameter(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE ? = TRUE
                    """);
            });

    public override Task Where_bool_parameter_compared_to_binary_expression(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_parameter_compared_to_binary_expression(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE ("productID" > 50) <> ?
                    """);
            });

    public override Task Where_bool_member_and_parameter_compared_to_binary_expression_nested(
        bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_bool_member_and_parameter_compared_to_binary_expression_nested(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "discontinued" = (("productID" > 50) <> ?)
                    """);
            });

    public override Task Where_de_morgan_or_optimized(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_de_morgan_or_optimized(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE OR "productID" < 20)
                    """);
            });

    public override Task Where_de_morgan_and_optimized(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_de_morgan_and_optimized(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT ("discontinued" = TRUE AND "productID" < 20)
                    """);
            });

    public override Task Where_complex_negated_expression_optimized(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_complex_negated_expression_optimized(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE NOT (NOT (NOT ("discontinued" = TRUE) AND "productID" < 60) OR NOT ("productID" > 30))
                    """);
            });

    public override Task Where_short_member_comparison(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_short_member_comparison(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "unitsInStock" > 10
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_comparison_to_nullable_bool(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_true(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_false(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_bool_closure(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_poco_closure(bool async) => Task.CompletedTask;

    public override Task Where_default(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_default(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "fax" IS NULL OR "fax" IS MISSING
                    """);
            });

    public override Task Where_expression_invoke_1(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_expression_invoke_1(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_expression_invoke_2(bool async) => Task.CompletedTask;

    public override Task Where_expression_invoke_3(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_expression_invoke_3(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    public override Task Where_ternary_boolean_condition_true(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_ternary_boolean_condition_true(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "unitsInStock" >= 20
                    """);
            });

    public override Task Where_ternary_boolean_condition_false(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_ternary_boolean_condition_false(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "unitsInStock" < 20
                    """);
            });

    public override Task Where_ternary_boolean_condition_with_another_condition(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_ternary_boolean_condition_with_another_condition(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "productID" < ? AND "unitsInStock" >= 20
                    """);
            });

    public override Task Where_ternary_boolean_condition_with_false_as_result_true(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_ternary_boolean_condition_with_false_as_result_true(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "unitsInStock" >= 20
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_ternary_boolean_condition_with_false_as_result_false(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_ternary_boolean_condition_negated(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_constructed_equal(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_constructed_multi_value_not_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_constructed_equal(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_create_constructed_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_null(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_compare_null(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE ("region" IS NULL OR "region" IS MISSING) AND "country" = 'UK'
                    """);
            });

    public override Task Where_compare_null_with_cast_to_object(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_compare_null_with_cast_to_object(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" IS NULL OR "region" IS MISSING
                    """);
            });

    public override Task Where_compare_with_both_cast_to_object(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_compare_with_both_cast_to_object(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" = 'London'
                    """);
            });

    public override Task Where_projection(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_projection(a);
                AssertSql(
                    """
                    SELECT "companyName"
                    FROM "Customers"
                    WHERE "city" = 'London'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Is_on_same_type(bool async) => Task.CompletedTask;

    public override Task Where_chain(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_chain(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = 'QUICK' AND "orderDate" > '1998-01-01 00:00:00'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_navigation_contains(bool async) => Task.CompletedTask;

    public override Task Where_array_index(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_array_index(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ?
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_multiple_contains_in_subquery_with_or(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_multiple_contains_in_subquery_with_and(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_contains_on_navigation(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_subquery_FirstOrDefault_is_null(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_subquery_FirstOrDefault_compared_to_entity(bool async)
        => Task.CompletedTask;

    public override Task TypeBinary_short_circuit(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.TypeBinary_short_circuit(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE ? = TRUE
                    """);
            });

    public override Task Decimal_cast_to_double_works(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Decimal_cast_to_double_works(a);
                AssertSql(
                    """
                    SELECT "productID", "discontinued", "productName", "supplierID", "unitPrice", "unitsInStock"
                    FROM "Products"
                    WHERE "unitPrice" > 100
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_is_conditional(bool async) => Task.CompletedTask;

    public override Task Enclosing_class_settable_member_generates_parameter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Enclosing_class_settable_member_generates_parameter(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" = ?
                    """,
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" = ?
                    """);
            });

    public override Task Enclosing_class_readonly_member_generates_parameter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Enclosing_class_readonly_member_generates_parameter(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" = ?
                    """);
            });

    public override Task Enclosing_class_const_member_does_not_generate_parameter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Enclosing_class_const_member_does_not_generate_parameter(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" = 10274
                    """);
            });

    public override Task Generic_Ilist_contains_translates_to_server(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Generic_Ilist_contains_translates_to_server(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "city" IN [?]
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Filter_non_nullable_value_after_FirstOrDefault_on_empty_collection(
        bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Using_same_parameter_twice_in_query_generates_one_sql_parameter(bool async)
        => Task.CompletedTask;

    public override Task Two_parameters_with_same_name_get_uniquified(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Two_parameters_with_same_name_get_uniquified(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ? OR "customerID" = ?
                    """);
            });

    public override Task Two_parameters_with_same_case_insensitive_name_get_uniquified(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Two_parameters_with_same_case_insensitive_name_get_uniquified(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ? OR "customerID" = ?
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToList_Count(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToList_Contains(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToArray_Count(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToArray_Contains(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_AsEnumerable_Count(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_AsEnumerable_Contains(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_AsEnumerable_Contains_negated(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToList_Count_member(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_Queryable_ToArray_Length_member(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToList_Count(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToList_Contains(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToArray_Count(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToArray_Contains(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_AsEnumerable_Count(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_AsEnumerable_Contains(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToList_Count_member(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_collection_navigation_ToArray_Length_member(bool async)
        => Task.CompletedTask;

    public override Task Where_list_object_contains_over_value_type(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_list_object_contains_over_value_type(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" IN [?, ?]
                    """);
            });

    public override Task Where_array_of_object_contains_over_value_type(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_array_of_object_contains_over_value_type(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" IN [?, ?]
                    """);
            });

    public override Task Multiple_OrElse_on_same_column_converted_to_in_with_overlap(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Multiple_OrElse_on_same_column_converted_to_in_with_overlap(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI' OR "customerID" = 'ANATR' OR "customerID" = 'ANTON' OR "customerID" = 'ANATR'
                    """);
            });

    public override Task
        Multiple_OrElse_on_same_column_with_null_constant_comparison_converted_to_in(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Multiple_OrElse_on_same_column_with_null_constant_comparison_converted_to_in(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" = 'WA' OR "region" = 'OR' OR "region" IS NULL OR "region" IS MISSING OR "region" = 'BC'
                    """);
            });

    public override Task
        Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN ['ALFKI', 'ANATR'] OR "customerID" = 'ANTON'
                    """);
            });

    public override Task
        Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in_with_overlap(
            bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in_with_overlap(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ANTON' OR "customerID" IN ['ALFKI', 'ANATR'] OR "customerID" = 'ALFKI'
                    """);
            });

    public override Task
        Constant_array_Contains_OrElse_another_Contains_gets_combined_to_one_in_with_overlap(
            bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Constant_array_Contains_OrElse_another_Contains_gets_combined_to_one_in_with_overlap(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN ['ALFKI', 'ANATR'] OR "customerID" IN ['ALFKI', 'ANTON']
                    """);
            });

    public override Task
        Constant_array_Contains_AndAlso_another_Contains_gets_combined_to_one_in_with_overlap(
            bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Constant_array_Contains_AndAlso_another_Contains_gets_combined_to_one_in_with_overlap(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE NOT ("customerID" IN ['ALFKI', 'ANATR']) AND NOT ("customerID" IN ['ALFKI', 'ANTON'])
                    """);
            });

    public override Task Multiple_AndAlso_on_same_column_converted_to_in_using_parameters(
        bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Multiple_AndAlso_on_same_column_converted_to_in_using_parameters(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" <> ? AND "customerID" <> ? AND "customerID" <> ?
                    """);
            });

    public override Task
        Array_of_parameters_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(
            bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Array_of_parameters_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN [?, ?] OR "customerID" = 'ANTON'
                    """);
            });

    public override Task
        Multiple_OrElse_on_same_column_with_null_parameter_comparison_converted_to_in(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Multiple_OrElse_on_same_column_with_null_parameter_comparison_converted_to_in(
                        a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" = 'WA' OR "region" = 'OR' OR "region" = ? OR "region" = 'BC'
                    """);
            });

    public override Task Parameter_array_Contains_OrElse_comparison_with_constant(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Parameter_array_Contains_OrElse_comparison_with_constant(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN [?, ?] OR "customerID" = 'ANTON'
                    """);
            });

    public override Task Parameter_array_Contains_OrElse_comparison_with_parameter_with_overlap(
        bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Parameter_array_Contains_OrElse_comparison_with_parameter_with_overlap(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ? OR "customerID" IN [?, ?] OR "customerID" = ?
                    """);
            });

    public override Task Two_sets_of_comparison_combine_correctly(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Two_sets_of_comparison_combine_correctly(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN ['ALFKI', 'ANATR'] AND ("customerID" = 'ANATR' OR "customerID" = 'ANTON')
                    """);
            });

    public override Task Two_sets_of_comparison_combine_correctly2(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Two_sets_of_comparison_combine_correctly2(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" <> 'WA' AND "region" <> 'OR' AND "region" IS NOT NULL AND "region" IS NOT MISSING OR "region" <> 'WA' AND "region" IS NOT NULL AND "region" IS NOT MISSING
                    """);
            });

    public override Task
        Filter_with_property_compared_to_null_wrapped_in_explicit_convert_to_object(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base
                    .Filter_with_property_compared_to_null_wrapped_in_explicit_convert_to_object(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "region" IS NULL OR "region" IS MISSING
                    """);
            });

    public override Task Filter_with_EF_Property_using_closure_for_property_name(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Filter_with_EF_Property_using_closure_for_property_name(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    public override Task Filter_with_EF_Property_using_function_for_property_name(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Filter_with_EF_Property_using_function_for_property_name(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = 'ALFKI'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_over_scalar_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_over_scalar_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task FirstOrDefault_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SingleOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task SingleOrDefault_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task LastOrDefault_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task First_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task First_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task ElementAt_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task ElementAtOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Single_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Single_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Last_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    public override Task Where_Contains_and_comparison(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_Contains_and_comparison(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN [?, ?, ?] AND "city" = 'Seattle'
                    """);
            });

    public override Task Where_Contains_or_comparison(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_Contains_or_comparison(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" IN [?, ?] OR "city" = 'Seattle'
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task GetType_on_non_hierarchy1(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task GetType_on_non_hierarchy2(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task GetType_on_non_hierarchy3(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task GetType_on_non_hierarchy4(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Case_block_simplification_works_correctly(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task EF_Constant(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task EF_Constant_with_subtree(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task EF_Constant_does_not_parameterized_as_part_of_bigger_subtree(bool async)
        => Task.CompletedTask;

    public override Task EF_Constant_with_non_evaluatable_argument_throws(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.EF_Constant_with_non_evaluatable_argument_throws(a);
                AssertSql();
            });

    public override Task EF_Parameter(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.EF_Parameter(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ?
                    """);
            });

    public override Task EF_Parameter_with_subtree(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.EF_Parameter_with_subtree(a);
                AssertSql(
                    """
                    SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    WHERE "customerID" = ?
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task EF_Parameter_does_not_parameterized_as_part_of_bigger_subtree(bool async)
        => Task.CompletedTask;

    public override Task EF_Parameter_with_non_evaluatable_argument_throws(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.EF_Parameter_with_non_evaluatable_argument_throws(a);
                AssertSql();
            });

    public override Task Implicit_cast_in_predicate(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Implicit_cast_in_predicate(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = '1337'
                    """,
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = ?
                    """,
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = ?
                    """,
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = ?
                    """,
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "customerID" = '1337'
                    """);
            });

    public override Task Interface_casting_though_generic_method(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Interface_casting_though_generic_method(a);
                AssertSql(
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    WHERE "orderID" = ?
                    """,
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    WHERE "orderID" = 10252
                    """,
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    WHERE "orderID" = 10252
                    """,
                    """
                    SELECT "orderID"
                    FROM "Orders"
                    WHERE "orderID" = 10252
                    """);
            });

    public override Task Simplifiable_coalesce_over_nullable(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Simplifiable_coalesce_over_nullable(a);
                AssertSql(
                    """
                    SELECT "orderID", "customerID", "employeeID", "orderDate"
                    FROM "Orders"
                    WHERE "orderID" = ?
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Take_and_Where_evaluation_order(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Skip_and_Where_evaluation_order(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Take_and_Distinct_evaluation_order(bool async) => Task.CompletedTask;

#if !NET10_0
    [ConditionalTheory(Skip = SkipReason.SubqueryContainsNotSupported)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public override Task Where_Enumerable_conditional_null_check_with_Contains(bool async, bool withNull)
        => base.Where_Enumerable_conditional_null_check_with_Contains(async, withNull);

    [ConditionalTheory(Skip = SkipReason.SubqueryContainsNotSupported)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public override Task Where_Enumerable_conditional_not_null_check_with_Contains(bool async, bool withNull)
        => base.Where_Enumerable_conditional_not_null_check_with_Contains(async, withNull);

    [ConditionalTheory(Skip = SkipReason.SubqueryContainsNotSupported)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public override Task Where_Queryable_conditional_null_check_with_Contains(bool async, bool withNull)
        => base.Where_Queryable_conditional_null_check_with_Contains(async, withNull);

    [ConditionalTheory(Skip = SkipReason.SubqueryContainsNotSupported)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public override Task Where_Queryable_conditional_not_null_check_with_Contains(bool async, bool withNull)
        => base.Where_Queryable_conditional_not_null_check_with_Contains(async, withNull);
#endif

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindWhereQueryDynamoTestDefault : NorthwindWhereQueryDynamoTest
    {
        public NorthwindWhereQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
