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
#pragma warning disable EF9102
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
    public async Task WithNextToken_SetsRequestNextToken_OnFirstRequest()
    {
        var (client, captured) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        await context
            .Items
            .WithNextToken("seed-token")
            .ToListAsync(TestContext.Current.CancellationToken);

        captured.Single().NextToken.Should().Be("seed-token");
    }

    [Fact]
    public async Task WithNextToken_CalledTwice_ThrowsTranslationFailure()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .WithNextToken("token-a")
            .WithNextToken("token-b")
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*WithNextToken*only be applied once*");
    }

    [Fact]
    public async Task ToPageAsync_InSubqueryShape_FailsFast()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .Select(x => context
                .Items
                .Where(y => y.Pk == x.Pk)
                .ToPageAsync(5, null, TestContext.Current.CancellationToken))
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ToPageAsync_WithLimit_ThrowsTranslationFailure()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .Limit(5)
            .ToPageAsync(10, null, TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ToPageAsync*Limit*");
    }

    [Fact]
    public async Task ToPageAsync_WithNonNullTokenAndWithNextToken_ThrowsTranslationFailure()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .WithNextToken("seed-token")
            .ToPageAsync(10, "method-token", TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only one non-null pagination token may be specified*");
    }

    [Fact]
    public async Task
        ToPageAsync_WithParameterizedTokenAndWithNextToken_ResolvesAmbiguityAtRuntime()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        string? nullableMethodToken = null;
        var nullTokenPage = await context
            .Items
            .WithNextToken("seed-token")
            .ToPageAsync(10, nullableMethodToken, TestContext.Current.CancellationToken);

        nullTokenPage.Should().NotBeNull();

        nullableMethodToken = "method-token";
        var act = async () => await context
            .Items
            .WithNextToken("seed-token")
            .ToPageAsync(10, nullableMethodToken, TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only one non-null pagination token may be specified*");
    }

    [Fact]
    public async Task ToPageAsync_ReturnsSinglePage_WithItemsAndNextToken()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured.Add(r)),
                Arg.Any<CancellationToken>())
            .Returns(
                new ExecuteStatementResponse
                {
                    Items =
                    [
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "P#1" },
                        },
                    ],
                    NextToken = "next-page-token",
                });

        await using var context = LimitTestDbContext.Create(client);

        var page = await context.Items.ToPageAsync(10, null, TestContext.Current.CancellationToken);

        captured.Should().HaveCount(1);
        captured.Single().Limit.Should().Be(10);
        captured.Single().NextToken.Should().BeNull();

        page.Items.Should().HaveCount(1);
        page.Items.Single().Pk.Should().Be("P#1");
        page.NextToken.Should().Be("next-page-token");
        page.HasMoreResults.Should().BeTrue();
    }

    [Fact]
    public async Task ToPageAsync_EmptyResponseToken_NormalizesToNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [], NextToken = string.Empty, });

        await using var context = LimitTestDbContext.Create(client);

        var page = await context.Items.ToPageAsync(10, null, TestContext.Current.CancellationToken);

        page.NextToken.Should().BeNull();
        page.HasMoreResults.Should().BeFalse();
    }

    [Fact]
    public async Task Limit_WithFirstOrDefault_ThrowsTranslationFailure()
    {
        // Limit(n) + First* is disallowed — use
        // .Limit(n).AsAsyncEnumerable().FirstOrDefaultAsync(ct).
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .Where(x => x.Pk == "P#1")
            .Limit(5)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
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

    [Fact]
    public async Task WithNextToken_WithFirstOrDefault_ThrowsTranslationFailure()
    {
        var (client, _) = SetupMockClient();
        await using var context = LimitTestDbContext.Create(client);

        var act = async () => await context
            .Items
            .Where(x => x.Pk == "P#1")
            .WithNextToken("seed-token")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*WithNextToken*First/FirstOrDefault*");
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

#pragma warning restore EF9102
}
