namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ConstructorMaterializationTable;

public sealed class ConstructorMaterializationDbContext(DbContextOptions options) : DbContext(
    options)
{
    public DbSet<ConstructorBlog> Blogs => Set<ConstructorBlog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<ConstructorBlog>(builder =>
        {
            builder.ToTable(ConstructorBlogTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.ComplexCollection(x => x.Posts);
        });
}
