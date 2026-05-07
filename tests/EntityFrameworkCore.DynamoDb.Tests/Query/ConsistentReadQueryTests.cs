using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

#pragma warning disable EF9102

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Tests read consistency preference propagation to ExecuteStatement requests.</summary>
public class ConsistentReadQueryTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task GlobalConsistentRead_AppliesWhenQueryHasNoOverride()
    {
        var (client, captured) = SetupMockClient();
        await using var context = ConsistentReadDbContext.Create(client, consistentRead: true);

        await DrainAsync(
            context.Items.Where(x => x.Pk == "P#1").AsAsyncEnumerable(),
            TestContext.Current.CancellationToken);

        captured.Single().ConsistentRead.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task QueryConsistentReadTrue_OverridesGlobalFalse()
    {
        var (client, captured) = SetupMockClient();
        await using var context = ConsistentReadDbContext.Create(client, consistentRead: false);

        await DrainAsync(
            context.Items.Where(x => x.Pk == "P#1").WithConsistentRead().AsAsyncEnumerable(),
            TestContext.Current.CancellationToken);

        captured.Single().ConsistentRead.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task QueryConsistentReadFalse_OverridesGlobalTrue()
    {
        var (client, captured) = SetupMockClient();
        await using var context = ConsistentReadDbContext.Create(client, consistentRead: true);

        await DrainAsync(
            context.Items.Where(x => x.Pk == "P#1").WithConsistentRead(false).AsAsyncEnumerable(),
            TestContext.Current.CancellationToken);

        captured.Single().ConsistentRead.Should().BeFalse();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToPageAsync_PropagatesPerQueryConsistentRead()
    {
        var (client, captured) = SetupMockClient();
        await using var context = ConsistentReadDbContext.Create(client);

        _ = await context
            .Items
            .Where(x => x.Pk == "P#1")
            .WithConsistentRead()
            .ToPageAsync(10, null, TestContext.Current.CancellationToken);

        captured.Single().ConsistentRead.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AllowScan_WithConsistentRead_PassesFlagWithoutWarningOrError()
    {
        var (client, captured) = SetupMockClient();
        await using var context = ConsistentReadDbContext.Create(client);

        await DrainAsync(
            context
                .Items
                .Where(x => x.Value == "non-key")
                .AllowScan()
                .WithConsistentRead()
                .AsAsyncEnumerable(),
            TestContext.Current.CancellationToken);

        captured.Single().ConsistentRead.Should().BeTrue();
    }

    private static (IAmazonDynamoDB client, List<ExecuteStatementRequest> captured)
        SetupMockClient()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured.Add(r)),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [], NextToken = null });

        return (client, captured);
    }

    private static async Task DrainAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken cancellationToken)
    {
        await foreach (var _ in source.WithCancellation(cancellationToken)) { }
    }

    private sealed record ConsistentReadEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Value { get; set; } = null!;
    }

    private sealed class ConsistentReadDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ConsistentReadEntity> Items => Set<ConsistentReadEntity>();

        /// <summary>Creates a context configured with the supplied client and consistency default.</summary>
        public static ConsistentReadDbContext
            Create(IAmazonDynamoDB client, bool consistentRead = false)
            => new(
                new DbContextOptionsBuilder<ConsistentReadDbContext>()
                    .UseDynamo(options => options
                        .DynamoDbClient(client)
                        .ConsistentRead(consistentRead))
                    .ConfigureWarnings(w
                        => w
                            .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                            .Ignore(DynamoEventId.ScanLikeQueryDetected))
                    .Options);

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConsistentReadEntity>(b =>
            {
                b.ToTable("ConsistentReadTable");
                b.HasPartitionKey(x => x.Pk);
            });
    }
}

#pragma warning restore EF9102
