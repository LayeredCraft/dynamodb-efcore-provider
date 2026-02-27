using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public class SharedTableDbContext(DbContextOptions<SharedTableDbContext> options) : DbContext(
    options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasKey(x => new { x.Pk, x.Sk });
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasKey(x => new { x.Pk, x.Sk });
        });
    }
}

public class SharedTableCustomDiscriminatorNameDbContext(
    DbContextOptions<SharedTableCustomDiscriminatorNameDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasEmbeddedDiscriminatorName("$kind");

        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasKey(x => new { x.Pk, x.Sk });
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasKey(x => new { x.Pk, x.Sk });
        });
    }
}

public class SharedTableSingleTypeDbContext(
    DbContextOptions<SharedTableSingleTypeDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasKey(x => new { x.Pk, x.Sk });
        });
}
