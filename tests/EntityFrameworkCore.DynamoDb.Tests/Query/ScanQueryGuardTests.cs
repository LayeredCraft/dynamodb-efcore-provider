using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for scan-like query protection.</summary>
public class ScanQueryGuardTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task MissingPartitionKeyEquality_ThrowsByDefault_BeforeAwsCall()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Status == "OPEN")
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scan-like DynamoDB query detected*missing equality predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyEquality_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyEquality_WithSingleSortKeyCondition_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1" && x.Sk.CompareTo("S#1") >= 0)
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task NonKeyOr_WithPartitionKeyEquality_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1" && (x.Status == "OPEN" || x.Total > 10))
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task PartitionKeyIn_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);
        var keys = new[] { "P#1", "P#2" };

        var act = async ()
            => await context
                .Items
                .Where(x => keys.Contains(x.Pk))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*partition key 'pk' uses IN/OR*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SortKeyOr_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1" && (x.Sk == "S#1" || x.Sk == "S#2"))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OR predicate references*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SortKeyIn_ThrowsByDefault()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);
        var sortKeys = new[] { "S#1", "S#2" };

        var act = async ()
            => await context
                .Items
                .Where(x => x.Pk == "P#1" && sortKeys.Contains(x.Sk))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*sort key 'sk' is used as a filter*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AllowScan_SuppressesThrow()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .AllowScan()
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_Log_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Log(DynamoEventId.ScanLikeQueryDetected));

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ConfigureWarnings_Ignore_Executes()
    {
        var client = CreateClient();
        await using var context = ScanGuardDbContext.Create(
            client,
            w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        await context
            .Items
            .Where(x => x.Status == "OPEN")
            .ToListAsync(TestContext.Current.CancellationToken);

        await client.ReceivedWithAnyArgs(1).ExecuteStatementAsync(default!);
    }

    private static IAmazonDynamoDB CreateClient()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [], NextToken = null });

        return client;
    }

    private sealed record ScanGuardItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Sk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Status { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public int Total { get; set; }
    }

    private sealed class ScanGuardDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ScanGuardItem> Items => Set<ScanGuardItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ScanGuardItem>(b =>
            {
                b.ToTable("ScanGuardTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ScanGuardDbContext Create(
            IAmazonDynamoDB client,
            Action<WarningsConfigurationBuilder>? configureWarnings = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ScanGuardDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w =>
                {
                    w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    configureWarnings?.Invoke(w);
                });

            return new ScanGuardDbContext(optionsBuilder.Options);
        }
    }
}
