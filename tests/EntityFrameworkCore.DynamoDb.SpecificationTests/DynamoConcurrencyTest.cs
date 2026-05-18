using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

#nullable disable

/// <summary>Optimistic concurrency tests for the DynamoDB provider.</summary>
[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoConcurrencyTest(DynamoConcurrencyTest.DynamoConcurrencyFixture fixture)
    : IClassFixture<DynamoConcurrencyTest.DynamoConcurrencyFixture>
{
    private const string DatabaseName = "DynamoConcurrencySpecTest";

    private DynamoConcurrencyFixture Fixture { get; } = fixture;

    /// <summary>Adding the same entity through two contexts throws a duplicate-key update exception.</summary>
    [ConditionalFact]
    public Task Adding_the_same_entity_twice_results_in_DbUpdateException()
        => ConcurrencyTestAsync<DbUpdateException>(ctx =>
        {
            ctx.Customers.Add(new Customer { Id = "1", Name = "CreatedTwice", Version = 1 });
            return Task.CompletedTask;
        });

    /// <summary>Updating then deleting the same entity through stale context state throws a concurrency exception.</summary>
    [ConditionalFact]
    public Task Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException>(
            ctx =>
            {
                ctx.Customers.Add(new Customer { Id = "2", Name = "Added", Version = 1 });
                return Task.CompletedTask;
            },
            async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(
                    c => c.Id == "2",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
            },
            async ctx => ctx.Customers.Remove(
                await ctx.Customers.FirstAsync(c => c.Id == "2", CancellationToken.None)));

    /// <summary>Updating the same entity through stale context state throws a concurrency exception.</summary>
    [ConditionalFact]
    public Task Updating_then_updating_the_same_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException>(
            ctx =>
            {
                ctx.Customers.Add(new Customer { Id = "3", Name = "Added", Version = 1 });
                return Task.CompletedTask;
            },
            async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(
                    c => c.Id == "3",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
            },
            async ctx =>
            {
                var customer = await ctx.Customers.FirstAsync(
                    c => c.Id == "3",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
            });

    /// <summary>
    ///     Runs the same change through two contexts so the store context succeeds and the stale
    ///     client context throws the expected update exception.
    /// </summary>
    private Task ConcurrencyTestAsync<TException>(Func<ConcurrencyContext, Task> change)
        where TException : DbUpdateException
        => ConcurrencyTestAsync<TException>(null, change, change);

    /// <summary>
    ///     Runs two changes with two different contexts, saving the store change before the stale
    ///     client change so optimistic concurrency can reject the stale save.
    /// </summary>
    private async Task ConcurrencyTestAsync<TException>(
        Func<ConcurrencyContext, Task> seedAction,
        Func<ConcurrencyContext, Task> storeChange,
        Func<ConcurrencyContext, Task> clientChange) where TException : DbUpdateException
    {
        await using var outerContext = CreateContext();
        await Fixture.TestStore.CleanAsync(outerContext);

        if (seedAction != null)
            await seedAction(outerContext);

        await outerContext.SaveChangesAsync(CancellationToken.None);

        if (clientChange != null)
            await clientChange(outerContext);

        await using (var innerContext = CreateContext())
        {
            if (storeChange != null)
                await storeChange(innerContext);

            await innerContext.SaveChangesAsync(CancellationToken.None);
        }

        var updateException = await Assert.ThrowsAnyAsync<TException>(()
            => outerContext.SaveChangesAsync(CancellationToken.None));

        var entry = updateException.Entries.Single();
        Assert.IsAssignableFrom<Customer>(entry.Entity);
    }

    /// <summary>Creates a new context configured by the shared fixture.</summary>
    private ConcurrencyContext CreateContext() => Fixture.CreateContext();

    /// <summary>Fixture for the DynamoDB concurrency test model.</summary>
    public class DynamoConcurrencyFixture : SharedStoreFixtureBase<ConcurrencyContext>
    {
        protected override string StoreName => DatabaseName;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        /// <inheritdoc />
        protected override async Task CleanAsync(DbContext context)
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Context used by optimistic concurrency tests.</summary>
    public class ConcurrencyContext(DbContextOptions options) : PoolableDbContext(options)
    {
        public DbSet<Customer> Customers { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder builder)
            => builder.Entity<Customer>(b =>
            {
                b.ToTable(DatabaseName);
                b.HasPartitionKey(c => c.Id);
                b.Property(c => c.Version).IsConcurrencyToken();
            });
    }

    /// <summary>Customer entity with a client-managed optimistic concurrency token.</summary>
    public class Customer
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public int Version { get; set; }
    }
}
