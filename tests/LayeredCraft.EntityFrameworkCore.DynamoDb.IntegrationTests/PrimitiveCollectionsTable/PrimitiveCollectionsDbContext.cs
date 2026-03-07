using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class PrimitiveCollectionsDbContext(DbContextOptions<PrimitiveCollectionsDbContext> options)
    : DbContext(options)
{
    public DbSet<PrimitiveCollectionsItem> Items => Set<PrimitiveCollectionsItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PrimitiveCollectionsItem>()
            .ToTable(PrimitiveCollectionsDynamoFixture.TableName)
            .HasPartitionKey(x => x.Pk);
}
