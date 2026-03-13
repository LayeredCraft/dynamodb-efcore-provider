using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable.SharedTableWithIndexes;

/// <summary>
/// DbContext that maps the <c>WorkOrderEntity</c> hierarchy to a separate physical table
/// that includes both a GSI and an LSI. Used to verify that discriminator predicates and automatic
/// index selection interact correctly when multiple entity types share an indexed table.
/// </summary>
/// <remarks>
/// Uses a distinct table name (<c>TableName</c>) so the schema can declare GSI and LSI
/// attributes without affecting the plain <c>"app-table"</c> used by other shared-table tests.
/// </remarks>
public class SharedTableWithIndexesDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Physical DynamoDB table name used by this context.</summary>
    public const string TableName = "work-orders-indexed-table";

    /// <summary>All work orders (base-type query spanning both concrete types).</summary>
    public DbSet<WorkOrderEntity> WorkOrders => Set<WorkOrderEntity>();

    /// <summary>Priority work orders only.</summary>
    public DbSet<PriorityWorkOrderEntity> PriorityWorkOrders => Set<PriorityWorkOrderEntity>();

    /// <summary>Archived work orders only.</summary>
    public DbSet<ArchivedWorkOrderEntity> ArchivedWorkOrders => Set<ArchivedWorkOrderEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrderEntity>(builder =>
        {
            builder.ToTable(TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            // LSI: work orders for a given tenant key, ordered by status value.
            builder.HasLocalSecondaryIndex("ByStatus", x => x.Status);
        });

        modelBuilder.Entity<PriorityWorkOrderEntity>(builder =>
        {
            builder.ToTable(TableName);
            builder.HasBaseType<WorkOrderEntity>();
            // GSI: all priority work orders at a given priority level, cross-tenant.
            builder.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
        });

        modelBuilder.Entity<ArchivedWorkOrderEntity>(builder =>
        {
            builder.ToTable(TableName);
            builder.HasBaseType<WorkOrderEntity>();
            // No additional indexes — only the inherited base LSI (ByStatus) applies.
        });
    }
}
