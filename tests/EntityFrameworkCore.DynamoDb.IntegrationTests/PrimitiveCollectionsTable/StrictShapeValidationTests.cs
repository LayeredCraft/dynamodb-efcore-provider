using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

/// <summary>Represents the StrictShapeValidationTests type.</summary>
public class StrictShapeValidationTests
{
    private readonly DynamoContainerFixture _fixture;

    public StrictShapeValidationTests(DynamoContainerFixture fixture) => _fixture = fixture;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DerivedDictionaryPrimitiveCollectionType_ThrowsModelValidationError()
    {
        using var context = new DerivedDictionaryContext(CreateOptions<DerivedDictionaryContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DerivedDictionaryItem.Scores*DynamoDB does not support this type*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
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
        builder.ConfigureWarnings(w
            => w
                .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return builder.Options;
    }

    private sealed class DerivedDictionaryContext(
        DbContextOptions<DerivedDictionaryContext> options) : DbContext(options)
    {
        public DbSet<DerivedDictionaryItem> Items => Set<DerivedDictionaryItem>();

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
        public DbSet<DerivedSetItem> Items => Set<DerivedSetItem>();

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
        public string Pk { get; set; } = default!;

        public CustomDictionary Scores { get; set; } = new();
    }

    private sealed class DerivedSetItem
    {
        public string Pk { get; set; } = default!;

        public SortedSet<string> Labels { get; set; } = [];
    }

    private sealed class CustomDictionary : Dictionary<string, int>;
}
