using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class SeedingDynamoTest : SeedingTestBase
{
    private readonly TestStore _testStore = DynamoTestStoreFactory.Instance.Create("Seeding");

    public SeedingDynamoTest(DynamoSpecificationContainerFixture containerFixture)
        => _ = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(SeedingDynamoTest));

    protected override TestStore TestStore => _testStore;

    public override async Task Seeding_does_not_leave_context_contaminated(bool async)
    {
        await using var context = CreateContextWithEmptyDatabase(async ? "1A" : "1S");

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        Assert.Empty(context.ChangeTracker.Entries());

        var seeds = (await context.Set<Seed>().AllowScan().ToListAsync()).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, seeds.Count);
        Assert.Equal(321, seeds[0].Id);
        Assert.Equal("Apple", seeds[0].Species);
        Assert.Equal(322, seeds[1].Id);
        Assert.Equal("Orange", seeds[1].Species);
    }

    [ConditionalTheory(Skip = SkipReason.PartitionKeyRequiredOnAllEntities)]
    public override Task Seeding_keyless_entity_throws_exception(bool async)
        => base.Seeding_keyless_entity_throws_exception(async);

    protected override SeedingContext CreateContextWithEmptyDatabase(string testId)
        => new DynamoSeedingContext(testId);

    protected override KeylessSeedingContext CreateKeylessContextWithEmptyDatabase()
        => new(TestStore.AddProviderOptions(new DbContextOptionsBuilder()).Options);

    private sealed class DynamoSeedingContext(string testId) : SeedingContext(testId)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseDynamo(options
                => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Seed>(entity =>
            {
                entity.ToTable($"Seeds_{TestId}");
                entity.HasPartitionKey(e => e.Id);
            });
        }
    }
}
