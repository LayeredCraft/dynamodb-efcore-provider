using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class PkSkTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PkSkItem> Items { get; set; }

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static PkSkTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<PkSkTableDbContext>().UseDynamo(options
                    => options.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PkSkItem>()
            .ToTable(PkSkTableDynamoFixture.TableName)
            .HasKey(x => new { x.Pk, x.Sk });
}
