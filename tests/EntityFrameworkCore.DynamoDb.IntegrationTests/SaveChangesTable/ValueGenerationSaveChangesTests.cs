using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>Integration coverage for DynamoDB value-generation conventions through SaveChanges.</summary>
[Collection("ValueGenerationSaveChangesTests")]
public sealed class ValueGenerationSaveChangesTests(DynamoContainerFixture fixture)
    : DynamoTestFixtureBase(fixture)
{
    protected override bool UseSharedInternalServiceProvider => false;

    /// <summary>
    ///     Verifies explicit integer keys are written and materialized without EF treating them as
    ///     generated.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_IntPartitionKey_ExplicitKey_PersistsAndMaterializes()
    {
        await using var context = CreateContext<IntKeyContext>();
        await ResetTable(context);
        var entity = new IntKeyEntity { Id = 123, Name = "assigned" };

        context.Entities.Add(entity);
        await context.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(
            IntKeyContext.TableName,
            "id",
            new AttributeValue { N = "123" });
        raw.Should().NotBeNull();
        raw!["id"].N.Should().Be("123");
        raw["name"].S.Should().Be("assigned");

        var materialized =
            await context.Entities.Where(x => x.Id == 123).ToListAsync(CancellationToken);
        materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);

        AssertSql(
            """
            INSERT INTO "value-generation-int-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-int-keys"
            WHERE "id" = 123
            """);
    }

    /// <summary>Verifies Guid partition keys still use EF Core client-side generation before writing.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_GuidPartitionKey_DefaultGuid_GeneratesPersistsAndMaterializes()
    {
        await using var context = CreateContext<GuidKeyContext>();
        await ResetTable(context);
        var entity = new GuidKeyEntity { Name = "generated" };

        entity.Id.Should().Be(Guid.Empty);
        context.Entities.Add(entity);
        await context.SaveChangesAsync(CancellationToken);

        entity.Id.Should().NotBe(Guid.Empty);
        var raw = await GetItemAsync(
            GuidKeyContext.TableName,
            "id",
            new AttributeValue { S = entity.Id.ToString() });
        raw.Should().NotBeNull();
        raw!["id"].S.Should().Be(entity.Id.ToString());
        raw["name"].S.Should().Be("generated");

        var materialized =
            await context.Entities.Where(x => x.Id == entity.Id).ToListAsync(CancellationToken);
        materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);

        AssertSql(
            """
            INSERT INTO "value-generation-guid-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-guid-keys"
            WHERE "id" = ?
            """);
    }

    /// <summary>Verifies composite string keys are explicitly written through SaveChanges.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_CompositeStringKeys_ExplicitKeys_PersistsAndMaterializes()
    {
        await using var context = CreateContext<CompositeKeyContext>();
        await ResetTable(context);
        var entity = new CompositeKeyEntity { Pk = "TENANT#1", Sk = "ORDER#1", Name = "composite" };

        context.Entities.Add(entity);
        await context.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(
            CompositeKeyContext.TableName,
            "pk",
            new AttributeValue { S = entity.Pk },
            "sk",
            new AttributeValue { S = entity.Sk });
        raw.Should().NotBeNull();
        raw!["pk"].S.Should().Be(entity.Pk);
        raw["sk"].S.Should().Be(entity.Sk);
        raw["name"].S.Should().Be(entity.Name);

        var materialized =
            await context
                .Entities
                .Where(x => x.Pk == entity.Pk && x.Sk == entity.Sk)
                .ToListAsync(CancellationToken);
        materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);

        AssertSql(
            """
            INSERT INTO "value-generation-composite-keys"
            VALUE {'pk': ?, 'sk': ?, 'name': ?}
            """,
            """
            SELECT "pk", "sk", "name"
            FROM "value-generation-composite-keys"
            WHERE "pk" = ? AND "sk" = ?
            """);
    }

    private TContext CreateContext<TContext>() where TContext : DbContext
        => (TContext)Activator.CreateInstance(
            typeof(TContext),
            CreateOptions<TContext>(o => o.DynamoDbClient(Client)))!;

    private static async Task ResetTable(DbContext context)
    {
        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string tableName,
        string partitionKeyName,
        AttributeValue partitionKeyValue,
        string? sortKeyName = null,
        AttributeValue? sortKeyValue = null)
    {
        var key = new Dictionary<string, AttributeValue> { [partitionKeyName] = partitionKeyValue };
        if (sortKeyName is not null && sortKeyValue is not null)
            key[sortKeyName] = sortKeyValue;

        var response = await Client.GetItemAsync(
            new GetItemRequest { TableName = tableName, Key = key },
            CancellationToken);

        return response.Item is { Count: > 0 } item ? item : null;
    }

    private sealed record IntKeyEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class IntKeyContext(DbContextOptions<IntKeyContext> options) : DbContext(options)
    {
        public const string TableName = "value-generation-int-keys";

        public DbSet<IntKeyEntity> Entities => Set<IntKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed record GuidKeyEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
    }

    private sealed class GuidKeyContext(DbContextOptions<GuidKeyContext> options) : DbContext(
        options)
    {
        public const string TableName = "value-generation-guid-keys";

        public DbSet<GuidKeyEntity> Entities => Set<GuidKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed record CompositeKeyEntity
    {
        public string Pk { get; set; } = null!;

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class CompositeKeyContext(DbContextOptions<CompositeKeyContext> options)
        : DbContext(options)
    {
        public const string TableName = "value-generation-composite-keys";

        public DbSet<CompositeKeyEntity> Entities => Set<CompositeKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Pk).HasAttributeName("pk");
                entity.Property(x => x.Sk).HasAttributeName("sk");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Pk);
                entity.HasSortKey(x => x.Sk);
            });
    }
}
