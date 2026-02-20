using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>
///     Tests for <c>GetPartitionKeyProperty</c> / <c>GetPartitionKeyPropertyName</c> /
///     <c>GetSortKeyProperty</c> / <c>GetSortKeyPropertyName</c> derivation logic, and the
///     corresponding <c>GetPartitionKeyPropertyNameConfigurationSource</c> /
///     <c>GetSortKeyPropertyNameConfigurationSource</c> readers.
/// </summary>
public class TableKeySchemaTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    // -------------------------------------------------------------------
    // Single-part EF primary key — auto-detection from first PK property
    // -------------------------------------------------------------------

    private sealed record SingleKeyEntity
    {
        public string Id { get; set; } = null!;
        public string? Name { get; set; }
    }

    private sealed class SingleKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SingleKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleKeyEntity>(b =>
            {
                b.ToTable("SingleKeyTable");
                b.HasKey(x => x.Id);
            });

        public static SingleKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleKeyContext>(client));
    }

    [Fact]
    public void GetPartitionKeyPropertyName_SingleEfKey_ReturnsClrPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        // No HasAttributeName configured → property name is "Id"
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
    }

    [Fact]
    public void GetPartitionKeyProperty_SingleEfKey_ReturnsPkProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetPartitionKeyProperty().Should().BeSameAs(entityType.FindProperty("Id"));
    }

    [Fact]
    public void GetSortKeyPropertyName_SingleEfKey_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    [Fact]
    public void GetSortKeyProperty_SingleEfKey_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetSortKeyProperty().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Single-part EF primary key with HasAttributeName → attribute name is separate from property
    // name
    // -------------------------------------------------------------------

    private sealed record SingleKeyWithAttributeEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed class SingleKeyWithAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<SingleKeyWithAttributeEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleKeyWithAttributeEntity>(b =>
            {
                b.ToTable("SingleKeyAttributeTable");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });

        public static SingleKeyWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleKeyWithAttributeContext>(client));
    }

    [Fact]
    public void
        GetPartitionKeyPropertyName_SingleEfKey_WithHasAttributeName_ReturnsClrPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyWithAttributeEntity))!;
        // Property name is still "Id"; "PK" is only the store attribute name
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
    }

    [Fact]
    public void GetPartitionKeyProperty_SingleEfKey_WithHasAttributeName_ReturnsPkProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyWithAttributeEntity))!;
        var property = entityType.GetPartitionKeyProperty();
        property.Should().NotBeNull();
        property!.GetAttributeName().Should().Be("PK");
    }

    // -------------------------------------------------------------------
    // Two-part EF primary key — auto-detection from first and second PK properties
    // -------------------------------------------------------------------

    private sealed record TwoPartKeyEntity
    {
        public string PartitionId { get; set; } = null!;
        public string SortId { get; set; } = null!;
        public string? Value { get; set; }
    }

    private sealed class TwoPartKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TwoPartKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TwoPartKeyEntity>(b =>
            {
                b.ToTable("TwoPartKeyTable");
                b.HasKey(x => new { x.PartitionId, x.SortId });
            });

        public static TwoPartKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<TwoPartKeyContext>(client));
    }

    [Fact]
    public void GetPartitionKeyPropertyName_TwoPartEfKey_ReturnsFirstPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("PartitionId");
    }

    [Fact]
    public void GetPartitionKeyProperty_TwoPartEfKey_ReturnsFirstPkProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType
            .GetPartitionKeyProperty()
            .Should()
            .BeSameAs(entityType.FindProperty("PartitionId"));
    }

    [Fact]
    public void GetSortKeyPropertyName_TwoPartEfKey_ReturnsSecondPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("SortId");
    }

    [Fact]
    public void GetSortKeyProperty_TwoPartEfKey_ReturnsSecondPkProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType.GetSortKeyProperty().Should().BeSameAs(entityType.FindProperty("SortId"));
    }

    // -------------------------------------------------------------------
    // HasPartitionKey / HasSortKey explicit overrides (lambda form)
    // -------------------------------------------------------------------

    private sealed record MultiPropEntity
    {
        public string Id { get; set; } = null!;
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasPartitionKeyOverrideContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<MultiPropEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MultiPropEntity>(b =>
            {
                b.ToTable("HasPartitionKeyTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasPartitionKey(x => x.OtherId);
            });

        public static HasPartitionKeyOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyOverrideContext>(client));
    }

    [Fact]
    public void HasPartitionKey_Lambda_OverridesDefaultDetection()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasPartitionKeyOverrideContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(MultiPropEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("OtherId");
        entityType.GetPartitionKeyProperty().Should().BeSameAs(entityType.FindProperty("OtherId"));
    }

    private sealed class HasSortKeyOverrideContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<MultiPropEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MultiPropEntity>(b =>
            {
                b.ToTable("HasSortKeyTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasSortKey(x => x.Id);
            });

        public static HasSortKeyOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyOverrideContext>(client));
    }

    [Fact]
    public void HasSortKey_Lambda_OverridesDefaultDetection()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasSortKeyOverrideContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(MultiPropEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyProperty().Should().BeSameAs(entityType.FindProperty("Id"));
    }

    // -------------------------------------------------------------------
    // HasPartitionKey + HasAttributeName → physical name derived from property's attribute name
    // -------------------------------------------------------------------

    private sealed record AttributeNameEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed class HasPartitionKeyWithAttributeNameContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<AttributeNameEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttributeNameEntity>(b =>
            {
                b.ToTable("AttributeNameTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("HASH");
            });

        public static HasPartitionKeyWithAttributeNameContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyWithAttributeNameContext>(client));
    }

    [Fact]
    public void HasPartitionKey_WithHasAttributeName_PropertyDerivesPhysicalName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasPartitionKeyWithAttributeNameContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(AttributeNameEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetPartitionKeyProperty()!.GetAttributeName().Should().Be("HASH");
    }

    // -------------------------------------------------------------------
    // Owned entity types — no auto-detection (no EF primary key)
    // -------------------------------------------------------------------

    private sealed record OwnerEntity
    {
        public string Id { get; set; } = null!;
        public OwnedDetail Detail { get; set; } = null!;
    }

    private sealed record OwnedDetail
    {
        public string Value { get; set; } = null!;
    }

    private sealed class OwnedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OwnerEntity> Owners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerEntity>(b =>
            {
                b.ToTable("OwnedTable");
                b.HasKey(x => x.Id);
                b.OwnsOne(x => x.Detail);
            });

        public static OwnedContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<OwnedContext>(client));
    }

    [Fact]
    public void OwnedEntityType_NoAutoDetection_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = OwnedContext.Create(client);

        var ownedType = ctx.Model.FindEntityType(typeof(OwnedDetail))!;
        // No explicit PartitionKeyPropertyName annotation is set on owned types — the
        // derivation is not configured for them, so the annotation should be absent.
        ownedType["Dynamo:PartitionKeyPropertyName"].Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Two-part EF primary key + HasAttributeName on second property
    // -------------------------------------------------------------------

    private sealed record TwoPartKeyWithSkAttributeEntity
    {
        public string PartitionId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed class TwoPartKeyWithSkAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<TwoPartKeyWithSkAttributeEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TwoPartKeyWithSkAttributeEntity>(b =>
            {
                b.ToTable("TwoPartKeySkAttributeTable");
                b.HasKey(x => new { x.PartitionId, x.SortId });
                b.Property(x => x.SortId).HasAttributeName("SK");
            });

        public static TwoPartKeyWithSkAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<TwoPartKeyWithSkAttributeContext>(client));
    }

    [Fact]
    public void GetSortKeyProperty_TwoPartEfKey_WithHasAttributeName_ReturnsMappedProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyWithSkAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyWithSkAttributeEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("SortId");
        entityType.GetSortKeyProperty()!.GetAttributeName().Should().Be("SK");
    }

    // -------------------------------------------------------------------
    // HasPartitionKey(string) — both with and without HasAttributeName
    // -------------------------------------------------------------------

    private sealed record StringOverridePkEntity
    {
        public string Id { get; set; } = null!;
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasPartitionKeyStringNoAttributeContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<StringOverridePkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverridePkEntity>(b =>
            {
                b.ToTable("PkStringNoAttrTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasPartitionKey("OtherId");
            });

        public static HasPartitionKeyStringNoAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyStringNoAttributeContext>(client));
    }

    [Fact]
    public void HasPartitionKey_StringOverride_ReturnsPropertyClrName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasPartitionKeyStringNoAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(StringOverridePkEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("OtherId");
        entityType.GetPartitionKeyProperty().Should().BeSameAs(entityType.FindProperty("OtherId"));
    }

    private sealed class HasPartitionKeyStringWithAttributeContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<StringOverridePkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverridePkEntity>(b =>
            {
                b.ToTable("PkStringWithAttrTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasPartitionKey("Id");
                b.Property(x => x.Id).HasAttributeName("HASH");
            });

        public static HasPartitionKeyStringWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyStringWithAttributeContext>(client));
    }

    [Fact]
    public void HasPartitionKey_StringOverride_WithHasAttributeName_PropertyDerivesPhysicalName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasPartitionKeyStringWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(StringOverridePkEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetPartitionKeyProperty()!.GetAttributeName().Should().Be("HASH");
    }

    // -------------------------------------------------------------------
    // HasSortKey(lambda) + HasAttributeName
    // -------------------------------------------------------------------

    private sealed record SortKeyLambdaAttributeEntity
    {
        public string Id { get; set; } = null!;
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasSortKeyLambdaWithAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<SortKeyLambdaAttributeEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SortKeyLambdaAttributeEntity>(b =>
            {
                b.ToTable("SkLambdaWithAttrTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasSortKey(x => x.OtherId);
                b.Property(x => x.OtherId).HasAttributeName("RANGE");
            });

        public static HasSortKeyLambdaWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyLambdaWithAttributeContext>(client));
    }

    [Fact]
    public void HasSortKey_Lambda_WithHasAttributeName_PropertyDerivesPhysicalName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasSortKeyLambdaWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SortKeyLambdaAttributeEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("OtherId");
        entityType.GetSortKeyProperty()!.GetAttributeName().Should().Be("RANGE");
    }

    // -------------------------------------------------------------------
    // HasSortKey(string) — both with and without HasAttributeName
    // -------------------------------------------------------------------

    private sealed record StringOverrideSkEntity
    {
        public string Id { get; set; } = null!;
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasSortKeyStringNoAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<StringOverrideSkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverrideSkEntity>(b =>
            {
                b.ToTable("SkStringNoAttrTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasSortKey("Id");
            });

        public static HasSortKeyStringNoAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyStringNoAttributeContext>(client));
    }

    [Fact]
    public void HasSortKey_StringOverride_ReturnsPropertyClrName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasSortKeyStringNoAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(StringOverrideSkEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyProperty().Should().BeSameAs(entityType.FindProperty("Id"));
    }

    private sealed class HasSortKeyStringWithAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<StringOverrideSkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverrideSkEntity>(b =>
            {
                b.ToTable("SkStringWithAttrTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasSortKey("OtherId");
                b.Property(x => x.OtherId).HasAttributeName("RANGE");
            });

        public static HasSortKeyStringWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyStringWithAttributeContext>(client));
    }

    [Fact]
    public void HasSortKey_StringOverride_WithHasAttributeName_PropertyDerivesPhysicalName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasSortKeyStringWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(StringOverrideSkEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("OtherId");
        entityType.GetSortKeyProperty()!.GetAttributeName().Should().Be("RANGE");
    }

    // -------------------------------------------------------------------
    // GetPartitionKeyPropertyNameConfigurationSource /
    // GetSortKeyPropertyNameConfigurationSource
    //
    // IConventionEntityType is only available during model building (OnModelCreating),
    // not after the model has been compiled to RuntimeEntityType. The configuration source
    // is captured inside OnModelCreating before finalization.
    // -------------------------------------------------------------------

    private sealed record ConfigSourceEntity
    {
        public string Id { get; set; } = null!;
        public string OtherId { get; set; } = null!;
    }

    private sealed class FluentApiPkConfigSourceContext(DbContextOptions options) : DbContext(
        options)
    {
        public ConfigurationSource? CapturedPkConfigSource { get; private set; }
        public DbSet<ConfigSourceEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigSourceEntity>(b =>
            {
                b.ToTable("FluentApiPkTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasPartitionKey(x => x.Id);
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConfigSourceEntity))!;
            CapturedPkConfigSource = et.GetPartitionKeyPropertyNameConfigurationSource();
        }

        public static FluentApiPkConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<FluentApiPkConfigSourceContext>(client));
    }

    [Fact]
    public void GetPartitionKeyPropertyNameConfigurationSource_FluentApi_ReportsExplicitSource()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = FluentApiPkConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedPkConfigSource.Should().Be(ConfigurationSource.Explicit);
    }

    private sealed class FluentApiSkConfigSourceContext(DbContextOptions options) : DbContext(
        options)
    {
        public ConfigurationSource? CapturedSkConfigSource { get; private set; }
        public DbSet<ConfigSourceEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigSourceEntity>(b =>
            {
                b.ToTable("FluentApiSkTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                b.HasSortKey(x => x.OtherId);
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConfigSourceEntity))!;
            CapturedSkConfigSource = et.GetSortKeyPropertyNameConfigurationSource();
        }

        public static FluentApiSkConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<FluentApiSkConfigSourceContext>(client));
    }

    [Fact]
    public void GetSortKeyPropertyNameConfigurationSource_FluentApi_ReportsExplicitSource()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = FluentApiSkConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedSkConfigSource.Should().Be(ConfigurationSource.Explicit);
    }

    private sealed class NoExplicitKeyConfigSourceContext(DbContextOptions options) : DbContext(
        options)
    {
        public ConfigurationSource? CapturedPkConfigSource { get; private set; }
        public ConfigurationSource? CapturedSkConfigSource { get; private set; }
        public DbSet<ConfigSourceEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigSourceEntity>(b =>
            {
                b.ToTable("NoExplicitKeyTable");
                b.HasKey(x => new { x.Id, x.OtherId });
                // no HasPartitionKey / HasSortKey calls
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConfigSourceEntity))!;
            CapturedPkConfigSource = et.GetPartitionKeyPropertyNameConfigurationSource();
            CapturedSkConfigSource = et.GetSortKeyPropertyNameConfigurationSource();
        }

        public static NoExplicitKeyConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<NoExplicitKeyConfigSourceContext>(client));
    }

    [Fact]
    public void GetPartitionKeyPropertyNameConfigurationSource_NoAnnotation_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = NoExplicitKeyConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedPkConfigSource.Should().BeNull();
    }

    [Fact]
    public void GetSortKeyPropertyNameConfigurationSource_NoAnnotation_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = NoExplicitKeyConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedSkConfigSource.Should().BeNull();
    }
}
