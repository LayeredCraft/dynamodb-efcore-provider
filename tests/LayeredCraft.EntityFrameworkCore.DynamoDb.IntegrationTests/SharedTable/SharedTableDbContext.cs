using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Represents the SharedTableDbContext type.</summary>
public class SharedTableDbContext(DbContextOptions<SharedTableDbContext> options) : DbContext(
    options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>Provides functionality for this member.</summary>
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

/// <summary>Represents the SharedTableCustomDiscriminatorNameDbContext type.</summary>
public class SharedTableCustomDiscriminatorNameDbContext(
    DbContextOptions<SharedTableCustomDiscriminatorNameDbContext> options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>Provides functionality for this member.</summary>
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

/// <summary>Represents the SharedTableSingleTypeDbContext type.</summary>
public class SharedTableSingleTypeDbContext(
    DbContextOptions<SharedTableSingleTypeDbContext> options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Provides functionality for this member.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedTableDynamoFixture.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
}

/// <summary>Represents the SharedTableInheritanceDbContext type.</summary>
public class SharedTableInheritanceDbContext(
    DbContextOptions<SharedTableInheritanceDbContext> options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<PersonEntity> People => Set<PersonEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    /// <summary>Provides functionality for this member.</summary>
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

/// <summary>Represents the SharedTableInheritanceBaseOnlyToTableDbContext type.</summary>
public class SharedTableInheritanceBaseOnlyToTableDbContext(
    DbContextOptions<SharedTableInheritanceBaseOnlyToTableDbContext> options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<PersonEntity> People => Set<PersonEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    /// <summary>Provides functionality for this member.</summary>
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

/// <summary>Represents the SharedTableInheritanceWithIndexesDbContext type.</summary>
public class SharedTableInheritanceWithIndexesDbContext(
    DbContextOptions<SharedTableInheritanceWithIndexesDbContext> options) : DbContext(options)
{
    /// <summary>Provides functionality for this member.</summary>
    public DbSet<ArchivedWorkOrderEntity> ArchivedWorkOrders => Set<ArchivedWorkOrderEntity>();

    /// <summary>Provides functionality for this member.</summary>
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
