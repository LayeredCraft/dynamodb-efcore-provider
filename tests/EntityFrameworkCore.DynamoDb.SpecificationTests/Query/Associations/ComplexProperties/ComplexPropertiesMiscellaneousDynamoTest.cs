using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Associations.ComplexProperties;

public abstract class ComplexPropertiesMiscellaneousDynamoTest
    : ComplexPropertiesMiscellaneousTestBase<ComplexPropertiesMiscellaneousDynamoTest.
        ComplexPropertiesMiscellaneousDynamoFixture>
{
    protected ComplexPropertiesMiscellaneousDynamoTest(
        ComplexPropertiesMiscellaneousDynamoFixture fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ComplexPropertiesMiscellaneousDynamoTest));

    public override Task Where_on_associate_scalar_property()
        => base.Where_on_associate_scalar_property();

    public override Task Where_on_optional_associate_scalar_property()
        => base.Where_on_optional_associate_scalar_property();

    public override Task Where_on_nested_associate_scalar_property()
        => base.Where_on_nested_associate_scalar_property();

    public override async Task Where_property_on_non_nullable_value_type()
    {
        await base.Where_property_on_non_nullable_value_type();

        AssertSql(
            """
            SELECT "id", "name", "associateCollection", "optionalAssociate", "requiredAssociate"
            FROM "ValueRootEntities"
            WHERE "requiredAssociate"."int" = 8
            """);
    }

    public override async Task Where_property_on_nullable_value_type_Value()
    {
        await base.Where_property_on_nullable_value_type_Value();

        AssertSql(
            """
            SELECT "id", "name", "associateCollection", "optionalAssociate", "requiredAssociate"
            FROM "ValueRootEntities"
            WHERE "optionalAssociate"."int" = 8
            """);
    }

    public override async Task Where_HasValue_on_nullable_value_type()
    {
        await base.Where_HasValue_on_nullable_value_type();

        AssertSql(
            """
            SELECT "id", "name", "associateCollection", "optionalAssociate", "requiredAssociate"
            FROM "ValueRootEntities"
            WHERE "optionalAssociate" IS NOT NULL AND "optionalAssociate" IS NOT MISSING
            """);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    public class ComplexPropertiesMiscellaneousDynamoFixture
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
    public sealed class ComplexPropertiesMiscellaneousDynamoTestDefault
        : ComplexPropertiesMiscellaneousDynamoTest
    {
        public ComplexPropertiesMiscellaneousDynamoTestDefault(
            ComplexPropertiesMiscellaneousDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
