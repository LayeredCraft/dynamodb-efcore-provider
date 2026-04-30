using System.Globalization;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Context used to verify that a complex collection element with a CLR property named
///     <c>Id</c> (mapped to a custom attribute name) does not trigger EF Core's Id-based key
///     discovery, since complex types have no key concept.
/// </summary>
public class ComplexCollectionWithIdPropertyDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<AnalysisReport> Reports => Set<AnalysisReport>();

    public static ComplexCollectionWithIdPropertyDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<ComplexCollectionWithIdPropertyDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<AnalysisReport>(builder =>
        {
            builder.ToTable(AnalysisReportTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.ComplexCollection(
                x => x.Results,
                results => results
                    .Property(r => r.Score)
                    .HasConversion(
                        new ValueConverter<float, string>(
                            v => v.ToString("F4", CultureInfo.InvariantCulture),
                            s => float.Parse(s, CultureInfo.InvariantCulture))));
        });
}

/// <summary>
///     Context used to verify convention-only complex discovery. The model intentionally relies
///     on provider discovery for nested complex properties and collections, with only the root table
///     and key configured explicitly.
/// </summary>
public class ConventionOnlyComplexTypesDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ComplexShapeItem> Items => Set<ComplexShapeItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static ConventionOnlyComplexTypesDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<ConventionOnlyComplexTypesDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>
    ///     Configures only the root entity table and key so complex members must be discovered by
    ///     convention.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<ComplexShapeItem>(builder =>
        {
            builder.ToTable(ComplexTypesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
        });
}

/// <summary>Represents the ComplexTypesTableDbContext type.</summary>
public class ComplexTypesTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ComplexShapeItem> Items => Set<ComplexShapeItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static ComplexTypesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<ComplexTypesTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>Configures the complex-shape model used by query and materialization tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<ComplexShapeItem>(builder =>
        {
            builder.ToTable(ComplexTypesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.ComplexProperty(
                x => x.Profile,
                profile =>
                {
                    profile.ComplexProperty(
                        x => x.Address,
                        address => address.ComplexProperty(x => x.Geo));
                });

            builder.ComplexCollection(
                x => x.Orders,
                order =>
                {
                    order.ComplexProperty(
                        x => x.Payment,
                        payment => payment.ComplexProperty(x => x.Card));
                    order.ComplexCollection(x => x.Lines);
                });

            builder.ComplexCollection(x => x.OrderSnapshots);
        });
}
