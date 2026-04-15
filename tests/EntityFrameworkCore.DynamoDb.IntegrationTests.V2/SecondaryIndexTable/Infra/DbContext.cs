using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;

public class SecondaryIndexDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OrderItem> Orders => Set<OrderItem>();

    public static SecondaryIndexDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SecondaryIndexDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(SecondaryIndexOrdersTable.TableName)
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.OrderId);

        entity.HasGlobalSecondaryIndex("ByStatus", x => x.Status, x => x.CreatedAt);
        entity.HasGlobalSecondaryIndex("ByRegion", x => x.Region, x => x.CreatedAt);
        entity.HasLocalSecondaryIndex("ByCreatedAt", x => x.CreatedAt);
        entity.HasLocalSecondaryIndex("ByPriority", x => x.Priority);
    }
}
