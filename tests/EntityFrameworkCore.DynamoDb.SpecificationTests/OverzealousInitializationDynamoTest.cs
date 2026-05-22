using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class OverzealousInitializationDynamoTest
    : OverzealousInitializationTestBase<
        OverzealousInitializationDynamoTest.OverzealousInitializationDynamoFixture>
{
    public OverzealousInitializationDynamoTest(
        OverzealousInitializationDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : base(fixture)
        => _ = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(OverzealousInitializationDynamoTest));

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Fixup_ignores_eagerly_initialized_reference_navs() { }

    public class OverzealousInitializationDynamoFixture : OverzealousInitializationFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<Album>().ToTable("Albums").HasPartitionKey(e => e.Id);
            modelBuilder.Entity<Artist>().ToTable("Artists").HasPartitionKey(e => e.Id);
            modelBuilder.Entity<Track>().ToTable("Tracks").HasPartitionKey(e => e.Id);
        }
    }
}
