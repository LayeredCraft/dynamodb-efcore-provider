namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class PrimitiveCollectionsDbContext(DbContextOptions<PrimitiveCollectionsDbContext> options)
    : DbContext(options)
{
    public DbSet<PrimitiveCollectionsItem> Items => Set<PrimitiveCollectionsItem>();

    public DbSet<MutablePrimitiveCollectionsItem> MutableItems
        => Set<MutablePrimitiveCollectionsItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<PrimitiveCollectionsItem>()
            .ToTable(PrimitiveCollectionsItemTable.TableName)
            .HasPartitionKey(x => x.Pk);

        modelBuilder
            .Entity<MutablePrimitiveCollectionsItem>()
            .ToTable(MutablePrimitiveCollectionsItemTable.TableName)
            .HasPartitionKey(x => x.Pk);
    }
}
