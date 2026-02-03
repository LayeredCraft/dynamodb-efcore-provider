using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

public class PkSkTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PkSkItem> Items { get; set; }

    public static PkSkTableDbContext Create(string serviceUrl)
        => new(
            new DbContextOptionsBuilder<PkSkTableDbContext>().UseDynamo(options
                    => options.ServiceUrl(serviceUrl))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder
            .Entity<PkSkItem>()
            .ToTable(PkSkTableDynamoFixture.TableName)
            .HasKey(x => new { x.Pk, x.Sk });
}
