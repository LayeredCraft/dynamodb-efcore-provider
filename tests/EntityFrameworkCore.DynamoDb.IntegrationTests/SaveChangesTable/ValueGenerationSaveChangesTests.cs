using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using LayeredCraft.DynamoMapper.Runtime;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

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
        var entity = new IntKeyEntity { Id = 123, Name = "assigned" };

        await using (var context = CreateContext<IntKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            IntKeyContext.TableName,
            new Dictionary<string, AttributeValue> { ["id"] = entity.Id.ToAttributeValue() },
            CancellationToken);

        await using (var context = CreateContext<IntKeyContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == 123).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

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

    /// <summary>Verifies CLR default integer keys are written as explicit DynamoDB keys.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_IntPartitionKey_DefaultZero_PersistsAsExplicitKey()
    {
        var entity = new IntKeyEntity { Id = 0, Name = "zero" };

        await using (var context = CreateContext<IntKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            IntKeyContext.TableName,
            new Dictionary<string, AttributeValue> { ["id"] = entity.Id.ToAttributeValue() },
            CancellationToken);

        await using (var context = CreateContext<IntKeyContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == 0).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-int-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-int-keys"
            WHERE "id" = 0
            """);
    }

    /// <summary>Verifies single string partition keys are explicitly written through SaveChanges.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_StringPartitionKey_ExplicitKey_PersistsAndMaterializes()
    {
        var entity = new StringKeyEntity { Id = "SESSION#1", Name = "assigned" };

        await using (var context = CreateContext<StringKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            StringKeyContext.TableName,
            new Dictionary<string, AttributeValue> { ["id"] = entity.Id.ToAttributeValue() },
            CancellationToken);

        await using (var context = CreateContext<StringKeyContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == entity.Id).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-string-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-string-keys"
            WHERE "id" = ?
            """);
    }

    /// <summary>Verifies binary partition keys are explicitly written and can be read back.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_BinaryPartitionKey_ExplicitKey_PersistsAndMaterializes()
    {
        var entity = new BinaryKeyEntity { Id = [0, 1, 2, 127, 255], Name = "binary" };

        await using (var context = CreateContext<BinaryKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            BinaryKeyContext.TableName,
            new Dictionary<string, AttributeValue> { ["id"] = entity.Id.ToAttributeValue() },
            CancellationToken);

        await using (var context = CreateContext<BinaryKeyContext>())
        {
            var materialized =
                await context
                    .Entities
                    .Where(x => x.Id == entity.Id)
                    .FirstOrDefaultAsync(CancellationToken);

            materialized.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-binary-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-binary-keys"
            WHERE "id" = ?
            """);
    }

    /// <summary>Verifies explicit integer key generation runs before writing to DynamoDB.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_IntPartitionKey_ExplicitGenerator_GeneratesPersistsAndMaterializes()
    {
        var entity = new IntKeyEntity { Name = "generated" };

        await using (var context = CreateContext<GeneratedIntKeyContext>())
        {
            await ResetTable(context);

            entity.Id.Should().Be(0);
            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        entity.Id.Should().Be(777);
        await AssertItemExistsInDynamoDbAsync(
            entity,
            GeneratedIntKeyContext.TableName,
            new Dictionary<string, AttributeValue> { ["id"] = entity.Id.ToAttributeValue() },
            CancellationToken);

        await using (var context = CreateContext<GeneratedIntKeyContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == entity.Id).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-generated-int-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-generated-int-keys"
            WHERE "id" = ?
            """);
    }

    /// <summary>Verifies Guid partition keys still use EF Core client-side generation before writing.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_GuidPartitionKey_DefaultGuid_GeneratesPersistsAndMaterializes()
    {
        var entity = new GuidKeyEntity { Name = "generated" };

        await using (var context = CreateContext<GuidKeyContext>())
        {
            await ResetTable(context);

            entity.Id.Should().Be(Guid.Empty);
            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        entity.Id.Should().NotBe(Guid.Empty);
        await AssertItemExistsInDynamoDbAsync(
            entity,
            GuidKeyContext.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["id"] = entity.Id.ToString().ToAttributeValue()
            },
            CancellationToken);

        await using (var context = CreateContext<GuidKeyContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == entity.Id).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

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

    /// <summary>Verifies explicit no-generation on Guid keys writes the supplied key value.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_GuidPartitionKey_ValueGeneratedNever_PersistsExplicitKey()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var entity = new GuidKeyEntity { Id = id, Name = "explicit" };

        await using (var context = CreateContext<ExplicitGuidNoGenerationContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        entity.Id.Should().Be(id);
        await AssertItemExistsInDynamoDbAsync(
            entity,
            ExplicitGuidNoGenerationContext.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["id"] = entity.Id.ToString().ToAttributeValue()
            },
            CancellationToken);

        await using (var context = CreateContext<ExplicitGuidNoGenerationContext>())
        {
            var materialized =
                await context.Entities.Where(x => x.Id == id).ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-explicit-guid-keys"
            VALUE {'id': ?, 'name': ?}
            """,
            """
            SELECT "id", "name"
            FROM "value-generation-explicit-guid-keys"
            WHERE "id" = ?
            """);
    }

    /// <summary>Verifies composite string keys are explicitly written through SaveChanges.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_CompositeStringKeys_ExplicitKeys_PersistsAndMaterializes()
    {
        var entity = new CompositeKeyEntity { Pk = "TENANT#1", Sk = "ORDER#1", Name = "composite" };

        await using (var context = CreateContext<CompositeKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            CompositeKeyContext.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = entity.Pk.ToAttributeValue(), ["sk"] = entity.Sk.ToAttributeValue()
            },
            CancellationToken);

        await using (var context = CreateContext<CompositeKeyContext>())
        {
            var materialized =
                await context
                    .Entities
                    .Where(x => x.Pk == entity.Pk && x.Sk == entity.Sk)
                    .ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

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

    /// <summary>Verifies composite Guid/string keys are application-assigned through SaveChanges.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_CompositeGuidStringKeys_ExplicitKeys_PersistsAndMaterializes()
    {
        var entity = new CompositeGuidKeyEntity
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Sk = "ORDER#1",
            Name = "composite-guid"
        };

        await using (var context = CreateContext<CompositeGuidKeyContext>())
        {
            await ResetTable(context);

            context.Entities.Add(entity);
            await context.SaveChangesAsync(CancellationToken);
        }

        await AssertItemExistsInDynamoDbAsync(
            entity,
            CompositeGuidKeyContext.TableName,
            new Dictionary<string, AttributeValue>
            {
                ["id"] = entity.Id.ToString().ToAttributeValue(),
                ["sk"] = entity.Sk.ToAttributeValue()
            },
            CancellationToken);

        await using (var context = CreateContext<CompositeGuidKeyContext>())
        {
            var materialized =
                await context
                    .Entities
                    .Where(x => x.Id == entity.Id && x.Sk == entity.Sk)
                    .ToListAsync(CancellationToken);

            materialized.Should().ContainSingle().Which.Should().BeEquivalentTo(entity);
        }

        AssertSql(
            """
            INSERT INTO "value-generation-composite-guid-keys"
            VALUE {'id': ?, 'sk': ?, 'name': ?}
            """,
            """
            SELECT "id", "sk", "name"
            FROM "value-generation-composite-guid-keys"
            WHERE "id" = ? AND "sk" = ?
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

    public sealed record IntKeyEntity
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

    public sealed record GuidKeyEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
    }

    public sealed record StringKeyEntity
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    public sealed record BinaryKeyEntity
    {
        public byte[] Id { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class StringKeyContext(DbContextOptions<StringKeyContext> options) : DbContext(
        options)
    {
        public const string TableName = "value-generation-string-keys";

        public DbSet<StringKeyEntity> Entities => Set<StringKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<StringKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed class BinaryKeyContext(DbContextOptions<BinaryKeyContext> options) : DbContext(
        options)
    {
        public const string TableName = "value-generation-binary-keys";

        public DbSet<BinaryKeyEntity> Entities => Set<BinaryKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<BinaryKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed class GeneratedIntKeyContext(DbContextOptions<GeneratedIntKeyContext> options)
        : DbContext(options)
    {
        public const string TableName = "value-generation-generated-int-keys";

        public DbSet<IntKeyEntity> Entities => Set<IntKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<IntKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity
                    .Property(x => x.Id)
                    .HasAttributeName("id")
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<StaticIntValueGenerator>();
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    private sealed class StaticIntValueGenerator : ValueGenerator<int>
    {
        public override bool GeneratesTemporaryValues => false;

        public override int Next(EntityEntry entry) => 777;
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

    private sealed class ExplicitGuidNoGenerationContext(
        DbContextOptions<ExplicitGuidNoGenerationContext> options) : DbContext(options)
    {
        public const string TableName = "value-generation-explicit-guid-keys";

        public DbSet<GuidKeyEntity> Entities => Set<GuidKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GuidKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id").ValueGeneratedNever();
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
            });
    }

    public sealed record CompositeKeyEntity
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

    public sealed record CompositeGuidKeyEntity
    {
        public Guid Id { get; set; }

        public string Sk { get; set; } = null!;

        public string Name { get; set; } = null!;
    }

    private sealed class CompositeGuidKeyContext(DbContextOptions<CompositeGuidKeyContext> options)
        : DbContext(options)
    {
        public const string TableName = "value-generation-composite-guid-keys";

        public DbSet<CompositeGuidKeyEntity> Entities => Set<CompositeGuidKeyEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CompositeGuidKeyEntity>(entity =>
            {
                entity.ToTable(TableName);
                entity.Property(x => x.Id).HasAttributeName("id");
                entity.Property(x => x.Sk).HasAttributeName("sk");
                entity.Property(x => x.Name).HasAttributeName("name");
                entity.HasPartitionKey(x => x.Id);
                entity.HasSortKey(x => x.Sk);
            });
    }
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class IntKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.IntKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.IntKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.IntKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class GuidKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.GuidKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.GuidKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.GuidKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class StringKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.StringKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.StringKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.StringKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class BinaryKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.BinaryKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.BinaryKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.BinaryKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class CompositeKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.CompositeKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.CompositeKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.CompositeKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = false)]
internal partial class CompositeGuidKeyEntityMapper
    : IDynamoMapper<ValueGenerationSaveChangesTests.CompositeGuidKeyEntity>
{
    public static partial Dictionary<string, AttributeValue> ToItem(
        ValueGenerationSaveChangesTests.CompositeGuidKeyEntity source);

    public static partial ValueGenerationSaveChangesTests.CompositeGuidKeyEntity FromItem(
        Dictionary<string, AttributeValue> item);
}
