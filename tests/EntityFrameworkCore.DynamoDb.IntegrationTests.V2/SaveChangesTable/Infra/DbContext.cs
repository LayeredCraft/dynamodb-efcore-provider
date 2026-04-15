using System.Globalization;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SaveChangesTable;

/// <summary>Represents the SaveChangesTableDbContext type.</summary>
public class SaveChangesTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<CustomerItem> Customers => Set<CustomerItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<OrderItem> Orders => Set<OrderItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ProductItem> Products => Set<ProductItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<SessionItem> Sessions => Set<SessionItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ConverterCoverageItem> ConverterCoverageItems => Set<ConverterCoverageItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<CustomConverterItem> CustomConverterItems => Set<CustomConverterItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ConvertedCollectionItem> ConvertedCollectionItems
        => Set<ConvertedCollectionItem>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<QuotedAttributeItem> QuotedAttributeItems => Set<QuotedAttributeItem>();

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
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(
                x => x.Profile,
                profile =>
                {
                    profile.OwnsOne(x => x.PreferredAddress);
                    profile.OwnsOne(x => x.BillingAddress);
                });

            builder.OwnsMany(x => x.Contacts, contact => contact.OwnsOne(x => x.Address));
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(
                x => x.Shipping,
                shipping =>
                {
                    shipping.OwnsOne(x => x.Address);
                    shipping.OwnsOne(x => x.DeliveryWindow);
                });

            builder.OwnsOne(x => x.Billing, billing => billing.OwnsOne(x => x.Address));

            builder.OwnsMany(x => x.Lines);
        });

        modelBuilder.Entity<ProductItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(x => x.Dimensions);
            builder.OwnsMany(x => x.Variants);
        });

        modelBuilder.Entity<SessionItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(x => x.Device, device => device.OwnsOne(x => x.LastKnownAddress));
        });

        modelBuilder.Entity<ConverterCoverageItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.Property(x => x.ExternalId);
            builder.Property(x => x.OccurredAt);
            builder.PrimitiveCollection(x => x.History).ElementType();
        });

        // CustomConverterItem uses a user-defined ProductCode type with a custom converter to
        // exercise the boxed scalar fallback write path.
        modelBuilder.Entity<CustomConverterItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Code).HasConversion<ProductCodeConverter>();
            builder.Property(x => x.OptionalCode).HasConversion<ProductCodeConverter>();
        });

        modelBuilder.Entity<ConvertedCollectionItem>(builder =>
        {
            builder.ToTable(SaveChangesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
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
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.Property(x => x.DisplayName).HasAttributeName("O'Brien");
        });
    }
}
