using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;
using EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexProjectionTable;

public sealed class SecondaryIndexProjectionDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OrderItem> Orders => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(SecondaryIndexProjectionOrdersTable.TableName)
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.OrderId);

        var byStatus =
            entity.HasGlobalSecondaryIndex("ByStatusKeysOnly", x => x.Status, x => x.CreatedAt);
        byStatus.IndexBuilder.Metadata.SetSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType.KeysOnly);

        var byRegion =
            entity.HasGlobalSecondaryIndex("ByRegionInclude", x => x.Region, x => x.CreatedAt);
        byRegion.IndexBuilder.Metadata.SetSecondaryIndexProjectionType(
            DynamoSecondaryIndexProjectionType.Include);
    }
}
