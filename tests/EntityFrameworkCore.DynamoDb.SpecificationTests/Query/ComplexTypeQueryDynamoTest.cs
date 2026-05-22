using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.ComplexTypeModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Complex type query specification tests for the DynamoDB provider.</summary>
public abstract class ComplexTypeQueryDynamoTest
    : ComplexTypeQueryTestBase<ComplexTypeQueryDynamoTest.ComplexTypeQueryDynamoFixture>
{
    protected ComplexTypeQueryDynamoTest(ComplexTypeQueryDynamoFixture fixture)
        : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(ComplexTypeQueryDynamoTest));

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Filter_on_property_inside_complex_type_after_subquery(bool async)
        => base.Filter_on_property_inside_complex_type_after_subquery(async);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Filter_on_property_inside_nested_complex_type_after_subquery(bool async)
        => base.Filter_on_property_inside_nested_complex_type_after_subquery(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_optional_navigation(bool async)
        => base.Filter_on_required_property_inside_required_complex_type_on_optional_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_required_navigation(bool async)
        => base.Filter_on_required_property_inside_required_complex_type_on_required_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_complex_type_via_optional_navigation(bool async)
        => base.Project_complex_type_via_optional_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_complex_type_via_required_navigation(bool async)
        => base.Project_complex_type_via_required_navigation(async);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Load_complex_type_after_subquery_on_entity_type(bool async)
        => base.Load_complex_type_after_subquery_on_entity_type(async);

    public override Task Select_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "Customers"
            """);
        });

    public override Task Select_nested_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_nested_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "Customers"
            """);
        });

    public override Task Select_single_property_on_nested_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_single_property_on_nested_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "Customers"
            """);
        });

    public override Task Select_complex_type_Where(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_complex_type_Where(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "Customers"
            WHERE "shippingAddress"."zipCode" = 7728
            """);
        });

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Select_complex_type_Distinct(bool async)
        => base.Select_complex_type_Distinct(async);

    public override Task Complex_type_equals_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Complex_type_equals_complex_type(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "optionalAddress", "shippingAddress"
            FROM "Customers"
            WHERE "shippingAddress" = "billingAddress"
            """);
        });

    public override Task Complex_type_equals_constant(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Complex_type_equals_constant(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "optionalAddress", "shippingAddress"
            FROM "Customers"
            WHERE "shippingAddress" = ?
            """);
        });

    public override Task Complex_type_equals_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Complex_type_equals_parameter(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "optionalAddress", "shippingAddress"
            FROM "Customers"
            WHERE "shippingAddress" = ?
            """);
        });

    [ConditionalTheory(Skip = SkipReason.ComplexTypeSubqueriesNotSupported)]
    public override Task Subquery_over_complex_type(bool async)
        => base.Subquery_over_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.ComplexTypeSubqueriesNotSupported)]
    public override Task Contains_over_complex_type(bool async)
        => base.Contains_over_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_entity_type_containing_complex_property(bool async)
        => base.Concat_entity_type_containing_complex_property(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_entity_type_containing_complex_property(bool async)
        => base.Union_entity_type_containing_complex_property(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_complex_type(bool async)
        => base.Concat_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_complex_type(bool async)
        => base.Union_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_property_in_complex_type(bool async)
        => base.Concat_property_in_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_property_in_complex_type(bool async)
        => base.Union_property_in_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_two_different_complex_type(bool async)
        => base.Concat_two_different_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_two_different_complex_type(bool async)
        => base.Union_two_different_complex_type(async);

    public override Task Filter_on_property_inside_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Filter_on_property_inside_struct_complex_type(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress"."zipCode" = 7728
            """);
        });

    public override Task Filter_on_property_inside_nested_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Filter_on_property_inside_nested_struct_complex_type(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress"."country"."code" = 'DE'
            """);
        });

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Filter_on_property_inside_struct_complex_type_after_subquery(bool async)
        => base.Filter_on_property_inside_struct_complex_type_after_subquery(async);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Filter_on_property_inside_nested_struct_complex_type_after_subquery(bool async)
        => base.Filter_on_property_inside_nested_struct_complex_type_after_subquery(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(bool async)
        => base.Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(bool async)
        => base.Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_struct_complex_type_via_optional_navigation(bool async)
        => base.Project_struct_complex_type_via_optional_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_nullable_struct_complex_type_via_optional_navigation(bool async)
        => base.Project_nullable_struct_complex_type_via_optional_navigation(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_struct_complex_type_via_required_navigation(bool async)
        => base.Project_struct_complex_type_via_required_navigation(async);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Load_struct_complex_type_after_subquery_on_entity_type(bool async)
        => base.Load_struct_complex_type_after_subquery_on_entity_type(async);

    public override Task Select_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_struct_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "ValuedCustomers"
            """);
        });

    public override Task Select_nested_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_nested_struct_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "ValuedCustomers"
            """);
        });

    public override Task Select_single_property_on_nested_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_single_property_on_nested_struct_complex_type(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "ValuedCustomers"
            """);
        });

    public override Task Select_struct_complex_type_Where(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Select_struct_complex_type_Where(a);
            AssertSql(
            """
            SELECT "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress"."zipCode" = 7728
            """);
        });

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Select_struct_complex_type_Distinct(bool async)
        => base.Select_struct_complex_type_Distinct(async);

    public override Task Struct_complex_type_equals_struct_complex_type(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Struct_complex_type_equals_struct_complex_type(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress" = "billingAddress"
            """);
        });

    public override Task Struct_complex_type_equals_constant(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Struct_complex_type_equals_constant(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress" = ?
            """);
        });

    public override Task Struct_complex_type_equals_parameter(bool async)
        => NoSyncTest(async, async a =>
        {
            await base.Struct_complex_type_equals_parameter(a);
            AssertSql(
            """
            SELECT "id", "name", "billingAddress", "shippingAddress"
            FROM "ValuedCustomers"
            WHERE "shippingAddress" = ?
            """);
        });

    [ConditionalTheory(Skip = SkipReason.ComplexTypeSubqueriesNotSupported)]
    public override Task Subquery_over_struct_complex_type(bool async)
        => base.Subquery_over_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.ComplexTypeSubqueriesNotSupported)]
    public override Task Contains_over_struct_complex_type(bool async)
        => base.Contains_over_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_entity_type_containing_struct_complex_property(bool async)
        => base.Concat_entity_type_containing_struct_complex_property(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_entity_type_containing_struct_complex_property(bool async)
        => base.Union_entity_type_containing_struct_complex_property(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_struct_complex_type(bool async)
        => base.Concat_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_struct_complex_type(bool async)
        => base.Union_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_property_in_struct_complex_type(bool async)
        => base.Concat_property_in_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_property_in_struct_complex_type(bool async)
        => base.Union_property_in_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Concat_two_different_struct_complex_type(bool async)
        => base.Concat_two_different_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_two_different_struct_complex_type(bool async)
        => base.Union_two_different_struct_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_nested_complex_type_twice_with_pushdown(bool async)
        => base.Project_same_nested_complex_type_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_pushdown(bool async)
        => base.Project_same_entity_with_nested_complex_type_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_nested_complex_type_twice_with_double_pushdown(bool async)
        => base.Project_same_nested_complex_type_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(bool async)
        => base.Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_struct_nested_complex_type_twice_with_pushdown(bool async)
        => base.Project_same_struct_nested_complex_type_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(bool async)
        => base.Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => base.Project_same_struct_nested_complex_type_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => base.Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(bool async)
        => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_pushdown(bool async)
        => base.Union_of_same_nested_complex_type_projected_twice_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => base.Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => base.Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async);

    // Skipped upstream in EF Core (issue #31376) and also unsupported by DynamoDB JOIN-style translation.
    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => base.Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task GroupBy_over_property_in_nested_complex_type(bool async)
        => base.GroupBy_over_property_in_nested_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task GroupBy_over_complex_type(bool async)
        => base.GroupBy_over_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task GroupBy_over_nested_complex_type(bool async)
        => base.GroupBy_over_nested_complex_type(async);

    [ConditionalTheory(Skip = SkipReason.GroupByNotSupported)]
    public override Task Entity_with_complex_type_with_group_by_and_first(bool async)
        => base.Entity_with_complex_type_with_group_by_and_first(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Projecting_property_of_complex_type_using_left_join_with_pushdown(bool async)
        => base.Projecting_property_of_complex_type_using_left_join_with_pushdown(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Projecting_complex_from_optional_navigation_using_conditional(bool async)
        => base.Projecting_complex_from_optional_navigation_using_conditional(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Project_entity_with_complex_type_pushdown_and_then_left_join(bool async)
        => base.Project_entity_with_complex_type_pushdown_and_then_left_join(async);

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(bool async, Func<bool, Task> testCode)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode);

    public class ComplexTypeQueryDynamoFixture : ComplexTypeQueryFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.ManyServiceProvidersCreatedWarning,
                    CoreEventId.MappedNavigationIgnoredWarning,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override Task SeedAsync(PoolableDbContext context)
        {
            var data = new ComplexTypeData();
            context.AddRange(data.Set<Customer>());
            context.AddRange(data.Set<ValuedCustomer>());
            return context.SaveChangesAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            // Do not call the base fixture mapping: it configures navigation-heavy group entities
            // that DynamoDB cannot map. This fixture maps only supported root document entities.
            modelBuilder.Ignore<CustomerGroup>();
            modelBuilder.Ignore<ValuedCustomerGroup>();

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers").HasPartitionKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.ComplexProperty(e => e.ShippingAddress, address =>
                    address.ComplexProperty(e => e.Country));
                entity.ComplexProperty(e => e.BillingAddress, address =>
                    address.ComplexProperty(e => e.Country));
                entity.ComplexProperty(e => e.OptionalAddress, address =>
                    address.ComplexProperty(e => e.Country));
            });

            modelBuilder.Entity<ValuedCustomer>(entity =>
            {
                entity.ToTable("ValuedCustomers").HasPartitionKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever();
                entity.ComplexProperty(e => e.ShippingAddress, address =>
                    address.ComplexProperty(e => e.Country));
                entity.ComplexProperty(e => e.BillingAddress, address =>
                    address.ComplexProperty(e => e.Country));
            });
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class ComplexTypeQueryDynamoTestDefault : ComplexTypeQueryDynamoTest
    {
        public ComplexTypeQueryDynamoTestDefault(
            ComplexTypeQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture)
            : base(fixture)
            => _ = containerFixture;
    }
}
