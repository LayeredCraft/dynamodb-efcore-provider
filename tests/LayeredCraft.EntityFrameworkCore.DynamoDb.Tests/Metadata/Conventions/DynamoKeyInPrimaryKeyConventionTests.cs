using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

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
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    // -------------------------------------------------------------------
    // HasPartitionKey only, property not auto-discoverable by EF naming
    // -------------------------------------------------------------------

    private sealed record AnnotationOnlyPkEntity
    {
        public string CustomPk { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class AnnotationOnlyPkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AnnotationOnlyPkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AnnotationOnlyPkEntity>(b =>
            {
                b.ToTable("AnnotationOnlyPkTable");
                b.HasPartitionKey(x => x.CustomPk);
                // No b.HasKey(...) call
            });

        public static AnnotationOnlyPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AnnotationOnlyPkContext>(client));
    }

    [Fact]
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
        public string CustomPk { get; set; } = null!;
        public string CustomSk { get; set; } = null!;
    }

    private sealed class AnnotationPkSkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AnnotationPkSkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AnnotationPkSkEntity>(b =>
            {
                b.ToTable("AnnotationPkSkTable");
                b.HasPartitionKey(x => x.CustomPk);
                b.HasSortKey(x => x.CustomSk);
                // No b.HasKey(...) call
            });

        public static AnnotationPkSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<AnnotationPkSkContext>(client));
    }

    [Fact]
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
    // HasSortKey only — validation fails because partition key is required
    // -------------------------------------------------------------------

    private sealed record SortKeyWithAutoDiscoveredPkEntity
    {
        public string Id { get; set; } = null!;
        public string Category { get; set; } = null!;
    }

    private sealed class SortKeyWithAutoDiscoveredPkContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<SortKeyWithAutoDiscoveredPkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SortKeyWithAutoDiscoveredPkEntity>(b =>
            {
                b.ToTable("AutoDiscoveredPkTable");
                b.HasSortKey(x => x.Category);
                // No HasKey, no HasPartitionKey — 'Id' is auto-discovered as the partition key
            });

        public static SortKeyWithAutoDiscoveredPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyWithAutoDiscoveredPkContext>(client));
    }

    [Fact]
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
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class RedundantAnnotationContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RedundantAnnotationEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RedundantAnnotationEntity>(b =>
            {
                b.ToTable("RedundantAnnotationTable");
                b.HasPartitionKey(x => x.Id);
                // EF would have auto-discovered 'Id' anyway
            });

        public static RedundantAnnotationContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<RedundantAnnotationContext>(client));
    }

    [Fact]
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
    // Explicit HasKey takes precedence — convention stands down
    // -------------------------------------------------------------------

    private sealed record ExplicitKeyEntity
    {
        public string PkProp { get; set; } = null!;
        public string SkProp { get; set; } = null!;
    }

    private sealed class ExplicitKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExplicitKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitKeyEntity>(b =>
            {
                b.ToTable("ExplicitKeyTable");
                b.HasKey(x => new { x.PkProp, x.SkProp }); // explicit — convention must stand down
                b.HasPartitionKey(x => x.PkProp);
                b.HasSortKey(x => x.SkProp);
            });

        public static ExplicitKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitKeyContext>(client));
    }

    [Fact]
    public void ExplicitHasKey_WithAnnotations_ConventionStandsDownAndKeyIsUnchanged()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitKeyEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Should().HaveCount(2);
        primaryKey.Properties[0].Name.Should().Be("PkProp");
        primaryKey.Properties[1].Name.Should().Be("SkProp");
        entityType.GetPartitionKeyPropertyName().Should().Be("PkProp");
        entityType.GetSortKeyPropertyName().Should().Be("SkProp");
    }

    // -------------------------------------------------------------------
    // Owned entity types — convention must not modify their keys
    // -------------------------------------------------------------------

    private sealed record OwnerWithAnnotationEntity
    {
        public string Id { get; set; } = null!;
        public OwnedPart Detail { get; set; } = null!;
    }

    private sealed record OwnedPart
    {
        public string Value { get; set; } = null!;
    }

    private sealed class OwnedPartContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<OwnerWithAnnotationEntity> Owners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithAnnotationEntity>(b =>
            {
                b.ToTable("OwnedPartTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                b.OwnsOne(x => x.Detail);
            });

        public static OwnedPartContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<OwnedPartContext>(client));
    }

    [Fact]
    public void OwnedEntityType_ConventionDoesNotApply_NoChange()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = OwnedPartContext.Create(client);

        // Owned types have no DynamoDB key annotations — their PK is managed by
        // OwnedTypePrimaryKeyConvention
        var ownedType = ctx.Model.FindEntityType(typeof(OwnedPart))!;
        ownedType["Dynamo:PartitionKeyPropertyName"].Should().BeNull();
        ownedType["Dynamo:SortKeyPropertyName"].Should().BeNull();
    }
}
