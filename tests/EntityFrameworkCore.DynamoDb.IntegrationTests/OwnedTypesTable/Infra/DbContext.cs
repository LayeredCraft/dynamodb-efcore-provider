using System.Globalization;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.OwnedTypesTable;

/// <summary>
///     Context used to verify that an owned collection element with a CLR property named
///     <c>Id</c> (mapped to a custom attribute name) does not trigger EF Core's Id-based key
///     discovery and correctly receives an ordinal shadow key.
/// </summary>
public class OwnedCollectionWithIdPropertyDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<AnalysisReport> Reports => Set<AnalysisReport>();

    public static OwnedCollectionWithIdPropertyDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<OwnedCollectionWithIdPropertyDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<AnalysisReport>(builder =>
        {
            builder.ToTable(AnalysisReportTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.OwnsMany(x => x.Results, results =>
            {
                results.Property(r => r.Id).HasAttributeName("id");
                results.Property(r => r.Score)
                    .HasConversion(new ValueConverter<float, string>(
                        v => v.ToString("F4", CultureInfo.InvariantCulture),
                        s => float.Parse(s, CultureInfo.InvariantCulture)));
            });
        });
}

/// <summary>Represents the OwnedTypesTableDbContext type.</summary>
public class OwnedTypesTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OwnedShapeItem> Items => Set<OwnedShapeItem>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static OwnedTypesTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<OwnedTypesTableDbContext>()
                .UseDynamo(options => options.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>Configures the owned-shape model used by materialization tests.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<OwnedShapeItem>(builder =>
        {
            builder.ToTable(OwnedTypesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);

            // Intentionally rely on EF Core convention-based owned type discovery in this suite.
        });
}
