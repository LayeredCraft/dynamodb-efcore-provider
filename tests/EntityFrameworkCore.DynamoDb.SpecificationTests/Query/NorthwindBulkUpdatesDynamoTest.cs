using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>
///     Northwind bulk-update specification tests for the DynamoDB provider. Nearly all inherited
///     cases target multi-row sources, joins, navigations, set operations, or ExecuteDelete, which
///     are outside the singleton key-targeted ExecuteUpdate scope; see the gap analysis in
///     docs/spec-test-coverage.md.
/// </summary>
public abstract class NorthwindBulkUpdatesDynamoTest
    : NorthwindBulkUpdatesTestBase<NorthwindBulkUpdatesDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindBulkUpdatesDynamoTest(
        NorthwindBulkUpdatesDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(NorthwindBulkUpdatesDynamoTest));

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_non_entity_projection(bool async)
        => base.Delete_non_entity_projection(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_non_entity_projection_2(bool async)
        => base.Delete_non_entity_projection_2(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_non_entity_projection_3(bool async)
        => base.Delete_non_entity_projection_3(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_without_property_to_set_throws(bool async)
        => base.Update_without_property_to_set_throws(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_with_invalid_lambda_in_set_property_throws(bool async)
        => base.Update_with_invalid_lambda_in_set_property_throws(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_multiple_tables_throws(bool async)
        => base.Update_multiple_tables_throws(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_unmapped_property_throws(bool async)
        => base.Update_unmapped_property_throws(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_TagWith(bool async) => base.Delete_Where_TagWith(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where(bool async) => base.Delete_Where(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_parameter(bool async) => base.Delete_Where_parameter(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_OrderBy(bool async) => base.Delete_Where_OrderBy(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_OrderBy_Skip(bool async)
        => base.Delete_Where_OrderBy_Skip(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_OrderBy_Take(bool async)
        => base.Delete_Where_OrderBy_Take(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_OrderBy_Skip_Take(bool async)
        => base.Delete_Where_OrderBy_Skip_Take(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_Skip(bool async) => base.Delete_Where_Skip(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_Take(bool async) => base.Delete_Where_Take(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_Skip_Take(bool async) => base.Delete_Where_Skip_Take(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_predicate_with_GroupBy_aggregate(bool async)
        => base.Delete_Where_predicate_with_GroupBy_aggregate(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_predicate_with_GroupBy_aggregate_2(bool async)
        => base.Delete_Where_predicate_with_GroupBy_aggregate_2(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_GroupBy_Where_Select(bool async)
        => base.Delete_GroupBy_Where_Select(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_GroupBy_Where_Select_2(bool async)
        => base.Delete_GroupBy_Where_Select_2(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_Skip_Take_Skip_Take_causing_subquery(bool async)
        => base.Delete_Where_Skip_Take_Skip_Take_causing_subquery(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_Distinct(bool async) => base.Delete_Where_Distinct(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_SelectMany(bool async) => base.Delete_SelectMany(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_SelectMany_subquery(bool async)
        => base.Delete_SelectMany_subquery(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_using_navigation(bool async)
        => base.Delete_Where_using_navigation(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_using_navigation_2(bool async)
        => base.Delete_Where_using_navigation_2(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Union(bool async) => base.Delete_Union(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Concat(bool async) => base.Delete_Concat(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Intersect(bool async) => base.Delete_Intersect(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Except(bool async) => base.Delete_Except(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_Where_optional_navigation_predicate(bool async)
        => base.Delete_Where_optional_navigation_predicate(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_join(bool async) => base.Delete_with_join(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_LeftJoin(bool async) => base.Delete_with_LeftJoin(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_LeftJoin_via_flattened_GroupJoin(bool async)
        => base.Delete_with_LeftJoin_via_flattened_GroupJoin(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_cross_join(bool async) => base.Delete_with_cross_join(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_cross_apply(bool async) => base.Delete_with_cross_apply(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_outer_apply(bool async) => base.Delete_with_outer_apply(async);

    [ConditionalTheory(Skip = SkipReason.ExecuteDeleteNotImplemented)]
    public override Task Delete_with_RightJoin(bool async) => base.Delete_with_RightJoin(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_constant_TagWith(bool async)
        => base.Update_Where_set_constant_TagWith(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_constant(bool async)
        => base.Update_Where_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_constant_via_lambda(bool async)
        => base.Update_Where_set_constant_via_lambda(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_nullable_int_constant_via_discard_lambda(bool async)
        => base.Update_Where_set_nullable_int_constant_via_discard_lambda(async);

    // The query shape itself (single key-targeted item) is supported, but the upstream bulk-update
    // assertion harness always opens an explicit transaction, which DynamoDB does not support.
    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Update_Where_parameter_set_constant(bool async)
        => base.Update_Where_parameter_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_parameter(bool async)
        => base.Update_Where_set_parameter(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_parameter_from_closure_array(bool async)
        => base.Update_Where_set_parameter_from_closure_array(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_parameter_from_inline_list(bool async)
        => base.Update_Where_set_parameter_from_inline_list(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_parameter_from_multilevel_property_access(bool async)
        => base.Update_Where_set_parameter_from_multilevel_property_access(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_Skip_set_constant(bool async)
        => base.Update_Where_Skip_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_Take_set_constant(bool async)
        => base.Update_Where_Take_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_Skip_Take_set_constant(bool async)
        => base.Update_Where_Skip_Take_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_OrderBy_set_constant(bool async)
        => base.Update_Where_OrderBy_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_OrderBy_Skip_set_constant(bool async)
        => base.Update_Where_OrderBy_Skip_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_OrderBy_Take_set_constant(bool async)
        => base.Update_Where_OrderBy_Take_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_OrderBy_Skip_Take_set_constant(bool async)
        => base.Update_Where_OrderBy_Skip_Take_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_OrderBy_Skip_Take_Skip_Take_set_constant(bool async)
        => base.Update_Where_OrderBy_Skip_Take_Skip_Take_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task Update_Where_GroupBy_aggregate_set_constant(bool async)
        => base.Update_Where_GroupBy_aggregate_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task Update_Where_GroupBy_First_set_constant(bool async)
        => base.Update_Where_GroupBy_First_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task Update_Where_GroupBy_First_set_constant_2(bool async)
        => base.Update_Where_GroupBy_First_set_constant_2(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task Update_Where_GroupBy_First_set_constant_3(bool async)
        => base.Update_Where_GroupBy_First_set_constant_3(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_Distinct_set_constant(bool async)
        => base.Update_Where_Distinct_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_using_navigation_set_null(bool async)
        => base.Update_Where_using_navigation_set_null(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_using_navigation_2_set_constant(bool async)
        => base.Update_Where_using_navigation_2_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_SelectMany_set_null(bool async)
        => base.Update_Where_SelectMany_set_null(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_property_plus_constant(bool async)
        => base.Update_Where_set_property_plus_constant(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_property_plus_parameter(bool async)
        => base.Update_Where_set_property_plus_parameter(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_property_plus_property(bool async)
        => base.Update_Where_set_property_plus_property(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_constant_using_ef_property(bool async)
        => base.Update_Where_set_constant_using_ef_property(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_set_null(bool async) => base.Update_Where_set_null(async);

    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_Where_multiple_set(bool async)
        => base.Update_Where_multiple_set(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Update_Union_set_constant(bool async)
        => base.Update_Union_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Update_Concat_set_constant(bool async)
        => base.Update_Concat_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Update_Except_set_constant(bool async)
        => base.Update_Except_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Update_Intersect_set_constant(bool async)
        => base.Update_Intersect_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_join_set_constant(bool async)
        => base.Update_with_join_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_LeftJoin(bool async) => base.Update_with_LeftJoin(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_LeftJoin_via_flattened_GroupJoin(bool async)
        => base.Update_with_LeftJoin_via_flattened_GroupJoin(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_RightJoin(bool async) => base.Update_with_RightJoin(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_cross_join_set_constant(bool async)
        => base.Update_with_cross_join_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_cross_apply_set_constant(bool async)
        => base.Update_with_cross_apply_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_outer_apply_set_constant(bool async)
        => base.Update_with_outer_apply_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_cross_join_left_join_set_constant(bool async)
        => base.Update_with_cross_join_left_join_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_cross_join_cross_apply_set_constant(bool async)
        => base.Update_with_cross_join_cross_apply_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_cross_join_outer_apply_set_constant(bool async)
        => base.Update_with_cross_join_outer_apply_set_constant(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_SelectMany_subquery_set_null(bool async)
        => base.Update_Where_SelectMany_subquery_set_null(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_Join_set_property_from_joined_single_result_table(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_table(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_Join_set_property_from_joined_table(bool async)
        => base.Update_Where_Join_set_property_from_joined_table(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_Where_Join_set_property_from_joined_single_result_scalar(bool async)
        => base.Update_Where_Join_set_property_from_joined_single_result_scalar(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_two_inner_joins(bool async)
        => base.Update_with_two_inner_joins(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Update_with_PK_pushdown_and_join_and_multiple_setters(bool async)
        => base.Update_with_PK_pushdown_and_join_and_multiple_setters(async);

    // EF11-only upstream test; multi-row source, so the key-targeting rejection applies.
#if NET11_0
    [ConditionalTheory(Skip = SkipReason.BulkUpdateRequiresKeyTargetedSingleton)]
    public override Task Update_set_constant_TagWith_null(bool async)
        => base.Update_set_constant_TagWith_null(async);
#endif

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindBulkUpdatesDynamoTestDefault : NorthwindBulkUpdatesDynamoTest
    {
        public NorthwindBulkUpdatesDynamoTestDefault(
            NorthwindBulkUpdatesDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
