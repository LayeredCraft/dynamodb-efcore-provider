using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Tests.Storage;

/// <summary>Tests unsupported database lifecycle APIs for the DynamoDB provider.</summary>
public sealed class DynamoDatabaseCreatorTests
{
    private const string DatabaseLifecycleNotSupported =
        "The DynamoDB database provider does not support database lifecycle operations.";

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnsureCreated_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.EnsureCreated());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EnsureDeleted_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception =
            Assert.Throws<NotSupportedException>(() => context.Database.EnsureDeleted());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureDeletedAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CanConnect_ThrowsNotSupportedException()
    {
        using var context = CreateContext();

        var exception = Assert.Throws<NotSupportedException>(() => context.Database.CanConnect());

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_ThrowsNotSupportedException()
    {
        await using var context = CreateContext();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(()
            => context.Database.CanConnectAsync(TestContext.Current.CancellationToken));

        exception.Message.Should().Be(DatabaseLifecycleNotSupported);
    }

    private static DatabaseCreatorContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DatabaseCreatorContext>()
            .UseDynamo()
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
