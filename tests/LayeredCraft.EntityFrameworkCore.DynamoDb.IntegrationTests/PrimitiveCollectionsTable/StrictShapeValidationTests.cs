using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class StrictShapeValidationTests : IClassFixture<PrimitiveCollectionsDynamoFixture>
{
    private readonly PrimitiveCollectionsDynamoFixture _fixture;

    public StrictShapeValidationTests(PrimitiveCollectionsDynamoFixture fixture)
        => _fixture = fixture;

    [Fact]
    public void DerivedDictionaryPrimitiveCollectionType_ThrowsModelValidationError()
    {
        using var context = new DerivedDictionaryContext(CreateOptions<DerivedDictionaryContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*DerivedDictionaryItem.Scores*database provider does not support this type*");
    }

    [Fact]
    public void DerivedSetPrimitiveCollectionType_ThrowsModelValidationError()
    {
        using var context = new DerivedSetContext(CreateOptions<DerivedSetContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DerivedSetItem.Labels*database provider does not support this type*");
    }

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseDynamo(options => options.DynamoDbClient(_fixture.Client));
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
                entity.HasKey(x => x.Pk);
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
                entity.HasKey(x => x.Pk);
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
