using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies the First* safe-path contract: key-only queries succeed server-side; non-key
///     predicates, scan-like paths, and user-specified Limit(n) always throw — use
///     <c>.AsAsyncEnumerable().FirstOrDefaultAsync(ct)</c> for those shapes.
/// </summary>
public class FirstOrDefaultSafePathTests
{
    // ── Unsafe paths: always throw ──────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithoutOptIn_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && x.IsActive)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task FirstOrDefault_ScanLike_WithoutOptIn_ThrowsTranslationFailure()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // No PK equality → scan-like path.
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.IsActive)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_WithUserLimit_ThrowsTranslationFailure()
    {
        // Limit(n) + First* is always disallowed — use AsAsyncEnumerable() instead.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1")
                .Limit(5)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── Safe paths: key-only ─────────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_KeyOnly_PkEquality_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // PK equality + no non-key filter → safe.
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Translation succeeds; execution fails because mock returns nothing — that's fine.
        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_PkAndSkEquality_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && x.Sk == "S#1")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefault_KeyOnly_PkEqualitySkStartsWith_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // SK starts_with is a sort-key predicate — safe path (no non-key attributes).
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && x.Sk.StartsWith("ORDER#"))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ── SK filter predicates: unsafe (IN/OR are filter expressions, not key conditions) ──

    [Fact]
    public async Task FirstOrDefault_SkIn_WithoutOptIn_ThrowsTranslationFailure()
    {
        // SK IN (...) is a filter expression, not a DynamoDB key condition. DynamoDB Limit
        // counts scanned items — Limit=1 can silently miss matching rows later in the partition.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var ids = new[] { "S#1", "S#2" };
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && ids.Contains(x.Sk))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task FirstOrDefault_SkOrEquality_WithoutOptIn_ThrowsTranslationFailure()
    {
        // SK = A OR SK = B is a filter expression on the sort key, not a key condition.
        // DynamoDB Limit counts scanned items — Limit=1 can silently miss items in the partition.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && (x.Sk == "S#1" || x.Sk == "S#2"))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── No-sort-key special case ─────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefault_PkOnlyEntity_NonKeyFilter_NoOptInRequired_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // Entity has no sort key. PK equality makes each partition a single-item set —
        // non-key predicates are irrelevant, so opt-in is not required.
        var act = async ()
            => await context
                .PkOnlyItems
                .Where(x => x.Pk == "P#1" && x.IsActive)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ── Inheritance / shared-table (discriminator regression) ───────────────

    [Fact]
    public async Task FirstOrDefault_InheritanceHierarchy_PkAndSkEquality_Succeeds()
    {
        // Regression: the provider-injected discriminator predicate (Discriminator = 'ChildItem')
        // must not cause the First* validator to reject an otherwise key-only query.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = InheritanceDbContext.Create(client);

        var act = async ()
            => await context
                .Children
                .Where(x => x.Pk == "P#1" && x.Sk == "S#1")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefault_InheritanceHierarchy_PkOnly_Succeeds()
    {
        // PK-only on SK table with discriminator: allowed per safe-path rules.
        // Returns the first item in the partition that matches the discriminator.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = InheritanceDbContext.Create(client);

        var act = async ()
            => await context
                .Children
                .Where(x => x.Pk == "P#1")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefault_InheritanceHierarchy_NonKeyUserFilter_Throws()
    {
        // User-supplied non-key filter must still throw even on an inheritance hierarchy.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = InheritanceDbContext.Create(client);

        var act = async ()
            => await context
                .Children
                .Where(x => x.Pk == "P#1" && x.Label == "active")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── Support types ────────────────────────────────────────────────────────

    /// <summary>Entity with partition key and sort key.</summary>
    private sealed record PkSkItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Sk { get; set; } = null!;

        /// <summary>Non-key attribute used to trigger non-key filter validation.</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>Entity with partition key only — no sort key.</summary>
    private sealed record PkOnlyItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Non-key attribute used to test the no-sort-key special case.</summary>
        public bool IsActive { get; set; }
    }

    private sealed class SafePathDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PkSkItem> PkSkItems => Set<PkSkItem>();

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PkOnlyItem> PkOnlyItems => Set<PkOnlyItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PkSkItem>(b =>
            {
                b.ToTable("PkSkTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

            modelBuilder.Entity<PkOnlyItem>(b =>
            {
                b.ToTable("PkOnlyTable");
                b.HasPartitionKey(x => x.Pk);
                // No sort key configured.
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SafePathDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<SafePathDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    /// <summary>Abstract base for a TPH inheritance hierarchy on a shared DynamoDB table.</summary>
    private abstract record BaseItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Sk { get; set; } = null!;
    }

    /// <summary>Derived type — carries a non-key attribute to test user filter rejection.</summary>
    private sealed record ChildItem : BaseItem
    {
        /// <summary>Non-key attribute used to verify that user-supplied non-key filters still throw.</summary>
        public string Label { get; set; } = null!;
    }

    private sealed class InheritanceDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ChildItem> Children => Set<ChildItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseItem>(b =>
            {
                b.ToTable("InheritanceTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
            });

            modelBuilder.Entity<ChildItem>(b => b.HasBaseType<BaseItem>());
        }

        /// <summary>Provides functionality for this member.</summary>
        public static InheritanceDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<InheritanceDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
