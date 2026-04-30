using System.ComponentModel.DataAnnotations.Schema;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <c>DynamoKeyDiscoveryConvention</c> — verifies that conventional DynamoDB
///     property names (<c>PK</c>/<c>PartitionKey</c>, <c>SK</c>/<c>SortKey</c>) are auto-configured as
///     the DynamoDB partition/sort key annotations without any explicit <c>HasPartitionKey</c> or
///     <c>HasSortKey</c> call.
/// </summary>
public class DynamoKeyDiscoveryConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    // -------------------------------------------------------------------
    // PK property → partition key auto-configured; EF PK rebuilt
    // -------------------------------------------------------------------

    private sealed record PkNamedEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class PkNamedContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PkNamedEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkNamedEntity>(b =>
            {
                b.ToTable("PkNamedTable");
                // No HasPartitionKey, no HasKey — convention should do all the work
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PkNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PkNamedContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertyNamedPK_AutoConfiguresPartitionKeyAndEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("PK");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // PartitionKey property → partition key auto-configured; EF PK rebuilt
    // -------------------------------------------------------------------

    private sealed record PartitionKeyNamedEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartitionKey { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class PartitionKeyNamedContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PartitionKeyNamedEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PartitionKeyNamedEntity>(b =>
            {
                b.ToTable("PartitionKeyNamedTable");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PartitionKeyNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyNamedContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertyNamedPartitionKey_AutoConfiguresPartitionKeyAndEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PartitionKeyNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PartitionKeyNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("PartitionKey");
        entityType.GetPartitionKeyPropertyName().Should().Be("PartitionKey");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // PK + SK properties → composite EF PK auto-configured
    // -------------------------------------------------------------------

    private sealed record PkSkNamedEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed class PkSkNamedContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PkSkNamedEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkSkNamedEntity>(b =>
            {
                b.ToTable("PkSkNamedTable");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PkSkNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PkSkNamedContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertiesNamedPKAndSK_AutoConfiguresCompositeEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkSkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkSkNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PK");
        primaryKey.Properties[1].Name.Should().Be("SK");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().Be("SK");
    }

    // -------------------------------------------------------------------
    // PartitionKey + SortKey properties → composite EF PK auto-configured
    // -------------------------------------------------------------------

    private sealed record PartitionSortKeyNamedEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartitionKey { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortKey { get; set; } = null!;
    }

    private sealed class PartitionSortKeyNamedContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PartitionSortKeyNamedEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PartitionSortKeyNamedEntity>(b =>
            {
                b.ToTable("PartitionSortKeyNamedTable");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PartitionSortKeyNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionSortKeyNamedContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertiesNamedPartitionKeyAndSortKey_AutoConfiguresCompositeEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PartitionSortKeyNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PartitionSortKeyNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PartitionKey");
        primaryKey.Properties[1].Name.Should().Be("SortKey");
        entityType.GetPartitionKeyPropertyName().Should().Be("PartitionKey");
        entityType.GetSortKeyPropertyName().Should().Be("SortKey");
    }

    // -------------------------------------------------------------------
    // Explicit HasPartitionKey overrides convention discovery
    // -------------------------------------------------------------------

    private sealed record ExplicitOverrideEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string CustomKey { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class ExplicitOverrideContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ExplicitOverrideEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitOverrideEntity>(b =>
            {
                b.ToTable("ExplicitOverrideTable");
                // Explicit fluent API call (Explicit source) overrides the convention (Convention
                // source)
                b.HasPartitionKey(x => x.CustomKey);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ExplicitOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitOverrideContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasPartitionKey_OverridesNameBasedDiscovery()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitOverrideContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitOverrideEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        // The explicit HasPartitionKey wins over the PK-named property
        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("CustomKey");
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomKey");
    }

    // -------------------------------------------------------------------
    // Explicit HasSortKey overrides convention discovery
    // -------------------------------------------------------------------

    private sealed record ExplicitSkOverrideEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string CustomSort { get; set; } = null!;
    }

    private sealed class ExplicitSkOverrideContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ExplicitSkOverrideEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitSkOverrideEntity>(b =>
            {
                b.ToTable("ExplicitSkOverrideTable");
                // Explicit HasSortKey points to CustomSort rather than the conventionally-named SK
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.CustomSort);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ExplicitSkOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitSkOverrideContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasSortKey_OverridesNameBasedDiscovery()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitSkOverrideContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitSkOverrideEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PK");
        primaryKey.Properties[1].Name.Should().Be("CustomSort");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().Be("CustomSort");
    }

    private sealed record ConventionalPkExplicitSortEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string CustomSort { get; set; } = null!;
    }

    private sealed class ConventionalPkExplicitSortContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ConventionalPkExplicitSortEntity> Entities { get; set; } = null!;

        /// <summary>
        ///     Configures only the sort key explicitly so the partition key must still be discovered from
        ///     the conventional <c>PK</c> name.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConventionalPkExplicitSortEntity>(b =>
            {
                b.ToTable("ConventionalPkExplicitSortTable");
                b.HasSortKey(x => x.CustomSort);
            });

        /// <summary>Creates a context instance using the DynamoDB test options.</summary>
        /// <returns>A configured test context.</returns>
        public static ConventionalPkExplicitSortContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConventionalPkExplicitSortContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ConventionalPartitionKey_WithExplicitSortKey_ResolvesBothRoles()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ConventionalPkExplicitSortContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ConventionalPkExplicitSortEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PK");
        primaryKey.Properties[1].Name.Should().Be("CustomSort");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().Be("CustomSort");
    }

    private sealed record ExplicitPartitionConventionalSkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string CustomPartition { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed class ExplicitPartitionConventionalSkContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ExplicitPartitionConventionalSkEntity> Entities { get; set; } = null!;

        /// <summary>
        ///     Configures only the partition key explicitly so the sort key must still be discovered from
        ///     the conventional <c>SK</c> name.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitPartitionConventionalSkEntity>(b =>
            {
                b.ToTable("ExplicitPartitionConventionalSkTable");
                b.HasPartitionKey(x => x.CustomPartition);
            });

        /// <summary>Creates a context instance using the DynamoDB test options.</summary>
        /// <returns>A configured test context.</returns>
        public static ExplicitPartitionConventionalSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitPartitionConventionalSkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitPartitionKey_WithConventionalSortKey_ResolvesBothRoles()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitPartitionConventionalSkContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitPartitionConventionalSkEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("CustomPartition");
        primaryKey.Properties[1].Name.Should().Be("SK");
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomPartition");
        entityType.GetSortKeyPropertyName().Should().Be("SK");
    }

    private sealed record ExplicitPartitionAmbiguousSortEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string CustomPartition { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortKey { get; set; } = null!;
    }

    private sealed class ExplicitPartitionAmbiguousSortContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ExplicitPartitionAmbiguousSortEntity> Entities { get; set; } = null!;

        /// <summary>
        ///     Configures an explicit partition key while leaving sort-key discovery to conventions,
        ///     which should fail because both <c>SK</c> and <c>SortKey</c> are present.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitPartitionAmbiguousSortEntity>(b =>
            {
                b.ToTable("ExplicitPartitionAmbiguousSortTable");
                b.HasPartitionKey(x => x.CustomPartition);
            });

        /// <summary>Creates a context instance using the DynamoDB test options.</summary>
        /// <returns>A configured test context.</returns>
        public static ExplicitPartitionAmbiguousSortContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitPartitionAmbiguousSortContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitPartitionKey_WithAmbiguousConventionalSortNames_ThrowsAmbiguityError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = ExplicitPartitionAmbiguousSortContext.Create(client);
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*multiple properties with conventional sort key names*")
            .And
            .Message
            .Should()
            .Contain("HasSortKey");
    }

    // -------------------------------------------------------------------
    // Names are case-sensitive — 'pk', 'sk', 'partitionkey' do NOT trigger
    // -------------------------------------------------------------------

    private sealed record WrongCaseEntity
    {
        // Lowercase — must NOT be auto-discovered
        /// <summary>Provides functionality for this member.</summary>
        public string pk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string sk { get; set; } = null!;

        // This is the real key EF discovers
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class WrongCaseContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<WrongCaseEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WrongCaseEntity>(b =>
            {
                b.ToTable("WrongCaseTable");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static WrongCaseContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<WrongCaseContext>(client));
    }

    // -------------------------------------------------------------------
    // Complex types — sort key convention does not apply to their properties
    // -------------------------------------------------------------------

    private sealed record OwnerEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public ComplexPart Detail { get; set; } = null!;
    }

    [ComplexType]
    private sealed record ComplexPart
    {
        // This property's name would normally trigger discovery on an entity type, but complex
        // types are not entity types, so the convention skips them.
        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Value { get; set; } = null!;
    }

    private sealed class ComplexConventionContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<OwnerEntity> Owners { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerEntity>(b =>
            {
                b.ToTable("ComplexConventionTable");
                b.ComplexProperty(x => x.Detail);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ComplexConventionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ComplexConventionContext>(client));
    }

    // -------------------------------------------------------------------
    // Annotations are set directly (not just inferred via EF PK position fallback)
    // so that code reading raw annotations (e.g. per-request analytics) sees values
    // without depending on GetPartitionKeyPropertyName()'s fallback path.
    // -------------------------------------------------------------------

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertyNamedPK_SetsAnnotationDirectly_NotJustFallback()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkNamedEntity))!;

        // Direct annotation read — must return "PK", not null.
        entityType["Dynamo:PartitionKeyPropertyName"].Should().Be("PK");
        entityType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void PropertiesNamedPKAndSK_SetsBothAnnotationsDirectly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkSkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkSkNamedEntity))!;

        entityType["Dynamo:PartitionKeyPropertyName"].Should().Be("PK");
        entityType["Dynamo:SortKeyPropertyName"].Should().Be("SK");
    }

    // -------------------------------------------------------------------
    // Ambiguous conventional names — must throw during model finalisation
    // with a clear message pointing to HasPartitionKey/HasSortKey
    // -------------------------------------------------------------------

    private sealed record AmbiguousPkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string PartitionKey { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class AmbiguousPkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<AmbiguousPkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AmbiguousPkEntity>(b =>
            {
                b.ToTable("AmbiguousPkTable");
                // No HasPartitionKey — convention sees both 'PK' and 'PartitionKey'
            });

        /// <summary>Provides functionality for this member.</summary>
        public static AmbiguousPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AmbiguousPkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void BothPKAndPartitionKey_WithoutExplicitOverride_ThrowsAmbiguityError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = AmbiguousPkContext.Create(client);
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*multiple properties with conventional partition key names*")
            .And
            .Message
            .Should()
            .Contain("HasPartitionKey");
    }

    private sealed record AmbiguousSkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortKey { get; set; } = null!;
    }

    private sealed class AmbiguousSkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<AmbiguousSkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AmbiguousSkEntity>(b =>
            {
                b.ToTable("AmbiguousSkTable");
                // No HasSortKey — convention sees both 'SK' and 'SortKey'
            });

        /// <summary>Provides functionality for this member.</summary>
        public static AmbiguousSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AmbiguousSkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void BothSKAndSortKey_WithoutExplicitOverride_ThrowsAmbiguityError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = AmbiguousSkContext.Create(client);
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*multiple properties with conventional sort key names*")
            .And
            .Message
            .Should()
            .Contain("HasSortKey");
    }

    private sealed record ResolvedAmbiguityEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string PartitionKey { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed class ResolvedAmbiguityContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ResolvedAmbiguityEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ResolvedAmbiguityEntity>(b =>
            {
                b.ToTable("ResolvedAmbiguityTable");
                // Explicit call at Explicit source resolves the PK ambiguity
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ResolvedAmbiguityContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ResolvedAmbiguityContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasPartitionKey_ResolvesAmbiguity_DoesNotThrow()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ResolvedAmbiguityContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ResolvedAmbiguityEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PK");
        primaryKey.Properties[1].Name.Should().Be("SK");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().Be("SK");
    }

    // -------------------------------------------------------------------

    /// <summary>
    ///     SK-named properties inside a complex type are not promoted to sort-key annotations on the
    ///     owning entity, because the convention only processes top-level entity types.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ComplexType_WithSkProperty_IsNotAutoDiscoveredAsSortKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ComplexConventionContext.Create(client);

        // Owner: PK discovered as partition key, no sort key
        var ownerType = ctx.Model.FindEntityType(typeof(OwnerEntity))!;
        ownerType.GetPartitionKeyPropertyName().Should().Be("PK");
        ownerType.GetSortKeyPropertyName().Should().BeNull();

        // The complex property SK sub-property must NOT cause a sort-key annotation on the owner.
        ownerType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }
}
