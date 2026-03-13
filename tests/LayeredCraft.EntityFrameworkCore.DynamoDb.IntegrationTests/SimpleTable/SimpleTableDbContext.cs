using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Represents the SimpleTableDbContext type.</summary>
public class SimpleTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<SimpleItem> SimpleItems { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SimpleTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SimpleTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    /// <summary>Provides functionality for this member.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SimpleItem>().ToTable("SimpleItems").HasPartitionKey(x => x.Pk);
}
