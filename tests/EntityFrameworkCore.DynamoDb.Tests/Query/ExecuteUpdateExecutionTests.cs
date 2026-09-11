using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies ExecuteUpdate end-to-end over the translation, PartiQL generation, and
///     storage-execution pipeline against a captured mock client: exact statement text,
///     parameter ordering (SET before WHERE), and 0/1 affected-count semantics.
/// </summary>
public class ExecuteUpdateExecutionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_ByKeyOnly_ProducesExactPartiQl_AndReturnsOne()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"name\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Should().HaveCount(1);
        request.Parameters[0].S.Should().Be("updated");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithExtraPredicate_KeepsNonKeyPredicate()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1" && i.Count == 7)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"name\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1' AND \"count\" = 7");
        request.Parameters.Should().HaveCount(1);
        request.Parameters[0].S.Should().Be("updated");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithCapturedValue_ParameterizesValue()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);
        var newValue = "captured";

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, newValue),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"name\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Should().HaveCount(1);
        request.Parameters[0].S.Should().Be("captured");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithNullValue_GeneratesSetNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);
        string? nullValue = null;

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, nullValue),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"name\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Should().HaveCount(1);
        request.Parameters[0].Should().BeEquivalentTo(new AttributeValue { NULL = true });
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithNumericSelfReference_GeneratesArithmetic()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Count, i => i.Count + 3),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"count\" = \"count\" + 3\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Should().BeNullOrEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithNumericSelfReferenceSubtract_GeneratesMinus()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Count, i => i.Count - 1),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request.Statement.Should().Contain("SET \"count\" = \"count\" - 1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithMultipleSetters_OrdersSetParamsBeforeWhere()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated").SetProperty(i => i.Count, 42),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"name\" = ?, \"count\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Select(p => p.S ?? p.N).Should().Equal(["updated", "42"]);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithNestedPath_GeneratesNestedSetClause()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Profile.City, "Seattle"),
                TestContext.Current.CancellationToken);

        affected.Should().Be(1);

        var request = captured.Single();
        request
            .Statement
            .Should()
            .Be(
                "UPDATE \"ExecuteUpdateTestsTable\"\n"
                + "SET \"profile\".\"city\" = ?\n"
                + "WHERE \"pk\" = 'pk1' AND \"sk\" = 'sk1'");
        request.Parameters.Should().HaveCount(1);
        request.Parameters[0].S.Should().Be("Seattle");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WhenItemMissing_ReturnsZero()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var captured = CaptureRequests(client, true);
        await using var context = ExecuteUpdateDbContext.Create(client);

        var affected = await context
            .Items
            .Where(i => i.Pk == "missing" && i.Sk == "missing")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken);

        affected.Should().Be(0);
        captured.Should().HaveCount(1);
    }

    private static List<ExecuteStatementRequest> CaptureRequests(
        IAmazonDynamoDB client,
        bool throwsConditionalCheckFailed = false)
    {
        var captured = new List<ExecuteStatementRequest>();

        if (throwsConditionalCheckFailed)
            client
                .ExecuteStatementAsync(
                    Arg.Do<ExecuteStatementRequest>(r => captured.Add(r)),
                    Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromException<ExecuteStatementResponse>(
                        new ConditionalCheckFailedException("The conditional request failed")));
        else
            client
                .ExecuteStatementAsync(
                    Arg.Do<ExecuteStatementRequest>(r => captured.Add(r)),
                    Arg.Any<CancellationToken>())
                .Returns(new ExecuteStatementResponse { Items = [] });

        return captured;
    }

    private sealed record ExecuteUpdateEntity
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Count { get; set; }

        public ProfileShape Profile { get; set; } = null!;
    }

    private sealed record ProfileShape
    {
        public string City { get; set; } = null!;
    }

    private sealed class ExecuteUpdateDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExecuteUpdateEntity> Items => Set<ExecuteUpdateEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExecuteUpdateEntity>(builder =>
            {
                builder.ToTable("ExecuteUpdateTestsTable");
                builder.HasPartitionKey(x => x.Pk);
                builder.HasSortKey(x => x.Sk);
                builder.ComplexProperty(x => x.Profile);
            });

        public static ExecuteUpdateDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<ExecuteUpdateDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w
                            .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                            .Ignore(DynamoEventId.ScanLikeQueryDetected))
                    .Options);
    }
}
