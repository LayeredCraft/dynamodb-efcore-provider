using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class SeedingDynamoTest : SeedingTestBase, IAsyncLifetime
{
    private readonly TestStore _testStore = DynamoTestStoreFactory.Instance.Create("Seeding");

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(SeedingDynamoTest));

    protected override TestStore TestStore => _testStore;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _testStore.DisposeAsync().ConfigureAwait(false);

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public override async Task Seeding_does_not_leave_context_contaminated(bool async)
    {
        await using var context = CreateContextWithEmptyDatabase(async ? "1A" : "1S");

        // DynamoDB lifecycle APIs are async-only, so both inherited sync/async variants use the
        // async clean path while still preserving the base test's two input cases.
        await TestStore.CleanAsync(context);

        Assert.Empty(context.ChangeTracker.Entries());

        var seeds = (await context.Set<Seed>().AllowScan().ToListAsync()).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, seeds.Count);
        Assert.Equal(321, seeds[0].Id);
        Assert.Equal("Apple", seeds[0].Species);
        Assert.Equal(322, seeds[1].Id);
        Assert.Equal("Orange", seeds[1].Species);
    }

    [ConditionalTheory(Skip = SkipReason.PartitionKeyRequiredOnAllEntities)]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Seeding_keyless_entity_throws_exception(bool async)
        => base.Seeding_keyless_entity_throws_exception(async);

    protected override SeedingContext CreateContextWithEmptyDatabase(string testId)
        => new DynamoSeedingContext(testId, TestStore);

    private sealed class DynamoSeedingContext(string testId, TestStore testStore)
        : SeedingContext(testId)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => testStore.AddProviderOptions(optionsBuilder);

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

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class SeedingDynamoTestDefault : SeedingDynamoTest
    {
        public SeedingDynamoTestDefault(DynamoSpecificationContainerFixture containerFixture)
            => _ = containerFixture;
    }
}
