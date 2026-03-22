using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Verifies the First* safe-path contract from ADR-002: key-only queries are safe by default;
///     non-key predicates and scan-like paths require <c>.WithNonKeyFilter()</c> opt-in.
/// </summary>
public class FirstOrDefaultSafePathTests
{
    // ── Unsafe paths: throw without opt-in ──────────────────────────────────

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
            .WithMessage("*WithNonKeyFilter*");

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
            .WithMessage("*WithNonKeyFilter*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── Safe paths: no opt-in required ──────────────────────────────────────

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

    // ── Opt-in: WithNonKeyFilter lets unsafe paths through ───────────────────

    [Fact]
    public async Task FirstOrDefault_NonKeyFilter_WithOptIn_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1" && x.IsActive)
                .WithNonKeyFilter()
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstOrDefault_ScanLike_WithOptIn_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.IsActive)
                .WithNonKeyFilter()
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
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

    // ── WithNonKeyFilter on other terminals ──────────────────────────────────

    [Fact]
    public async Task WithNonKeyFilter_OnToListAsync_IsNoOp_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // WithNonKeyFilter on ToListAsync should be a silent no-op (no validation, no error).
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.IsActive)
                .WithNonKeyFilter()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WithNonKeyFilter_OnKeyOnlyFirst_IsNoOp_Succeeds()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        // Key-only query is already safe; WithNonKeyFilter is accepted silently.
        var act = async ()
            => await context
                .PkSkItems
                .Where(x => x.Pk == "P#1")
                .WithNonKeyFilter()
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
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
}
