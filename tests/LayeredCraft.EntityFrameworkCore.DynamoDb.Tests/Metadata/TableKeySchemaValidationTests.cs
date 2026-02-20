using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Metadata;

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

    // -------------------------------------------------------------------
    // Conflicting partition key names on the same table
    // -------------------------------------------------------------------

    private sealed record EntityA
    {
        public string Id { get; set; } = null!;
    }

    private sealed record EntityB
    {
        public string Id { get; set; } = null!;
    }

    private sealed class ConflictingPkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityA> EntitiesA { get; set; } = null!;
        public DbSet<EntityB> EntitiesB { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityA>(b =>
            {
                b.ToTable("SharedTable");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK1");
            });
            modelBuilder.Entity<EntityB>(b =>
            {
                b.ToTable("SharedTable");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK2");
            });
        }

        public static ConflictingPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingPkContext>(client));
    }

    [Fact]
    public void SharedTable_ConflictingPartitionKeyNames_Throws()
    {
        var ctx = ConflictingPkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute names*");
    }

    // -------------------------------------------------------------------
    // One entity has PK+SK, the other has PK-only
    // -------------------------------------------------------------------

    private sealed record EntityC
    {
        public string Id { get; set; } = null!;
    }

    private sealed record EntityD
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed class MixedSkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityC> EntitiesC { get; set; } = null!;
        public DbSet<EntityD> EntitiesD { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityC>(b =>
            {
                b.ToTable("SharedTable2");
                b.HasKey(x => x.Id);
                // Map to "PK" so both entities share the same partition key attribute name
                b.Property(x => x.Id).HasAttributeName("PK");
                // single-part key → no sort key detected
            });
            modelBuilder.Entity<EntityD>(b =>
            {
                b.ToTable("SharedTable2");
                b.HasKey(x => new { x.PartId, x.SortId });
                // Map first PK property to "PK" so partition key names match
                b.Property(x => x.PartId).HasAttributeName("PK");
                // two-part key → sort key "SortId" detected
            });
        }

        public static MixedSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MixedSkContext>(client));
    }

    [Fact]
    public void SharedTable_OneHasSortKeyOtherDoesNot_Throws()
    {
        var ctx = MixedSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*sort key attribute names*");
    }

    // -------------------------------------------------------------------
    // Both entities have SK but different names
    // -------------------------------------------------------------------

    private sealed record EntityE
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed record EntityF
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed class ConflictingSkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityE> EntitiesE { get; set; } = null!;
        public DbSet<EntityF> EntitiesF { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityE>(b =>
            {
                b.ToTable("SharedTable3");
                b.HasKey(x => new { x.PartId, x.SortId });
                // Both share same PK attribute name; differ on SK
                b.Property(x => x.SortId).HasAttributeName("SK1");
            });
            modelBuilder.Entity<EntityF>(b =>
            {
                b.ToTable("SharedTable3");
                b.HasKey(x => new { x.PartId, x.SortId });
                b.Property(x => x.SortId).HasAttributeName("SK2");
            });
        }

        public static ConflictingSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingSkContext>(client));
    }

    [Fact]
    public void SharedTable_ConflictingSortKeyNames_Throws()
    {
        var ctx = ConflictingSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*sort key attribute names*");
    }

    // -------------------------------------------------------------------
    // Consistent PK-only — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityG
    {
        public string Id { get; set; } = null!;
    }

    private sealed record EntityH
    {
        public string Id { get; set; } = null!;
    }

    private sealed class ConsistentPkOnlyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityG> EntitiesG { get; set; } = null!;
        public DbSet<EntityH> EntitiesH { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityG>(b =>
            {
                b.ToTable("SharedTable4");
                b.HasKey(x => x.Id);
                // Both auto-detect "Id" as the PK attribute name (same CLR property name)
            });
            modelBuilder.Entity<EntityH>(b =>
            {
                b.ToTable("SharedTable4");
                b.HasKey(x => x.Id);
            });
        }

        public static ConsistentPkOnlyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkOnlyContext>(client));
    }

    [Fact]
    public void SharedTable_ConsistentPkOnly_DoesNotThrow()
    {
        var ctx = ConsistentPkOnlyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------
    // Consistent PK + SK — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityI
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed record EntityJ
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed class ConsistentPkSkContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityI> EntitiesI { get; set; } = null!;
        public DbSet<EntityJ> EntitiesJ { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityI>(b =>
            {
                b.ToTable("SharedTable5");
                b.HasKey(x => new { x.PartId, x.SortId });
                // Both auto-detect "PartId"/"SortId" as PK/SK attribute names
            });
            modelBuilder.Entity<EntityJ>(b =>
            {
                b.ToTable("SharedTable5");
                b.HasKey(x => new { x.PartId, x.SortId });
            });
        }

        public static ConsistentPkSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkSkContext>(client));
    }

    [Fact]
    public void SharedTable_ConsistentPkSk_DoesNotThrow()
    {
        var ctx = ConsistentPkSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------
    // Different tables with different schemas — should not throw
    // -------------------------------------------------------------------

    private sealed record EntityK
    {
        public string Id { get; set; } = null!;
    }

    private sealed record EntityL
    {
        public string PartId { get; set; } = null!;
        public string SortId { get; set; } = null!;
    }

    private sealed class DifferentTablesContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityK> EntitiesK { get; set; } = null!;
        public DbSet<EntityL> EntitiesL { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityK>(b =>
            {
                b.ToTable("TableAlpha");
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("MY_PK");
            });
            modelBuilder.Entity<EntityL>(b =>
            {
                b.ToTable("TableBeta");
                b.HasKey(x => new { x.PartId, x.SortId });
                b.Property(x => x.PartId).HasAttributeName("HASH");
                b.Property(x => x.SortId).HasAttributeName("RANGE");
            });
        }

        public static DifferentTablesContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DifferentTablesContext>(client));
    }

    [Fact]
    public void DifferentTables_DifferentKeySchemas_DoesNotThrow()
    {
        var ctx = DifferentTablesContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------
    // New: HasPartitionKey/HasSortKey with non-existent property throws
    // -------------------------------------------------------------------

    private sealed record GhostPropEntity
    {
        public string Id { get; set; } = null!;
    }

    private sealed class GhostPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GhostPropEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GhostPropEntity>(b =>
            {
                b.ToTable("GhostPkTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey("Ghost");
            });

        public static GhostPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostPartitionKeyContext>(client));
    }

    [Fact]
    public void HasPartitionKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key property 'Ghost'*does not exist*");
    }

    private sealed class GhostSortKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GhostPropEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GhostPropEntity>(b =>
            {
                b.ToTable("GhostSkTable");
                b.HasKey(x => x.Id);
                b.HasSortKey("Ghost");
            });

        public static GhostSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostSortKeyContext>(client));
    }

    [Fact]
    public void HasSortKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key property 'Ghost'*does not exist*");
    }

    // -------------------------------------------------------------------
    // New: HasPartitionKey/HasSortKey with property not in EF primary key throws
    // -------------------------------------------------------------------

    private sealed record KeyMismatchEntity
    {
        public string Id { get; set; } = null!;
        public string SomeProp { get; set; } = null!;
    }

    private sealed class PartitionKeyNotInEfKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                b.ToTable("MismatchPkTable");
                b.HasKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasPartitionKey(x => x.SomeProp);
            });

        public static PartitionKeyNotInEfKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyNotInEfKeyContext>(client));
    }

    [Fact]
    public void HasPartitionKey_PropertyNotInEfKey_ThrowsOnValidation()
    {
        var ctx = PartitionKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key property 'SomeProp'*not part of the EF primary key*");
    }

    private sealed class SortKeyNotInEfKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                b.ToTable("MismatchSkTable");
                b.HasKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasSortKey(x => x.SomeProp);
            });

        public static SortKeyNotInEfKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyNotInEfKeyContext>(client));
    }

    [Fact]
    public void HasSortKey_PropertyNotInEfKey_ThrowsOnValidation()
    {
        var ctx = SortKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key property 'SomeProp'*not part of the EF primary key*");
    }

    // -------------------------------------------------------------------
    // HasSortKey with no resolvable partition key — no HasPartitionKey, no
    // auto-discoverable PK property — must produce a DynamoDB-specific error
    // -------------------------------------------------------------------

    private sealed record NoDiscoverablePkEntity
    {
        // Properties are not named 'Id' / '<EntityName>Id', so EF will not auto-discover a PK.
        public string PartitionKey { get; set; } = null!;
        public string SortKey { get; set; } = null!;
    }

    private sealed class SortKeyWithNoResolvablePkContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<NoDiscoverablePkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoDiscoverablePkEntity>(b =>
            {
                b.ToTable("SortKeyOnlyTable");
                // HasSortKey set, but no HasPartitionKey and no auto-discoverable PK property.
                b.HasSortKey(x => x.SortKey);
            });

        public static SortKeyWithNoResolvablePkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyWithNoResolvablePkContext>(client));
    }

    [Fact]
    public void HasSortKey_WithNoResolvablePartitionKey_ThrowsDynamoSpecificError()
    {
        var ctx = SortKeyWithNoResolvablePkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Sort key property 'SortKey'*no partition key can be determined*");
    }
}
