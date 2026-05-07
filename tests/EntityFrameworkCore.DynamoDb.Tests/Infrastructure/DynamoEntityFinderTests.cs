using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>Tests DynamoDB provider find behavior.</summary>
public class DynamoEntityFinderTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ProviderServices_ResolveDynamoEntityFinderSource()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var finderSource = context.GetService<IEntityFinderSource>();

        finderSource.Should().BeOfType<DynamoEntityFinderSource>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_PkOnly_SetsLimit1_OnExecuteStatementRequest()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var result =
            await context.PkItems.FindAsync(["P#1"], TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().ContainSingle();
        captured.Single().Limit.Should().Be(1);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_CompositeKey_SetsLimit1_OnExecuteStatementRequest()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var result =
            await context.CompositeItems.FindAsync(
                ["P#1", "S#1"],
                TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().ContainSingle();
        captured.Single().Limit.Should().Be(1);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_MissingKey_ReturnsNull()
    {
        var (client, _) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var result =
            await context.PkItems.FindAsync(["P#missing"], TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_TrackedEntity_ReturnsTrackedInstance_WithoutNetworkCall()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);
        var tracked = context.Attach(new PkItem { Pk = "P#tracked", Name = "Tracked", }).Entity;

        var result =
            await context.PkItems.FindAsync(["P#tracked"], TestContext.Current.CancellationToken);

        result.Should().BeSameAs(tracked);
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Find_AlwaysThrowsAsyncOnlyError()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var act = () => context.PkItems.Find("P#1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Synchronous Find*FindAsync*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Find_TrackedEntity_StillThrowsAsyncOnlyError()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);
        context.Attach(new PkItem { Pk = "P#tracked", Name = "Tracked", });

        var act = () => context.PkItems.Find("P#tracked");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Synchronous Find*FindAsync*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DbContextFind_AlsoThrowsAsyncOnlyError()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var act = () => context.Find<PkItem>("P#1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Synchronous Find*FindAsync*");
    }

    private static (IAmazonDynamoDB client, List<ExecuteStatementRequest> captured)
        SetupMockClient()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(captured.Add),
                Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [], });

        return (client, captured);
    }

    private sealed class FindTestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkItem> PkItems => Set<PkItem>();

        public DbSet<CompositeItem> CompositeItems => Set<CompositeItem>();

        public static FindTestDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<FindTestDbContext>()
                    .ConfigureWarnings(warnings
                        => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .Options);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PkItem>(builder =>
            {
                builder.ToTable("FindPkItems");
                builder.HasPartitionKey(x => x.Pk);
            });

            modelBuilder.Entity<CompositeItem>(builder =>
            {
                builder.ToTable("FindCompositeItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
            });
        }
    }

    private sealed class PkItem
    {
        public string Pk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class CompositeItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }
}
