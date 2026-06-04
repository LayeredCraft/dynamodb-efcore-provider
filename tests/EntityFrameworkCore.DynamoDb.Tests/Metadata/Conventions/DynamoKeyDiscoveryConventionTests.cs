using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <c>DynamoKeyDiscoveryConvention</c> — verifies that conventional DynamoDB
///     property names (<c>PK</c>/<c>PartitionKey</c>, fallback <c>Id</c>, <c>SK</c>/<c>SortKey</c>)
///     are auto-configured as the DynamoDB partition/sort key annotations without any explicit
///     <c>HasPartitionKey</c> or <c>HasSortKey</c> call.
/// </summary>
public class DynamoKeyDiscoveryConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
    // Id property → fallback partition key auto-configured; EF PK rebuilt
    // -------------------------------------------------------------------

    private sealed record IdNamedEntity
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class IdNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<IdNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IdNamedEntity>(b => b.ToTable("IdNamedTable"));

        public static IdNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<IdNamedContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertyNamedId_AutoConfiguresFallbackPartitionKeyAndEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = IdNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(IdNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("Id");
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // ID property → fallback partition key auto-configured; EF PK rebuilt
    // -------------------------------------------------------------------

    private sealed record UpperIdNamedEntity
    {
        public string ID { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class UpperIdNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UpperIdNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UpperIdNamedEntity>(b => b.ToTable("UpperIdNamedTable"));

        public static UpperIdNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<UpperIdNamedContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertyNamedID_AutoConfiguresFallbackPartitionKeyAndEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = UpperIdNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(UpperIdNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("ID");
        entityType.GetPartitionKeyPropertyName().Should().Be("ID");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Shadow PK + Id properties → fallback Id ignores unsupported shadow property
    // -------------------------------------------------------------------

    private sealed record ShadowPkWithIdEntity
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class ShadowPkWithIdContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowPkWithIdEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowPkWithIdEntity>(b =>
            {
                b.ToTable("ShadowPkWithIdTable");
                b.Property<string>("PK");
            });

        public static ShadowPkWithIdContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowPkWithIdContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ShadowPropertyNamedPK_DoesNotBlockFallbackId()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ShadowPkWithIdContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ShadowPkWithIdEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("Id");
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Id + SK properties → composite EF PK auto-configured
    // -------------------------------------------------------------------

    private sealed record IdSkNamedEntity
    {
        public string Id { get; set; } = null!;

        public string SK { get; set; } = null!;
    }

    private sealed class IdSkNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<IdSkNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IdSkNamedEntity>(b => b.ToTable("IdSkNamedTable"));

        public static IdSkNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<IdSkNamedContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertiesNamedIdAndSK_AutoConfiguresCompositeEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = IdSkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(IdSkNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("Id");
        primaryKey.Properties[1].Name.Should().Be("SK");
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyPropertyName().Should().Be("SK");
    }

    // -------------------------------------------------------------------
    // Explicit partition key wins over fallback Id
    // -------------------------------------------------------------------

    private sealed record ExplicitKeyWithIdEntity
    {
        public string Id { get; set; } = null!;

        public string CustomKey { get; set; } = null!;
    }

    private sealed class ExplicitKeyWithIdContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExplicitKeyWithIdEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitKeyWithIdEntity>(b =>
            {
                b.ToTable("ExplicitKeyWithIdTable");
                b.HasPartitionKey(x => x.CustomKey);
            });

        public static ExplicitKeyWithIdContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitKeyWithIdContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasPartitionKey_OverridesFallbackId()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitKeyWithIdContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitKeyWithIdEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("CustomKey");
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomKey");
    }

    // -------------------------------------------------------------------
    // PartitionKey wins over fallback Id
    // -------------------------------------------------------------------

    private sealed record PartitionKeyAndIdNamedEntity
    {
        public string PartitionKey { get; set; } = null!;

        public string Id { get; set; } = null!;
    }

    private sealed class PartitionKeyAndIdNamedContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<PartitionKeyAndIdNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PartitionKeyAndIdNamedEntity>(b
                => b.ToTable("PartitionKeyAndIdNamedTable"));

        public static PartitionKeyAndIdNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyAndIdNamedContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertyNamedPartitionKey_BeatsFallbackId()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PartitionKeyAndIdNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PartitionKeyAndIdNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("PartitionKey");
        entityType.GetPartitionKeyPropertyName().Should().Be("PartitionKey");
    }

    // -------------------------------------------------------------------
    // PK wins over fallback Id
    // -------------------------------------------------------------------

    private sealed record PkAndIdNamedEntity
    {
        public string PK { get; set; } = null!;

        public string Id { get; set; } = null!;
    }

    private sealed class PkAndIdNamedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PkAndIdNamedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PkAndIdNamedEntity>(b => b.ToTable("PkAndIdNamedTable"));

        public static PkAndIdNamedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PkAndIdNamedContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertyNamedPK_BeatsFallbackId()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkAndIdNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkAndIdNamedEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("PK");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        public string PK { get; set; } = null!;

        public string CustomSort { get; set; } = null!;
    }

    private sealed class ConventionalPkExplicitSortContext(DbContextOptions options) : DbContext(
        options)
    {
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        public string CustomPartition { get; set; } = null!;

        public string SK { get; set; } = null!;
    }

    private sealed class ExplicitPartitionConventionalSkContext(DbContextOptions options)
        : DbContext(options)
    {
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        public string CustomPartition { get; set; } = null!;

        public string SK { get; set; } = null!;

        public string SortKey { get; set; } = null!;
    }

    private sealed class ExplicitPartitionAmbiguousSortContext(DbContextOptions options)
        : DbContext(options)
    {
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
    // Conventional names are matched case-insensitively
    // -------------------------------------------------------------------

    private sealed record WrongCaseEntity
    {
        public string pk { get; set; } = null!;

        public string sk { get; set; } = null!;

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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void LowercaseConventionalNames_AutoConfigureCompositeKeyAndBeatFallbackId()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = WrongCaseContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(WrongCaseEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("pk");
        primaryKey.Properties[1].Name.Should().Be("sk");
        entityType.GetPartitionKeyPropertyName().Should().Be("pk");
        entityType.GetSortKeyPropertyName().Should().Be("sk");
    }

    // -------------------------------------------------------------------
    // Complex types — sort key convention does not apply to their properties
    // -------------------------------------------------------------------

    private sealed record OwnerEntity
    {
        public string PK { get; set; } = null!;

        public ComplexPart Detail { get; set; } = null!;
    }

    private sealed record ComplexPart
    {
        // This property's name would normally trigger discovery on an entity type, but complex
        // types are not entity types, so the convention skips them.
        public string SK { get; set; } = null!;

        public string Value { get; set; } = null!;
    }

    private sealed class ComplexConventionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OwnerEntity> Owners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerEntity>(b =>
            {
                b.ToTable("ComplexConventionTable");
                b.ComplexProperty(x => x.Detail);
            });

        public static ComplexConventionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ComplexConventionContext>(client));
    }

    // -------------------------------------------------------------------
    // Annotations are set directly (not just inferred via EF PK position fallback)
    // so that code reading raw annotations (e.g. per-request analytics) sees values
    // without depending on GetPartitionKeyPropertyName()'s fallback path.
    // -------------------------------------------------------------------

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PropertyNamedPK_SetsAnnotationDirectly_NotJustFallback()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PkNamedContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PkNamedEntity))!;

        // Direct annotation read — must return "PK", not null.
        entityType["Dynamo:PartitionKeyPropertyName"].Should().Be("PK");
        entityType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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

    private sealed record EntityNameIdOnlyEntity
    {
        public string EntityNameIdOnlyEntityId { get; set; } = null!;
    }

    private sealed class EntityNameIdOnlyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityNameIdOnlyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EntityNameIdOnlyEntity>(b => b.ToTable("EntityNameIdOnlyTable"));

        public static EntityNameIdOnlyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<EntityNameIdOnlyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EntityNameIdConventionKey_InfersPartitionKey_WhenNoDynamoNamesExist()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = EntityNameIdOnlyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(EntityNameIdOnlyEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("EntityNameIdOnlyEntityId");
        entityType.GetPartitionKeyPropertyName().Should().Be("EntityNameIdOnlyEntityId");
    }

    private sealed record ExplicitHasKeyResolvesAmbiguousPkEntity
    {
        public string PK { get; set; } = null!;

        public string PartitionKey { get; set; } = null!;

        public string CustomId { get; set; } = null!;
    }

    private sealed class ExplicitHasKeyResolvesAmbiguousPkContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<ExplicitHasKeyResolvesAmbiguousPkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitHasKeyResolvesAmbiguousPkEntity>(b =>
            {
                b.ToTable("ExplicitHasKeyResolvesAmbiguousPkTable");
                b.HasKey(x => x.CustomId);
            });

        public static ExplicitHasKeyResolvesAmbiguousPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitHasKeyResolvesAmbiguousPkContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_ResolvesAmbiguousConventionalPartitionNames()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitHasKeyResolvesAmbiguousPkContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitHasKeyResolvesAmbiguousPkEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("CustomId");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // ShouldDiscoverKeyProperties — owned entity types are skipped
    // -------------------------------------------------------------------

    /// <summary>Test-only subclass that exposes the protected ShouldDiscoverKeyProperties method.</summary>
    private sealed class TestableDynamoKeyDiscoveryConvention(
        ProviderConventionSetBuilderDependencies dependencies)
        : DynamoKeyDiscoveryConvention(dependencies)
    {
        public bool ShouldDiscover(IConventionEntityType entityType)
            => ShouldDiscoverKeyProperties(entityType);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ShouldDiscoverKeyProperties_ReturnsFalse_ForOwnedEntityType()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var options = BuildOptions<PkNamedContext>(client);

        using var ctx = new PkNamedContext(options);
        var dependencies = ctx.GetService<ProviderConventionSetBuilderDependencies>();

        var convention = new TestableDynamoKeyDiscoveryConvention(dependencies);

        // IsOwned() is an EF Core extension method — build a real model with an owned
        // relationship so the method returns true without mocking.
        var modelBuilder =
            new ModelBuilder(
                new Microsoft.EntityFrameworkCore.Metadata.Conventions.ConventionSet());
        modelBuilder.Entity<OwnedTypeOwner>().OwnsOne<OwnedTypeValue>(nameof(OwnedTypeOwner.Owned));
        var ownedEntityType =
            (IConventionEntityType)((IConventionModel)modelBuilder.Model).FindEntityType(
                typeof(OwnedTypeValue))!;

        convention.ShouldDiscover(ownedEntityType).Should().BeFalse();
    }

    private sealed class OwnedTypeOwner
    {
        public int Id { get; set; }
        public OwnedTypeValue Owned { get; set; } = null!;
    }

    private sealed class OwnedTypeValue
    {
        public string? Value { get; set; }
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
