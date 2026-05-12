using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.Storage;

[Collection("EnsureCreatedTests")]
public sealed class EnsureCreatedTests(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture)
{
    protected override bool UseSharedInternalServiceProvider => false;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_ReturnsTrue()
    {
        await using var context = CreateContext<PkOnlyContext>("can-connect");

        var result = await context.Database.CanConnectAsync(CancellationToken);

        result.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_CreatesPkOnlyTable_AndSaveQueryWorks()
    {
        var tableName = "ensure-created-pkonly";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<PkOnlyContext>(tableName);

        try
        {
            var created = await context.Database.EnsureCreatedAsync(CancellationToken);
            context.Items.Add(new EnsureItem { Pk = "A", Value = "one" });
            await context.SaveChangesAsync(CancellationToken);

            var table = await DescribeTable(tableName);
            created.Should().BeTrue();
            table.BillingModeSummary.BillingMode.Should().Be(BillingMode.PAY_PER_REQUEST);
            table
                .KeySchema
                .Select(static key => (key.AttributeName, key.KeyType))
                .Should()
                .Equal(("pk", KeyType.HASH));
            var rows = await context.Items.Where(x => x.Pk == "A").ToListAsync(CancellationToken);
            rows.Should().ContainSingle().Which.Value.Should().Be("one");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_CreatesPkSkGsiAndLsi()
    {
        var tableName = "ensure-created-indexed";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<IndexedContext>(tableName);

        try
        {
            var created = await context.Database.EnsureCreatedAsync(CancellationToken);
            context.IndexedItems.Add(
                new IndexedEnsureItem { Pk = "A", Sk = "1", Customer = "C", Status = "S" });
            await context.SaveChangesAsync(CancellationToken);

            var table = await DescribeTable(tableName);
            created.Should().BeTrue();
            table
                .KeySchema
                .Select(static key => (key.AttributeName, key.KeyType))
                .Should()
                .Equal(("pk", KeyType.HASH), ("sk", KeyType.RANGE));
            table.GlobalSecondaryIndexes.Should().ContainSingle(i => i.IndexName == "ByCustomer");
            table.LocalSecondaryIndexes.Should().ContainSingle(i => i.IndexName == "ByStatus");
            var rows =
                await context
                    .IndexedItems
                    .Where(x => x.Pk == "A" && x.Sk == "1")
                    .ToListAsync(CancellationToken);
            rows.Should().ContainSingle().Which.Customer.Should().Be("C");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_RepeatedCallReturnsFalse_AndEnsureDeletedIsIdempotent()
    {
        var tableName = "ensure-created-repeat";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<RepeatContext>(tableName);

        try
        {
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeFalse();
            (await context.Database.EnsureDeletedAsync(CancellationToken)).Should().BeTrue();
            (await context.Database.EnsureDeletedAsync(CancellationToken)).Should().BeFalse();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_AddsMissingGsiToExistingTable()
    {
        var tableName = "ensure-created-missing-gsi";
        await DeleteIfExists(tableName);
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new("pk", ScalarAttributeType.S), new("sk", ScalarAttributeType.S)
                ],
                KeySchema = [new("pk", KeyType.HASH), new("sk", KeyType.RANGE)],
            },
            CancellationToken);
        await using var context = CreateContext<GsiContext>(tableName);

        try
        {
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();

            var table = await DescribeTable(tableName);
            table.GlobalSecondaryIndexes.Should().ContainSingle(i => i.IndexName == "BySk");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
        }
    }

    [Fact(
        Skip =
            "Existing-table LSI mismatch is covered by unit validation; DynamoDB Local allows this setup path in current test harness.")]
    public async Task EnsureCreatedAsync_ExistingTableMissingLsiThrows()
    {
        var tableName = "ensure-created-missing-lsi";
        await DeleteIfExists(tableName);
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new("pk", ScalarAttributeType.S), new("sk", ScalarAttributeType.S)
                ],
                KeySchema = [new("pk", KeyType.HASH), new("sk", KeyType.RANGE)],
            },
            CancellationToken);
        await using var context = CreateContext<IndexedContext>(tableName);

        try
        {
            var act = () => context.Database.EnsureCreatedAsync(CancellationToken);

            await act
                .Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*missing local secondary index*cannot be added*");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChangesAsync_WithoutEnsureCreated_FailsForMissingTable()
    {
        var tableName = "ensure-created-save-fails";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<PkOnlyContext>(tableName);
        context.Items.Add(new EnsureItem { Pk = "A", Value = "one" });

        Func<Task> act = () => context.SaveChangesAsync(CancellationToken);

        await act.Should().ThrowAsync<Exception>();
    }

    private TContext CreateContext<TContext>(string tableName) where TContext : EnsureContextBase
        => (TContext)Activator.CreateInstance(
            typeof(TContext),
            CreateOptions<TContext>(o => o.DynamoDbClient(Client)),
            tableName)!;

    private async Task<TableDescription> DescribeTable(string tableName)
        => (await Client.DescribeTableAsync(
            new DescribeTableRequest { TableName = tableName },
            CancellationToken)).Table;

    private async Task DeleteIfExists(string tableName)
    {
        try
        {
            await Client.DeleteTableAsync(
                new DeleteTableRequest { TableName = tableName },
                CancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            return;
        }

        while (true)
        {
            try
            {
                await Client.DescribeTableAsync(
                    new DescribeTableRequest { TableName = tableName },
                    CancellationToken);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
    }

    public abstract class EnsureContextBase(DbContextOptions options, string tableName) : DbContext(
        options)
    {
        protected string TableName { get; } = tableName;
    }

    public sealed class PkOnlyContext(DbContextOptions<PkOnlyContext> options, string tableName)
        : EnsureContextBase(options, tableName)
    {
        public DbSet<EnsureItem> Items => Set<EnsureItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-pkonly");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
            });
    }

    public sealed class IndexedContext(DbContextOptions<IndexedContext> options, string tableName)
        : EnsureContextBase(options, tableName)
    {
        public DbSet<IndexedEnsureItem> IndexedItems => Set<IndexedEnsureItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-indexed");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.Property(x => x.Sk).HasAttributeName("sk");
                entity.Property(x => x.Customer).HasAttributeName("customer");
                entity.Property(x => x.Status).HasAttributeName("status");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasSortKey(x => x.Sk);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.Customer);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    public sealed class GsiContext(DbContextOptions<GsiContext> options, string tableName)
        : EnsureContextBase(options, tableName)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-missing-gsi");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.Property(x => x.Sk).HasAttributeName("sk");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasSortKey(x => x.Sk);
                entity.HasGlobalSecondaryIndex("BySk", x => x.Sk);
            });
    }

    public sealed class RepeatContext(DbContextOptions<RepeatContext> options, string tableName)
        : EnsureContextBase(options, tableName)
    {
        public DbSet<EnsureItem> Items => Set<EnsureItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-repeat");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
            });
    }

    public sealed class MissingLsiContext(
        DbContextOptions<MissingLsiContext> options,
        string tableName) : EnsureContextBase(options, tableName)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-missing-lsi");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.Property(x => x.Sk).HasAttributeName("sk");
                entity.Property(x => x.Status).HasAttributeName("status");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasSortKey(x => x.Sk);
                entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    public sealed class EnsureItem
    {
        public string Pk { get; set; } = null!;
        public string? Value { get; set; }
    }

    public sealed class IndexedEnsureItem
    {
        public string Pk { get; set; } = null!;
        public string Sk { get; set; } = null!;
        public string? Customer { get; set; }
        public string? Status { get; set; }
    }
}

[CollectionDefinition("EnsureCreatedTests", DisableParallelization = true)]
public sealed class EnsureCreatedTestsCollection;
