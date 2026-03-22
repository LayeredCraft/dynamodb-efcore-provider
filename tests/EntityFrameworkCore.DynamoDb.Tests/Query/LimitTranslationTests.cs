using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies that <c>Limit(n)</c> correctly flows through translation and sets
///     <c>ExecuteStatementRequest.Limit</c> during execution.
/// </summary>
public class LimitTranslationTests
{
    [Fact]
    public async Task Limit_SetsRequestLimit_OnToListAsync()
    {
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context.Items.Limit(10).ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Limit.Should().Be(10);
    }

    [Fact]
    public async Task Limit_ChainedTwice_LastOneWins()
    {
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context.Items.Limit(10).Limit(20).ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().Limit.Should().Be(20);
    }

    [Fact]
    public async Task Limit_OnToListAsync_SingleRequest()
    {
        // Limit(n) always stops after one request — no follow-up page calls.
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context.Items.Limit(5).ToListAsync(TestContext.Current.CancellationToken);

        captured.Should().HaveCount(1);
    }

    [Fact]
    public async Task Limit_WithFirstOrDefault_UserLimitWinsOverImplicitOne()
    {
        // First* sets an implicit Limit=1, but user Limit(n) must override it.
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .Limit(5)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        captured.Single().Limit.Should().Be(5);
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_ImplicitLimit1_SetOnRequest()
    {
        // No user Limit → First* should use implicit Limit=1.
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context
            .Items
            .Where(x => x.Pk == "P#1")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        captured.Single().Limit.Should().Be(1);
    }

    // ── Support types ────────────────────────────────────────────────────────

    /// <summary>Sets up a mock DynamoDB client that captures all ExecuteStatementAsync calls.</summary>
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

    private sealed record LimitTestEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;
    }

    private sealed class LimitTestDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<LimitTestEntity> Items => Set<LimitTestEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<LimitTestEntity>(b =>
            {
                b.ToTable("LimitTestTable");
                b.HasPartitionKey(x => x.Pk);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static LimitTestDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<LimitTestDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
