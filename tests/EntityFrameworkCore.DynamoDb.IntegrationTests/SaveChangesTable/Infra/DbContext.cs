using System.Globalization;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the SaveChangesTableDbContext type.</summary>
public class SaveChangesTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<CustomerItem> Customers => Set<CustomerItem>();

    public DbSet<OrderItem> Orders => Set<OrderItem>();

    public DbSet<ProductItem> Products => Set<ProductItem>();

    public DbSet<SessionItem> Sessions => Set<SessionItem>();

    public DbSet<ConverterCoverageItem> ConverterCoverageItems => Set<ConverterCoverageItem>();

    public DbSet<CustomConverterItem> CustomConverterItems => Set<CustomConverterItem>();

    public DbSet<ConvertedCollectionItem> ConvertedCollectionItems
        => Set<ConvertedCollectionItem>();

    public DbSet<QuotedAttributeItem> QuotedAttributeItems => Set<QuotedAttributeItem>();

    public DbSet<SparseGsiItem> SparseGsiItems => Set<SparseGsiItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SaveChangesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SaveChangesTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>Configures the shared-table SaveChanges model used by future CRUD tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.ComplexProperty(
                x => x.Shipping,
                shipping =>
                {
                    shipping.ComplexProperty(x => x.Address);
                    shipping.ComplexProperty(x => x.DeliveryWindow);
                });

            builder.ComplexProperty(
                x => x.Billing,
                billing => billing.ComplexProperty(x => x.Address));

            builder.ComplexCollection(x => x.Lines);
        });

        modelBuilder.Entity<ProductItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<SessionItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<ConverterCoverageItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
        });

        // CustomConverterItem uses a user-defined ProductCode type with a custom converter to
        // exercise the boxed scalar fallback write path.
        modelBuilder.Entity<CustomConverterItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Code).HasConversion<ProductCodeConverter>();
            builder.Property(x => x.OptionalCode).HasConversion<ProductCodeConverter>();
        });

        modelBuilder.Entity<ConvertedCollectionItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder
                .Property(x => x.Scores)
                .HasConversion(
                    list => string.Join('|', list),
                    text => string.IsNullOrWhiteSpace(text)
                        ? new List<int>()
                        : text
                            .Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Select(static part => int.Parse(part, CultureInfo.InvariantCulture))
                            .ToList());
        });

        modelBuilder.Entity<QuotedAttributeItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.Property(x => x.DisplayName).HasAttributeName("O'Brien");
        });

        modelBuilder.Entity<SparseGsiItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.Property(x => x.Gs1Pk).HasAttributeName("gs1-pk");
            builder.Property(x => x.Gs1Sk).HasAttributeName("gs1-sk");
            builder.HasGlobalSecondaryIndex(
                "gs1-index",
                nameof(SparseGsiItem.Gs1Pk),
                nameof(SparseGsiItem.Gs1Sk));
        });
    }
}
