using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

public class ExecuteDeleteTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteDeleteAsync_ByKeyAndPredicate_GeneratesDeleteAndReturnsOne()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var requests = CaptureRequests(client);
        await using var context = CreateContext(client);

        var affected =
            await context
                .Items
                .Where(item => item.Pk == "pk1" && item.Sk == "sk1" && item.IsTarget)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        requests
            .Single()
            .Statement
            .Should()
            .Be(
                "DELETE FROM \"ExecuteDeleteTestsTable\"\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1' AND \"isTarget\" = TRUE");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteDeleteAsync_WhenItemDoesNotMatch_ReturnsZero()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var requests = CaptureRequests(client, true);
        await using var context = CreateContext(client);

        var affected =
            await context
                .Items
                .Where(item => item.Pk == "missing" && item.Sk == "missing")
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(0);
        requests.Should().ContainSingle();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteDeleteAsync_WithoutFullKey_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateContext(client);

        var exception = await Record.ExceptionAsync(()
            => context
                .Items
                .Where(item => item.Pk == "pk1")
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        InnermostMessage(exception)
            .Should()
            .Contain("ExecuteDelete requires the WHERE clause")
            .And
            .Contain("equality-constrain the sort key");
    }

    private static List<ExecuteStatementRequest> CaptureRequests(
        IAmazonDynamoDB client,
        bool throwsConditionalCheckFailed = false)
    {
        var requests = new List<ExecuteStatementRequest>();
        client
            .ExecuteStatementAsync(
                Arg.Do<ExecuteStatementRequest>(request => requests.Add(request)),
                Arg.Any<CancellationToken>())
            .Returns(
                throwsConditionalCheckFailed
                    ? Task.FromException<ExecuteStatementResponse>(
                        new ConditionalCheckFailedException("The conditional request failed"))
                    : Task.FromResult(new ExecuteStatementResponse()));
        return requests;
    }

    private static string InnermostMessage(Exception? exception)
    {
        exception.Should().NotBeNull();
        while (exception!.InnerException is not null)
            exception = exception.InnerException;

        return exception.Message;
    }

    private static ExecuteDeleteDbContext CreateContext(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<ExecuteDeleteDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
                .Options);

    private sealed class ExecuteDeleteDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExecuteDeleteEntity> Items => Set<ExecuteDeleteEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExecuteDeleteEntity>(builder =>
            {
                builder.ToTable("ExecuteDeleteTestsTable");
                builder.HasPartitionKey(item => item.Pk);
                builder.HasSortKey(item => item.Sk);
            });
    }

    private sealed class ExecuteDeleteEntity
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public bool IsTarget { get; set; }
    }
}
