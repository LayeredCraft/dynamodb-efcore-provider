using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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

    [Fact(
        Skip =
            "CanConnectAsync now performs an AWS ListTables probe; covered by integration tests.")]
    public Task CanConnectAsync_ThrowsNotSupportedException() => Task.CompletedTask;

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
