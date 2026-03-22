using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for DynamoDbQueryableExtensions — Limit, WithIndex, WithoutIndex.</summary>
public class DynamoDbQueryableExtensionsTests
{
    // ── Limit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Limit_Zero_ThrowsArgumentOutOfRangeException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.Limit(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact]
    public void Limit_Negative_ThrowsArgumentOutOfRangeException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.Limit(-5);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact]
    public void Limit_Positive_ReturnsNewQueryable()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = LimitDbContext.Create(client);

        var original = context.Items.AsQueryable();
        var limited = original.Limit(10);

        // A new IQueryable wrapping a different expression is returned.
        limited.Should().NotBeSameAs(original);
    }

    [Fact]
    public void Limit_OnNonEntityQueryProvider_ReturnsSourceUnchanged()
    {
        // Array.AsQueryable() uses EnumerableQuery<T>, not EntityQueryProvider.
        var source = new[] { new TestEntity() }.AsQueryable();

        var result = source.Limit(5);

        result.Should().BeSameAs(source);
    }

    // ── WithIndex / WithoutIndex (regression) ────────────────────────────────

    [Fact]
    public void WithIndex_EmptyString_ThrowsArgumentException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.WithIndex(string.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("indexName");
    }

    [Fact]
    public void WithoutIndex_ReturnsNewQueryable()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = LimitDbContext.Create(client);

        var original = context.Items.AsQueryable();
        var withoutIndex = original.WithoutIndex();

        withoutIndex.Should().NotBeSameAs(original);
    }

    // ── Support types ────────────────────────────────────────────────────────

    private sealed class TestEntity;

    private sealed record LimitEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;
    }

    private sealed class LimitDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<LimitEntity> Items => Set<LimitEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<LimitEntity>(b =>
            {
                b.ToTable("LimitTable");
                b.HasPartitionKey(x => x.Pk);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static LimitDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<LimitDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
