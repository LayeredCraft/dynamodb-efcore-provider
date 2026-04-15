using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.V2.PkSkTable;

/// <summary>Represents the PkSkTableDbContext type.</summary>
public class PkSkTableDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<PkSkItem> Items { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static PkSkTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<PkSkTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    /// <summary>Provides functionality for this member.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PkSkItem>()
            .ToTable(PkSkItemTable.TableName)
            .HasPartitionKey(x => x.Pk)
            .HasSortKey(x => x.Sk);
}
