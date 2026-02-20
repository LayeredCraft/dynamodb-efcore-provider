using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

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
        public string PK { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class PkNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkNamedEntity>(b =>
            {
                b.ToTable("PkNamedTable");
                // No HasPartitionKey, no HasKey — convention should do all the work
            });

        public static PkNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PkNamedContext>(client));
    }

    [Fact]
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
        public string PartitionKey { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class PartitionKeyNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PartitionKeyNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PartitionKeyNamedEntity>(b =>
            {
                b.ToTable("PartitionKeyNamedTable");
            });

        public static PartitionKeyNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyNamedContext>(client));
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string SK { get; set; } = null!;
    }

    private sealed class PkSkNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkSkNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkSkNamedEntity>(b =>
            {
                b.ToTable("PkSkNamedTable");
            });

        public static PkSkNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PkSkNamedContext>(client));
    }

    [Fact]
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
        public string PartitionKey { get; set; } = null!;
        public string SortKey { get; set; } = null!;
    }

    private sealed class PartitionSortKeyNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PartitionSortKeyNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PartitionSortKeyNamedEntity>(b =>
            {
                b.ToTable("PartitionSortKeyNamedTable");
            });

        public static PartitionSortKeyNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionSortKeyNamedContext>(client));
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string CustomKey { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class ExplicitOverrideContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExplicitOverrideEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitOverrideEntity>(b =>
            {
                b.ToTable("ExplicitOverrideTable");
                // Explicit fluent API call (Explicit source) overrides the convention (Convention
                // source)
                b.HasPartitionKey(x => x.CustomKey);
            });

        public static ExplicitOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitOverrideContext>(client));
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string SK { get; set; } = null!;
        public string CustomSort { get; set; } = null!;
    }

    private sealed class ExplicitSkOverrideContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExplicitSkOverrideEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitSkOverrideEntity>(b =>
            {
                b.ToTable("ExplicitSkOverrideTable");
                // Explicit HasSortKey points to CustomSort rather than the conventionally-named SK
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.CustomSort);
            });

        public static ExplicitSkOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitSkOverrideContext>(client));
    }

    [Fact]
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

    // -------------------------------------------------------------------
    // Names are case-sensitive — 'pk', 'sk', 'partitionkey' do NOT trigger
    // -------------------------------------------------------------------

    private sealed record WrongCaseEntity
    {
        // Lowercase — must NOT be auto-discovered
        public string pk { get; set; } = null!;

        public string sk { get; set; } = null!;

        // This is the real key EF discovers
        public string Id { get; set; } = null!;
    }

    private sealed class WrongCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<WrongCaseEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<WrongCaseEntity>(b =>
            {
                b.ToTable("WrongCaseTable");
            });

        public static WrongCaseContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<WrongCaseContext>(client));
    }

    // -------------------------------------------------------------------
    // Owned entity types — convention does not apply
    // -------------------------------------------------------------------

    private sealed record OwnerEntity
    {
        public string PK { get; set; } = null!;
        public OwnedPart Detail { get; set; } = null!;
    }

    private sealed record OwnedPart
    {
        // This property's name would normally trigger discovery, but owned types are excluded
        public string SK { get; set; } = null!;
        public string Value { get; set; } = null!;
    }

    private sealed class OwnedConventionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OwnerEntity> Owners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerEntity>(b =>
            {
                b.ToTable("OwnedConventionTable");
                b.OwnsOne(x => x.Detail);
            });

        public static OwnedConventionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<OwnedConventionContext>(client));
    }

    // -------------------------------------------------------------------
    // Annotations are set directly (not just inferred via EF PK position fallback)
    // so that code reading raw annotations (e.g. per-request analytics) sees values
    // without depending on GetPartitionKeyPropertyName()'s fallback path.
    // -------------------------------------------------------------------

    [Fact]
    public void PropertyNamedPK_SetsAnnotationDirectly_NotJustFallback()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkNamedEntity))!;

        // Direct annotation read — must return "PK", not null.
        entityType["Dynamo:PartitionKeyPropertyName"].Should().Be("PK");
        entityType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string PartitionKey { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class AmbiguousPkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AmbiguousPkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AmbiguousPkEntity>(b =>
            {
                b.ToTable("AmbiguousPkTable");
                // No HasPartitionKey — convention sees both 'PK' and 'PartitionKey'
            });

        public static AmbiguousPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AmbiguousPkContext>(client));
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string SK { get; set; } = null!;
        public string SortKey { get; set; } = null!;
    }

    private sealed class AmbiguousSkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AmbiguousSkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AmbiguousSkEntity>(b =>
            {
                b.ToTable("AmbiguousSkTable");
                // No HasSortKey — convention sees both 'SK' and 'SortKey'
            });

        public static AmbiguousSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AmbiguousSkContext>(client));
    }

    [Fact]
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
        public string PK { get; set; } = null!;
        public string PartitionKey { get; set; } = null!;
        public string SK { get; set; } = null!;
    }

    private sealed class ResolvedAmbiguityContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ResolvedAmbiguityEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ResolvedAmbiguityEntity>(b =>
            {
                b.ToTable("ResolvedAmbiguityTable");
                // Explicit call at Explicit source resolves the PK ambiguity
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

        public static ResolvedAmbiguityContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ResolvedAmbiguityContext>(client));
    }

    [Fact]
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

    [Fact]
    public void OwnedEntityType_WithSkProperty_IsNotAutoDiscoveredAsSortKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = OwnedConventionContext.Create(client);

        // Owner: PK discovered as partition key, no sort key
        var ownerType = ctx.Model.FindEntityType(typeof(OwnerEntity))!;
        ownerType.GetPartitionKeyPropertyName().Should().Be("PK");
        ownerType.GetSortKeyPropertyName().Should().BeNull();

        // Owned: sort key annotation is NOT set (owned types are excluded)
        var ownedType = ctx.Model.FindEntityType(typeof(OwnedPart))!;
        ownedType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }
}
