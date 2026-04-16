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
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.CamelCase);
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
    // No convention configured — provider default (camelCase)
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
    public void NoConvention_DefaultsToCamelCase()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new NoConventionContext(BuildOptions<NoConventionContext>(client));
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
        addressType.GetContainingAttributeName().Should().Be("home_address");
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
                        ab.HasAttributeNamingConvention(DynamoAttributeNamingConvention.CamelCase));
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
        addressType.GetContainingAttributeName().Should().Be("homeAddress");
    }

    private sealed class OwnedContainingAttributeOverrideContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<PersonEntity> People { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<PersonEntity>(b =>
            {
                b.ToTable("People");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.OwnsOne(x => x.HomeAddress, ab => ab.HasAttributeName("home_payload"));
            });
    }

    [Fact]
    public void OwnedType_ContainingAttribute_ExplicitOverrideWinsOverConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx =
            new OwnedContainingAttributeOverrideContext(
                BuildOptions<OwnedContainingAttributeOverrideContext>(client));

        var addressType = ctx.Model.FindEntityType(typeof(Address))!;
        addressType.GetContainingAttributeName().Should().Be("home_payload");
    }

    // -------------------------------------------------------------------
    // Provider-internal shadow properties are skipped
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
    public void ProviderInternalShadowProperty_IsSkipped_ByNamingConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new ShadowPropertyContext(BuildOptions<ShadowPropertyContext>(client));

        var ownedType = ctx.Model.FindEntityType(typeof(OwnedItem))!;

        // CLR property gets snake_case applied
        ownedType.FindProperty(nameof(OwnedItem.Label))!.GetAttributeName().Should().Be("label");

        // Provider-internal owned ordinal key must not be renamed — it stays as-is
        var ownedOrdinalProperty =
            ownedType.GetProperties().Single(p => p.IsOwnedOrdinalKeyProperty());
        ownedOrdinalProperty.GetAttributeName().Should().Be(ownedOrdinalProperty.Name);
    }

    private sealed record UserShadowEntity
    {
        public string Pk { get; set; } = null!;
    }

    private sealed class UserShadowPropertyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UserShadowEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UserShadowEntity>(b =>
            {
                b.ToTable("UserShadow");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.Property<string>("ShadowValue");
            });
    }

    [Fact]
    public void UserShadowProperty_GetsNamingConventionTranslation()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx =
            new UserShadowPropertyContext(BuildOptions<UserShadowPropertyContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(UserShadowEntity))!;

        entityType.FindProperty("ShadowValue")!.GetAttributeName().Should().Be("shadow_value");
    }

    private sealed class UserShadowPropertyOverrideContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<UserShadowEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UserShadowEntity>(b =>
            {
                b.ToTable("UserShadow");
                b.HasPartitionKey(x => x.Pk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
                b.Property<string>("ShadowValue").HasAttributeName("shadow");
            });
    }

    [Fact]
    public void UserShadowProperty_ExplicitOverride_WinsOverConvention()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx =
            new UserShadowPropertyOverrideContext(
                BuildOptions<UserShadowPropertyOverrideContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(UserShadowEntity))!;

        entityType.FindProperty("ShadowValue")!.GetAttributeName().Should().Be("shadow");
    }

    private sealed record AcronymEntity
    {
        public string PK { get; set; } = null!;
        public string SK { get; set; } = null!;
        public string URLValue { get; set; } = null!;
    }

    private sealed class AcronymSnakeCaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<AcronymEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AcronymEntity>(b =>
            {
                b.ToTable("Acronyms");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
            });
    }

    [Fact]
    public void Acronyms_UseHumanizerTranslationBehavior()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new AcronymSnakeCaseContext(BuildOptions<AcronymSnakeCaseContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(AcronymEntity))!;

        entityType.FindProperty(nameof(AcronymEntity.PK))!.GetAttributeName().Should().Be("pk");
        entityType.FindProperty(nameof(AcronymEntity.SK))!.GetAttributeName().Should().Be("sk");
        entityType.FindProperty(nameof(AcronymEntity.URLValue))!
            .GetAttributeName()
            .Should()
            .Be("url_value");
    }

    private sealed class ReadOnlyKeyEntity
    {
        public string Pk { get; } = string.Empty;

        public string Sk { get; } = string.Empty;

        public string Value { get; set; } = null!;
    }

    private sealed class ReadOnlyKeyEntityContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ReadOnlyKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReadOnlyKeyEntity>(b =>
            {
                b.ToTable("ReadOnlyKeyEntity");
                b.HasPartitionKey(x => x.Pk);
                b.HasSortKey(x => x.Sk);
                b.HasAttributeNamingConvention(DynamoAttributeNamingConvention.SnakeCase);
            });
    }

    [Fact]
    public void HasPartitionKeyAndSortKey_LambdaOverloads_MapReadOnlyMembers()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx =
            new ReadOnlyKeyEntityContext(BuildOptions<ReadOnlyKeyEntityContext>(client));
        var entityType = ctx.Model.FindEntityType(typeof(ReadOnlyKeyEntity))!;

        entityType.FindProperty(nameof(ReadOnlyKeyEntity.Pk))!.GetAttributeName().Should().Be("pk");
        entityType.FindProperty(nameof(ReadOnlyKeyEntity.Sk))!.GetAttributeName().Should().Be("sk");
    }
}
