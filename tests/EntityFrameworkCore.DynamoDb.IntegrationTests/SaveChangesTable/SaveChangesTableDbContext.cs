using System.Globalization;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Represents the SaveChangesTableDbContext type.</summary>
public class SaveChangesTableDbContext(DbContextOptions options) : DbContext(options)
{
    private static readonly ValueConverter<Guid, string> GuidAsNStringConverter = new(
        static value => value.ToString("N"),
        static value => Guid.ParseExact(value, "N"));

    private static readonly ValueConverter<DateTimeOffset, string> UnixSecondsConverter = new(
        static value => value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        static value => DateTimeOffset.FromUnixTimeSeconds(
            long.Parse(value, CultureInfo.InvariantCulture)));

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

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SaveChangesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SaveChangesTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    /// <summary>Configures the shared-table SaveChanges model used by future CRUD tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerItem>(builder =>
        {
            builder.ToTable(SaveChangesTableDynamoFixture.TableName);
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
            builder.ToTable(SaveChangesTableDynamoFixture.TableName);
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
            builder.ToTable(SaveChangesTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(x => x.Dimensions);
            builder.OwnsMany(x => x.Variants);
        });

        modelBuilder.Entity<SessionItem>(builder =>
        {
            builder.ToTable(SaveChangesTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();

            builder.OwnsOne(x => x.Device, device => device.OwnsOne(x => x.LastKnownAddress));
        });

        modelBuilder.Entity<ConverterCoverageItem>(builder =>
        {
            builder.ToTable(SaveChangesTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.Property(x => x.ExternalId).HasConversion(GuidAsNStringConverter);
            builder.Property(x => x.OccurredAt).HasConversion(UnixSecondsConverter);
            builder
                .PrimitiveCollection(x => x.History)
                .ElementType()
                .HasConversion(UnixSecondsConverter);
        });
    }
}
