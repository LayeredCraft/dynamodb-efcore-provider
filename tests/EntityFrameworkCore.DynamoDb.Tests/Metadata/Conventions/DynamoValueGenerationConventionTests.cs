using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <c>DynamoValueGenerationConvention</c>.
/// </summary>
public class DynamoValueGenerationConventionTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

    private sealed record IntPartitionKeyEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class IntPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<IntPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntPartitionKeyEntity>(b =>
            {
                b.ToTable("IntPartitionKeyTable");
                b.HasPartitionKey(x => x.Id);
            });

        public static IntPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<IntPartitionKeyContext>(client));
    }

    /// <summary>
    ///     Verifies numeric DynamoDB partition keys are application-assigned by convention.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void IntegerPartitionKey_IsNotValueGeneratedByConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = IntPartitionKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(IntPartitionKeyEntity))!;

        entityType.FindProperty(nameof(IntPartitionKeyEntity.Id))!
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    private sealed record GuidPartitionKeyEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class GuidPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                b.ToTable("GuidPartitionKeyTable");
                b.HasPartitionKey(x => x.Id);
            });

        public static GuidPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidPartitionKeyContext>(client));
    }

    /// <summary>
    ///     Verifies Guid DynamoDB partition keys keep EF Core client-side value generation by convention.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GuidPartitionKey_IsValueGeneratedByConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = GuidPartitionKeyContext.Create(client);

        var entityType = ctx.Model.FindEntityType(typeof(GuidPartitionKeyEntity))!;

        entityType.FindProperty(nameof(GuidPartitionKeyEntity.Id))!
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.OnAdd);
    }
}
