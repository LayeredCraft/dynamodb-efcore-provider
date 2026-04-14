using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>
///     Validates that the model validator throws early, actionable errors when properties have
///     CLR types that cannot be serialized to DynamoDB wire format.
/// </summary>
public class DynamoModelValidatorTypeMappingTests
{
    [Fact]
    public void Validate_ThrowsWithHelpfulMessage_WhenExplicitScalarPropertyHasUnmappedClrType()
    {
        // CustomPayload has no DynamoDB mapping and no EF Core built-in auto-converter.
        // When explicitly included via Property(), EF includes the property but the provider
        // cannot produce a type mapping → our ThrowPropertyNotMappedException override fires.
        using var context = new ExplicitUnmappedScalarContext();

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Metadata*CustomPayload*")
            .And
            .Message
            .Should()
            .ContainAny("HasConversion", "value converter");
    }

    [Fact]
    public void Validate_Succeeds_WhenExplicitPrimitiveCollectionHasMappedElementType()
    {
        // Guid is supported via string wire representation, so Guid[] configured as a primitive
        // collection should validate successfully.
        using var context = new ExplicitUnmappedCollectionContext();

        var act = () => _ = context.Model;

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Succeeds_WhenScalarPropertyHasConverter()
    {
        // Guid with HasConversion<string>() — provider type is string (mappable) → passes.
        using var context = new ConvertedGuidContext();

        var act = () => _ = context.Model;

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_Throws_WhenRowVersionConfigured()
    {
        using var context = new RowVersionConcurrencyContext();

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*IsRowVersion()*not currently support*application code*");
    }

    [Fact]
    public void Validate_Succeeds_WhenManualConcurrencyTokenConfigured()
    {
        using var context = new ManualConcurrencyTokenContext();

        var act = () => _ = context.Model;

        act.Should().NotThrow();
    }

    // --- Invalid: explicit scalar property with unmappable CLR type ---

    private sealed class ExplicitUnmappedScalarContext : DbContext
    {
        public DbSet<ExplicitUnmappedScalarEntity> Items => Set<ExplicitUnmappedScalarEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitUnmappedScalarEntity>(builder =>
            {
                builder.ToTable("Items");
                builder.HasPartitionKey(x => x.Pk);
                // Explicitly include the property so EF adds it to the model even though
                // the DynamoDB source cannot produce a type mapping for CustomPayload.
                builder.Property(x => x.Metadata);
            });
    }

    private sealed class ExplicitUnmappedScalarEntity
    {
        public string Pk { get; set; } = null!;

        /// <summary>
        ///     A custom application type with no DynamoDB mapping and no EF Core auto-converter —
        ///     requires HasConversion to be usable as a property.
        /// </summary>
        public CustomPayload Metadata { get; set; } = null!;
    }

    /// <summary>Opaque custom type with no EF Core built-in value converter.</summary>
    private sealed class CustomPayload { }

    // --- Invalid: explicit primitive collection with unmappable element type ---

    private sealed class ExplicitUnmappedCollectionContext : DbContext
    {
        public DbSet<ExplicitUnmappedCollectionEntity> Items
            => Set<ExplicitUnmappedCollectionEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExplicitUnmappedCollectionEntity>(builder =>
            {
                builder.ToTable("Items");
                builder.HasPartitionKey(x => x.Pk);
                // Explicitly configure as primitive collection so EF includes it in the model.
                builder.PrimitiveCollection(x => x.Tags);
            });
    }

    private sealed class ExplicitUnmappedCollectionEntity
    {
        public string Pk { get; set; } = null!;

        /// <summary>Guid[] maps via Guid string wire representation.</summary>
        public Guid[] Tags { get; set; } = [];
    }

    // --- Valid: converter present ---

    private sealed class ConvertedGuidContext : DbContext
    {
        public DbSet<ConvertedGuidEntity> Items => Set<ConvertedGuidEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedGuidEntity>(builder =>
            {
                builder.ToTable("Items");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.Identifier).HasConversion<string>();
            });
    }

    private sealed class ConvertedGuidEntity
    {
        public string Pk { get; set; } = null!;

        public Guid Identifier { get; set; }
    }

    // --- Concurrency-token validation ---

    private sealed class RowVersionConcurrencyContext : DbContext
    {
        public DbSet<RowVersionConcurrencyEntity> Items => Set<RowVersionConcurrencyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RowVersionConcurrencyEntity>(builder =>
            {
                builder.ToTable("Items");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.Token).IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
            });
    }

    private sealed class RowVersionConcurrencyEntity
    {
        public string Pk { get; set; } = null!;

        public long Token { get; set; }
    }

    private sealed class ManualConcurrencyTokenContext : DbContext
    {
        public DbSet<ManualConcurrencyTokenEntity> Items => Set<ManualConcurrencyTokenEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ManualConcurrencyTokenEntity>(builder =>
            {
                builder.ToTable("Items");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.Token).IsConcurrencyToken().ValueGeneratedNever();
            });
    }

    private sealed class ManualConcurrencyTokenEntity
    {
        public string Pk { get; set; } = null!;

        public long Token { get; set; }
    }
}
