using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests fallback and precedence behavior for DynamoDB entity key-mapping annotations.
/// </summary>
public class DynamoEntityKeyMappingConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private sealed record SingleKeyEntity
    {
        public string TenantId { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class SingleKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SingleKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleKeyEntity>(b =>
            {
                b.ToTable("SingleKeyTable");
                b.HasKey(x => x.TenantId);
            });

        public static SingleKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleKeyContext>(client));
    }

    [Fact]
    public void ExplicitSinglePropertyPrimaryKey_InferredAsPartitionKeyMapping()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleKeyEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    private sealed record CompositeKeyEntity
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class CompositeKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<CompositeKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeKeyEntity>(b =>
            {
                b.ToTable("CompositeKeyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
            });

        public static CompositeKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<CompositeKeyContext>(client));
    }

    [Fact]
    public void ExplicitCompositePrimaryKey_InferredAsPartitionAndSortKeyMapping()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = CompositeKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(CompositeKeyEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Select(static p => p.Name).Should().Equal("TenantId", "OrderId");
        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    private sealed record ExplicitPrimaryKeyBeatsNameEntity
    {
        public string PK { get; set; } = null!;
        public string CustomKey { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class ExplicitPrimaryKeyBeatsNameContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ExplicitPrimaryKeyBeatsNameEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitPrimaryKeyBeatsNameEntity>(b =>
            {
                b.ToTable("ExplicitPrimaryKeyBeatsNameTable");
                b.HasKey(x => x.CustomKey);
            });

        public static ExplicitPrimaryKeyBeatsNameContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitPrimaryKeyBeatsNameContext>(client));
    }

    [Fact]
    public void ExplicitPrimaryKeyShape_TakesPrecedenceOverPkNamedProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitPrimaryKeyBeatsNameContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitPrimaryKeyBeatsNameEntity))!;
        entityType.GetPartitionKeyPropertyName().Should().Be("CustomKey");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    private sealed record ThreePartPrimaryKeyEntity
    {
        public string A { get; set; } = null!;
        public string B { get; set; } = null!;
        public string C { get; set; } = null!;
    }

    private sealed class ThreePartPrimaryKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ThreePartPrimaryKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ThreePartPrimaryKeyEntity>(b =>
            {
                b.ToTable("ThreePartPrimaryKeyTable");
                b.HasKey(x => new { x.A, x.B, x.C });
            });

        public static ThreePartPrimaryKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ThreePartPrimaryKeyContext>(client));
    }

    [Fact]
    public void PrimaryKeyWithMoreThanTwoProperties_ThrowsTargetedValidationMessage()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => ThreePartPrimaryKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*supports only one- or two-part keys*");
    }
}
