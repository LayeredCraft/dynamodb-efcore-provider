using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests unsupported transaction behavior for the DynamoDB provider.</summary>
public sealed class DynamoTransactionManagerTests
{
    private const string TransactionsNotSupported =
        "The DynamoDB database provider does not support explicit transactions.";

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void BeginTransaction_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.BeginTransaction());

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task BeginTransactionAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CommitTransaction_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.CommitTransaction());

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CommitTransactionAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.CommitTransactionAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void RollbackTransaction_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.RollbackTransaction());

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task RollbackTransactionAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.RollbackTransactionAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnlistTransaction_WithNull_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.EnlistTransaction(null));

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnlistTransaction_WithTransaction_ThrowsNotSupportedException()
    {
        using var context = CreateContext();
        using var transaction = new CommittableTransaction();

        var exception =
            Assert.Throws<NotSupportedException>(()
                => context.Database.EnlistTransaction(transaction));

        exception.Message.Should().Be(TransactionsNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CurrentTransaction_ReturnsNull()
    {
        using var context = CreateContext();

        context.Database.CurrentTransaction.Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GetEnlistedTransaction_ReturnsNull()
    {
        using var context = CreateContext();

        context.Database.GetEnlistedTransaction().Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CurrentAmbientTransaction_IgnoresTransactionScope()
    {
        using var context = CreateContext();
        var transactionManager =
            context
                .GetService<IDbContextTransactionManager>()
                .Should()
                .BeAssignableTo<ITransactionEnlistmentManager>()
                .Subject;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        Transaction.Current.Should().NotBeNull();
        transactionManager.CurrentAmbientTransaction.Should().BeNull();
    }

    private static TransactionContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TransactionContext>()
            .UseDynamo()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        return new TransactionContext(options);
    }

    private sealed class TransactionContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TransactionEntity>(b =>
            {
                b.ToTable("Transactions");
                b.HasPartitionKey(e => e.Id);
            });
    }

    private sealed class TransactionEntity
    {
        public string Id { get; set; } = null!;
    }
}
