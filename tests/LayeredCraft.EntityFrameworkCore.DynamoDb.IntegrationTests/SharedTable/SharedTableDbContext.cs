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
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
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
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
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
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
}

public class SharedTableInheritanceDbContext(
    DbContextOptions<SharedTableInheritanceDbContext> options) : DbContext(options)
{
    public DbSet<PersonEntity> People => Set<PersonEntity>();

    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<EmployeeEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasBaseType<PersonEntity>();
        });

        modelBuilder.Entity<ManagerEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasBaseType<PersonEntity>();
            builder.Property(x => x.Level).HasAttributeName("ManagerLevel");
        });
    }
}

public class SharedTableInheritanceBaseOnlyToTableDbContext(
    DbContextOptions<SharedTableInheritanceBaseOnlyToTableDbContext> options) : DbContext(options)
{
    public DbSet<PersonEntity> People => Set<PersonEntity>();

    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<EmployeeEntity>(builder => builder.HasBaseType<PersonEntity>());

        modelBuilder.Entity<ManagerEntity>(builder =>
        {
            builder.HasBaseType<PersonEntity>();
            builder.Property(x => x.Level).HasAttributeName("ManagerLevel");
        });
    }
}

public class SharedTableInheritanceWithIndexesDbContext(
    DbContextOptions<SharedTableInheritanceWithIndexesDbContext> options) : DbContext(options)
{
    public DbSet<ArchivedWorkOrderEntity> ArchivedWorkOrders => Set<ArchivedWorkOrderEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.HasLocalSecondaryIndex("ByStatus", x => x.Status);
        });

        modelBuilder.Entity<PriorityWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasBaseType<WorkOrderEntity>();
            builder.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
        });

        modelBuilder.Entity<ArchivedWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasBaseType<WorkOrderEntity>();
        });
    }
}
