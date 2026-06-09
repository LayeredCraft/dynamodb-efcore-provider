using System.ComponentModel.DataAnnotations;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .Options;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_ConflictingPartitionKeyNames_Throws()
    {
        var ctx = ConflictingPkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute names*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_OneHasSortKeyOtherDoesNot_Throws()
    {
        var ctx = MixedSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*mixed key shapes*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_ConflictingSortKeyNames_Throws()
    {
        var ctx = ConflictingSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().Throw<InvalidOperationException>().WithMessage("*sort key attribute names*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_ConsistentPkOnly_DoesNotThrow()
    {
        var ctx = ConsistentPkOnlyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_ConsistentPkSk_WithProviderAndHasKeyStyles_DoesNotThrow()
    {
        var ctx = ConsistentPkSkContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DifferentTables_DifferentKeySchemas_DoesNotThrow()
    {
        var ctx = DifferentTablesContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key property 'Ghost'*does not exist*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasSortKey_NonExistentProperty_ThrowsOnValidation()
    {
        var ctx = GhostSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key property 'Ghost'*does not exist*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_WithPartitionKey_OnRootEntity_ThrowsOnValidation()
    {
        var ctx = PartitionKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key 'SomeProp'*EF primary key starts with 'Id'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_WithSortKey_OnRootEntity_ThrowsOnValidation()
    {
        var ctx = SortKeyNotInEfKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key 'SomeProp'*EF primary key has only one property*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void KeyAttribute_WithConflictingPartitionKey_ThrowsTargetedError()
    {
        var ctx = KeyAttributeConflictContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key 'OtherId'*EF primary key starts with 'Id'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PrimaryKeyAttribute_WithThreeParts_ThrowsDynamoShapeError()
    {
        var ctx = ThreePartPrimaryKeyAttributeContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DynamoDB table keys support only one- or two-part keys*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasSortKey_WithNoResolvablePartitionKey_ThrowsDynamoSpecificError()
    {
        var ctx = SortKeyWithNoResolvablePkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*No DynamoDB partition key is configured*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SamePropertyConfiguredAsPartitionAndSortKey_ThrowsTargetedError()
    {
        var ctx = SamePropertyPartitionAndSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*property 'Id'*both the DynamoDB partition key and sort key*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_WithSamePropertyConfiguredAsPartitionAndSortKey_ThrowsTargetedError()
    {
        var ctx = ExplicitHasKeySamePropertyPartitionAndSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*property 'Id'*both the DynamoDB partition key and sort key*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_WithSortKeyMatchingImplicitPartitionKey_ThrowsTargetedError()
    {
        var ctx = ExplicitHasKeySortKeyMatchesImplicitPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*property 'Id'*both the DynamoDB partition key and sort key*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ExplicitHasKey_WithReversedPartitionAndSortKeyOrder_ThrowsTargetedError()
    {
        var ctx = ReversedKeyOrderContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key 'TenantId'*EF primary key starts with 'OrderId'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_ShadowProperty_DoesNotThrow()
    {
        var ctx = ShadowPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKeyAndSortKey_ShadowProperties_DoNotThrow()
    {
        var ctx = ShadowPartitionAndSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasKey_ShadowProperties_DoNotThrow()
    {
        var ctx = ShadowHasKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasKey_ShadowBoolKey_ThrowsUnsupportedTypeError()
    {
        var ctx = ShadowBoolKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key*must be string, number, or binary*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasKey_NullableShadowKey_IsMadeRequiredByEfAndValid()
    {
        var ctx = NullableShadowKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void RuntimeOnlyProperty_ConfiguredAsKey_ThrowsRuntimeOnlyError()
    {
        var ctx = RuntimeOnlyKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*runtime-only provider metadata*cannot be used as a table key*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_KeyProperties_WithMatchingAttributeNames_DoesNotThrow()
    {
        var ctx = SharedTableShadowKeyConsistentContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_KeyProperties_WithConflictingPartitionAttributeNames_Throws()
    {
        var ctx = SharedTableShadowKeyConflictingPkContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute names*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_BoolType_ThrowsOnValidation()
    {
        var ctx = BoolPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key*must be string, number, or binary*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasSortKey_BoolType_ThrowsOnValidation()
    {
        var ctx = BoolSortKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sort key*must be string, number, or binary*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_GuidWithoutConverter_DoesNotThrow()
    {
        var ctx = GuidPartitionKeyWithoutConverterContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_GuidWithStringConverter_DoesNotThrow()
    {
        var ctx = GuidPartitionKeyWithConverterContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_DateTimeOffsetWithoutConverter_DoesNotThrow()
    {
        var ctx = DateTimeOffsetPartitionKeyWithoutConverterContext.Create(MockClient());
        var act = () => ctx.Model;
        act.Should().NotThrow();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void HasPartitionKey_ConverterWithNullableProviderType_ThrowsOnValidation()
    {
        var ctx = NullableProviderPartitionKeyContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*effective provider type 'int?' is nullable*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SharedTable_PartitionKeyTypeCategoryMismatch_Throws()
    {
        var ctx = SharedTablePartitionTypeMismatchContext.Create(MockClient());
        var act = () => ctx.Model;
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*partition key attribute 'PK'*different key type categories*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK1");
            });
            modelBuilder.Entity<EntityB>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK2");
            });
        }

        public static ConflictingPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingPkContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable2");
                b.HasPartitionKey(x => x.Id);
                // Map to "PK" so both entities share the same partition key attribute name
                b.Property(x => x.Id).HasAttributeName("PK");
                // single-part key → no sort key detected
            });
            modelBuilder.Entity<EntityD>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable2");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Map first PK property to "PK" so partition key names match
                b.Property(x => x.PartId).HasAttributeName("PK");
                // two-part key → sort key "SortId" detected
            });
        }

        public static MixedSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<MixedSkContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable3");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Both share same PK attribute name; differ on SK
                b.Property(x => x.SortId).HasAttributeName("SK1");
            });
            modelBuilder.Entity<EntityF>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable3");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.SortId).HasAttributeName("SK2");
            });
        }

        public static ConflictingSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConflictingSkContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable4");
                b.HasPartitionKey(x => x.Id);
                // Both auto-detect "Id" as the PK attribute name (same CLR property name)
            });
            modelBuilder.Entity<EntityH>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable4");
                b.HasPartitionKey(x => x.Id);
            });
        }

        public static ConsistentPkOnlyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkOnlyContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable5");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                // Both auto-detect "PartId"/"SortId" as PK/SK attribute names
            });
            modelBuilder.Entity<EntityJ>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SharedTable5");
                b.HasKey(x => new { x.PartId, x.SortId });
            });
        }

        public static ConsistentPkSkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ConsistentPkSkContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "TableAlpha");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("MY_PK");
            });
            modelBuilder.Entity<EntityL>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "TableBeta");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("HASH");
                b.Property(x => x.SortId).HasAttributeName("RANGE");
            });
        }

        public static DifferentTablesContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<DifferentTablesContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "GhostPkTable");
                b.HasPartitionKey("Ghost");
            });

        public static GhostPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostPartitionKeyContext>(client));
    }

    private sealed class GhostSortKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GhostPropEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GhostPropEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "GhostSkTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey("Ghost");
            });

        public static GhostSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GhostSortKeyContext>(client));
    }

    private sealed record KeyAttributeConflictEntity
    {
        [Key]
        public string Id { get; set; } = null!;

        public string OtherId { get; set; } = null!;
    }

    private sealed class KeyAttributeConflictContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<KeyAttributeConflictEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyAttributeConflictEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "KeyAttributeConflictTable");
                b.HasPartitionKey(x => x.OtherId);
            });

        public static KeyAttributeConflictContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<KeyAttributeConflictContext>(client));
    }

    [PrimaryKey(nameof(A), nameof(B), nameof(C))]
    private sealed record ThreePartPrimaryKeyAttributeEntity
    {
        public string A { get; set; } = null!;

        public string B { get; set; } = null!;

        public string C { get; set; } = null!;
    }

    private sealed class ThreePartPrimaryKeyAttributeContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<ThreePartPrimaryKeyAttributeEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ThreePartPrimaryKeyAttributeEntity>(b
                => DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ThreePartPrimaryKeyAttributeTable"));

        public static ThreePartPrimaryKeyAttributeContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ThreePartPrimaryKeyAttributeContext>(client));
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
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "MismatchPkTable");
                b.HasKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasPartitionKey(x => x.SomeProp);
            });

        public static PartitionKeyNotInEfKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<PartitionKeyNotInEfKeyContext>(client));
    }

    private sealed class SortKeyNotInEfKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "MismatchSkTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                // SomeProp exists but is NOT in the EF primary key
                b.HasSortKey(x => x.SomeProp);
            });

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
        public string HashAttr { get; set; } = null!;

        public string RangeAttr { get; set; } = null!;
    }

    private sealed class SortKeyWithNoResolvablePkContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<NoDiscoverablePkEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NoDiscoverablePkEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SortKeyOnlyTable");
                // HasSortKey set, but no HasPartitionKey and no auto-discoverable PK property.
                b.HasSortKey(x => x.RangeAttr);
            });

        public static SortKeyWithNoResolvablePkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SortKeyWithNoResolvablePkContext>(client));
    }

    private sealed class SamePropertyPartitionAndSortKeyContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "SamePkSkTable");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.Id);
            });

        public static SamePropertyPartitionAndSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SamePropertyPartitionAndSortKeyContext>(client));
    }

    private sealed class ExplicitHasKeySamePropertyPartitionAndSortKeyContext(
        DbContextOptions options) : DbContext(options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ExplicitSamePkSkTable");
                b.HasKey(x => x.Id);
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.Id);
            });

        public static ExplicitHasKeySamePropertyPartitionAndSortKeyContext Create(
            IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitHasKeySamePropertyPartitionAndSortKeyContext>(client));
    }

    private sealed class ExplicitHasKeySortKeyMatchesImplicitPartitionKeyContext(
        DbContextOptions options) : DbContext(options)
    {
        public DbSet<KeyMismatchEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<KeyMismatchEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ImplicitPartitionSameSortKeyTable");
                b.HasKey(x => x.Id);
                b.HasSortKey(x => x.Id);
            });

        public static ExplicitHasKeySortKeyMatchesImplicitPartitionKeyContext Create(
            IAmazonDynamoDB client)
            => new(BuildOptions<ExplicitHasKeySortKeyMatchesImplicitPartitionKeyContext>(client));
    }

    private sealed record ReversedKeyOrderEntity
    {
        public string TenantId { get; set; } = null!;

        public string OrderId { get; set; } = null!;
    }

    private sealed class ReversedKeyOrderContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ReversedKeyOrderEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReversedKeyOrderEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ReversedKeyOrderTable");
                b.HasKey(x => new { x.OrderId, x.TenantId });
                b.HasPartitionKey(x => x.TenantId);
                b.HasSortKey(x => x.OrderId);
            });

        public static ReversedKeyOrderContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ReversedKeyOrderContext>(client));
    }

    // -------------------------------------------------------------------
    // Shadow key properties (unsupported)
    // -------------------------------------------------------------------

    private sealed record ShadowKeyEntity;

    private sealed class ShadowPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "ShadowPkTable");
                b.Property<string>("PK");
                b.HasPartitionKey("PK");
            });

        public static ShadowPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowPartitionKeyContext>(client));
    }

    private sealed class ShadowPartitionAndSortKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "ShadowPkSkTable");
                b.Property<string>("PK");
                b.Property<string>("SK");
                b.HasPartitionKey("PK");
                b.HasSortKey("SK");
            });

        public static ShadowPartitionAndSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowPartitionAndSortKeyContext>(client));
    }

    private sealed class ShadowHasKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ShadowHasKeyTable");
                b.Property<string>("PK");
                b.Property<string>("SK");
                b.HasKey("PK", "SK");
            });

        public static ShadowHasKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowHasKeyContext>(client));
    }

    private sealed class ShadowBoolKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "ShadowBoolKeyTable");
                b.Property<bool>("PK");
                b.HasKey("PK");
            });

        public static ShadowBoolKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<ShadowBoolKeyContext>(client));
    }

    private sealed class NullableShadowKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "NullableShadowKeyTable");
                b.Property<string?>("PK");
                b.HasKey("PK");
            });

        public static NullableShadowKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<NullableShadowKeyContext>(client));
    }

    private sealed class RuntimeOnlyKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable(
                    (EntityTypeBuilder)b,
                    "RuntimeOnlyKeyTable");
                b.HasPartitionKey("__executeStatementResponse");
            });

        public static RuntimeOnlyKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<RuntimeOnlyKeyContext>(client));
    }

    private sealed record SharedShadowEntityA
    {
        public string InternalPK { get; set; } = null!;
        public string InternalSK { get; set; } = null!;
    }

    private sealed record SharedShadowEntityB
    {
        public string OtherPk { get; set; } = null!;
        public string OtherSk { get; set; } = null!;
    }

    private sealed class SharedTableShadowKeyConsistentContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<SharedShadowEntityA> EntitiesA { get; set; } = null!;

        public DbSet<SharedShadowEntityB> EntitiesB { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedShadowEntityA>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedShadowTable");
                b.Property(x => x.InternalPK).HasAttributeName("PK");
                b.Property(x => x.InternalSK).HasAttributeName("SK");
                b.HasPartitionKey("InternalPK");
                b.HasSortKey("InternalSK");
            });

            modelBuilder.Entity<SharedShadowEntityB>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedShadowTable");
                b.Property(x => x.OtherPk).HasAttributeName("PK");
                b.Property(x => x.OtherSk).HasAttributeName("SK");
                b.HasPartitionKey("OtherPk");
                b.HasSortKey("OtherSk");
            });
        }

        public static SharedTableShadowKeyConsistentContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableShadowKeyConsistentContext>(client));
    }

    private sealed class SharedTableShadowKeyConflictingPkContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<SharedShadowEntityA> EntitiesA { get; set; } = null!;

        public DbSet<SharedShadowEntityB> EntitiesB { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedShadowEntityA>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedShadowConflictTable");
                b.Property(x => x.InternalPK).HasAttributeName("PK");
                b.Property(x => x.InternalSK).HasAttributeName("SK");
                b.HasPartitionKey("InternalPK");
                b.HasSortKey("InternalSK");
            });

            modelBuilder.Entity<SharedShadowEntityB>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedShadowConflictTable");
                b.Property(x => x.OtherPk).HasAttributeName("PK2");
                b.Property(x => x.OtherSk).HasAttributeName("SK");
                b.HasPartitionKey("OtherPk");
                b.HasSortKey("OtherSk");
            });
        }

        public static SharedTableShadowKeyConflictingPkContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableShadowKeyConflictingPkContext>(client));
    }

    // -------------------------------------------------------------------
    // Key provider type validation
    // -------------------------------------------------------------------

    private sealed record BoolPartitionKeyEntity
    {
        public bool Id { get; set; }
    }

    private sealed class BoolPartitionKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<BoolPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BoolPartitionKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "BoolPkTable");
                b.HasPartitionKey(x => x.Id);
            });

        public static BoolPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<BoolPartitionKeyContext>(client));
    }

    private sealed record BoolSortKeyEntity
    {
        public string PK { get; set; } = null!;

        public bool SK { get; set; }
    }

    private sealed class BoolSortKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<BoolSortKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BoolSortKeyEntity>(b =>
            {
                DynamoEntityTypeBuilderExtensions.ToTable((EntityTypeBuilder)b, "BoolSkTable");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

        public static BoolSortKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<BoolSortKeyContext>(client));
    }

    private sealed record GuidPartitionKeyEntity
    {
        public Guid Id { get; set; }
    }

    private sealed class GuidPartitionKeyWithoutConverterContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("GuidPkNoConverter");
                b.HasPartitionKey(x => x.Id);
            });

        public static GuidPartitionKeyWithoutConverterContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidPartitionKeyWithoutConverterContext>(client));
    }

    private sealed class GuidPartitionKeyWithConverterContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<GuidPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidPartitionKeyEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("GuidPkWithConverter");
                b.HasPartitionKey(x => x.Id);
                b
                    .Property(x => x.Id)
                    .HasConversion(
                        new ValueConverter<Guid, string>(
                            static value => value.ToString("N"),
                            static value => Guid.Parse(value)));
            });

        public static GuidPartitionKeyWithConverterContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<GuidPartitionKeyWithConverterContext>(client));
    }

    private sealed record DateTimeOffsetPartitionKeyEntity
    {
        public DateTimeOffset Id { get; set; }
    }

    private sealed class DateTimeOffsetPartitionKeyWithoutConverterContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<DateTimeOffsetPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DateTimeOffsetPartitionKeyEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("DateTimeOffsetPkNoConverter");
                b.HasPartitionKey(x => x.Id);
            });

        public static DateTimeOffsetPartitionKeyWithoutConverterContext Create(
            IAmazonDynamoDB client)
            => new(BuildOptions<DateTimeOffsetPartitionKeyWithoutConverterContext>(client));
    }

    private sealed record NullableProviderPartitionKeyEntity
    {
        public int Id { get; set; }
    }

    private sealed class NullableProviderPartitionKeyContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<NullableProviderPartitionKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullableProviderPartitionKeyEntity>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("NullableProviderPkTable");
                b.HasPartitionKey(x => x.Id);
                b
                    .Property(x => x.Id)
                    .HasConversion(
                        new ValueConverter<int, int?>(
                            static value => value,
                            static value => value ?? 0));
            });

        public static NullableProviderPartitionKeyContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<NullableProviderPartitionKeyContext>(client));
    }

    // -------------------------------------------------------------------
    // Shared-table key type category consistency
    // -------------------------------------------------------------------

    private sealed record SharedPartitionTypeEntityA
    {
        public string Id { get; set; } = null!;
    }

    private sealed record SharedPartitionTypeEntityB
    {
        public int Id { get; set; }
    }

    private sealed class SharedTablePartitionTypeMismatchContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<SharedPartitionTypeEntityA> EntitiesA { get; set; } = null!;

        public DbSet<SharedPartitionTypeEntityB> EntitiesB { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedPartitionTypeEntityA>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedPartitionTypeMismatchTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });

            modelBuilder.Entity<SharedPartitionTypeEntityB>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedPartitionTypeMismatchTable");
                b.HasPartitionKey(x => x.Id);
                b.Property(x => x.Id).HasAttributeName("PK");
            });
        }

        public static SharedTablePartitionTypeMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTablePartitionTypeMismatchContext>(client));
    }

    private sealed record SharedSortTypeEntityA
    {
        public string PartId { get; set; } = null!;

        public string SortId { get; set; } = null!;
    }

    private sealed record SharedSortTypeEntityB
    {
        public string PartId { get; set; } = null!;

        public int SortId { get; set; }
    }

    private sealed class SharedTableSortTypeMismatchContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<SharedSortTypeEntityA> EntitiesA { get; set; } = null!;

        public DbSet<SharedSortTypeEntityB> EntitiesB { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedSortTypeEntityA>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedSortTypeMismatchTable");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("PK");
                b.Property(x => x.SortId).HasAttributeName("SK");
            });

            modelBuilder.Entity<SharedSortTypeEntityB>(b =>
            {
                ((EntityTypeBuilder)b).ToTable("SharedSortTypeMismatchTable");
                b.HasPartitionKey(x => x.PartId);
                b.HasSortKey(x => x.SortId);
                b.Property(x => x.PartId).HasAttributeName("PK");
                b.Property(x => x.SortId).HasAttributeName("SK");
            });
        }

        public static SharedTableSortTypeMismatchContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableSortTypeMismatchContext>(client));
    }
}
