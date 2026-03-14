using EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.CompetingGsiTable;

/// <summary>DbContext for integration tests that require two eligible GSIs competing for selection.</summary>
public sealed class CompetingGsiDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Orders stored in the competing-GSI test table.</summary>
    public DbSet<OrderItem> Orders { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(CompetingGsiDynamoFixture.TableName)
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.OrderId);

        entity.HasGlobalSecondaryIndex("ByStatusCreatedAt", x => x.Status, x => x.CreatedAt);
        entity.HasGlobalSecondaryIndex("ByStatusPriority", x => x.Status, x => x.Priority);
    }
}
