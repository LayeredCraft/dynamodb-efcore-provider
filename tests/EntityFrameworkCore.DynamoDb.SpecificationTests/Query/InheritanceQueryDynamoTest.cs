#if NET10_0
using Microsoft.EntityFrameworkCore.Query;
#else
using Microsoft.EntityFrameworkCore.Query.Inheritance;
#endif
using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.InheritanceModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Inheritance query specification tests for the DynamoDB provider.</summary>
public abstract class InheritanceQueryDynamoTest
    : InheritanceQueryTestBase<InheritanceQueryDynamoTest.InheritanceQueryDynamoFixture>
{
    protected InheritanceQueryDynamoTest(InheritanceQueryDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(InheritanceQueryDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_when_shared_column(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_when_shared_column(a));

    public override Task Can_query_all_types_when_shared_column(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_query_all_types_when_shared_column(a);
                AssertSql(
#if NET11_0_OR_GREATER
                    """
                    SELECT "id", "discriminator", "sortIndex", "caffeineGrams", "carbonation", "ints", "sugarGrams", "hasMilk", "complexTypeCollection", "parentComplexType", "childComplexType"
                    FROM "Drinks"
                    WHERE ("discriminator" = 0 OR "discriminator" = 1 OR "discriminator" = 2 OR "discriminator" = 3)
                    """
#else
                    """
                    SELECT "id", "discriminator", "sortIndex", "caffeineGrams", "carbonation", "sugarGrams", "hasMilk"
                    FROM "Drinks"
                    WHERE ("discriminator" = 0 OR "discriminator" = 1 OR "discriminator" = 2 OR "discriminator" = 3)
                    """
#endif
                );
            });

#if NET11_0_OR_GREATER
    public override Task Primitive_collection_on_subtype(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Primitive_collection_on_subtype(a);
                AssertSql(
                    """
                    SELECT "id", "discriminator", "sortIndex", "caffeineGrams", "carbonation", "ints", "sugarGrams", "hasMilk", "complexTypeCollection", "parentComplexType", "childComplexType"
                    FROM "Drinks"
                    WHERE "ints" IS NOT MISSING AND ("discriminator" = 0 OR "discriminator" = 1 OR "discriminator" = 2 OR "discriminator" = 3)
                    """);
            });
#endif

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_animal(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_animal(a));

    public override Task Can_use_is_kiwi(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_is_kiwi(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Can_use_is_kiwi_with_cast(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_is_kiwi_with_cast(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Can_use_backwards_is_animal(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_backwards_is_animal(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "foundOn"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi') AND "discriminator" = 'Kiwi'
                    """);
            });

    public override Task Can_use_is_kiwi_with_other_predicate(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_is_kiwi_with_other_predicate(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND "countryId" = 1 AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Can_use_is_kiwi_in_projection(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_is_kiwi_in_projection(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_bird(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird_predicate(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Can_use_of_type_bird_predicate(a));

    public override Task Can_use_of_type_bird_with_projection(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_of_type_bird_with_projection(a);
                AssertSql(
                    """
                    SELECT "EagleId"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi') AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_use_of_type_bird_first(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_use_of_type_bird_first(a));

    public override Task Can_use_of_type_kiwi(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_of_type_kiwi(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Can_use_backwards_of_type_animal(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_backwards_of_type_animal(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi'
                    """);
            });

    public override Task Can_use_of_type_rose(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_of_type_rose(a);
                AssertSql(
                    """
                    SELECT "species", "$type", "genus", "name", "hasThorns"
                    FROM "Plants"
                    WHERE "$type" = 'Rose' AND ("$type" = 'Daisy' OR "$type" = 'Rose')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_query_all_animals(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_all_animals(a));

    [ConditionalTheory(Skip = SkipReason.PartitionKeyRequiredOnAllEntities)]
    public override Task Can_query_all_animal_views(bool async)
        => base.Can_query_all_animal_views(async);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_query_all_plants(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_all_plants(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_filter_all_animals(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_filter_all_animals(a));

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_query_all_birds(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_all_birds(a));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_just_kiwis(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_just_kiwis(a));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_just_roses(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(async, a => base.Can_query_just_roses(a));

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_include_animals(bool async) => base.Can_include_animals(async);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_include_prey(bool async) => base.Can_include_prey(async);

    public override Task Can_use_of_type_kiwi_where_south_on_derived_property(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_of_type_kiwi_where_south_on_derived_property(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND "foundOn" = 1 AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Can_use_of_type_kiwi_where_north_on_derived_property(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Can_use_of_type_kiwi_where_north_on_derived_property(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND "foundOn" = 0 AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Discriminator_used_when_projection_over_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Discriminator_used_when_projection_over_derived_type(a);
                AssertSql(
                    """
                    SELECT "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi'
                    """);
            });

    public override Task Discriminator_used_when_projection_over_derived_type2(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Discriminator_used_when_projection_over_derived_type2(a);
                AssertSql(
                    """
                    SELECT "isFlightless", "discriminator"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Discriminator_with_cast_in_shadow_property(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Discriminator_with_cast_in_shadow_property(a));

    public override Task Discriminator_used_when_projection_over_of_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Discriminator_used_when_projection_over_of_type(a);
                AssertSql(
                    """
                    SELECT "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    [ConditionalFact(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Can_insert_update_delete() => base.Can_insert_update_delete();

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_siblings_with_duplicate_property_in_subquery(bool async)
        => base.Union_siblings_with_duplicate_property_in_subquery(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task OfType_Union_subquery(bool async) => base.OfType_Union_subquery(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task OfType_Union_OfType(bool async) => base.OfType_Union_OfType(async);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Subquery_OfType(bool async) => base.Subquery_OfType(async);

    [ConditionalTheory(Skip = SkipReason.SetOperationsNotSupported)]
    public override Task Union_entity_equality(bool async) => base.Union_entity_equality(async);

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Setting_foreign_key_to_a_different_type_throws()
        => base.Setting_foreign_key_to_a_different_type_throws();

    public override Task Byte_enum_value_constant_used_in_projection(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Byte_enum_value_constant_used_in_projection(a);
                AssertSql(
                    """
                    SELECT "isFlightless"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi'
                    """);
            });

    [ConditionalFact(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Member_access_on_intermediate_type_works()
        => base.Member_access_on_intermediate_type_works();

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    public override Task Is_operator_on_result_of_FirstOrDefault(bool async)
        => base.Is_operator_on_result_of_FirstOrDefault(async);

    public override Task Selecting_only_base_properties_on_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Selecting_only_base_properties_on_base_type(a);
                AssertSql(
                    """
                    SELECT "name"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Selecting_only_base_properties_on_derived_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Selecting_only_base_properties_on_derived_type(a);
                AssertSql(
                    """
                    SELECT "name"
                    FROM "Animals"
                    WHERE ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Using_is_operator_on_multiple_type_with_no_result(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Using_is_operator_on_multiple_type_with_no_result(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND "discriminator" = 'Eagle' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task Using_is_operator_with_of_type_on_multiple_type_with_no_result(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.Using_is_operator_with_of_type_on_multiple_type_with_no_result(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND "discriminator" = 'Eagle' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Using_OfType_on_multiple_type_with_no_result(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Using_OfType_on_multiple_type_with_no_result(a));

    public override Task GetType_in_hierarchy_in_abstract_base_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_abstract_base_type(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE 1 = 0 AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task GetType_in_hierarchy_in_intermediate_type(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_intermediate_type(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE 1 = 0 AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Eagle' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2_reverse(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2_reverse(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE "discriminator" = 'Kiwi' AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2_not_equal(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2_not_equal(a);
                AssertSql(
                    """
                    SELECT "id", "countryId", "discriminator", "name", "species", "isFlightless", "group", "foundOn"
                    FROM "Animals"
                    WHERE NOT ("discriminator" = 'Kiwi') AND ("discriminator" = 'Eagle' OR "discriminator" = 'Kiwi')
                    """);
            });

#if !NET11_0_OR_GREATER
    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Filter_on_property_inside_complex_type_on_derived_type(bool async)
        => base.Filter_on_property_inside_complex_type_on_derived_type(async);
#endif

    protected override void ClearLog() => Fixture.ClearSql();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    public class InheritanceQueryDynamoFixture
        : InheritanceQueryFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        public override bool UseGeneratedKeys => false;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Ignore<KiwiQuery>();
            modelBuilder.Ignore<EagleQuery>();
            modelBuilder.Ignore<BirdQuery>();
            modelBuilder.Ignore<AnimalQuery>();

            modelBuilder.Entity<Animal>().ToTable("Animals").HasPartitionKey(e => e.Id);
            modelBuilder.Entity<Country>(entity =>
            {
                entity.ToTable("Countries").HasPartitionKey(e => e.Id);
                entity.Ignore(e => e.Animals);
                entity.Ignore(e => e.Plants);
            });
            modelBuilder.Entity<Plant>().ToTable("Plants").HasPartitionKey(e => e.Species);
            modelBuilder.Entity<Drink>().ToTable("Drinks").HasPartitionKey(e => e.Id);

            modelBuilder.Entity<Eagle>().Ignore(e => e.Prey);
            modelBuilder.Entity<Bird>().Ignore(e => e.EagleId);

            modelBuilder.Entity<Animal>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<Country>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<Drink>().Property(e => e.Id).ValueGeneratedNever();
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class InheritanceQueryDynamoTestDefault : InheritanceQueryDynamoTest
    {
        public InheritanceQueryDynamoTestDefault(
            InheritanceQueryDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
