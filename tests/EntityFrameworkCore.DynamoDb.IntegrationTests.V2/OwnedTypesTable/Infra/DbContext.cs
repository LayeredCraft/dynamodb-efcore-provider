using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.OwnedTypesTable;

/// <summary>Represents the OwnedTypesTableDbContext type.</summary>
public class OwnedTypesTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
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
