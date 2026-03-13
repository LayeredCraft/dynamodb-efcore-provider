using LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexProjectionTable;

/// <summary>DbContext for projection-type index integration tests.</summary>
public sealed class SecondaryIndexProjectionDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Orders stored in the projection-type test table.</summary>
    public DbSet<OrderItem> Orders { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(SecondaryIndexProjectionDynamoFixture.TableName)
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
