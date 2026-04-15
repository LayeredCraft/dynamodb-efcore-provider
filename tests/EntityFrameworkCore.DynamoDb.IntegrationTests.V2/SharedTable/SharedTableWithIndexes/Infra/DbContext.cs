using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SharedTable.SharedTableWithIndexes;

public class SharedTableWithIndexesDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<WorkOrderEntity> WorkOrders => Set<WorkOrderEntity>();
    public DbSet<PriorityWorkOrderEntity> PriorityWorkOrders => Set<PriorityWorkOrderEntity>();
    public DbSet<ArchivedWorkOrderEntity> ArchivedWorkOrders => Set<ArchivedWorkOrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableWithIndexesItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.HasLocalSecondaryIndex("ByStatus", x => x.Status);
        });

        modelBuilder.Entity<PriorityWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableWithIndexesItemTable.TableName);
            builder.HasBaseType<WorkOrderEntity>();
            builder.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
        });

        modelBuilder.Entity<ArchivedWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableWithIndexesItemTable.TableName);
            builder.HasBaseType<WorkOrderEntity>();
        });
    }
}
