using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>Validates that the DynamoDB provider rejects owned entity types and unsupported complex collection shapes.</summary>
public class OwnedTypesModelValidationTests
{
    private readonly DynamoContainerFixture _fixture;

    public OwnedTypesModelValidationTests(DynamoContainerFixture fixture) => _fixture = fixture;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void OwnedEntityType_ThrowsNotSupported()
    {
        using var context = new OwnedTypeContext(CreateOptions<OwnedTypeContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*not supported by the DynamoDB provider*Use EF Core complex types*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ComplexCollectionWithUnsupportedClrShape_ThrowsModelValidationError()
    {
        using var context = new UnsupportedComplexCollectionShapeContext(
            CreateOptions<UnsupportedComplexCollectionShapeContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Complex collection property*unsupported CLR type*");
    }

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.DynamoDbClient(_fixture.Client));
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder.Options;
    }

    private sealed class OwnedTypeContext(DbContextOptions<OwnedTypeContext> options) : DbContext(
        options)
    {
        public DbSet<OwnerWithOwnedProfile> Items => Set<OwnerWithOwnedProfile>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithOwnedProfile>(entity =>
            {
                entity.ToTable(OwnedTypesItemTable.TableName);
                entity.HasPartitionKey(x => x.Pk);
                entity.OwnsOne(x => x.Profile);
            });
    }

    private sealed class UnsupportedComplexCollectionShapeContext(
        DbContextOptions<UnsupportedComplexCollectionShapeContext> options) : DbContext(options)
    {
        public DbSet<OwnerWithUnsupportedCollectionShape> Items
            => Set<OwnerWithUnsupportedCollectionShape>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<OwnerWithUnsupportedCollectionShape>(entity =>
            {
                entity.ToTable(OwnedTypesItemTable.TableName);
                entity.HasPartitionKey(x => x.Pk);
                entity.ComplexCollection(x => x.Profiles);
            });
    }

    private sealed class OwnerWithOwnedProfile
    {
        public string Pk { get; set; } = string.Empty;

        public NonComplexProfile Profile { get; set; } = new();
    }

    private sealed class OwnerWithUnsupportedCollectionShape
    {
        public string Pk { get; set; } = string.Empty;

        public ICollection<ComplexProfile> Profiles { get; set; } = new List<ComplexProfile>();
    }

    /// <summary>Plain class — not marked [ComplexType], used with OwnsOne to verify owned-type rejection.</summary>
    private sealed class NonComplexProfile
    {
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>Complex type — used in collection shape validation test.</summary>
    [ComplexType]
    private sealed class ComplexProfile
    {
        public string DisplayName { get; set; } = string.Empty;
    }
}
