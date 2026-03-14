using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata;

/// <summary>
///     Tests for <c>DynamoModelValidator</c> table key schema consistency and key property
///     validation.
/// </summary>
public class TableKeySchemaValidationTests
{
    private static IAmazonDynamoDB MockClient() => Substitute.For<IAmazonDynamoDB>();

    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ConflictingPartitionKeyNames_Throws()
    {
        var ctx = ConflictingPkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute names*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_OneHasSortKeyOtherDoesNot_Throws()
    {
        var ctx = MixedSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*mixed key shapes*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ConflictingSortKeyNames_Throws()
    {
        var ctx = ConflictingSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*sort key attribute names*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ConsistentPkOnly_DoesNotThrow()
    {
        var ctx = ConsistentPkOnlyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ConsistentPkSk_DoesNotThrow()
    {
        var ctx = ConsistentPkSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DifferentTables_DifferentKeySchemas_DoesNotThrow()
    {
        var ctx = DifferentTablesContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key property 'Ghost'*does not exist*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasSortKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key property 'Ghost'*does not exist*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasKey_WithPartitionKey_OnRootEntity_ThrowsOnValidation()
    {
        var ctx = PartitionKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*must use HasPartitionKey(...) and optional HasSortKey(...)*do not use HasKey(...) or [Key]*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitHasKey_WithSortKey_OnRootEntity_ThrowsOnValidation()
    {
        var ctx = SortKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*must use HasPartitionKey(...) and optional HasSortKey(...)*do not use HasKey(...) or [Key]*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasSortKey_WithNoResolvablePartitionKey_ThrowsDynamoSpecificError()
    {
        var ctx = SortKeyWithNoResolvablePkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*No DynamoDB partition key is configured*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_ShadowProperty_DoesNotThrow()
    {
        var ctx = ShadowPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKeyAndSortKey_ShadowProperties_DoesNotThrow()
    {
        var ctx = ShadowPartitionAndSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ShadowKeyProperties_WithMatchingAttributeNames_DoesNotThrow()
    {
        var ctx = SharedTableShadowKeyConsistentContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_ShadowKeyProperties_WithConflictingPartitionAttributeNames_Throws()
    {
        var ctx = SharedTableShadowKeyConflictingPkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute names*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_BoolType_ThrowsOnValidation()
    {
        var ctx = BoolPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key*must be string, number, or binary*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasSortKey_BoolType_ThrowsOnValidation()
    {
        var ctx = BoolSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key*must be string, number, or binary*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_GuidWithoutConverter_DoesNotThrow()
    {
        var ctx = GuidPartitionKeyWithoutConverterContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_GuidWithStringConverter_DoesNotThrow()
    {
        var ctx = GuidPartitionKeyWithConverterContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void HasPartitionKey_ConverterWithNullableProviderType_ThrowsOnValidation()
    {
        var ctx = NullableProviderPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*effective provider type 'int?' is nullable*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_PartitionKeyTypeCategoryMismatch_Throws()
    {
        var ctx = SharedTablePartitionTypeMismatchContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute 'PK'*different key type categories*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void SharedTable_SortKeyTypeCategoryMismatch_Throws()
    {
        var ctx = SharedTableSortTypeMismatchContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key attribute 'SK'*different key type categories*");
    }

    // -------------------------------------------------------------------
    // Conflicting partition key names on the same table
    // -------------------------------------------------------------------

    private sealed record EntityA
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed record EntityB
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class ConflictingPkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityA> EntitiesA { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityB> EntitiesB { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityA>(b =>
            {
                b.ToTable("SharedTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK1");
            });
            modelBuilder.Entity<EntityB>(b =>
            {
                b.ToTable("SharedTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK2");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static ConflictingPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingPkContext>(client));
    }

    // -------------------------------------------------------------------
    // One entity has PK+SK, the other has PK-only
    // -------------------------------------------------------------------

    private sealed record EntityC
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed record EntityD
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed class MixedSkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityC> EntitiesC { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityD> EntitiesD { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityC>(b =>
            {
                b.ToTable("SharedTable2");
                b.HasPartitionKey(x => x.Id);
                // Map to "PK" so both entities share the same partition key attribute name
                b.Property(x => x.Id).HasAttributeName("PK");
                // single-part key → no sort key detected
            });
            modelBuilder.Entity<EntityD>(b =>
            {
                b.ToTable("SharedTable2");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Map first PK property to "PK" so partition key names match
                b.Property(x => x.PartId).HasAttributeName("PK");
                // two-part key → sort key "SortId" detected
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static MixedSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MixedSkContext>(client));
    }

    // -------------------------------------------------------------------
    // Both entities have SK but different names
    // -------------------------------------------------------------------

    private sealed record EntityE
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed record EntityF
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed class ConflictingSkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityE> EntitiesE { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityF> EntitiesF { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityE>(b =>
            {
                b.ToTable("SharedTable3");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Both share same PK attribute name; differ on SK
                b.Property(x => x.SortId).HasAttributeName("SK1");
            });
            modelBuilder.Entity<EntityF>(b =>
            {
                b.ToTable("SharedTable3");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.SortId).HasAttributeName("SK2");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static ConflictingSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingSkContext>(client));
    }

    // -------------------------------------------------------------------
    // Consistent PK-only — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityG
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed record EntityH
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class ConsistentPkOnlyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityG> EntitiesG { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityH> EntitiesH { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityG>(b =>
            {
                b.ToTable("SharedTable4");
                b.HasPartitionKey(x => x.Id);
                // Both auto-detect "Id" as the PK attribute name (same CLR property name)
            });
            modelBuilder.Entity<EntityH>(b =>
            {
                b.ToTable("SharedTable4");
                b.HasPartitionKey(x => x.Id);
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static ConsistentPkOnlyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkOnlyContext>(client));
    }

    // -------------------------------------------------------------------
    // Consistent PK + SK — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityI
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed record EntityJ
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed class ConsistentPkSkContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityI> EntitiesI { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityJ> EntitiesJ { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityI>(b =>
            {
                b.ToTable("SharedTable5");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Both auto-detect "PartId"/"SortId" as PK/SK attribute names
            });
            modelBuilder.Entity<EntityJ>(b =>
            {
                b.ToTable("SharedTable5");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static ConsistentPkSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkSkContext>(client));
    }

    // -------------------------------------------------------------------
    // Different tables with different schemas — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityK
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed record EntityL
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed class DifferentTablesContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityK> EntitiesK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EntityL> EntitiesL { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityK>(b =>
            {
                b.ToTable("TableAlpha");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("MY_PK");
            });
            modelBuilder.Entity<EntityL>(b =>
            {
                b.ToTable("TableBeta");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("HASH");
                b.Property(x => x.SortId).HasAttributeName("RANGE");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static DifferentTablesContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DifferentTablesContext>(client));
    }

    // -------------------------------------------------------------------
    // New: HasPartitionKey/HasSortKey with non-existent property throws
    // -------------------------------------------------------------------

    private sealed record GhostPropEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed class GhostPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<GhostPropEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GhostPropEntity>(b =>
            {
                b.ToTable("GhostPkTable");
                b.HasPartitionKey("Ghost");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static GhostPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostPartitionKeyContext>(client));
    }

    private sealed class GhostSortKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<GhostPropEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GhostPropEntity>(b =>
            {
                b.ToTable("GhostSkTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey("Ghost");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static GhostSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostSortKeyContext>(client));
    }

    // -------------------------------------------------------------------
    // New: HasPartitionKey/HasSortKey with property not in EF primary key throws
    // -------------------------------------------------------------------

    private sealed record KeyMismatchEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SomeProp { get; set; } = null!;
    }

    private sealed class PartitionKeyNotInEfKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                b.ToTable("MismatchPkTable");
                b.HasKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasPartitionKey(x => x.SomeProp);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static PartitionKeyNotInEfKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyNotInEfKeyContext>(client));
    }

    private sealed class SortKeyNotInEfKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                b.ToTable("MismatchSkTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasSortKey(x => x.SomeProp);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static SortKeyNotInEfKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyNotInEfKeyContext>(client));
    }

    // -------------------------------------------------------------------
    // HasSortKey with no resolvable partition key — no HasPartitionKey, no
    // auto-discoverable PK property — must produce a DynamoDB-specific error
    // -------------------------------------------------------------------

    private sealed record NoDiscoverablePkEntity
    {
        // Properties are not named 'Id', '<EntityName>Id', 'PK', or 'PartitionKey', so neither
        // EF Core key discovery nor the DynamoDB key discovery convention will find a partition
        // key.
        /// <summary>Provides functionality for this member.</summary>
        public string HashAttr { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string RangeAttr { get; set; } = null!;
    }

    private sealed class SortKeyWithNoResolvablePkContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<NoDiscoverablePkEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoDiscoverablePkEntity>(b =>
            {
                b.ToTable("SortKeyOnlyTable");
                // HasSortKey set, but no HasPartitionKey and no auto-discoverable PK property.
                b.HasSortKey(x => x.RangeAttr);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static SortKeyWithNoResolvablePkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyWithNoResolvablePkContext>(client));
    }

    // -------------------------------------------------------------------
    // Shadow key properties
    // -------------------------------------------------------------------

    private sealed record ShadowKeyEntity;

    private sealed class ShadowPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                b.ToTable("ShadowPkTable");
                b.Property<string>("PK");
                b.HasPartitionKey("PK");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ShadowPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowPartitionKeyContext>(client));
    }

    private sealed class ShadowPartitionAndSortKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                b.ToTable("ShadowPkSkTable");
                b.Property<string>("PK");
                b.Property<string>("SK");
                b.HasPartitionKey("PK");
                b.HasSortKey("SK");
            });

        /// <summary>Provides functionality for this member.</summary>
        public static ShadowPartitionAndSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowPartitionAndSortKeyContext>(client));
    }

    private sealed record SharedShadowEntityA;

    private sealed record SharedShadowEntityB;

    private sealed class SharedTableShadowKeyConsistentContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedShadowEntityA> EntitiesA { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedShadowEntityB> EntitiesB { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedShadowEntityA>(b =>
            {
                b.ToTable("SharedShadowTable");
                b.Property<string>("InternalPK").HasAttributeName("PK");
                b.Property<string>("InternalSK").HasAttributeName("SK");
                b.HasPartitionKey("InternalPK");
                b.HasSortKey("InternalSK");
            });

            modelBuilder.Entity<SharedShadowEntityB>(b =>
            {
                b.ToTable("SharedShadowTable");
                b.Property<string>("OtherPk").HasAttributeName("PK");
                b.Property<string>("OtherSk").HasAttributeName("SK");
                b.HasPartitionKey("OtherPk");
                b.HasSortKey("OtherSk");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableShadowKeyConsistentContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableShadowKeyConsistentContext>(client));
    }

    private sealed class SharedTableShadowKeyConflictingPkContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedShadowEntityA> EntitiesA { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedShadowEntityB> EntitiesB { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedShadowEntityA>(b =>
            {
                b.ToTable("SharedShadowConflictTable");
                b.Property<string>("InternalPK").HasAttributeName("PK");
                b.Property<string>("InternalSK").HasAttributeName("SK");
                b.HasPartitionKey("InternalPK");
                b.HasSortKey("InternalSK");
            });

            modelBuilder.Entity<SharedShadowEntityB>(b =>
            {
                b.ToTable("SharedShadowConflictTable");
                b.Property<string>("OtherPk").HasAttributeName("PK2");
                b.Property<string>("OtherSk").HasAttributeName("SK");
                b.HasPartitionKey("OtherPk");
                b.HasSortKey("OtherSk");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableShadowKeyConflictingPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableShadowKeyConflictingPkContext>(client));
    }

    // -------------------------------------------------------------------
    // Key provider type validation
    // -------------------------------------------------------------------

    private sealed record BoolPartitionKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public bool Id { get; set; }
    }

    private sealed class BoolPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<BoolPartitionKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BoolPartitionKeyEntity>(b =>
            {
                b.ToTable("BoolPkTable");
                b.HasPartitionKey(x => x.Id);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static BoolPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<BoolPartitionKeyContext>(client));
    }

    private sealed record BoolSortKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public bool SK { get; set; }
    }

    private sealed class BoolSortKeyContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<BoolSortKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BoolSortKeyEntity>(b =>
            {
                b.ToTable("BoolSkTable");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static BoolSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<BoolSortKeyContext>(client));
    }

    private sealed record GuidPartitionKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public Guid Id { get; set; }
    }

    private sealed class GuidPartitionKeyWithoutConverterContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                b.ToTable("GuidPkNoConverter");
                b.HasPartitionKey(x => x.Id);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static GuidPartitionKeyWithoutConverterContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidPartitionKeyWithoutConverterContext>(client));
    }

    private sealed class GuidPartitionKeyWithConverterContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                b.ToTable("GuidPkWithConverter");
                b.HasPartitionKey(x => x.Id);
                b
                    .Property(x => x.Id)
                    .HasConversion(
                        new ValueConverter<Guid, string>(
                            static value => value.ToString("N"),
                            static value => Guid.Parse(value)));
            });

        /// <summary>Provides functionality for this member.</summary>
        public static GuidPartitionKeyWithConverterContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidPartitionKeyWithConverterContext>(client));
    }

    private sealed record NullableProviderPartitionKeyEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public int Id { get; set; }
    }

    private sealed class NullableProviderPartitionKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<NullableProviderPartitionKeyEntity> Entities { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullableProviderPartitionKeyEntity>(b =>
            {
                b.ToTable("NullableProviderPkTable");
                b.HasPartitionKey(x => x.Id);
                b
                    .Property(x => x.Id)
                    .HasConversion(
                        new ValueConverter<int, int?>(
                            static value => value,
                            static value => value ?? 0));
            });

        /// <summary>Provides functionality for this member.</summary>
        public static NullableProviderPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<NullableProviderPartitionKeyContext>(client));
    }

    // -------------------------------------------------------------------
    // Shared-table key type category consistency
    // -------------------------------------------------------------------

    private sealed record SharedPartitionTypeEntityA
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Id { get; set; } = null!;
    }

    private sealed record SharedPartitionTypeEntityB
    {
        /// <summary>Provides functionality for this member.</summary>
        public int Id { get; set; }
    }

    private sealed class SharedTablePartitionTypeMismatchContext(DbContextOptions options)
        : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedPartitionTypeEntityA> EntitiesA { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedPartitionTypeEntityB> EntitiesB { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedPartitionTypeEntityA>(b =>
            {
                b.ToTable("SharedPartitionTypeMismatchTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });

            modelBuilder.Entity<SharedPartitionTypeEntityB>(b =>
            {
                b.ToTable("SharedPartitionTypeMismatchTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTablePartitionTypeMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTablePartitionTypeMismatchContext>(client));
    }

    private sealed record SharedSortTypeEntityA
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SortId { get; set; } = null!;
    }

    private sealed record SharedSortTypeEntityB
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PartId { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public int SortId { get; set; }
    }

    private sealed class SharedTableSortTypeMismatchContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedSortTypeEntityA> EntitiesA { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<SharedSortTypeEntityB> EntitiesB { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedSortTypeEntityA>(b =>
            {
                b.ToTable("SharedSortTypeMismatchTable");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("PK");
                b.Property(x => x.SortId).HasAttributeName("SK");
            });

            modelBuilder.Entity<SharedSortTypeEntityB>(b =>
            {
                b.ToTable("SharedSortTypeMismatchTable");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("PK");
                b.Property(x => x.SortId).HasAttributeName("SK");
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableSortTypeMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableSortTypeMismatchContext>(client));
    }
}
