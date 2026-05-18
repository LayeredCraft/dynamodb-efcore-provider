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
    ///     Updating the same derived entity through stale context state throws a concurrency
    ///     exception.
    /// </summary>
    [ConditionalFact]
    public Task
        Updating_then_updating_the_same_derived_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException, PremiumCustomer>(
            ctx =>
            {
                ctx.PremiumCustomers.Add(
                    new PremiumCustomer
                    {
                        Id = "4", Name = "Added", Version = 1, LoyaltyLevel = "Bronze",
                    });
                return Task.CompletedTask;
            },
            async ctx =>
            {
                var customer = await ctx.PremiumCustomers.FirstAsync(
                    c => c.Id == "4",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
                customer.LoyaltyLevel = "Silver";
            },
            async ctx =>
            {
                var customer = await ctx.PremiumCustomers.FirstAsync(
                    c => c.Id == "4",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
                customer.LoyaltyLevel = "Gold";
            });

    /// <summary>
    ///     Updating then deleting the same derived entity through stale context state throws a
    ///     concurrency exception.
    /// </summary>
    [ConditionalFact]
    public Task
        Updating_then_deleting_the_same_derived_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException, PremiumCustomer>(
            ctx =>
            {
                ctx.PremiumCustomers.Add(
                    new PremiumCustomer
                    {
                        Id = "5", Name = "Added", Version = 1, LoyaltyLevel = "Bronze",
                    });
                return Task.CompletedTask;
            },
            async ctx =>
            {
                var customer = await ctx.PremiumCustomers.FirstAsync(
                    c => c.Id == "5",
                    CancellationToken.None);
                customer.Name = "Updated";
                customer.Version = 2;
                customer.LoyaltyLevel = "Silver";
            },
            async ctx => ctx.PremiumCustomers.Remove(
                await ctx.PremiumCustomers.FirstAsync(c => c.Id == "5", CancellationToken.None)));

    /// <summary>
    ///     Runs the same change through two contexts so the store context succeeds and the stale
    ///     client context throws the expected update exception.
    /// </summary>
    /// <typeparam name="TException">Expected exception type thrown by the stale save.</typeparam>
    /// <param name="change">Change applied by both competing contexts.</param>
    private Task ConcurrencyTestAsync<TException>(Func<ConcurrencyContext, Task> change)
        where TException : DbUpdateException
        => ConcurrencyTestAsync<TException, Customer>(null, change, change);

    /// <summary>
    ///     Runs two changes with two different contexts, saving the store change before the stale
    ///     client change so optimistic concurrency can reject the stale save.
    /// </summary>
    /// <typeparam name="TException">Expected exception type thrown by the stale save.</typeparam>
    /// <param name="seedAction">Optional action that seeds state before both contexts make changes.</param>
    /// <param name="storeChange">Change saved by the winning store context.</param>
    /// <param name="clientChange">Change saved last by the stale client context.</param>
    private Task ConcurrencyTestAsync<TException>(
        Func<ConcurrencyContext, Task> seedAction,
        Func<ConcurrencyContext, Task> storeChange,
        Func<ConcurrencyContext, Task> clientChange) where TException : DbUpdateException
        => ConcurrencyTestAsync<TException, Customer>(seedAction, storeChange, clientChange);

    /// <summary>
    ///     Runs two changes with two different contexts, saving the store change before the stale
    ///     client change so optimistic concurrency can reject the stale save.
    /// </summary>
    /// <typeparam name="TException">Expected exception type thrown by the stale save.</typeparam>
    /// <typeparam name="TEntity">Expected entity type carried by the update exception entry.</typeparam>
    /// <param name="seedAction">Optional action that seeds state before both contexts make changes.</param>
    /// <param name="storeChange">Change saved by the winning store context.</param>
    /// <param name="clientChange">Change saved last by the stale client context.</param>
    private async Task ConcurrencyTestAsync<TException, TEntity>(
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
        Assert.IsAssignableFrom<TEntity>(entry.Entity);
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
        /// <summary>Customers participating in concurrency tests.</summary>
        public DbSet<Customer> Customers { get; set; }

        /// <summary>Premium customers participating in inherited concurrency-token tests.</summary>
        public DbSet<PremiumCustomer> PremiumCustomers { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Customer>(b =>
            {
                b.ToTable(DatabaseName);
                b.HasPartitionKey(c => c.Id);
                b.Property(c => c.Version).IsConcurrencyToken();
            });

            builder.Entity<PremiumCustomer>().HasBaseType<Customer>();
        }
    }

    /// <summary>Premium customer entity used to verify inherited concurrency tokens.</summary>
    public class PremiumCustomer : Customer
    {
        public string LoyaltyLevel { get; set; }
    }

    /// <summary>Customer entity with a client-managed optimistic concurrency token.</summary>
    public class Customer
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public int Version { get; set; }
    }
}
