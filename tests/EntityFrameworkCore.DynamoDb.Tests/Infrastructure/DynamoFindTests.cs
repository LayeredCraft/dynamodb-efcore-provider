using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>Tests DynamoDB provider find behavior.</summary>
public class DynamoFindTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ProviderServices_UseDefaultEntityFinderSource()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var finderSource = context.GetService<IEntityFinderSource>();

        finderSource.Should().BeOfType<EntityFinderSource>();
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
    public async Task FindAsync_HasKeyOnlyPk_UsesBaseTableAndLimit1()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var result =
            await context.HasKeyPkItems.FindAsync(["P#1"], TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().ContainSingle();
        captured.Single().Limit.Should().Be(1);
        captured.Single().Statement.Should().Contain("FROM \"FindHasKeyPkItems\"");
        captured.Single().Statement.Should().Contain("\"pk_attr\" = ?");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_HasKeyOnlyComposite_UsesKeyOrderAndLimit1()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var result = await context.HasKeyCompositeItems.FindAsync(
            ["P#1", "S#1"],
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().ContainSingle();
        captured.Single().Limit.Should().Be(1);
        captured.Single().Statement.Should().Contain("\"pk_attr\" = ?");
        captured.Single().Statement.Should().Contain("\"sk_attr\" = ?");
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
    public async Task FindAsync_PkOnly_UsesBaseTable_WhenIndexSelectionCouldSelectSecondaryIndex()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        _ = await context.PkItems.FindAsync(["P#1"], TestContext.Current.CancellationToken);

        captured.Should().ContainSingle();
        captured.Single().Statement.Should().Contain("FROM \"FindPkItems\"");
        captured.Single().Statement.Should().NotContain("\"ByPkName\"");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_CancellationTokenAsLastKeyValue_UsesToken()
    {
        var (client, _) = SetupMockClient(out var capturedTokens);
        await using var context = FindTestDbContext.Create(client);
        using var cts = new CancellationTokenSource();

        _ = await context.PkItems.FindAsync(["P#1", cts.Token]);

        capturedTokens.Should().ContainSingle();
        capturedTokens.Single().Should().Be(cts.Token);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_SimpleKey_WithTooManyValues_ThrowsArgumentException()
    {
        var (client, _) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var act = async ()
            => await context.PkItems.FindAsync(
                ["P#1", "extra"],
                TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*single key property*2 values*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_CompositeKey_WithWrongValueCount_ThrowsArgumentException()
    {
        var (client, _) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var act = async ()
            => await context.CompositeItems.FindAsync(
                ["P#1"],
                TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*2-part composite key*1 values*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_WrongKeyType_ThrowsArgumentException()
    {
        var (client, _) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);

        var act = async ()
            => await context.PkItems.FindAsync([1], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*value at position 0*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task FindAsync_TrackedEntity_ReturnsTrackedInstance_WithoutNetworkCall()
    {
        var (client, captured) = SetupMockClient();
        await using var context = FindTestDbContext.Create(client);
        var tracked = context.Attach(new PkItem { Pk = "P#tracked", Name = "Tracked" }).Entity;

        var result =
            await context.PkItems.FindAsync(["P#tracked"], TestContext.Current.CancellationToken);

        result.Should().BeSameAs(tracked);
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Find_UntrackedEntity_ThrowsSyncQueryError()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var act = () => context.PkItems.Find("P#1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sync enumerating*DynamoDB*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Find_TrackedEntity_ReturnsTrackedInstance_WithoutNetworkCall()
    {
        var (client, captured) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);
        var tracked = context.Attach(new PkItem { Pk = "P#tracked", Name = "Tracked" }).Entity;

        var result = context.PkItems.Find("P#tracked");

        result.Should().BeSameAs(tracked);
        captured.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DbContextFind_UntrackedEntity_ThrowsSyncQueryError()
    {
        var (client, _) = SetupMockClient();
        using var context = FindTestDbContext.Create(client);

        var act = () => context.Find<PkItem>("P#1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sync enumerating*DynamoDB*");
    }

    private static (IAmazonDynamoDB client, List<ExecuteStatementRequest> captured)
        SetupMockClient()
        => SetupMockClient(out _);

    private static (IAmazonDynamoDB client, List<ExecuteStatementRequest> captured) SetupMockClient(
        out List<CancellationToken> capturedTokens)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();
        capturedTokens = [];

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(captured.Add),
                Arg.Do<CancellationToken>(capturedTokens.Add))
            .Returns(new ExecuteStatementResponse { Items = [] });

        return (client, captured);
    }

    private sealed class FindTestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkItem> PkItems => Set<PkItem>();

        public DbSet<CompositeItem> CompositeItems => Set<CompositeItem>();

        public DbSet<HasKeyPkItem> HasKeyPkItems => Set<HasKeyPkItem>();

        public DbSet<HasKeyCompositeItem> HasKeyCompositeItems => Set<HasKeyCompositeItem>();

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
                builder.HasGlobalSecondaryIndex("ByPkName", x => x.Pk, x => x.Name);
            });

            modelBuilder.Entity<CompositeItem>(builder =>
            {
                builder.ToTable("FindCompositeItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
            });

            modelBuilder.Entity<HasKeyPkItem>(builder =>
            {
                builder.ToTable("FindHasKeyPkItems");
                builder.HasKey(x => x.Pk);
                builder.Property(x => x.Pk).HasAttributeName("pk_attr");
            });

            modelBuilder.Entity<HasKeyCompositeItem>(builder =>
            {
                builder.ToTable("FindHasKeyCompositeItems");
                builder.HasKey(x => new { x.Pk, x.Sk });
                builder.Property(x => x.Pk).HasAttributeName("pk_attr");
                builder.Property(x => x.Sk).HasAttributeName("sk_attr");
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

    private sealed class HasKeyPkItem
    {
        public string Pk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class HasKeyCompositeItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }
}
