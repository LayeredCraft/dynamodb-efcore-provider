using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

/// <summary>Represents the PrimitiveCollectionsDbContext type.</summary>
public class PrimitiveCollectionsDbContext(DbContextOptions<PrimitiveCollectionsDbContext> options)
    : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<PrimitiveCollectionsItem> Items => Set<PrimitiveCollectionsItem>();

    /// <summary>Provides functionality for this member.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PrimitiveCollectionsItem>()
            .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
            .HasPartitionKey(x => x.Pk);
}
