using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies unsupported LINQ operators fail with translation details instead of
///     NotImplementedException.
/// </summary>
public class UnsupportedOperatorTranslationTests
{
    [Fact]
    public async Task AnyAsync_ThrowsTranslationFailureWithDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async () => await context.Items.AnyAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*LINQ operator 'Any'*not currently supported*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task AllAsync_ThrowsTranslationFailureWithDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async ()
            => await context.Items.AllAsync(i => i.IsActive, TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*LINQ operator 'All'*not currently supported*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_ThrowsTranslationFailureWithDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async () => await context.Items.SingleOrDefaultAsync(
            i => i.Pk == "ITEM#1",
            TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*LINQ operator 'SingleOrDefault'*not currently supported*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task SingleAsync_UsesSingleOperatorNameInFailureDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async () => await context.Items.SingleAsync(
            i => i.Pk == "ITEM#1",
            TestContext.Current.CancellationToken);

        var exceptionAssertion = await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*LINQ operator 'Single'*")
            .WithMessage("*not currently supported*");

        exceptionAssertion.Which.Message.Should().NotContain("SingleOrDefault");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task BitwiseComplementInPredicate_ThrowsTranslationFailureWithDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async ()
            => await context
                .Items
                .Where(i => ~i.Priority > 0)
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*bitwise complement is not translated*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    private sealed record UnsupportedOperatorEntity
    {
        public string Pk { get; set; } = null!;

        public bool IsActive { get; set; }

        public int Priority { get; set; }
    }

    private sealed class UnsupportedOperatorDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UnsupportedOperatorEntity> Items => Set<UnsupportedOperatorEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedOperatorEntity>(builder =>
            {
                builder.ToTable("UnsupportedOperatorsTable");
                builder.HasKey(x => x.Pk);
                builder.HasPartitionKey(x => x.Pk);
            });

        public static UnsupportedOperatorDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<UnsupportedOperatorDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
