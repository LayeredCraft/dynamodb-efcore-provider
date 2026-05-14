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

    /// <summary>Verifies numeric DynamoDB partition keys are application-assigned by convention.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void IntegerPartitionKey_IsNotValueGeneratedByConvention()
    {
        using var ctx = IntPartitionKeyContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<IntPartitionKeyEntity>(ctx, nameof(IntPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    /// <summary>Verifies string DynamoDB partition keys are application-assigned by convention.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void StringPartitionKey_IsNotValueGeneratedByConvention()
    {
        using var ctx = StringPartitionKeyContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<StringPartitionKeyEntity>(ctx, nameof(StringPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    /// <summary>
    ///     Verifies Guid DynamoDB partition keys keep EF Core client-side value generation by
    ///     convention.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void GuidPartitionKey_IsValueGeneratedByConvention()
    {
        using var ctx = GuidPartitionKeyContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<GuidPartitionKeyEntity>(ctx, nameof(GuidPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.OnAdd);
    }

    /// <summary>Verifies explicit value generation on non-Guid keys overrides DynamoDB conventions.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitValueGeneratedOnAdd_ForIntegerKey_IsPreserved()
    {
        using var ctx = ExplicitIntegerGenerationContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<IntPartitionKeyEntity>(ctx, nameof(IntPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.OnAdd);
    }

    /// <summary>Verifies explicit no-generation on Guid keys overrides EF Core conventions.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitValueGeneratedNever_ForGuidKey_IsPreserved()
    {
        using var ctx = ExplicitGuidNoGenerationContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<GuidPartitionKeyEntity>(ctx, nameof(GuidPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    /// <summary>Verifies composite DynamoDB keys are application-assigned by convention.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CompositeGuidPartitionAndStringSortKey_AreNotValueGeneratedByConvention()
    {
        using var ctx = CompositeKeyContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<CompositeKeyEntity>(ctx, nameof(CompositeKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
        FindProperty<CompositeKeyEntity>(ctx, nameof(CompositeKeyEntity.SortKey))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    /// <summary>Verifies non-key Guid properties are not accidentally configured for generation.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void NonKeyGuidProperty_IsNotValueGeneratedByConvention()
    {
        using var ctx = GuidNonKeyContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<GuidNonKeyEntity>(ctx, nameof(GuidNonKeyEntity.ExternalId))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    /// <summary>Verifies table mapping annotation changes re-apply DynamoDB key-generation conventions.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void LateTableMapping_ReappliesDynamoValueGenerationConvention()
    {
        using var ctx = LateTableMappingContext.Create(Substitute.For<IAmazonDynamoDB>());

        FindProperty<IntPartitionKeyEntity>(ctx, nameof(IntPartitionKeyEntity.Id))
            .ValueGenerated
            .Should()
            .Be(ValueGenerated.Never);
    }

    private static IProperty FindProperty<TEntity>(DbContext context, string name)
        => context.Model.FindEntityType(typeof(TEntity))!.FindProperty(name)!;

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

    private sealed record StringPartitionKeyEntity
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class StringPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<StringPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringPartitionKeyEntity>(b =>
            {
                b.ToTable("StringPartitionKeyTable");
                b.HasPartitionKey(x => x.Id);
            });

        public static StringPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<StringPartitionKeyContext>(client));
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

    private sealed class ExplicitIntegerGenerationContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<IntPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntPartitionKeyEntity>(b =>
            {
                b.ToTable("ExplicitIntegerGenerationTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedOnAdd();
            });

        public static ExplicitIntegerGenerationContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitIntegerGenerationContext>(client));
    }

    private sealed class ExplicitGuidNoGenerationContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                b.ToTable("ExplicitGuidNoGenerationTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedNever();
            });

        public static ExplicitGuidNoGenerationContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitGuidNoGenerationContext>(client));
    }

    private sealed record CompositeKeyEntity
    {
        public Guid Id { get; set; }

        public string SortKey { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class CompositeKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<CompositeKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeKeyEntity>(b =>
            {
                b.ToTable("CompositeKeyTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.SortKey);
            });

        public static CompositeKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<CompositeKeyContext>(client));
    }

    private sealed record GuidNonKeyEntity
    {
        public int Id { get; set; }

        public Guid ExternalId { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class GuidNonKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GuidNonKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidNonKeyEntity>(b =>
            {
                b.ToTable("GuidNonKeyTable");
                b.HasPartitionKey(x => x.Id);
            });

        public static GuidNonKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidNonKeyContext>(client));
    }

    private sealed class LateTableMappingContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<IntPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntPartitionKeyEntity>(b =>
            {
                b.HasPartitionKey(x => x.Id);
                b.ToTable("LateTableMappingTable");
            });

        public static LateTableMappingContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<LateTableMappingContext>(client));
    }
}
