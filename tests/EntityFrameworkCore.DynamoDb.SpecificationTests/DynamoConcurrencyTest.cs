using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

#nullable disable

[Collection(DynamoSpecificationCollection.Name)]
public sealed class DynamoConcurrencyTest(DynamoConcurrencyTest.DynamoConcurrencyFixture fixture)
    : IClassFixture<DynamoConcurrencyTest.DynamoConcurrencyFixture>
{
    private const string DatabaseName = "DynamoConcurrencySpecTest";

    private DynamoConcurrencyFixture Fixture { get; } = fixture;

    [ConditionalFact]
    public Task Adding_the_same_entity_twice_results_in_DbUpdateException()
        => ConcurrencyTestAsync<DbUpdateException>(ctx =>
        {
            ctx.Customers.Add(new Customer { Id = "1", Name = "CreatedTwice", Version = 1 });
            return Task.CompletedTask;
        });

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
    ///     Runs the same change in both contexts so the inner context commits first, then the outer
    ///     context attempts the duplicate or stale write and throws.
    /// </summary>
    private Task ConcurrencyTestAsync<TException>(Func<ConcurrencyContext, Task> change)
        where TException : DbUpdateException
        => ConcurrencyTestAsync<TException, Customer>(null, change, change);

    private Task ConcurrencyTestAsync<TException>(
        Func<ConcurrencyContext, Task> seedAction,
        Func<ConcurrencyContext, Task> innerContextChange,
        Func<ConcurrencyContext, Task> outerContextChange) where TException : DbUpdateException
        => ConcurrencyTestAsync<TException, Customer>(
            seedAction,
            innerContextChange,
            outerContextChange);

    private async Task ConcurrencyTestAsync<TException, TEntity>(
        Func<ConcurrencyContext, Task> seedAction,
        Func<ConcurrencyContext, Task> innerContextChange,
        Func<ConcurrencyContext, Task> outerContextChange) where TException : DbUpdateException
    {
        await using var outerContext = CreateContext();
        await Fixture.TestStore.CleanAsync(outerContext);

        if (seedAction != null)
            await seedAction(outerContext);

        await outerContext.SaveChangesAsync(CancellationToken.None);

        if (outerContextChange != null)
            await outerContextChange(outerContext);

        await using (var innerContext = CreateContext())
        {
            if (innerContextChange != null)
                await innerContextChange(innerContext);

            await innerContext.SaveChangesAsync(CancellationToken.None);
        }

        var updateException = await Assert.ThrowsAnyAsync<TException>(()
            => outerContext.SaveChangesAsync(CancellationToken.None));

        var entry = updateException.Entries.Single();
        Assert.IsAssignableFrom<TEntity>(entry.Entity);
    }

    private ConcurrencyContext CreateContext() => Fixture.CreateContext();

    public class DynamoConcurrencyFixture : SharedStoreFixtureBase<ConcurrencyContext>
    {
        protected override string StoreName => DatabaseName;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));
    }

    public class ConcurrencyContext(DbContextOptions options) : PoolableDbContext(options)
    {
        public DbSet<Customer> Customers { get; set; }

        public DbSet<PremiumCustomer> PremiumCustomers { get; set; }

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

    public class PremiumCustomer : Customer
    {
        public string LoyaltyLevel { get; set; }
    }

    public class Customer
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public int Version { get; set; }
    }
}
