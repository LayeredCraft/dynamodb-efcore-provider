using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

internal class SimpleTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<SimpleItem> SimpleItems { get; set; }

    public static SimpleTableDbContext Create(string serviceUrl)
        => new(
            new DbContextOptionsBuilder<SimpleTableDbContext>().UseDynamo(options
                    => options.ServiceUrl(serviceUrl))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SimpleItem>().ToTable("SimpleItems").HasKey(x => x.Pk);
}
