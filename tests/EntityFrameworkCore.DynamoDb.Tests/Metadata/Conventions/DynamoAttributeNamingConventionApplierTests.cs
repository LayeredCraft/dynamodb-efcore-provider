using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Tests for <c>DynamoAttributeNamingConventionApplier</c> — verifies that per-entity naming
///     conventions transform CLR property names to DynamoDB attribute names correctly, that explicit
///     <c>HasAttributeName</c> overrides are preserved, and that shadow properties and owned type
///     inheritance behave correctly.
/// </summary>
public class DynamoAttributeNamingConventionApplierTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    // -------------------------------------------------------------------
    // Shared entity shape
    // -------------------------------------------------------------------

    private sealed record SampleEntity
    {
        public string Pk { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public int ItemCount { get; set; }
        public bool IsActive { get; set; }
    }

    // -------------------------------------------------------------------
    // SnakeCase
    // -------------------------------------------------------------------

    private sealed class SnakeCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
            });
    }

    [Fact]
    public void SnakeCase_TransformsAllDeclaredProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new SnakeCaseContext(BuildOptions<SnakeCaseContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.Pk))!.GetAttributeName().Should().Be("pk");
        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("first_name");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("item_count");
        entityType.FindProperty(nameof(SampleEntity.IsActive))!
            .GetAttributeName()
            .Should()
            .Be("is_active");
    }

    // -------------------------------------------------------------------
    // CamelCase
    // -------------------------------------------------------------------

    private sealed class CamelCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention();
            });
    }

    [Fact]
    public void CamelCase_TransformsAllDeclaredProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new CamelCaseContext(BuildOptions<CamelCaseContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.Pk))!.GetAttributeName().Should().Be("pk");
        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("firstName");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("itemCount");
        entityType.FindProperty(nameof(SampleEntity.IsActive))!
            .GetAttributeName()
            .Should()
            .Be("isActive");
    }

    // -------------------------------------------------------------------
    // KebabCase
    // -------------------------------------------------------------------

    private sealed class KebabCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.KebabCase);
            });
    }

    [Fact]
    public void KebabCase_TransformsAllDeclaredProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new KebabCaseContext(BuildOptions<KebabCaseContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("first-name");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("item-count");
        entityType.FindProperty(nameof(SampleEntity.IsActive))!
            .GetAttributeName()
            .Should()
            .Be("is-active");
    }

    // -------------------------------------------------------------------
    // UpperSnakeCase
    // -------------------------------------------------------------------

    private sealed class UpperSnakeCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.UpperSnakeCase);
            });
    }

    [Fact]
    public void UpperSnakeCase_TransformsAllDeclaredProperties()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new UpperSnakeCaseContext(BuildOptions<UpperSnakeCaseContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("FIRST_NAME");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("ITEM_COUNT");
        entityType.FindProperty(nameof(SampleEntity.IsActive))!
            .GetAttributeName()
            .Should()
            .Be("IS_ACTIVE");
    }

    // -------------------------------------------------------------------
    // Custom delegate
    // -------------------------------------------------------------------

    private sealed class CustomDelegateContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                // Prefix every attribute name with "x_"
                b.HasAttributeNamingConvention(name => "x_" + name.ToLowerInvariant());
            });
    }

    [Fact]
    public void CustomDelegate_AppliesTransformation()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new CustomDelegateContext(BuildOptions<CustomDelegateContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("x_firstname");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("x_itemcount");
    }

    // -------------------------------------------------------------------
    // HasAttributeName explicit override wins
    // -------------------------------------------------------------------

    private sealed record EntityWithOverride
    {
        public string Pk { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public int Score { get; set; }
    }

    private sealed class ExplicitOverrideContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityWithOverride> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EntityWithOverride>(b =>
            {
                b.ToTable("Overrides");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                // Explicit override — must survive convention
                b.Property(x => x.DisplayName).HasAttributeName("label");
            });
    }

    [Fact]
    public void ExplicitHasAttributeName_WinsOverConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new ExplicitOverrideContext(BuildOptions<ExplicitOverrideContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(EntityWithOverride))!;

        // Explicit name wins
        entityType.FindProperty(nameof(EntityWithOverride.DisplayName))!
            .GetAttributeName()
            .Should()
            .Be("label");

        // Convention still applies to non-overridden properties
        entityType.FindProperty(nameof(EntityWithOverride.Score))!
            .GetAttributeName()
            .Should()
            .Be("score");
    }

    // -------------------------------------------------------------------
    // No convention — CLR name unchanged
    // -------------------------------------------------------------------

    private sealed class NoConventionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                // No HasAttributeNamingConvention call
            });
    }

    [Fact]
    public void NoConvention_PropertyNameUnchanged()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new NoConventionContext(BuildOptions<NoConventionContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("FirstName");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("ItemCount");
    }

    // -------------------------------------------------------------------
    // None — explicit opt-out, CLR name unchanged
    // -------------------------------------------------------------------

    private sealed class NoneConventionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SampleEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SampleEntity>(b =>
            {
                b.ToTable("Samples");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.None);
            });
    }

    [Fact]
    public void None_PropertyNameUnchanged()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new NoneConventionContext(BuildOptions<NoneConventionContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(SampleEntity))!;

        entityType.FindProperty(nameof(SampleEntity.FirstName))!
            .GetAttributeName()
            .Should()
            .Be("FirstName");
        entityType.FindProperty(nameof(SampleEntity.ItemCount))!
            .GetAttributeName()
            .Should()
            .Be("ItemCount");
        entityType.FindProperty(nameof(SampleEntity.IsActive))!
            .GetAttributeName()
            .Should()
            .Be("IsActive");
    }

    // -------------------------------------------------------------------
    // Owned type inherits root entity convention
    // -------------------------------------------------------------------

    private sealed record Address
    {
        public string Street { get; set; } = null!;
        public string CityName { get; set; } = null!;
    }

    private sealed record PersonEntity
    {
        public string Pk { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public Address HomeAddress { get; set; } = null!;
    }

    private sealed class OwnedInheritContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PersonEntity> People { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PersonEntity>(b =>
            {
                b.ToTable("People");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.OwnsOne(x => x.HomeAddress);
            });
    }

    [Fact]
    public void OwnedType_InheritsRootEntityConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new OwnedInheritContext(BuildOptions<OwnedInheritContext>(client));

        // Root entity properties get snake_case
        var personType = ctx.Model.FindEntityType(typeof(PersonEntity))!;
        personType.FindProperty(nameof(PersonEntity.FullName))!
            .GetAttributeName()
            .Should()
            .Be("full_name");

        // Owned entity properties also get snake_case from root
        var addressType = ctx.Model.FindEntityType(typeof(Address))!;
        addressType.FindProperty(nameof(Address.Street))!.GetAttributeName().Should().Be("street");
        addressType.FindProperty(nameof(Address.CityName))!
            .GetAttributeName()
            .Should()
            .Be("city_name");
    }

    // -------------------------------------------------------------------
    // Owned type own convention overrides root
    // -------------------------------------------------------------------

    private sealed class OwnedOwnConventionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PersonEntity> People { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PersonEntity>(b =>
            {
                b.ToTable("People");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.OwnsOne(
                    x => x.HomeAddress,
                    ab =>
                        // Owned type has its own convention — should override root
                        ab.HasAttributeNamingConvention());
            });
    }

    [Fact]
    public void OwnedType_OwnConventionOverridesRoot()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx =
            new OwnedOwnConventionContext(BuildOptions<OwnedOwnConventionContext>(client));

        // Root still snake_case
        var personType = ctx.Model.FindEntityType(typeof(PersonEntity))!;
        personType.FindProperty(nameof(PersonEntity.FullName))!
            .GetAttributeName()
            .Should()
            .Be("full_name");

        // Owned type uses its own camelCase, not root's snake_case
        var addressType = ctx.Model.FindEntityType(typeof(Address))!;
        addressType.FindProperty(nameof(Address.CityName))!
            .GetAttributeName()
            .Should()
            .Be("cityName");
    }

    // -------------------------------------------------------------------
    // Shadow properties are skipped
    // -------------------------------------------------------------------

    private sealed record OwnedItem
    {
        public string Label { get; set; } = null!;
    }

    private sealed record CollectionOwner
    {
        public string Pk { get; set; } = null!;
        public List<OwnedItem> Items { get; set; } = null!;
    }

    private sealed class ShadowPropertyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<CollectionOwner> Owners { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CollectionOwner>(b =>
            {
                b.ToTable("Owners");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.OwnsMany(x => x.Items);
            });
    }

    [Fact]
    public void ShadowProperty_IsSkipped_ByNamingConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new ShadowPropertyContext(BuildOptions<ShadowPropertyContext>(client));

        var ownedType = ctx.Model.FindEntityType(typeof(OwnedItem))!;

        // CLR property gets snake_case applied
        ownedType.FindProperty(nameof(OwnedItem.Label))!.GetAttributeName().Should().Be("label");

        // The shadow ordinal key (__OwnedOrdinal) must not be renamed — it stays as-is
        var shadowProps = ownedType.GetProperties().Where(p => p.IsShadowProperty()).ToList();
        foreach (var shadow in shadowProps)
            // Shadow property attribute name should equal its original (untransformed) name
            shadow.GetAttributeName().Should().Be(shadow.Name);
    }
}
