using System.ComponentModel.DataAnnotations;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests DynamoDB entity key-mapping annotations are derived only from Dynamo-specific
///     configuration or Dynamo naming conventions.
/// </summary>
public class DynamoEntityKeyMappingConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

    private sealed record ConventionalKeyEntity
    {
        public string PK { get; set; } = null!;

        public string SK { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class ConventionalKeyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConventionalKeyEntity>(b => b.ToTable("ConventionalKeyTable"));

        public static ConventionalKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConventionalKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ConventionalPkSkNames_AreInferredAsDynamoKeyMapping()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ConventionalKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ConventionalKeyEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Select(static p => p.Name).Should().Equal("PK", "SK");
        entityType.GetPartitionKeyPropertyName().Should().Be("PK");
        entityType.GetSortKeyPropertyName().Should().Be("SK");
    }

    private sealed record ExplicitKeyOnlyEntity
    {
        public string TenantId { get; set; } = null!;

        public string OrderId { get; set; } = null!;
    }

    private sealed class ExplicitKeyOnlyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitKeyOnlyEntity>(b =>
            {
                b.ToTable("ExplicitKeyOnlyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
            });

        public static ExplicitKeyOnlyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitKeyOnlyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_OnRootEntity_InfersDynamoKeyMapping()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitKeyOnlyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitKeyOnlyEntity))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        primaryKey.Properties.Select(static p => p.Name).Should().Equal("TenantId", "OrderId");
        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    private sealed record ExplicitPartitionKeyBeatsConventionEntity
    {
        public string PK { get; set; } = null!;

        public string CustomKey { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class ExplicitPartitionKeyBeatsConventionContext(DbContextOptions options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitPartitionKeyBeatsConventionEntity>(b =>
            {
                b.ToTable("ExplicitPartitionKeyBeatsConventionTable");
                b.HasPartitionKey(x => x.CustomKey);
            });

        public static ExplicitPartitionKeyBeatsConventionContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitPartitionKeyBeatsConventionContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitPartitionKey_TakesPrecedenceOverConventionalPkNamedProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitPartitionKeyBeatsConventionContext.Create(client);

        var entityType =
            ctx.Model.FindEntityType(typeof(ExplicitPartitionKeyBeatsConventionEntity))!;
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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ThreePartPrimaryKeyEntity>(b =>
            {
                b.ToTable("ThreePartPrimaryKeyTable");
                b.HasKey(x => new { x.A, x.B, x.C });
            });

        public static ThreePartPrimaryKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ThreePartPrimaryKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitThreePartHasKey_OnRootEntity_IsRejectedWithDynamoShapeError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => ThreePartPrimaryKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DynamoDB table keys support only one- or two-part keys*");
    }

    private sealed record SingleExplicitKeyEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed record KeyAttributeEntity
    {
        [Key]
        public string Id { get; set; } = null!;
    }

    private sealed class KeyAttributeContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyAttributeEntity>(b => b.ToTable("KeyAttributeTable"));

        public static KeyAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<KeyAttributeContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void KeyAttribute_InfersPartitionKeyOnly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = KeyAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(KeyAttributeEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    [PrimaryKey(nameof(TenantId), nameof(OrderId))]
    private sealed record PrimaryKeyAttributeEntity
    {
        public string TenantId { get; set; } = null!;

        public string OrderId { get; set; } = null!;
    }

    private sealed class PrimaryKeyAttributeContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PrimaryKeyAttributeEntity>(b
                => b.ToTable("PrimaryKeyAttributeTable"));

        public static PrimaryKeyAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PrimaryKeyAttributeContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PrimaryKeyAttribute_InfersPartitionAndSortKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = PrimaryKeyAttributeContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(PrimaryKeyAttributeEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    private sealed class SingleExplicitKeyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleExplicitKeyEntity>(b =>
            {
                b.ToTable("SingleExplicitKeyTable");
                b.HasKey(x => x.Id);
            });

        public static SingleExplicitKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SingleExplicitKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OnePartExplicitHasKey_InfersPartitionKeyOnly()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SingleExplicitKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(SingleExplicitKeyEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("Id");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    private sealed record ExplicitKeyWithFallbackIdEntity
    {
        public string Id { get; set; } = null!;

        public string CustomId { get; set; } = null!;
    }

    private sealed class ExplicitKeyWithFallbackIdContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitKeyWithFallbackIdEntity>(b =>
            {
                b.ToTable("ExplicitKeyWithFallbackIdTable");
                b.HasKey(x => x.CustomId);
            });

        public static ExplicitKeyWithFallbackIdContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitKeyWithFallbackIdContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_BeatsFallbackIdConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitKeyWithFallbackIdContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitKeyWithFallbackIdEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("CustomId");
        entityType.GetSortKeyPropertyName().Should().BeNull();
    }

    private sealed record CombinedKeyEntity
    {
        public string TenantId { get; set; } = null!;

        public string OrderId { get; set; } = null!;

        public string OtherId { get; set; } = null!;
    }

    private sealed class MatchingCombinedKeyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CombinedKeyEntity>(b =>
            {
                b.ToTable("MatchingCombinedKeyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
                b.HasPartitionKey(x => x.TenantId);
                b.HasSortKey(x => x.OrderId);
            });

        public static MatchingCombinedKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MatchingCombinedKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void MatchingHasKeyAndProviderKeys_IsValid()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = MatchingCombinedKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(CombinedKeyEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    private sealed class MismatchedPartitionCombinedKeyContext(DbContextOptions options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CombinedKeyEntity>(b =>
            {
                b.ToTable("MismatchedPartitionCombinedKeyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
                b.HasPartitionKey(x => x.OtherId);
            });

        public static MismatchedPartitionCombinedKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MismatchedPartitionCombinedKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void MismatchedHasKeyAndPartitionKey_ThrowsTargetedError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => MismatchedPartitionCombinedKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key 'OtherId'*EF primary key starts with 'TenantId'*");
    }

    private sealed class MismatchedSortCombinedKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CombinedKeyEntity>(b =>
            {
                b.ToTable("MismatchedSortCombinedKeyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
                b.HasSortKey(x => x.OtherId);
            });

        public static MismatchedSortCombinedKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MismatchedSortCombinedKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void MismatchedHasKeyAndSortKey_ThrowsTargetedError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => MismatchedSortCombinedKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key 'OtherId'*second EF primary-key property is 'OrderId'*");
    }

    private sealed class SortKeyOnlyWithTwoPartHasKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CombinedKeyEntity>(b =>
            {
                b.ToTable("SortKeyOnlyWithTwoPartHasKeyTable");
                b.HasKey(x => new { x.TenantId, x.OrderId });
                b.HasSortKey(x => x.OrderId);
            });

        public static SortKeyOnlyWithTwoPartHasKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyOnlyWithTwoPartHasKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SortKeyOnlyWithTwoPartHasKey_IsValidAndInfersPartitionKey()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = SortKeyOnlyWithTwoPartHasKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(CombinedKeyEntity))!;

        entityType.GetPartitionKeyPropertyName().Should().Be("TenantId");
        entityType.GetSortKeyPropertyName().Should().Be("OrderId");
    }

    private sealed class SortKeyOnSingleHasKeyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CombinedKeyEntity>(b =>
            {
                b.ToTable("SortKeyOnSingleHasKeyTable");
                b.HasKey(x => x.TenantId);
                b.HasSortKey(x => x.OrderId);
            });

        public static SortKeyOnSingleHasKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyOnSingleHasKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SortKeyWithOnePartHasKey_ThrowsTargetedError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => SortKeyOnSingleHasKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key 'OrderId'*EF primary key has only one property*");
    }

    private sealed record SortKeyOnlyEntity
    {
        public string Identifier { get; set; } = null!;

        public string OrderId { get; set; } = null!;
    }

    private sealed class SortKeyOnlyWithoutPrimaryKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SortKeyOnlyEntity>(b =>
            {
                b.ToTable("SortKeyOnlyWithoutPrimaryKeyTable");
                b.HasSortKey(x => x.OrderId);
            });

        public static SortKeyOnlyWithoutPrimaryKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyOnlyWithoutPrimaryKeyContext>(client));
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SortKeyOnlyWithoutPrimaryKey_ThrowsPartitionMissingError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => SortKeyOnlyWithoutPrimaryKeyContext.Create(client).Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*No DynamoDB partition key is configured*Sort key property 'OrderId'*");
    }
}
