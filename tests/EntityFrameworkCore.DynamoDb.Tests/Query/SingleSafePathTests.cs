using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Verifies server-side Single* support is key-condition-only and one request.</summary>
public class SingleSafePathTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_KeyOnly_PkEquality_UsesImplicitLimit2()
    {
        var (client, captured) = SetupMockClient(new ExecuteStatementResponse { Items = [] });
        await using var context = SingleDbContext.Create(client);

        var result = await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().HaveCount(1);
        captured.Single().Limit.Should().Be(2);
        captured.Single().Statement.Should().NotContain("LIMIT");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_KeyOnly_PkIn_UsesImplicitLimit2()
    {
        var (client, captured) = SetupMockClient(new ExecuteStatementResponse { Items = [] });
        await using var context = SingleDbContext.Create(client);
        var pks = new[] { "P#1", "P#2" };

        var result = await context
            .PkSkItems
            .Where(x => pks.Contains(x.Pk))
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        result.Should().BeNull();
        captured.Should().HaveCount(1);
        captured.Single().Limit.Should().Be(2);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Single_KeyOnly_ZeroItems_ThrowsNoElements()
    {
        var (client, _) = SetupMockClient(new ExecuteStatementResponse { Items = [] });
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1" && x.Sk == "S#1")
            .SingleAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Sequence contains no elements.");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_TwoItems_ThrowsMoreThanOneElement()
    {
        var (client, captured) = SetupMockClient(
            new ExecuteStatementResponse { Items = [CreateItem("S#1"), CreateItem("S#2")] });
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Sequence contains more than one element.");
        captured.Should().HaveCount(1);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_ResponseWithNextToken_ThrowsProviderGuard()
    {
        var (client, captured) = SetupMockClient(
            new ExecuteStatementResponse { Items = [CreateItem("S#1")], NextToken = "next" });
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*continuation token*Single/SingleOrDefault*");
        captured.Should().HaveCount(1);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_ResponseWithTwoItemsAndNextToken_ThrowsProviderGuard()
    {
        var (client, captured) = SetupMockClient(
            new ExecuteStatementResponse
            {
                Items = [CreateItem("S#1"), CreateItem("S#2")], NextToken = "next"
            });
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*continuation token*Single/SingleOrDefault*");
        captured.Should().HaveCount(1);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_NonKeyFilter_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1" && x.IsActive)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Single/SingleOrDefault*key-condition-only*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        SingleOrDefault_UnsafeFilteredQuery_NonKeyFilter_StillThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SingleDbContext.Create(client, true);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1" && x.IsActive)
            .AsUnsafeFilteredQuery()
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Single/SingleOrDefault*key-condition-only*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_SkIn_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SingleDbContext.Create(client);
        var sks = new[] { "S#1", "S#2" };

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1" && sks.Contains(x.Sk))
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*sort-key filter predicate*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_UserLimit_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .Limit(5)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Limit(n)*Single/SingleOrDefault*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefault_WithNextToken_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SingleDbContext.Create(client);

        var act = async () => await context
            .PkSkItems
            .Where(x => x.Pk == "P#1")
            .WithNextToken("seed-token")
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*WithNextToken*Single/SingleOrDefault*");
        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    private static (IAmazonDynamoDB client, List<ExecuteStatementRequest> captured) SetupMockClient(
        ExecuteStatementResponse response)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = new List<ExecuteStatementRequest>();

        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(r => captured.Add(r)),
                Arg.Any<CancellationToken>())
            .Returns(response);

        return (client, captured);
    }

    private static Dictionary<string, AttributeValue> CreateItem(string sk)
        => new()
        {
            ["pk"] = new AttributeValue("P#1"),
            ["sk"] = new AttributeValue(sk),
            ["isActive"] = new AttributeValue { BOOL = true }
        };

    private sealed record PkSkItem
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public bool IsActive { get; set; }
    }

    private sealed class SingleDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkSkItem> PkSkItems => Set<PkSkItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkSkItem>(b =>
            {
                b.ToTable("PkSkTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

        public static SingleDbContext Create(
            IAmazonDynamoDB client,
            bool allowUnsafeFilteredQueries = false)
        {
            var builder = new DbContextOptionsBuilder<SingleDbContext>().UseDynamo(options =>
            {
                options.DynamoDbClient(client);
                options.AllowUnsafeFilteredQueries(allowUnsafeFilteredQueries);
            });

            builder.ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));

            return new SingleDbContext(builder.Options);
        }
    }
}
