using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

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
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

    // -------------------------------------------------------------------
    // Single-part key with explicit Dynamo partition key mapping
    // -------------------------------------------------------------------

    private sealed record SingleKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string? Name { get; set; }
    }

    private sealed class SingleKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SingleKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleKeyEntity>(b =>
            {
                b.ToTable("SingleKeyTable");
                b.HasPartitionKey(x => x.Id);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static SingleKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleKeyContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyPropertyName_SingleKey_ReturnsConfiguredPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        // No HasAttributeName configured → property name is "Id"
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyProperty_SingleKey_ReturnsConfiguredProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetPartitionKeyProperty().Should().BeSameAs(entityType.FindProperty("Id"));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyPropertyName_SingleKey_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyProperty_SingleKey_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetSortKeyProperty().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Single-part key with HasAttributeName on the partition key property
    // -------------------------------------------------------------------

    private sealed record SingleKeyWithAttributeEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class SingleKeyWithAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SingleKeyWithAttributeEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleKeyWithAttributeEntity>(b =>
            {
                b.ToTable("SingleKeyAttributeTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static SingleKeyWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleKeyWithAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyPropertyName_SingleKey_WithHasAttributeName_ReturnsClrPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyWithAttributeEntity))!;
        // Property name is still "Id"; "PK" is only the store attribute name
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyProperty_SingleKey_WithHasAttributeName_ReturnsPkProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyWithAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyWithAttributeEntity))!;
        var property = entityType.GetPartitionKeyProperty();
        property.Should().NotBeNull();
        property!.GetAttributeName().Should().Be("PK");
    }

    // -------------------------------------------------------------------
    // Two-part key with explicit Dynamo partition/sort key mapping
    // -------------------------------------------------------------------

    private sealed record TwoPartKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartitionId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string? Value { get; set; }
    }

    private sealed class TwoPartKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<TwoPartKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TwoPartKeyEntity>(b =>
            {
                b.ToTable("TwoPartKeyTable");
                b.HasPartitionKey(x => x.PartitionId);
                b.HasSortKey(x => x.SortId);
                b.HasPartitionKey(x => x.PartitionId);
                b.HasSortKey(x => x.SortId);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static TwoPartKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<TwoPartKeyContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyPropertyName_TwoPartKey_ReturnsPartitionPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("PartitionId");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetPartitionKeyProperty_TwoPartKey_ReturnsPartitionProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType
            .GetPartitionKeyProperty()
            .Should()
            .BeSameAs(entityType.FindProperty("PartitionId"));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyPropertyName_TwoPartKey_ReturnsSortPropertyName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = TwoPartKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(TwoPartKeyEntity))!;
        entityType.GetSortKeyPropertyName().Should().Be("SortId");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyProperty_TwoPartKey_ReturnsSortProperty()
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasPartitionKeyOverrideContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<MultiPropEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MultiPropEntity>(b =>
            {
                b.ToTable("HasPartitionKeyTable");
                b.HasPartitionKey(x => x.OtherId);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasPartitionKeyOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyOverrideContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<MultiPropEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MultiPropEntity>(b =>
            {
                b.ToTable("HasSortKeyTable");
                b.HasPartitionKey(x => x.OtherId);
                b.HasSortKey(x => x.Id);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasSortKeyOverrideContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyOverrideContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class HasPartitionKeyWithAttributeNameContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<AttributeNameEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AttributeNameEntity>(b =>
            {
                b.ToTable("AttributeNameTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("HASH");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasPartitionKeyWithAttributeNameContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyWithAttributeNameContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_WithHasAttributeName_PropertyDerivesPhysicalName()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = HasPartitionKeyWithAttributeNameContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(AttributeNameEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetPartitionKeyProperty()!.GetAttributeName().Should().Be("HASH");
    }

    // -------------------------------------------------------------------
    // Complex types — no auto-detection
    // -------------------------------------------------------------------

    private sealed record OwnerEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public ComplexDetail Detail { get; set; } = null!;
    }

    private sealed record ComplexDetail
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Value { get; set; } = null!;
    }

    private sealed class ComplexContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<OwnerEntity> Owners { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerEntity>(b =>
            {
                b.ToTable("OwnedTable");
                b.HasPartitionKey(x => x.Id);
                b.ComplexProperty(x => x.Detail);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ComplexContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ComplexContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ComplexType_NoAutoDetection_ReturnsNull()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ComplexContext.Create(client);

        var ownerType = ctx.Model.FindEntityType(typeof(OwnerEntity))!;
        var complexType = ownerType.FindComplexProperty(nameof(OwnerEntity.Detail))!.ComplexType;
        complexType["Dynamo:PartitionKeyPropertyName"].Should().BeNull();
    }

    // -------------------------------------------------------------------
    // Two-part key + HasAttributeName on sort key property
    // -------------------------------------------------------------------

    private sealed record TwoPartKeyWithSkAttributeEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartitionId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed class TwoPartKeyWithSkAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<TwoPartKeyWithSkAttributeEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TwoPartKeyWithSkAttributeEntity>(b =>
            {
                b.ToTable("TwoPartKeySkAttributeTable");
                b.HasPartitionKey(x => x.PartitionId);
                b.HasSortKey(x => x.SortId);
                b.HasPartitionKey(x => x.PartitionId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.SortId).HasAttributeName("SK");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static TwoPartKeyWithSkAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<TwoPartKeyWithSkAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyProperty_TwoPartKey_WithHasAttributeName_ReturnsMappedProperty()
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasPartitionKeyStringNoAttributeContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<StringOverridePkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverridePkEntity>(b =>
            {
                b.ToTable("PkStringNoAttrTable");
                b.HasPartitionKey("OtherId");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasPartitionKeyStringNoAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyStringNoAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<StringOverridePkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverridePkEntity>(b =>
            {
                b.ToTable("PkStringWithAttrTable");
                b.HasPartitionKey("Id");
                b.Property(x => x.Id).HasAttributeName("HASH");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasPartitionKeyStringWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasPartitionKeyStringWithAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasSortKeyLambdaWithAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SortKeyLambdaAttributeEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SortKeyLambdaAttributeEntity>(b =>
            {
                b.ToTable("SkLambdaWithAttrTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.OtherId);
                b.Property(x => x.OtherId).HasAttributeName("RANGE");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasSortKeyLambdaWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyLambdaWithAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string OtherId { get; set; } = null!;
    }

    private sealed class HasSortKeyStringNoAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<StringOverrideSkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverrideSkEntity>(b =>
            {
                b.ToTable("SkStringNoAttrTable");
                b.HasPartitionKey("OtherId");
                b.HasSortKey("Id");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasSortKeyStringNoAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyStringNoAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<StringOverrideSkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringOverrideSkEntity>(b =>
            {
                b.ToTable("SkStringWithAttrTable");
                b.HasPartitionKey("Id");
                b.HasSortKey("OtherId");
                b.Property(x => x.OtherId).HasAttributeName("RANGE");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static HasSortKeyStringWithAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<HasSortKeyStringWithAttributeContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string OtherId { get; set; } = null!;
    }

    private sealed class FluentApiPkConfigSourceContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public ConfigurationSource? CapturedPkConfigSource { get; private set; }

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ConfigSourceEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigSourceEntity>(b =>
            {
                b.ToTable("FluentApiPkTable");
                b.HasPartitionKey(x => x.Id);
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConfigSourceEntity))!;
            CapturedPkConfigSource = et.GetPartitionKeyPropertyNameConfigurationSource();
        }

        /// <summary>Provides functionality for this member.</summary>
        public static FluentApiPkConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<FluentApiPkConfigSourceContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
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
        /// <summary>Provides functionality for this member.</summary>
        public ConfigurationSource? CapturedSkConfigSource { get; private set; }

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ConfigSourceEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigSourceEntity>(b =>
            {
                b.ToTable("FluentApiSkTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.OtherId);
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConfigSourceEntity))!;
            CapturedSkConfigSource = et.GetSortKeyPropertyNameConfigurationSource();
        }

        /// <summary>Provides functionality for this member.</summary>
        public static FluentApiSkConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<FluentApiSkConfigSourceContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void GetSortKeyPropertyNameConfigurationSource_FluentApi_ReportsExplicitSource()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = FluentApiSkConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedSkConfigSource.Should().Be(ConfigurationSource.Explicit);
    }

    private sealed record ConventionConfigSourceEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;
    }

    private sealed class ConventionKeyConfigSourceContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public ConfigurationSource? CapturedPkConfigSource { get; private set; }

        /// <summary>Provides functionality for this member.</summary>
        public ConfigurationSource? CapturedSkConfigSource { get; private set; }

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ConventionConfigSourceEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConventionConfigSourceEntity>(b =>
            {
                b.ToTable("ConventionKeyTable");
                // no HasPartitionKey / HasSortKey calls; convention should set both.
            });

            var conventionModel = (IConventionModel)modelBuilder.Model;
            var et = conventionModel.FindEntityType(typeof(ConventionConfigSourceEntity))!;
            CapturedPkConfigSource = et.GetPartitionKeyPropertyNameConfigurationSource();
            CapturedSkConfigSource = et.GetSortKeyPropertyNameConfigurationSource();
        }

        /// <summary>Provides functionality for this member.</summary>
        public static ConventionKeyConfigSourceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConventionKeyConfigSourceContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void
        GetPartitionKeyPropertyNameConfigurationSource_Convention_IsNotAvailableDuringOnModelCreating()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ConventionKeyConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedPkConfigSource.Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void
        GetSortKeyPropertyNameConfigurationSource_Convention_IsNotAvailableDuringOnModelCreating()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ConventionKeyConfigSourceContext.Create(client);
        _ = ctx.Model; // trigger OnModelCreating

        ctx.CapturedSkConfigSource.Should().BeNull();
    }
}
