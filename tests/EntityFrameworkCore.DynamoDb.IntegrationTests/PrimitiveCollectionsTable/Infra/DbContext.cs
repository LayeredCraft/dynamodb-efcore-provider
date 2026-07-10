namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PrimitiveCollectionsTable;

public class PrimitiveCollectionsDbContext(DbContextOptions<PrimitiveCollectionsDbContext> options)
    : DbContext(options)
{
    public DbSet<PrimitiveCollectionsItem> Items => Set<PrimitiveCollectionsItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PrimitiveCollectionsItem>()
            .ToTable(PrimitiveCollectionsItemTable.TableName)
            .HasPartitionKey(x => x.Pk);
}
