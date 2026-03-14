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
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
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

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task
        StringCompareWithStringComparisonInPredicate_ThrowsTranslationFailureWithDetails()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = UnsupportedOperatorDbContext.Create(client);

        var act = async () => await context
            .Items
            .Where(i => string.Compare(i.Pk, "item#1", StringComparison.OrdinalIgnoreCase) == 0)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Method calls are not supported*predicate translation*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    private sealed record UnsupportedOperatorEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public bool IsActive { get; set; }

        /// <summary>Provides functionality for this member.</summary>
        public int Priority { get; set; }
    }

    private sealed class UnsupportedOperatorDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<UnsupportedOperatorEntity> Items => Set<UnsupportedOperatorEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedOperatorEntity>(builder =>
            {
                builder.ToTable("UnsupportedOperatorsTable");
                builder.HasPartitionKey(x => x.Pk);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static UnsupportedOperatorDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<UnsupportedOperatorDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
