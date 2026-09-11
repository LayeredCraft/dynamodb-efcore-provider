using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies ExecuteUpdate translation rejects unsupported shapes with actionable errors.
///     Accepted shapes are covered by statement-generation and integration tests. Assertions
///     target the innermost message because EF10 and EF11 wrap non-query translation failures
///     with different outer exception messages.
/// </summary>
public class ExecuteUpdateTranslationTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithIndex_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .WithIndex("GSI1")
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("index 'GSI1'").And.Contain("base table");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithoutPartitionKeyEquality_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Name == "value")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("equality-constrain the partition key");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithoutSortKeyEquality_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("equality-constrain the sort key");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithPartitionKeyIn_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);
        var keys = new[] { "pk1", "pk2" };

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => keys.Contains(i.Pk))
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("partition key cannot be constrained with IN");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithSortKeyRange_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && string.Compare(i.Sk, "sk1") > 0)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("sort key must be equality-constrained");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithOrTouchingKey_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1" && (i.Name == "a" || i.Sk == "b"))
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, "updated"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception)
            .Should()
            .Contain("OR predicates must not reference key attributes");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExecuteUpdate_Sync_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = ExecuteUpdateDbContext.Create(client);

        var exception = Record.Exception(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdate(s => s.SetProperty(i => i.Name, "updated")));

        exception.Should().NotBeNull();
        InnermostMessage(exception).Should().Contain("Use ExecuteUpdateAsync");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithDuplicateSetterTarget_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(i => i.Name, "updated")
                    .SetProperty(i => i.Name, "updated-again"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("multiple setters targeting attribute 'name'");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithStringConcatenation_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Name, i => i.Name + "-suffix"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("String concatenation is not supported");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithMultiplySelfReference_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Count, i => i.Count * 2),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception)
            .Should()
            .Contain("supports only addition and subtraction")
            .And
            .Contain("Multiply");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithDivideSelfReference_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Count, i => i.Count / 2),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception)
            .Should()
            .Contain("supports only addition and subtraction")
            .And
            .Contain("Divide");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithSelfReferenceOnConvertedProperty_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Converted, i => i.Converted + 1),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("value converter");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithUnmappedProperty_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => EF.Property<string>(i, "NotAProperty"), "value"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception)
            .Should()
            .Contain("must be a member path over the entity parameter");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ExecuteUpdateAsync_WithKeyMutation_Throws()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = ExecuteUpdateDbContext.Create(client);

        var exception = await Record.ExceptionAsync(() => context
            .Items
            .Where(i => i.Pk == "pk1" && i.Sk == "sk1")
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Sk, "sk2"),
                TestContext.Current.CancellationToken));

        InnermostMessage(exception).Should().Contain("cannot set key property");
    }

    /// <summary>Unwraps nested exception wrappers down to the innermost non-empty message.</summary>
    private static string InnermostMessage(Exception? exception)
    {
        exception.Should().NotBeNull();

        var current = exception!;
        while (current.InnerException is { } inner)
            current = inner;

        return current.Message;
    }

    private sealed record ExecuteUpdateEntity
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Count { get; set; }

        public int Converted { get; set; }
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
                builder.Property(x => x.Converted).HasConversion(v => v + 100, v => v - 100);
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
