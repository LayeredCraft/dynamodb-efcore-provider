using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Extensions;

/// <summary>Unit tests for <see cref="DynamoDatabaseFacadeExtensions" />.</summary>
public class DynamoDatabaseFacadeExtensionsTests
{
    // ── GetDynamoClient ──────────────────────────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GetDynamoClient_ReturnsDynamoClientFromWrapper()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var result = context.Database.GetDynamoClient();

        result.Should().BeSameAs(client);
    }

    // ── TransactionOverflowBehavior ──────────────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SetTransactionOverflowBehavior_OverridesConfiguredDefault()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, TransactionOverflowBehavior.Throw);

        context.Database.SetTransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking);

        context
            .Database
            .GetTransactionOverflowBehavior()
            .Should()
            .Be(TransactionOverflowBehavior.UseChunking);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GetTransactionOverflowBehavior_FallsBackToConfiguredDefault_WhenNoOverride()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, TransactionOverflowBehavior.Throw);

        context
            .Database
            .GetTransactionOverflowBehavior()
            .Should()
            .Be(TransactionOverflowBehavior.Throw);
    }

    // ── MaxTransactionSize ───────────────────────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SetMaxTransactionSize_OverridesConfiguredDefault()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, maxTransactionSize: 50);

        context.Database.SetMaxTransactionSize(10);

        context.Database.GetMaxTransactionSize().Should().Be(10);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GetMaxTransactionSize_FallsBackToConfiguredDefault_WhenNoOverride()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, maxTransactionSize: 50);

        context.Database.GetMaxTransactionSize().Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void SetMaxTransactionSize_InvalidValue_ThrowsInvalidOperationException(int value)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var act = () => context.Database.SetMaxTransactionSize(value);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*'{value}'*");
    }

    // ── MaxBatchWriteSize ────────────────────────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SetMaxBatchWriteSize_OverridesConfiguredDefault()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, maxBatchWriteSize: 20);

        context.Database.SetMaxBatchWriteSize(5);

        context.Database.GetMaxBatchWriteSize().Should().Be(5);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GetMaxBatchWriteSize_FallsBackToConfiguredDefault_WhenNoOverride()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client, maxBatchWriteSize: 20);

        context.Database.GetMaxBatchWriteSize().Should().Be(20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(26)]
    public void SetMaxBatchWriteSize_InvalidValue_ThrowsInvalidOperationException(int value)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = TestDbContext.Create(client);

        var act = () => context.Database.SetMaxBatchWriteSize(value);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*'{value}'*");
    }

    // ── Support types ────────────────────────────────────────────────────────

    private sealed record TestEntity
    {
        public string Pk { get; set; } = null!;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestEntity> Items => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(b =>
            {
                b.ToTable("TestTable");
                b.HasPartitionKey(x => x.Pk);
            });

        public static TestDbContext Create(
            IAmazonDynamoDB client,
            TransactionOverflowBehavior transactionOverflowBehavior =
                TransactionOverflowBehavior.UseChunking,
            int maxTransactionSize = 100,
            int maxBatchWriteSize = 25)
            => new(
                new DbContextOptionsBuilder<TestDbContext>()
                    .UseDynamo(options => options
                        .DynamoDbClient(client)
                        .TransactionOverflowBehavior(transactionOverflowBehavior)
                        .MaxTransactionSize(maxTransactionSize)
                        .MaxBatchWriteSize(maxBatchWriteSize))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
