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

    [ConditionalTheory(Skip = SkipReason.OrderedResultSetNotSupported)]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Seeding_does_not_leave_context_contaminated(bool async)
        => base.Seeding_does_not_leave_context_contaminated(async);

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
