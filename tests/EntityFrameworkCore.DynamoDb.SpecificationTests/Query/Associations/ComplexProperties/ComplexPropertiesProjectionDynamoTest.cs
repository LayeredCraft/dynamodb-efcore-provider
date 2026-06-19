using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Associations.ComplexProperties;

public abstract class ComplexPropertiesProjectionDynamoTest
    : ComplexPropertiesProjectionTestBase<ComplexPropertiesProjectionDynamoTest.
        ComplexPropertiesProjectionDynamoFixture>
{
    protected ComplexPropertiesProjectionDynamoTest(
        ComplexPropertiesProjectionDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ComplexPropertiesProjectionDynamoTest));

    public override Task Select_root(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_root(queryTrackingBehavior);

    public override Task Select_scalar_property_on_required_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_scalar_property_on_required_associate(queryTrackingBehavior);

    public override Task Select_property_on_optional_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_property_on_optional_associate(queryTrackingBehavior);

    public override Task Select_value_type_property_on_null_associate_throws(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_value_type_property_on_null_associate_throws(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.ComplexProjectionMaterializationGap)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_nullable_value_type_property_on_null_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_nullable_value_type_property_on_null_associate(queryTrackingBehavior);

    public override Task Select_associate(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_associate(queryTrackingBehavior);

    public override Task Select_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_optional_associate(queryTrackingBehavior);

    public override Task Select_required_nested_on_required_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_required_nested_on_required_associate(queryTrackingBehavior);

    public override Task Select_optional_nested_on_required_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_optional_nested_on_required_associate(queryTrackingBehavior);

    public override Task Select_required_nested_on_optional_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_required_nested_on_optional_associate(queryTrackingBehavior);

    public override Task Select_optional_nested_on_optional_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_optional_nested_on_optional_associate(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_required_associate_via_optional_navigation(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior);

    public override Task Select_unmapped_associate_scalar_property(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_unmapped_associate_scalar_property(queryTrackingBehavior);

    public override Task Select_untranslatable_method_on_associate_scalar_property(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_untranslatable_method_on_associate_scalar_property(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_associate_collection(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_nested_collection_on_required_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_nested_collection_on_required_associate(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_nested_collection_on_optional_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_nested_collection_on_optional_associate(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task SelectMany_associate_collection(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.SelectMany_associate_collection(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task SelectMany_nested_collection_on_required_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task SelectMany_nested_collection_on_optional_associate(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.ComplexProjectionMaterializationGap)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_root_duplicated(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_root_duplicated(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_subquery_required_related_FirstOrDefault(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.SubqueryPushdownNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_subquery_optional_related_FirstOrDefault(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_subquery_FirstOrDefault_complex_collection(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior);

    public override Task Select_root_with_value_types(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_root_with_value_types(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_non_nullable_value_type(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_non_nullable_value_type(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_nullable_value_type(QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_nullable_value_type(queryTrackingBehavior);

    [ConditionalTheory(Skip = SkipReason.ComplexProjectionMaterializationGap)]
    [MemberData(nameof(TrackingData))]
    public override Task Select_nullable_value_type_with_Value(
        QueryTrackingBehavior queryTrackingBehavior)
        => base.Select_nullable_value_type_with_Value(queryTrackingBehavior);

    public class ComplexPropertiesProjectionDynamoFixture
        : ComplexPropertiesFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.ManyServiceProvidersCreatedWarning,
                    CoreEventId.MappedNavigationIgnoredWarning,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        protected override async Task SeedAsync(PoolableDbContext context)
        {
            context.Set<RootEntity>().AddRange(Data.RootEntities);
            context.Set<ValueRootEntity>().AddRange(Data.ValueRootEntities);

            await context.SaveChangesAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Ignore<RootReferencingEntity>();

            modelBuilder.Entity<RootEntity>(b =>
            {
                b.ToTable("RootEntities").HasPartitionKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();

                b.ComplexProperty(
                    e => e.RequiredAssociate,
                    rrb => rrb.ComplexProperty(r => r.OptionalNestedAssociate).IsRequired(false));

                b.ComplexProperty(
                    e => e.OptionalAssociate,
                    orb =>
                    {
                        orb.IsRequired(false);
                        orb.ComplexProperty(r => r.OptionalNestedAssociate).IsRequired(false);
                    });

                b.ComplexCollection(
                    e => e.AssociateCollection,
                    rcb => rcb.ComplexProperty(r => r.OptionalNestedAssociate).IsRequired(false));
            });

            modelBuilder.Entity<ValueRootEntity>(b =>
            {
                b.ToTable("ValueRootEntities").HasPartitionKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
                b.ComplexProperty(e => e.RequiredAssociate);

                b.ComplexProperty(
                    e => e.OptionalAssociate,
                    orb =>
                    {
                        orb.IsRequired(false);
                        orb.ComplexProperty(r => r.OptionalNested).IsRequired(false);
                    });

                b.ComplexCollection(
                    e => e.AssociateCollection,
                    rcb => rcb.ComplexProperty(r => r.OptionalNestedAssociate).IsRequired(false));
            });
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class ComplexPropertiesProjectionDynamoTestDefault
        : ComplexPropertiesProjectionDynamoTest
    {
        public ComplexPropertiesProjectionDynamoTestDefault(
            ComplexPropertiesProjectionDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
