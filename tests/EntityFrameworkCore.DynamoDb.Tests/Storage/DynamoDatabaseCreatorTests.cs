using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests unsupported database lifecycle APIs for the DynamoDB provider.</summary>
public sealed class DynamoDatabaseCreatorTests
{
    private const string DatabaseLifecycleNotSupported =
        "The DynamoDB database provider only supports async database lifecycle operations. Use EnsureCreatedAsync, EnsureDeletedAsync, or CanConnectAsync.";

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnsureCreated_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.EnsureCreated());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(
        Skip =
            "EnsureCreatedAsync now performs AWS table lifecycle operations; covered by integration tests.")]
    public Task EnsureCreatedAsync_ThrowsNotSupportedException() => Task.CompletedTask;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnsureDeleted_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.EnsureDeleted());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(
        Skip =
            "EnsureDeletedAsync now performs AWS table lifecycle operations; covered by integration tests.")]
    public Task EnsureDeletedAsync_ThrowsNotSupportedException() => Task.CompletedTask;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CanConnect_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception = Assert.Throws<NotSupportedException>(() => context.Database.CanConnect());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_ReturnsTrue_WhenListTablesSucceeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ListTablesAsync(Arg.Any<ListTablesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListTablesResponse());
        await using var context = CreateContext(client);

        var result = await context.Database.CanConnectAsync();

        result.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_RethrowsOperationCanceledException()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ListTablesAsync(Arg.Any<ListTablesRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ListTablesResponse>>(_ => throw new OperationCanceledException());
        await using var context = CreateContext(client);

        var act = () => context.Database.CanConnectAsync();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_ReturnsFalse_WhenListTablesFails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ListTablesAsync(Arg.Any<ListTablesRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ListTablesResponse>>(_ => throw new AmazonDynamoDBException("Boom"));
        await using var context = CreateContext(client);

        var result = await context.Database.CanConnectAsync();

        result.Should().BeFalse();
    }

    private static DatabaseCreatorContext CreateContext(IAmazonDynamoDB? client = null)
    {
        var options = new DbContextOptionsBuilder<DatabaseCreatorContext>()
            .UseDynamo(options
                => options.DynamoDbClient(client ?? Substitute.For<IAmazonDynamoDB>()))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

        return new DatabaseCreatorContext(options);
    }

    private sealed class DatabaseCreatorContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DatabaseCreatorEntity>(b =>
            {
                b.ToTable("DatabaseCreator");
                b.HasPartitionKey(e => e.Id);
            });
    }

    private sealed class DatabaseCreatorEntity
    {
        public string Id { get; set; } = null!;
    }
}
