using Amazon.DynamoDBv2;
using EntityFrameworkCore.DynamoDb.IntegrationTests.V2.SecondaryIndexTable;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.CompetingGsiTable;

public sealed class CompetingGsiDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<OrderItem> Orders => Set<OrderItem>();

    public static CompetingGsiDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<CompetingGsiDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();

        entity
            .ToTable(CompetingGsiOrdersTable.TableName)
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.OrderId);

        entity.HasGlobalSecondaryIndex("ByStatusCreatedAt", x => x.Status, x => x.CreatedAt);
        entity.HasGlobalSecondaryIndex("ByStatusPriority", x => x.Status, x => x.Priority);
    }
}
