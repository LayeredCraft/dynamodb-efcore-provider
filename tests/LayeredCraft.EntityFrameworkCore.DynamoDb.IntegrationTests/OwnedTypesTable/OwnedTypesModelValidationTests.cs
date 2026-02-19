using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

public class OwnedTypesModelValidationTests : IClassFixture<OwnedTypesTableDynamoFixture>
{
    private readonly OwnedTypesTableDynamoFixture _fixture;

    public OwnedTypesModelValidationTests(OwnedTypesTableDynamoFixture fixture)
        => _fixture = fixture;

    [Fact]
    public void OwnedTypeWithExplicitTableName_ThrowsModelValidationError()
    {
        using var context =
            new OwnedTypeWithTableNameContext(CreateOptions<OwnedTypeWithTableNameContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Owned entity type*explicit table name*cannot have separate table mappings*");
    }

    [Fact]
    public void
        OwnedContainingAttributeName_CollidingWithScalarProperty_ThrowsModelValidationError()
    {
        using var context = new OwnedNameCollidesWithPropertyContext(
            CreateOptions<OwnedNameCollidesWithPropertyContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*containing attribute name*collides with scalar property*");
    }

    [Fact]
    public void
        OwnedContainingAttributeName_CollidingWithOwnedNavigation_ThrowsModelValidationError()
    {
        using var context = new OwnedNameCollidesWithNavigationContext(
            CreateOptions<OwnedNameCollidesWithNavigationContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*containing attribute name*collides with owned navigation*");
    }

    [Fact]
    public void EmbeddedOwnedCollectionWithUnsupportedClrShape_ThrowsModelValidationError()
    {
        using var context = new UnsupportedOwnedCollectionShapeContext(
            CreateOptions<UnsupportedOwnedCollectionShapeContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Embedded owned collection navigation*Supported list shapes*");
    }

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.DynamoDbClient(_fixture.Client));
        return builder.Options;
    }

    private sealed class OwnedTypeWithTableNameContext(
        DbContextOptions<OwnedTypeWithTableNameContext> options) : DbContext(options)
    {
        public DbSet<OwnerWithSingleOwned> Items => Set<OwnerWithSingleOwned>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithSingleOwned>(entity =>
            {
                entity.ToTable(OwnedTypesTableDynamoFixture.TableName);
                entity.HasKey(x => x.Pk);
                entity.OwnsOne(
                    x => x.Profile,
                    owned =>
                    {
                        owned.OwnedEntityType.SetAnnotation(
                            DynamoAnnotationNames.TableName,
                            "OwnedTable");
                    });
            });
    }

    private sealed class OwnedNameCollidesWithPropertyContext(
        DbContextOptions<OwnedNameCollidesWithPropertyContext> options) : DbContext(options)
    {
        public DbSet<OwnerWithPropertyCollision> Items => Set<OwnerWithPropertyCollision>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithPropertyCollision>(entity =>
            {
                entity.ToTable(OwnedTypesTableDynamoFixture.TableName);
                entity.HasKey(x => x.Pk);
                entity.OwnsOne(
                    x => x.Profile,
                    owned => owned.HasAttributeName(
                        nameof(OwnerWithPropertyCollision.ProfileData)));
            });
    }

    private sealed class OwnedNameCollidesWithNavigationContext(
        DbContextOptions<OwnedNameCollidesWithNavigationContext> options) : DbContext(options)
    {
        public DbSet<OwnerWithNavigationCollision> Items => Set<OwnerWithNavigationCollision>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithNavigationCollision>(entity =>
            {
                entity.ToTable(OwnedTypesTableDynamoFixture.TableName);
                entity.HasKey(x => x.Pk);
                entity.OwnsOne(x => x.PrimaryProfile, owned => owned.HasAttributeName("Profile"));
                entity.OwnsOne(x => x.SecondaryProfile, owned => owned.HasAttributeName("Profile"));
            });
    }

    private sealed class OwnerWithSingleOwned
    {
        public string Pk { get; set; } = string.Empty;

        public Profile Profile { get; set; } = new();
    }

    private sealed class OwnerWithPropertyCollision
    {
        public string Pk { get; set; } = string.Empty;

        public string ProfileData { get; set; } = string.Empty;

        public Profile Profile { get; set; } = new();
    }

    private sealed class OwnerWithNavigationCollision
    {
        public string Pk { get; set; } = string.Empty;

        public Profile PrimaryProfile { get; set; } = new();

        public Profile SecondaryProfile { get; set; } = new();
    }

    private sealed class UnsupportedOwnedCollectionShapeContext(
        DbContextOptions<UnsupportedOwnedCollectionShapeContext> options) : DbContext(options)
    {
        public DbSet<OwnerWithUnsupportedCollectionShape> Items
            => Set<OwnerWithUnsupportedCollectionShape>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithUnsupportedCollectionShape>(entity =>
            {
                entity.ToTable(OwnedTypesTableDynamoFixture.TableName);
                entity.HasKey(x => x.Pk);
                entity.OwnsMany(x => x.Profiles);
            });
    }

    private sealed class OwnerWithUnsupportedCollectionShape
    {
        public string Pk { get; set; } = string.Empty;

        public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
    }

    private sealed class Profile
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
