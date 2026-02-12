using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class SimpleTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<SimpleItem> SimpleItems { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SimpleTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SimpleTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SimpleItem>().ToTable("SimpleItems").HasKey(x => x.Pk);
}
