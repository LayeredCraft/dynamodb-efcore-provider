using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Infrastructure;

public sealed class FindSpecificationTests(DynamoContainerFixture containerFixture) : IAsyncLifetime
{
    private readonly TestPartiQlLoggerFactory _loggerFactory = new();

    private IAmazonDynamoDB Client => containerFixture.Client;

    public async ValueTask InitializeAsync()
    {
        await RecreateTablesAsync(TestContext.Current.CancellationToken);
        await SeedAsync(TestContext.Current.CancellationToken);
        _loggerFactory.Clear();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set)]
    [InlineData(FinderKind.Context)]
    [InlineData(FinderKind.NonGenericContext)]
    public void Find_tracked_int_key_returns_tracked_instance_without_query(FinderKind finderKind)
    {
        using var context = CreateContext();
        var tracked = context.Attach(new IntKey { Id = 88, Foo = "Tracked" }).Entity;

        var result = Find<IntKey>(context, finderKind, [88]);

        result.Should().BeSameAs(tracked);
        AssertNoPartiQl();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set)]
    [InlineData(FinderKind.Context)]
    [InlineData(FinderKind.NonGenericContext)]
    public async Task FindAsync_tracked_int_key_returns_completed_tracked_instance_without_query(
        FinderKind finderKind)
    {
        await using var context = CreateContext();
        var tracked = context.Attach(new IntKey { Id = 88, Foo = "Tracked" }).Entity;

        var valueTask = FindAsync<IntKey>(
            context,
            finderKind,
            CancellationKind.Right,
            [88],
            TestContext.Current.CancellationToken);

        valueTask.IsCompleted.Should().BeTrue();
        var result = await valueTask;

        result.Should().BeSameAs(tracked);
        AssertNoPartiQl();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_int_key_from_store_returns_entity_and_sets_limit_1(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var result = await FindAsync<IntKey>(
            context,
            finderKind,
            cancellationKind,
            [77],
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Foo.Should().Be("Smokey");
        AssertSingleLimit1Call();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_string_key_from_store_returns_entity(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var result = await FindAsync<StringKey>(
            context,
            finderKind,
            cancellationKind,
            ["Cat"],
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Foo.Should().Be("Alice");
        AssertSingleLimit1Call();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_composite_key_from_store_returns_entity(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var result = await FindAsync<CompositeKey>(
            context,
            finderKind,
            cancellationKind,
            [77, "Dog"],
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Foo.Should().Be("Olive");
        AssertSingleLimit1Call();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_missing_key_returns_null(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var result = await FindAsync<IntKey>(
            context,
            finderKind,
            cancellationKind,
            [99],
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
        AssertSingleLimit1Call();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_base_and_derived_types_respect_discriminator(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using (var context = CreateContext())
        {
            var baseResult = await FindAsync<BaseType>(
                context,
                finderKind,
                cancellationKind,
                [77],
                TestContext.Current.CancellationToken);

            baseResult.Should().NotBeNull();
            baseResult!.Foo.Should().Be("Baxter");
        }

        await using (var context = CreateContext())
        {
            var derivedResult = await FindAsync<DerivedType>(
                context,
                finderKind,
                cancellationKind,
                [78],
                TestContext.Current.CancellationToken);

            derivedResult.Should().NotBeNull();
            derivedResult!.Foo.Should().Be("Strawberry");
            derivedResult.Boo.Should().Be("Cheesecake");
        }

        await using (var context = CreateContext())
        {
            var baseFromDerivedSet = await FindAsync<DerivedType>(
                context,
                finderKind,
                cancellationKind,
                [77],
                TestContext.Current.CancellationToken);

            baseFromDerivedSet.Should().BeNull();
        }

        await using (var context = CreateContext())
        {
            var derivedFromBaseSet = await FindAsync<BaseType>(
                context,
                finderKind,
                cancellationKind,
                [78],
                TestContext.Current.CancellationToken);

            derivedFromBaseSet.Should().BeOfType<DerivedType>();
            ((DerivedType)derivedFromBaseSet!).Boo.Should().Be("Cheesecake");
        }

        _loggerFactory.ExecuteStatementCalls.Should().HaveCount(4);
        _loggerFactory.ExecuteStatementCalls.Should().OnlyContain(call => call.Limit == 1);
        _loggerFactory.Clear();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set)]
    [InlineData(FinderKind.Context)]
    [InlineData(FinderKind.NonGenericContext)]
    public void Find_untracked_entity_throws_sync_not_supported(FinderKind finderKind)
    {
        using var context = CreateContext();

        var act = () => Find<IntKey>(context, finderKind, [77]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Sync enumerating*DynamoDB*");
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_null_key_values_return_null_without_query(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var nullArray = await FindAsync<CompositeKey>(
            context,
            finderKind,
            cancellationKind,
            null,
            TestContext.Current.CancellationToken);
        var nullSimpleKey = await FindAsync<IntKey>(
            context,
            finderKind,
            cancellationKind,
            [null],
            TestContext.Current.CancellationToken);
        var nullCompositePart = await FindAsync<CompositeKey>(
            context,
            finderKind,
            cancellationKind,
            [77, null],
            TestContext.Current.CancellationToken);

        nullArray.Should().BeNull();
        nullSimpleKey.Should().BeNull();
        nullCompositePart.Should().BeNull();
        AssertNoPartiQl();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set)]
    [InlineData(FinderKind.Context)]
    [InlineData(FinderKind.NonGenericContext)]
    public void Find_null_key_values_return_null_without_query(FinderKind finderKind)
    {
        using var context = CreateContext();

        Find<CompositeKey>(context, finderKind, null).Should().BeNull();
        Find<IntKey>(context, finderKind, [null]).Should().BeNull();
        Find<CompositeKey>(context, finderKind, [77, null]).Should().BeNull();
        AssertNoPartiQl();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(FinderKind.Set, CancellationKind.Right)]
    [InlineData(FinderKind.Set, CancellationKind.Wrong)]
    [InlineData(FinderKind.Set, CancellationKind.None)]
    [InlineData(FinderKind.Context, CancellationKind.Right)]
    [InlineData(FinderKind.Context, CancellationKind.Wrong)]
    [InlineData(FinderKind.Context, CancellationKind.None)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Right)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.Wrong)]
    [InlineData(FinderKind.NonGenericContext, CancellationKind.None)]
    public async Task FindAsync_invalid_key_values_throw_before_query(
        FinderKind finderKind,
        CancellationKind cancellationKind)
    {
        await using var context = CreateContext();

        var tooManyValues = async () => await FindAsync<IntKey>(
            context,
            finderKind,
            cancellationKind,
            [77, 88],
            TestContext.Current.CancellationToken);
        var wrongCompositeCount = async () => await FindAsync<CompositeKey>(
            context,
            finderKind,
            cancellationKind,
            [77],
            TestContext.Current.CancellationToken);
        var badSimpleType = async () => await FindAsync<IntKey>(
            context,
            finderKind,
            cancellationKind,
            ["77"],
            TestContext.Current.CancellationToken);
        var badCompositeType = async () => await FindAsync<CompositeKey>(
            context,
            finderKind,
            cancellationKind,
            [77, 88],
            TestContext.Current.CancellationToken);

        await tooManyValues.Should().ThrowAsync<ArgumentException>();
        await wrongCompositeCount.Should().ThrowAsync<ArgumentException>();
        await badSimpleType.Should().ThrowAsync<ArgumentException>();
        await badCompositeType.Should().ThrowAsync<ArgumentException>();
        AssertNoPartiQl();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Nullable_key_shape_is_not_supported_by_dynamodb_model_validation()
    {
        using var context = new NullableKeyContext(CreateOptions<NullableKeyContext>());

        var act = () => _ = context.Model;

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition key*nullable*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Shadow_key_shape_is_not_supported_by_dynamodb_model_validation()
    {
        using var context = new ShadowKeyContext(CreateOptions<ShadowKeyContext>());

        var act = () => _ = context.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*shadow key properties are not supported*");
    }

    private FindContext CreateContext() => new(CreateOptions<FindContext>());

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        builder
            .UseDynamo(options => options.DynamoDbClient(Client))
            .UseLoggerFactory(_loggerFactory)
            .ConfigureWarnings(warnings => warnings.Ignore(DynamoEventId.ScanLikeQueryDetected));

        return builder.Options;
    }

    private static TEntity? Find<TEntity>(
        DbContext context,
        FinderKind finderKind,
        object?[]? keyValues) where TEntity : class
        => finderKind switch
        {
            FinderKind.Set => context.Set<TEntity>().Find(keyValues),
            FinderKind.Context => context.Find<TEntity>(keyValues),
            FinderKind.NonGenericContext => (TEntity?)context.Find(typeof(TEntity), keyValues),
            _ => throw new ArgumentOutOfRangeException(nameof(finderKind), finderKind, null)
        };

    private static async ValueTask<TEntity?> FindAsync<TEntity>(
        DbContext context,
        FinderKind finderKind,
        CancellationKind cancellationKind,
        object?[]? keyValues,
        CancellationToken cancellationToken) where TEntity : class
    {
        var effectiveKeyValues = cancellationKind == CancellationKind.Wrong
            ? keyValues?.Concat([cancellationToken]).ToArray()
            : keyValues;

        return finderKind switch
        {
            FinderKind.Set => cancellationKind == CancellationKind.Right
                ? await context.Set<TEntity>().FindAsync(keyValues, cancellationToken)
                : await context.Set<TEntity>().FindAsync(effectiveKeyValues),
            FinderKind.Context => cancellationKind == CancellationKind.Right
                ? await context.FindAsync<TEntity>(keyValues, cancellationToken)
                : await context.FindAsync<TEntity>(effectiveKeyValues),
            FinderKind.NonGenericContext => (TEntity?)await (
                cancellationKind == CancellationKind.Right
                    ? context.FindAsync(typeof(TEntity), keyValues, cancellationToken)
                    : context.FindAsync(typeof(TEntity), effectiveKeyValues)),
            _ => throw new ArgumentOutOfRangeException(nameof(finderKind), finderKind, null)
        };
    }

    private void AssertSingleLimit1Call()
    {
        var calls = _loggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(1);
        _loggerFactory.Clear();
    }

    private void AssertNoPartiQl()
    {
        _loggerFactory.PartiQlStatements.Should().BeEmpty();
        _loggerFactory.ExecuteStatementCalls.Should().BeEmpty();
    }

    private async Task RecreateTablesAsync(CancellationToken cancellationToken)
    {
        foreach (var tableName in FindTables.All)
            await DeleteIfExistsAsync(tableName, cancellationToken);

        await CreatePkTableAsync(
            FindTables.IntKeys,
            "id",
            ScalarAttributeType.N,
            cancellationToken);
        await CreatePkTableAsync(
            FindTables.StringKeys,
            "id",
            ScalarAttributeType.S,
            cancellationToken);
        await CreatePkSkTableAsync(
            FindTables.CompositeKeys,
            "id1",
            ScalarAttributeType.N,
            "id2",
            ScalarAttributeType.S,
            cancellationToken);
        await CreatePkTableAsync(
            FindTables.BaseTypes,
            "id",
            ScalarAttributeType.N,
            cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await Client.SeedItemsAsync(
            FindTables.IntKeys,
            [
                new Dictionary<string, AttributeValue>
                {
                    ["id"] = new() { N = "77" }, ["foo"] = new() { S = "Smokey" }
                }
            ],
            cancellationToken);

        await Client.SeedItemsAsync(
            FindTables.StringKeys,
            [
                new Dictionary<string, AttributeValue>
                {
                    ["id"] = new() { S = "Cat" }, ["foo"] = new() { S = "Alice" }
                }
            ],
            cancellationToken);

        await Client.SeedItemsAsync(
            FindTables.CompositeKeys,
            [
                new Dictionary<string, AttributeValue>
                {
                    ["id1"] = new() { N = "77" },
                    ["id2"] = new() { S = "Dog" },
                    ["foo"] = new() { S = "Olive" }
                }
            ],
            cancellationToken);

        await Client.SeedItemsAsync(
            FindTables.BaseTypes,
            [
                new Dictionary<string, AttributeValue>
                {
                    ["id"] = new() { N = "77" },
                    ["$type"] = new() { S = nameof(BaseType) },
                    ["foo"] = new() { S = "Baxter" }
                },
                new Dictionary<string, AttributeValue>
                {
                    ["id"] = new() { N = "78" },
                    ["$type"] = new() { S = nameof(DerivedType) },
                    ["foo"] = new() { S = "Strawberry" },
                    ["boo"] = new() { S = "Cheesecake" }
                }
            ],
            cancellationToken);
    }

    private async Task DeleteIfExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        try
        {
            await Client.DeleteTableAsync(tableName, cancellationToken);
            while (true)
            {
                try
                {
                    await Client.DescribeTableAsync(tableName, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }
                catch (ResourceNotFoundException)
                {
                    return;
                }
            }
        }
        catch (ResourceNotFoundException) { }
    }

    private Task CreatePkTableAsync(
        string tableName,
        string partitionKeyName,
        ScalarAttributeType partitionKeyType,
        CancellationToken cancellationToken)
        => Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition(partitionKeyName, partitionKeyType)
                ],
                KeySchema = [new KeySchemaElement(partitionKeyName, KeyType.HASH)],
                BillingMode = BillingMode.PAY_PER_REQUEST
            },
            cancellationToken);

    private Task CreatePkSkTableAsync(
        string tableName,
        string partitionKeyName,
        ScalarAttributeType partitionKeyType,
        string sortKeyName,
        ScalarAttributeType sortKeyType,
        CancellationToken cancellationToken)
        => Client.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition(partitionKeyName, partitionKeyType),
                    new AttributeDefinition(sortKeyName, sortKeyType)
                ],
                KeySchema =
                [
                    new KeySchemaElement(partitionKeyName, KeyType.HASH),
                    new KeySchemaElement(sortKeyName, KeyType.RANGE)
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST
            },
            cancellationToken);

    public enum FinderKind
    {
        Set,
        Context,
        NonGenericContext
    }

    public enum CancellationKind
    {
        Right,
        Wrong,
        None
    }

    private static class FindTables
    {
        public const string IntKeys = "SpecFind_IntKeys";
        public const string StringKeys = "SpecFind_StringKeys";
        public const string CompositeKeys = "SpecFind_CompositeKeys";
        public const string BaseTypes = "SpecFind_BaseTypes";

        public static IReadOnlyList<string> All { get; } =
        [
            IntKeys, StringKeys, CompositeKeys, BaseTypes
        ];
    }

    private sealed class FindContext(DbContextOptions<FindContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntKey>(builder =>
            {
                builder.ToTable(FindTables.IntKeys);
                builder.HasPartitionKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<StringKey>(builder =>
            {
                builder.ToTable(FindTables.StringKeys);
                builder.HasPartitionKey(entity => entity.Id);
            });

            modelBuilder.Entity<CompositeKey>(builder =>
            {
                builder.ToTable(FindTables.CompositeKeys);
                builder.HasPartitionKey(entity => entity.Id1);
                builder.HasSortKey(entity => entity.Id2);
                builder.Property(entity => entity.Id1).ValueGeneratedNever();
            });

            modelBuilder.Entity<BaseType>(builder =>
            {
                builder.ToTable(FindTables.BaseTypes);
                builder.HasPartitionKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<DerivedType>(builder => builder.ToTable(FindTables.BaseTypes));
        }
    }

    private sealed class NullableKeyContext(DbContextOptions<NullableKeyContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<NullableIntKey>(builder =>
            {
                builder.ToTable("SpecFind_NullableKeys");
                builder.HasPartitionKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedNever();
            });
    }

    private sealed class ShadowKeyContext(DbContextOptions<ShadowKeyContext> options) : DbContext(
        options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ShadowKey>(builder =>
            {
                builder.ToTable("SpecFind_ShadowKeys");
                builder.Property<int>("Id");
                builder.HasPartitionKey("Id");
            });
    }

    private class BaseType
    {
        public int Id { get; set; }

        public string Foo { get; set; } = null!;
    }

    private sealed class DerivedType : BaseType
    {
        public string Boo { get; set; } = null!;
    }

    private sealed class IntKey
    {
        public int Id { get; set; }

        public string Foo { get; set; } = null!;
    }

    private sealed class NullableIntKey
    {
        public int? Id { get; set; }

        public string Foo { get; set; } = null!;
    }

    private sealed class StringKey
    {
        public string Id { get; set; } = null!;

        public string Foo { get; set; } = null!;
    }

    private sealed class CompositeKey
    {
        public int Id1 { get; set; }

        public string Id2 { get; set; } = null!;

        public string Foo { get; set; } = null!;
    }

    private sealed class ShadowKey
    {
        public string Foo { get; set; } = null!;
    }
}
