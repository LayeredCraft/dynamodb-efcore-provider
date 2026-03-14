using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SecondaryIndexTable;

/// <summary>
///     EF Core DbContext for the secondary-index integration test table. Configures one entity
///     type (<c>OrderItem</c>) with a composite primary key plus two GSIs and two LSIs so
///     that all supported index shapes can be exercised in a single table.
/// </summary>
public class SecondaryIndexDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Order items stored in the <c>SecondaryIndexOrders</c> DynamoDB table.</summary>
    public DbSet<OrderItem> Orders { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SecondaryIndexDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SecondaryIndexDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(SecondaryIndexDynamoFixture.TableName)
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.OrderId);

        // GSI: query all orders for a given status, optionally sorted by creation date.
        entity.HasGlobalSecondaryIndex("ByStatus", x => x.Status, x => x.CreatedAt);

        // GSI: query all orders fulfilled in a given region, optionally sorted by creation date.
        entity.HasGlobalSecondaryIndex("ByRegion", x => x.Region, x => x.CreatedAt);

        // LSI: query a customer's orders sorted by creation date instead of OrderId.
        entity.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAt);

        // LSI: query a customer's orders sorted by numeric dispatch priority.
        entity.HasLocalSecondaryIndex("ByPriority", x => x.Priority);
    }
}
