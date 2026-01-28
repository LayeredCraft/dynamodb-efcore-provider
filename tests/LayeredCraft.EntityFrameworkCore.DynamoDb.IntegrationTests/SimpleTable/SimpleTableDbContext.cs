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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(options => options.ServiceUrl("http://localhost:8002"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<SimpleItem>().ToTable("SimpleItems").HasKey(x => x.Pk);
}
