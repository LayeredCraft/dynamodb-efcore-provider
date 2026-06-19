using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Associations.ComplexProperties;

public abstract class ComplexPropertiesStructuralEqualityDynamoTest
    : ComplexPropertiesStructuralEqualityTestBase<ComplexPropertiesStructuralEqualityDynamoTest.
        ComplexPropertiesStructuralEqualityDynamoFixture>
{
    protected ComplexPropertiesStructuralEqualityDynamoTest(
        ComplexPropertiesStructuralEqualityDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ComplexPropertiesStructuralEqualityDynamoTest));

    public override Task Two_associates() => base.Two_associates();

    public override Task Two_nested_associates() => base.Two_nested_associates();

    public override Task Not_equals() => base.Not_equals();

    public override Task Associate_with_inline_null() => base.Associate_with_inline_null();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Associate_with_parameter_null() => base.Associate_with_parameter_null();

    public override Task Nested_associate_with_inline_null()
        => base.Nested_associate_with_inline_null();

    public override Task Nested_associate_with_inline() => base.Nested_associate_with_inline();

    public override Task Nested_associate_with_parameter()
        => base.Nested_associate_with_parameter();

    [ConditionalFact(Skip = SkipReason.ComplexCollectionStructuralEqualityNotSupported)]
    public override Task Two_nested_collections() => base.Two_nested_collections();

    [ConditionalFact(Skip = SkipReason.ComplexCollectionStructuralEqualityNotSupported)]
    public override Task Nested_collection_with_inline() => base.Nested_collection_with_inline();

    [ConditionalFact(Skip = SkipReason.ComplexCollectionStructuralEqualityNotSupported)]
    public override Task Nested_collection_with_parameter()
        => base.Nested_collection_with_parameter();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_inline() => base.Contains_with_inline();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_parameter() => base.Contains_with_parameter();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_operators_composed_on_the_collection()
        => base.Contains_with_operators_composed_on_the_collection();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Contains_with_nested_and_composed_operators()
        => base.Contains_with_nested_and_composed_operators();

    public override Task Nullable_value_type_with_null() => base.Nullable_value_type_with_null();

    public class ComplexPropertiesStructuralEqualityDynamoFixture
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
    public sealed class ComplexPropertiesStructuralEqualityDynamoTestDefault
        : ComplexPropertiesStructuralEqualityDynamoTest
    {
        public ComplexPropertiesStructuralEqualityDynamoTestDefault(
            ComplexPropertiesStructuralEqualityDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
