using System.Transactions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;

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
    public async Task SaveChanges_InsideAmbientTransaction_ThrowsNotSupportedException()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse());
        await using var context = CreateContext(client);
        context.Add(new TransactionEntity { Id = "TRANSACTION#1" });

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.SaveChangesAsync(TestContext.Current.CancellationToken));

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
    public void CurrentAmbientTransaction_ReturnsAmbientTransactionScope()
    {
        using var context = CreateContext();
        var transactionManager =
            context
                .GetService<IDbContextTransactionManager>()
                .Should()
                .BeAssignableTo<ITransactionEnlistmentManager>()
                .Subject;

        transactionManager.CurrentAmbientTransaction.Should().BeNull();

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        Transaction.Current.Should().NotBeNull();
        transactionManager.CurrentAmbientTransaction.Should().BeSameAs(Transaction.Current);
    }

    private static TransactionContext CreateContext(IAmazonDynamoDB? client = null)
    {
        var options = new DbContextOptionsBuilder<TransactionContext>()
            .UseDynamo(options =>
            {
                if (client is not null)
                    options.DynamoDbClient(client);
            })
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
