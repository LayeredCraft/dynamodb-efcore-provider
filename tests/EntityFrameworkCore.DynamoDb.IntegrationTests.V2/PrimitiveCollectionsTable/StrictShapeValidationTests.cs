using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PrimitiveCollectionsTable;

/// <summary>Represents the StrictShapeValidationTests type.</summary>
public class StrictShapeValidationTests : IClassFixture<DynamoContainerFixture>
{
    private readonly DynamoContainerFixture _fixture;

    /// <summary>Provides functionality for this member.</summary>
    public StrictShapeValidationTests(DynamoContainerFixture fixture) => _fixture = fixture;

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DerivedDictionaryPrimitiveCollectionType_ThrowsModelValidationError()
    {
        using var context = new DerivedDictionaryContext(CreateOptions<DerivedDictionaryContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DerivedDictionaryItem.Scores*DynamoDB does not support this type*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DerivedSetPrimitiveCollectionType_ThrowsModelValidationError()
    {
        using var context = new DerivedSetContext(CreateOptions<DerivedSetContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DerivedSetItem.Labels*DynamoDB does not support this type*");
    }

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.DynamoDbClient(_fixture.Client));
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        return builder.Options;
    }

    private sealed class DerivedDictionaryContext(
        DbContextOptions<DerivedDictionaryContext> options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<DerivedDictionaryItem> Items => Set<DerivedDictionaryItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DerivedDictionaryItem>(entity =>
            {
                entity.ToTable("DerivedDictionaryItems");
                entity.HasPartitionKey(x => x.Pk);
                entity.Property(x => x.Scores);
            });
    }

    private sealed class DerivedSetContext(DbContextOptions<DerivedSetContext> options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<DerivedSetItem> Items => Set<DerivedSetItem>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<DerivedSetItem>(entity =>
            {
                entity.ToTable("DerivedSetItems");
                entity.HasPartitionKey(x => x.Pk);
                entity.Property(x => x.Labels);
            });
    }

    private sealed class DerivedDictionaryItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = default!;

        /// <summary>Provides functionality for this member.</summary>
        public CustomDictionary Scores { get; set; } = new();
    }

    private sealed class DerivedSetItem
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = default!;

        /// <summary>Provides functionality for this member.</summary>
        public SortedSet<string> Labels { get; set; } = [];
    }

    private sealed class CustomDictionary : Dictionary<string, int>;
}
