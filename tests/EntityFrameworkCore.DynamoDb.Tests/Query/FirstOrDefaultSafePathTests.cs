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
    public async Task FirstOrDefault_PkOnlyEntity_NonKeyFilter_ThrowsTranslationFailure()
    {
        // Even on a no-sort-key table, a non-key filter is not allowed with First*.
        // The absence of a sort key does not make a non-key predicate safe — it simply
        // means the partition holds one item, but the non-key filter is still a filter
        // expression that could mask intent. Consistency requires rejection.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = SafePathDbContext.Create(client);

        var act = async ()
            => await context
                .PkOnlyItems
                .Where(x => x.Pk == "P#1" && x.IsActive)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── Partition-only GSI: non-key filter must throw ────────────────────────

    [Fact]
    public async Task FirstOrDefault_PartitionOnlyGsi_NonKeyFilter_ThrowsTranslationFailure()
    {
        // A partition-only GSI can hold many items per partition — Limit=1 with a non-key
        // filter expression can silently miss matching items later in the GSI partition.
        // The guard must reject this regardless of whether the query source has a sort key.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = PartitionOnlyGsiDbContext.Create(client);

        var act = async ()
            => await context
                .GsiItems
                .WithIndex("ByPriority")
                .Where(x => x.Priority == 3 && x.Status == "OPEN")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    // ── Nested-path / list-index predicates: unsafe (DynamoScalarAccessExpression /
    // DynamoListIndexExpression regression) ──

    [Fact]
    public async Task FirstOrDefault_NestedOwnedPropertyPredicate_ThrowsTranslationFailure()
    {
        // Regression: ContainsNonKeyProperty() does not descend into DynamoScalarAccessExpression,
        // so x.Profile.City == "Seattle" (which becomes DynamoScalarAccessExpression wrapping a
        // SqlPropertyExpression("Profile")) is silently misclassified as key-only.
        // The guard must recognise the nested path as a non-key predicate and throw.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = NestedPathDbContext.Create(client);

        var act = async ()
            => await context
                .NestedItems
                .Where(x => x.Pk == "P#1" && x.Profile!.City == "Seattle")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task FirstOrDefault_ListIndexPredicate_ThrowsTranslationFailure()
    {
        // Regression: ContainsNonKeyProperty() does not descend into DynamoListIndexExpression,
        // so x.Tags[0] == "vip" (which becomes DynamoListIndexExpression wrapping
        // SqlPropertyExpression("Tags")) is silently misclassified as key-only.
        // The guard must recognise the list-index access as a non-key predicate and throw.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = NestedPathDbContext.Create(client);

        var act = async ()
            => await context
                .NestedItems
                .Where(x => x.Pk == "P#1" && x.Tags[0] == "vip")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task FirstOrDefault_DeepNestedOwnedPropertyPredicate_ThrowsTranslationFailure()
    {
        // Regression: a two-level nested path (x.Profile.Address.City) produces a chain of
        // DynamoScalarAccessExpression nodes. The guard must walk the whole chain and still
        // detect the non-key attribute, not stop at the first DynamoScalarAccessExpression.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = DeepNestedPathDbContext.Create(client);

        var act = async ()
            => await context
                .DeepItems
                .Where(x => x.Pk == "P#1" && x.Profile!.Address!.City == "Seattle")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
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
    public async Task FirstOrDefault_InheritanceHierarchy_PkOnly_ThrowsTranslationFailure()
    {
        // Derived/shared-table PK-only queries include a discriminator filter over a multi-item
        // partition. Limit=1 can evaluate a sibling type first and miss matching derived items
        // later, so the safe-path guard must reject this shape.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = InheritanceDbContext.Create(client);

        var act = async ()
            => await context
                .Children
                .Where(x => x.Pk == "P#1")
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
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

    // ── Nested-path / list-index support types ──────────────────────────────

    /// <summary>Single-level owned reference — used to exercise DynamoScalarAccessExpression in the guard.</summary>
    private sealed record FlatProfile
    {
        /// <summary>Non-key nested attribute.</summary>
        public string City { get; } = null!;
    }

    /// <summary>Two-level owned reference — wraps a second owned type to exercise deep nesting.</summary>
    private sealed record Address
    {
        /// <summary>Non-key nested attribute.</summary>
        public string City { get; } = null!;
    }

    /// <summary>Outer owned reference — contains a nested owned reference for deep-path tests.</summary>
    private sealed record DeepProfile
    {
        /// <summary>Nested owned reference.</summary>
        public Address? Address { get; set; }
    }

    /// <summary>Entity with PK, SK, an owned reference, and a primitive string list.</summary>
    private sealed record NestedPathItem
    {
        /// <summary>Partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Sort key — required so the no-sort-key bypass does not mask the bug.</summary>
        public string Sk { get; set; } = null!;

        /// <summary>Single-level owned reference whose properties are non-key attributes.</summary>
        public FlatProfile? Profile { get; set; }

        /// <summary>Primitive collection — element access becomes DynamoListIndexExpression.</summary>
        public List<string> Tags { get; set; } = [];
    }

    /// <summary>Entity with PK, SK, and a two-level owned reference for deep-nesting tests.</summary>
    private sealed record DeepNestedItem
    {
        /// <summary>Partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Sort key — required so the no-sort-key bypass does not mask the bug.</summary>
        public string Sk { get; set; } = null!;

        /// <summary>Two-level owned reference whose leaf property is a non-key attribute.</summary>
        public DeepProfile? Profile { get; set; }
    }

    private sealed class NestedPathDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<NestedPathItem> NestedItems => Set<NestedPathItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NestedPathItem>(b =>
            {
                b.ToTable("NestedPathTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.OwnsOne(x => x.Profile);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static NestedPathDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<NestedPathDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    private sealed class DeepNestedPathDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<DeepNestedItem> DeepItems => Set<DeepNestedItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DeepNestedItem>(b =>
            {
                b.ToTable("DeepNestedTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.OwnsOne(x => x.Profile, pb => pb.OwnsOne(p => p.Address));
            });

        /// <summary>Provides functionality for this member.</summary>
        public static DeepNestedPathDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<DeepNestedPathDbContext>()
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

    /// <summary>
    ///     Entity with a base table PK+SK and a partition-only GSI (no sort key on the index). Used
    ///     to verify that non-key filters on a partition-only GSI are rejected by First*.
    /// </summary>
    private sealed record GsiItem
    {
        /// <summary>Base table partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Base table sort key.</summary>
        public string Sk { get; set; } = null!;

        /// <summary>GSI partition key for the partition-only ByPriority index.</summary>
        public int Priority { get; set; }

        /// <summary>Non-key attribute — not part of any index key schema.</summary>
        public string Status { get; set; } = null!;
    }

    private sealed class PartitionOnlyGsiDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<GsiItem> GsiItems => Set<GsiItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GsiItem>(b =>
            {
                b.ToTable("GsiTable");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                // Partition-only GSI: Priority is the GSI PK, no GSI sort key.
                b.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PartitionOnlyGsiDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<PartitionOnlyGsiDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    /// <summary>Derived type — carries a non-key attribute to test user filter rejection.</summary>
    private sealed record ChildItem : BaseItem
    {
        /// <summary>Non-key attribute used to verify that user-supplied non-key filters still throw.</summary>
        public string Label { get; set; } = null!;
    }

    /// <summary>Sibling derived type used to force discriminator narrowing in inheritance safe-path tests.</summary>
    private sealed record SiblingItem : BaseItem;

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
            modelBuilder.Entity<SiblingItem>(b => b.HasBaseType<BaseItem>());
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
