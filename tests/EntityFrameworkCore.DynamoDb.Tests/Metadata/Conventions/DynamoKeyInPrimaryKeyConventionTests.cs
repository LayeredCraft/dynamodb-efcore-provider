using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <c>DynamoKeyInPrimaryKeyConvention</c> — verifies that the EF Core primary key
///     is automatically configured to match the DynamoDB key schema when <c>HasPartitionKey</c> and/or
///     <c>HasSortKey</c> annotations are set without an explicit <c>HasKey</c> call.
/// </summary>
public class DynamoKeyInPrimaryKeyConventionTests
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
    // HasPartitionKey only, property not auto-discoverable by EF naming
    // -------------------------------------------------------------------

    private sealed record AnnotationOnlyPkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string CustomPk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class AnnotationOnlyPkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<AnnotationOnlyPkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AnnotationOnlyPkEntity>(b =>
            {
                b.ToTable("AnnotationOnlyPkTable");
                b.HasPartitionKey(x => x.CustomPk);
                // No b.HasKey(...) call
            });

        /// <summary>Provides functionality for this member.</summary>
        public static AnnotationOnlyPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AnnotationOnlyPkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_WithoutExplicitHasKey_AutoConfiguresEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = AnnotationOnlyPkContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(AnnotationOnlyPkEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("CustomPk");
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomPk");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    // -------------------------------------------------------------------
    // HasPartitionKey + HasSortKey, neither property is auto-discoverable
    // -------------------------------------------------------------------

    private sealed record AnnotationPkSkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string CustomPk { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string CustomSk { get; set; } = null!;
    }

    private sealed class AnnotationPkSkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<AnnotationPkSkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AnnotationPkSkEntity>(b =>
            {
                b.ToTable("AnnotationPkSkTable");
                b.HasPartitionKey(x => x.CustomPk);
                b.HasSortKey(x => x.CustomSk);
                // No b.HasKey(...) call
            });

        /// <summary>Provides functionality for this member.</summary>
        public static AnnotationPkSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AnnotationPkSkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void
        HasPartitionKeyAndSortKey_WithoutExplicitHasKey_AutoConfiguresCompositeEfPrimaryKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = AnnotationPkSkContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(AnnotationPkSkEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("CustomPk");
        primaryKey.Properties[1].Name.Should().Be("CustomSk");
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomPk");
        entityType.GetSortKeyPropertyName().Should().Be("CustomSk");
    }

    // -------------------------------------------------------------------
    // Shadow properties used as configured table keys are rejected
    // -------------------------------------------------------------------

    private sealed record LateShadowKeyEntity;

    private sealed class LateShadowKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<LateShadowKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<LateShadowKeyEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("LateShadowKeyTable");
                b.HasPartitionKey("PK");
                b.HasSortKey("SK");
                b.Property<string>("PK");
                b.Property<string>("SK");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static LateShadowKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<LateShadowKeyContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionAndSortKey_BeforeShadowProperties_ThrowsValidationError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = LateShadowKeyContext.Create(client);

        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*shadow key properties are not supported*");
    }

    // -------------------------------------------------------------------
    // HasSortKey only — validation fails because partition key is required
    // -------------------------------------------------------------------

    private sealed record SortKeyWithAutoDiscoveredPkEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Category { get; set; } = null!;
    }

    private sealed class SortKeyWithAutoDiscoveredPkContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SortKeyWithAutoDiscoveredPkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SortKeyWithAutoDiscoveredPkEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("AutoDiscoveredPkTable");
                b.HasSortKey(x => x.Category);
                // No HasKey, no HasPartitionKey — 'Id' is auto-discovered as the partition key
            });

        /// <summary>Provides functionality for this member.</summary>
        public static SortKeyWithAutoDiscoveredPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyWithAutoDiscoveredPkContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void HasSortKey_WithoutPartitionKey_ThrowsValidationError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = SortKeyWithAutoDiscoveredPkContext.Create(client);

        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*No DynamoDB partition key is configured*");
    }

    // -------------------------------------------------------------------
    // HasPartitionKey pointing to the same property EF would auto-discover
    // (redundant but should produce exactly the same result)
    // -------------------------------------------------------------------

    private sealed record RedundantAnnotationEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed class RedundantAnnotationContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<RedundantAnnotationEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RedundantAnnotationEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("RedundantAnnotationTable");
                b.HasPartitionKey(x => x.Id);
                // EF would have auto-discovered 'Id' anyway
            });

        /// <summary>Provides functionality for this member.</summary>
        public static RedundantAnnotationContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<RedundantAnnotationContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_MatchingAutoDiscoveredPk_EfPrimaryKeyIsUnchanged()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = RedundantAnnotationContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(RedundantAnnotationEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be("Id");
        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
    }

    // -------------------------------------------------------------------
    // Explicit root HasKey is rejected — Dynamo key annotations stay authoritative
    // -------------------------------------------------------------------

    private sealed record ExplicitKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PkProp { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SkProp { get; set; } = null!;
    }

    private sealed class ExplicitKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ExplicitKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "ExplicitKeyTable");
                b.HasKey(x => new { x.PkProp, x.SkProp });
                b.HasPartitionKey(x => x.PkProp);
                b.HasSortKey(x => x.SkProp);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ExplicitKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitKeyContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasKey_WithAnnotations_IsRejected()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => ExplicitKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*must use HasPartitionKey(...) and optional HasSortKey(...)*do not use HasKey(...) or [Key]*");
    }

    // -------------------------------------------------------------------
    // Complex types — convention must not add key annotations
    // -------------------------------------------------------------------

    private sealed record OwnerWithAnnotationEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public ComplexPart Detail { get; set; } = null!;
    }

    private sealed record ComplexPart
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Value { get; set; } = null!;
    }

    private sealed class ComplexPartContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<OwnerWithAnnotationEntity> Owners { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithAnnotationEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "OwnedPartTable");
                b.HasPartitionKey(x => x.Id);
                b.ComplexProperty(x => x.Detail);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ComplexPartContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ComplexPartContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    /// <summary>Provides functionality for this member.</summary>
    public void ComplexType_ConventionDoesNotApply_NoChange()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ComplexPartContext.Create(client);

        var ownerType = ctx.Model.FindEntityType(typeof(OwnerWithAnnotationEntity))!;
        var complexType = ownerType.FindComplexProperty(nameof(OwnerWithAnnotationEntity.Detail))!
            .ComplexType;
        complexType["Dynamo:PartitionKeyPropertyName"].Should().BeNull();
        complexType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }
}
