using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.Storage;

[Collection("EnsureCreatedTests")]
public sealed class EnsureCreatedTests(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture)
{
    protected override bool UseSharedInternalServiceProvider => false;

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task CanConnectAsync_ReturnsTrue()
    {
        await using var context = CreateContext<PkOnlyContext>();

        var result = await context.Database.CanConnectAsync(CancellationToken);

        result.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_CreatesPkOnlyTable_AndSaveQueryWorks()
    {
        var tableName = "ensure-created-pkonly";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<PkOnlyContext>();

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
        await using var context = CreateContext<IndexedContext>();

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
    public async Task EnsureCreatedAsync_SeedsOnlyNewTables()
    {
        var existingTableName = "ensure-created-seed-existing";
        var missingTableName = "ensure-created-seed-missing";
        await DeleteIfExists(existingTableName);
        await DeleteIfExists(missingTableName);
        await using var context = CreateContext<SeedScopeContext>();

        try
        {
            await Client.CreateTableAsync(
                new CreateTableRequest
                {
                    TableName = existingTableName,
                    BillingMode = BillingMode.PAY_PER_REQUEST,
                    AttributeDefinitions =
                    [
                        new AttributeDefinition("pk", ScalarAttributeType.S)
                    ],
                    KeySchema = [new KeySchemaElement("pk", KeyType.HASH)]
                },
                CancellationToken);
            await WaitUntilActive(existingTableName);

            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();

            var existingRows = await context.ExistingSeedItems.ToListAsync(CancellationToken);
            var missingRows = await context.MissingSeedItems.ToListAsync(CancellationToken);
            existingRows.Should().BeEmpty();
            missingRows.Should().ContainSingle().Which.Pk.Should().Be("missing");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
            await DeleteIfExists(existingTableName);
            await DeleteIfExists(missingTableName);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_WaitsBeforeSeeding_WhenWaitForCompletionFalse()
    {
        var tableName = "ensure-created-seed-nowait";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<SeedNoWaitContext>(options
            => options.TableLifecycle(lifecycle => lifecycle.WaitForCompletion = false));

        try
        {
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();

            var rows = await context.SeedItems.ToListAsync(CancellationToken);
            rows.Should().ContainSingle().Which.Pk.Should().Be("seeded");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken);
            await DeleteIfExists(tableName);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_RepeatedCallReturnsFalse_AndEnsureDeletedIsIdempotent()
    {
        var tableName = "ensure-created-repeat";
        await DeleteIfExists(tableName);
        await using var context = CreateContext<RepeatContext>();

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
    public async Task EnsureCreatedAsync_RecreatesTableDeletedWithoutWaiting()
    {
        var tableName = "ensure-created-recreate-nowait";
        await DeleteIfExists(tableName);
        await using var deletingContext = CreateContext<RecreateNoWaitContext>(options
            => options.TableLifecycle(lifecycle =>
            {
                lifecycle.WaitForCompletion = false;
                lifecycle.InitialPollingDelay = TimeSpan.FromMilliseconds(100);
            }));
        await using var creatingContext = CreateContext<RecreateNoWaitContext>(options
            => options.TableLifecycle(lifecycle =>
            {
                lifecycle.InitialPollingDelay = TimeSpan.FromMilliseconds(100);
                lifecycle.MaxPollingDelay = TimeSpan.FromMilliseconds(200);
            }));

        try
        {
            (await deletingContext.Database.EnsureCreatedAsync(CancellationToken))
                .Should()
                .BeTrue();
            (await deletingContext.Database.EnsureDeletedAsync(CancellationToken))
                .Should()
                .BeTrue();

            (await creatingContext.Database.EnsureCreatedAsync(CancellationToken))
                .Should()
                .BeTrue();

            var table = await DescribeTable(tableName);
            table.TableStatus.Should().Be(TableStatus.ACTIVE);
        }
        finally
        {
            await DeleteIfExists(tableName);
        }
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task EnsureCreatedAsync_AddsMultipleMissingGsis_WhenWaitForCompletionFalse()
    {
        var tableName = "ensure-created-missing-gsis";
        await DeleteIfExists(tableName);
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
                KeySchema = [new KeySchemaElement("pk", KeyType.HASH)]
            },
            CancellationToken);
        await WaitUntilActive(tableName);
        await using var context = CreateContext<MultipleGsiContext>(options
            => options.TableLifecycle(lifecycle => lifecycle.WaitForCompletion = false));

        try
        {
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();

            await WaitUntilActive(tableName);
            var table = await DescribeTable(tableName);
            table
                .GlobalSecondaryIndexes
                .Select(static index => index.IndexName)
                .Should()
                .BeEquivalentTo(["ByCustomer", "ByStatus"]);
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
                    new AttributeDefinition("pk", ScalarAttributeType.S),
                    new AttributeDefinition("sk", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("pk", KeyType.HASH),
                    new KeySchemaElement("sk", KeyType.RANGE)
                ]
            },
            CancellationToken);
        await using var context = CreateContext<GsiContext>();

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
                    new AttributeDefinition("pk", ScalarAttributeType.S),
                    new AttributeDefinition("sk", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("pk", KeyType.HASH),
                    new KeySchemaElement("sk", KeyType.RANGE)
                ]
            },
            CancellationToken);
        await using var context = CreateContext<MissingLsiContext>();

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
    public async Task EnsureCreatedAsync_AsyncSeeder_ReceivesCreatedTrue_WhenGsiAdded()
    {
        var tableName = "ensure-created-seeder-gsi";
        await DeleteIfExists(tableName);
        await Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions = [new AttributeDefinition("pk", ScalarAttributeType.S)],
                KeySchema = [new KeySchemaElement("pk", KeyType.HASH)]
            },
            CancellationToken);
        await WaitUntilActive(tableName);

        bool? seederCreatedFlag = null;
        var builder = new DbContextOptionsBuilder<GsiContext>();
        builder.UseDynamo(o => o.DynamoDbClient(Client));
        builder.UseAsyncSeeding((_, created, _) =>
        {
            seederCreatedFlag = created;
            return Task.CompletedTask;
        });
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        await using var context = new GsiContext(builder.Options);

        try
        {
            (await context.Database.EnsureCreatedAsync(CancellationToken)).Should().BeTrue();

            seederCreatedFlag.Should().BeTrue();
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
        await using var context = CreateContext<PkOnlyContext>();
        context.Items.Add(new EnsureItem { Pk = "A", Value = "one" });

        Func<Task> act = () => context.SaveChangesAsync(CancellationToken);

        await act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    private TContext CreateContext<TContext>(
        Action<DynamoDbContextOptionsBuilder>? configure = null) where TContext : EnsureContextBase
        => (TContext)Activator.CreateInstance(
            typeof(TContext),
            CreateOptions<TContext>(o =>
            {
                o.DynamoDbClient(Client);
                configure?.Invoke(o);
            }))!;

    private async Task<TableDescription> DescribeTable(string tableName)
        => (await Client.DescribeTableAsync(
            new DescribeTableRequest { TableName = tableName },
            CancellationToken)).Table;

    private async Task WaitUntilActive(string tableName)
    {
        while (true)
        {
            CancellationToken.ThrowIfCancellationRequested();
            var table = await DescribeTable(tableName);
            if (table.TableStatus == TableStatus.ACTIVE
                && (table.GlobalSecondaryIndexes ?? []).All(static index
                    => index.IndexStatus == IndexStatus.ACTIVE))
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
    }

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
            CancellationToken.ThrowIfCancellationRequested();
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

    public abstract class EnsureContextBase(DbContextOptions options) : DbContext(options);

    public sealed class PkOnlyContext(DbContextOptions<PkOnlyContext> options)
        : EnsureContextBase(options)
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

    public sealed class IndexedContext(DbContextOptions<IndexedContext> options)
        : EnsureContextBase(options)
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

    public sealed class GsiContext(DbContextOptions<GsiContext> options)
        : EnsureContextBase(options)
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

    public sealed class MultipleGsiContext(DbContextOptions<MultipleGsiContext> options)
        : EnsureContextBase(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IndexedEnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-missing-gsis");
                entity.Ignore(x => x.Sk);
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.Property(x => x.Customer).HasAttributeName("customer");
                entity.Property(x => x.Status).HasAttributeName("status");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasGlobalSecondaryIndex("ByCustomer", x => x.Customer);
                entity.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
            });
    }

    public sealed class RepeatContext(DbContextOptions<RepeatContext> options)
        : EnsureContextBase(options)
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

    public sealed class RecreateNoWaitContext(DbContextOptions<RecreateNoWaitContext> options)
        : EnsureContextBase(options)
    {
        public DbSet<EnsureItem> Items => Set<EnsureItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<EnsureItem>(entity =>
            {
                entity.ToTable("ensure-created-recreate-nowait");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
            });
    }

    public sealed class SeedScopeContext(DbContextOptions<SeedScopeContext> options)
        : EnsureContextBase(options)
    {
        public DbSet<ExistingSeedItem> ExistingSeedItems => Set<ExistingSeedItem>();

        public DbSet<MissingSeedItem> MissingSeedItems => Set<MissingSeedItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ExistingSeedItem>(entity =>
            {
                entity.ToTable("ensure-created-seed-existing");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasData(new ExistingSeedItem { Pk = "existing" });
            });
            modelBuilder.Entity<MissingSeedItem>(entity =>
            {
                entity.ToTable("ensure-created-seed-missing");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasData(new MissingSeedItem { Pk = "missing" });
            });
        }
    }

    public sealed class SeedNoWaitContext(DbContextOptions<SeedNoWaitContext> options)
        : EnsureContextBase(options)
    {
        public DbSet<SeedNoWaitItem> SeedItems => Set<SeedNoWaitItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SeedNoWaitItem>(entity =>
            {
                entity.ToTable("ensure-created-seed-nowait");
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasData(new SeedNoWaitItem { Pk = "seeded" });
            });
    }

    public sealed class MissingLsiContext(DbContextOptions<MissingLsiContext> options)
        : EnsureContextBase(options)
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

    public sealed class ExistingSeedItem
    {
        public string Pk { get; set; } = null!;
    }

    public sealed class MissingSeedItem
    {
        public string Pk { get; set; } = null!;
    }

    public sealed class SeedNoWaitItem
    {
        public string Pk { get; set; } = null!;
    }
}

[CollectionDefinition("EnsureCreatedTests", DisableParallelization = true)]
public sealed class EnsureCreatedTestsCollection;
