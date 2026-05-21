using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Composite-key end-to-end specification tests for the DynamoDB provider.</summary>
public abstract class CompositeKeyEndToEndDynamoTest
    : CompositeKeyEndToEndTestBase<CompositeKeyEndToEndDynamoTest.CompositeKeyEndToEndDynamoFixture>
{
    protected CompositeKeyEndToEndDynamoTest(CompositeKeyEndToEndDynamoFixture fixture)
        : base(fixture)
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

    public override async Task Only_one_part_of_a_composite_key_needs_to_vary_for_uniqueness()
    {
        // The base test uses sync enumeration and aggregate Count(). DynamoDB query execution is
        // async-only and does not translate query aggregates, so this keeps the same end-to-end
        // scenario with async reads and intentional scans.
        int[] ids;
        await using (var context = CreateContext())
        {
            var pony1 = context.EarthPonies.Add(new EarthPony { Id1 = 1, Id2 = 7, Name = "Apple Jack 1" }).Entity;
            var pony2 = context.EarthPonies.Add(new EarthPony { Id1 = 2, Id2 = 7, Name = "Apple Jack 2" }).Entity;
            var pony3 = context.EarthPonies.Add(new EarthPony { Id1 = 3, Id2 = 7, Name = "Apple Jack 3" }).Entity;

            await context.SaveChangesAsync();
            ids = [pony1.Id1, pony2.Id1, pony3.Id1];
        }

        await using (var context = CreateContext())
        {
            var list = await context.EarthPonies.AllowScan().ToListAsync();
            Assert.Equal(list.Count, list.Count(e => e.Name == "Apple Jack 1") * 3);
            Assert.Equal("Apple Jack 1", list.Single(e => e.Id1 == ids[0]).Name);
            Assert.Equal("Apple Jack 2", list.Single(e => e.Id1 == ids[1]).Name);
            Assert.Equal("Apple Jack 3", list.Single(e => e.Id1 == ids[2]).Name);
            list.Single(e => e.Id1 == ids[1]).Name = "Pinky Pie 2";
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var array = await context.EarthPonies.AllowScan().ToArrayAsync();
            Assert.Equal(array.Length, array.Count(e => e.Name == "Apple Jack 1") * 3);
            Assert.Equal("Apple Jack 1", array.Single(e => e.Id1 == ids[0]).Name);
            Assert.Equal("Pinky Pie 2", array.Single(e => e.Id1 == ids[1]).Name);
            Assert.Equal("Apple Jack 3", array.Single(e => e.Id1 == ids[2]).Name);
            context.EarthPonies.RemoveRange(array);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            Assert.Empty(await context.EarthPonies.AllowScan().ToListAsync());
        }
    }

    public class CompositeKeyEndToEndDynamoFixture
        : CompositeKeyEndToEndFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override Type ContextType { get; } = typeof(DynamoBronieContext);

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .UseDynamo(options => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

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
            modelBuilder.Entity<EarthPony>()
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
            DynamoSpecificationContainerFixture containerFixture)
            : base(fixture)
            => _ = containerFixture;
    }
}
