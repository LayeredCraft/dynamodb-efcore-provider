using Amazon.DynamoDBv2;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests DynamoDB entity key-mapping annotations are derived only from Dynamo-specific
///     configuration or Dynamo naming conventions.
/// </summary>
public class DynamoEntityKeyMappingConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
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

    [Fact]
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

    [Fact]
    public void ExplicitHasKey_OnRootEntity_IsRejected()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => ExplicitKeyOnlyContext.Create(client).Model;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use HasPartitionKey(...) and optional HasSortKey(...)*do not use HasKey(...) or [Key]*");
    }

    private sealed record ExplicitPartitionKeyBeatsConventionEntity
    {
        public string PK { get; set; } = null!;
        public string CustomKey { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class ExplicitPartitionKeyBeatsConventionContext(DbContextOptions options) : DbContext(options)
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

    [Fact]
    public void ExplicitPartitionKey_TakesPrecedenceOverConventionalPkNamedProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = ExplicitPartitionKeyBeatsConventionContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(ExplicitPartitionKeyBeatsConventionEntity))!;
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

    [Fact]
    public void ExplicitMultiPropertyHasKey_OnRootEntity_IsRejected()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var act = () => ThreePartPrimaryKeyContext.Create(client).Model;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must use HasPartitionKey(...) and optional HasSortKey(...)*do not use HasKey(...) or [Key]*");
    }
}
