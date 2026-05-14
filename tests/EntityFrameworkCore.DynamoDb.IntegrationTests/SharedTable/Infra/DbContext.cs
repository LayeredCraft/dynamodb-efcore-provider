using Amazon.DynamoDBv2;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Context with two unrelated entity types sharing the same table, default discriminator.</summary>
public class SharedTableDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
    }
}

/// <summary>Context with a custom embedded discriminator name (<c>$kind</c>).</summary>
public class SharedTableCustomDiscriminatorNameDbContext(DbContextOptions options) : DbContext(
    options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableCustomDiscriminatorNameDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableCustomDiscriminatorNameDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasEmbeddedDiscriminatorName("$kind");

        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
    }
}

/// <summary>Context with a single entity type mapped to the shared table (no discriminator).</summary>
public class SharedTableSingleTypeDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableSingleTypeDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableSingleTypeDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
}

/// <summary>
///     Context with two unrelated entity types sharing the same table while explicitly removing
///     discriminator metadata from one type via an early <c>HasNoDiscriminator()</c> call.
/// </summary>
public class SharedTableEarlyHasNoDiscriminatorDbContext(DbContextOptions options) : DbContext(
    options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableEarlyHasNoDiscriminatorDbContext Create(IAmazonDynamoDB client)
        => new(
            is there a

    private hybrid option

    private where
        we're using both we can sacrifice ordering and stuff like that for using HasnoDiscriminator? But I think it'
    private s good
    private to keep
    private that option
    private because I
    private think people
    private still will
    private use that if
    private unintentionally.I think
    private having a
    private hybrid approach
    private where we're adding stuff but we'
    private re also
    private exclusively removing
    private it or something else.
    private I want
    private you to
    private think this
    private through but
    private also validate
    private against Mongo and Dynamo.Sorry, I'm not talking Mongo and Cosmos to see if there'
    private s lessons
    private learned from
    private how they
    private handle it
    private because I
    private know they
    private have different
    private types of discriminators too.So
    private we need
    private to figure out
    private a better
    private holistic solution
    private here that is user-
    private friendly and it's
    private not doing
    private weird stuff
    private like shadows
    private because that
    private will lead
    private to the
    private bug we had originally. new
        DbContextOptionsBuilder<SharedTableEarlyHasNoDiscriminatorDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.HasNoDiscriminator();
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<OrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });
    }
}

/// <summary>Context with a TPH inheritance hierarchy mapped to the shared table.</summary>
public class SharedTableInheritanceDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<PersonEntity> People => Set<PersonEntity>();
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();
    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableInheritanceDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableInheritanceDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
        });

        modelBuilder.Entity<EmployeeEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasBaseType<PersonEntity>();
        });

        modelBuilder.Entity<ManagerEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasBaseType<PersonEntity>();
            builder.Property(x => x.Level).HasAttributeName("ManagerLevel");
        });
    }
}

/// <summary>Context where only the base type calls <c>ToTable</c>; derived types inherit the mapping.</summary>
public class SharedTableInheritanceBaseOnlyToTableDbContext(DbContextOptions options) : DbContext(
    options)
{
    public DbSet<PersonEntity> People => Set<PersonEntity>();
    public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();
    public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableInheritanceBaseOnlyToTableDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableInheritanceBaseOnlyToTableDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
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

/// <summary>Context with inheritance hierarchy that includes LSI/GSI definitions.</summary>
public class SharedTableInheritanceWithIndexesDbContext(DbContextOptions options) : DbContext(
    options)
{
    public DbSet<ArchivedWorkOrderEntity> ArchivedWorkOrders => Set<ArchivedWorkOrderEntity>();

    /// <summary>Creates a context configured to use the provided DynamoDB client instance.</summary>
    public static SharedTableInheritanceWithIndexesDbContext Create(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableInheritanceWithIndexesDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasPartitionKey(x => x.Pk);
            builder.HasSortKey(x => x.Sk);
            builder.HasLocalSecondaryIndex("ByStatus", x => x.Status);
        });

        modelBuilder.Entity<PriorityWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasBaseType<WorkOrderEntity>();
            builder.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
        });

        modelBuilder.Entity<ArchivedWorkOrderEntity>(builder =>
        {
            builder.ToTable(SharedItemTable.TableName);
            builder.HasBaseType<WorkOrderEntity>();
        });
    }
}
