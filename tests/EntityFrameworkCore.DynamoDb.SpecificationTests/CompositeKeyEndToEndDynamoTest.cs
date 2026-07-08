using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Composite-key end-to-end specification tests for the DynamoDB provider.</summary>
public abstract class CompositeKeyEndToEndDynamoTest
    : CompositeKeyEndToEndTestBase<CompositeKeyEndToEndDynamoTest.CompositeKeyEndToEndDynamoFixture>
{
    protected CompositeKeyEndToEndDynamoTest(CompositeKeyEndToEndDynamoFixture fixture) :
        base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(CompositeKeyEndToEndDynamoTest));

    [ConditionalFact(Skip = SkipReason.ThreePartCompositeKeysNotSupported)]
    public override Task Can_use_two_non_generated_integers_as_composite_key_end_to_end()
        => base.Can_use_two_non_generated_integers_as_composite_key_end_to_end();

    [ConditionalFact(Skip = SkipReason.ThreePartCompositeKeysNotSupported)]
    public override Task Can_use_generated_values_in_composite_key_end_to_end()
        => base.Can_use_generated_values_in_composite_key_end_to_end();

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override Task Only_one_part_of_a_composite_key_needs_to_vary_for_uniqueness()
        => base.Only_one_part_of_a_composite_key_needs_to_vary_for_uniqueness();

    public class CompositeKeyEndToEndDynamoFixture
        : CompositeKeyEndToEndFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override Type ContextType { get; } = typeof(DynamoBronieContext);

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }

    protected class DynamoBronieContext(DbContextOptions options) : BronieContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Keep only the two-part key entity. Ignoring the base type does not cascade to the
            // derived types, so each unsupported three-part-key type is ignored explicitly.
            modelBuilder.Ignore<Flyer>();
            modelBuilder.Ignore<Pegasus>();
            modelBuilder.Ignore<Unicorn>();
            modelBuilder
                .Entity<EarthPony>()
                .ToTable("EarthPonies")
                .HasPartitionKey(e => e.Id1)
                .HasSortKey(e => e.Id2);
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class CompositeKeyEndToEndDynamoTestDefault : CompositeKeyEndToEndDynamoTest
    {
        public CompositeKeyEndToEndDynamoTestDefault(
            CompositeKeyEndToEndDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
