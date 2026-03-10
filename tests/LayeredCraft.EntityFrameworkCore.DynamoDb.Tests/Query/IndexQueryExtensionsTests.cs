using System.Linq.Expressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class IndexQueryExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(builder => builder.HasPartitionKey(x => x.Id));
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
    }

    /// <summary>Context with a GSI on <see cref="GsiEntity.CustomerId" /> used for IN-limit tests.</summary>
    private sealed class GsiDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<GsiEntity> Orders => Set<GsiEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<GsiEntity>(b =>
            {
                b.HasPartitionKey(x => x.TenantId);
                b.HasGlobalSecondaryIndex("ByCustomer", x => x.CustomerId);
            });
    }

    private sealed class GsiEntity
    {
        public string TenantId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string CustomerId { get; set; } = null!;
    }

    private static GsiDbContext CreateGsiContext(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<GsiDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    private static GsiDbContext CreateGsiContextWithAutoAnalyzer(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<GsiDbContext>()
                .ReplaceService<IDynamoIndexSelectionAnalyzer, AutoSelectByCustomerIndexAnalyzer>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    /// <summary>
    ///     Analyzer used by tests to simulate index auto-selection without an explicit WithIndex
    ///     hint.
    /// </summary>
    private sealed class AutoSelectByCustomerIndexAnalyzer : IDynamoIndexSelectionAnalyzer
    {
        /// <summary>Selects the ByCustomer index when no explicit hint is provided.</summary>
        /// <param name="context">The query analysis context.</param>
        /// <returns>The selected index decision.</returns>
        public DynamoIndexSelectionDecision Analyze(DynamoIndexAnalysisContext context)
            => context.ExplicitIndexHint is { } explicitIndexHint
                ? new DynamoIndexSelectionDecision(
                    explicitIndexHint,
                    DynamoIndexSelectionReason.ExplicitHint,
                    [])
                : new DynamoIndexSelectionDecision(
                    "ByCustomer",
                    DynamoIndexSelectionReason.AutoSelected,
                    []);
    }

    /// <summary>
    ///     Shared-table context: <see cref="SharedOrder"/> and <see cref="SharedInvoice"/> both map
    ///     to <c>SharedDocs</c>, but only <see cref="SharedOrder"/> has the <c>ByStatus</c> GSI.
    ///     Used to verify that index validation is scoped to the queried entity type, not the whole table.
    /// </summary>
    private sealed class SharedTableDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<SharedOrder> Orders => Set<SharedOrder>();
        public DbSet<SharedInvoice> Invoices => Set<SharedInvoice>();
        public DbSet<SharedCreditInvoice> CreditInvoices => Set<SharedCreditInvoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SharedOrder>(b =>
            {
                b.ToTable("SharedDocs");
                b.HasPartitionKey(x => x.Id);
                b.HasGlobalSecondaryIndex("ByStatus", x => x.Status);
            });

            modelBuilder.Entity<SharedInvoice>(b =>
            {
                b.ToTable("SharedDocs");
                b.HasPartitionKey(x => x.Id);
                // No secondary indexes on Invoice
            });
        }
    }

    private sealed class SharedOrder
    {
        public string Id { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private class SharedInvoice
    {
        public string Id { get; set; } = null!;
    }

    private sealed class SharedCreditInvoice : SharedInvoice
    {
        public string CreditMemoNumber { get; set; } = null!;
    }

    private static SharedTableDbContext CreateSharedTableContext(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<SharedTableDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    private sealed class DerivedIndexQueryDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<BaseOrderForIndexQuery> Orders => Set<BaseOrderForIndexQuery>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseOrderForIndexQuery>(b =>
            {
                b.ToTable("Orders");
                b.HasPartitionKey(x => x.Id);
                b.HasSortKey(x => x.OrderId);
                b.HasLocalSecondaryIndex("ByStatus", x => x.Status);
            });

            modelBuilder.Entity<PriorityOrderForIndexQuery>(b =>
            {
                b.HasBaseType<BaseOrderForIndexQuery>();
                b.HasGlobalSecondaryIndex("ByPriority", x => x.Priority);
            });

            modelBuilder.Entity<ArchivedOrderForIndexQuery>(b =>
            {
                b.HasBaseType<BaseOrderForIndexQuery>();
            });
        }
    }

    private class BaseOrderForIndexQuery
    {
        public string Id { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public string Status { get; set; } = null!;
    }

    private sealed class PriorityOrderForIndexQuery : BaseOrderForIndexQuery
    {
        public int Priority { get; set; }
    }

    private sealed class ArchivedOrderForIndexQuery : BaseOrderForIndexQuery;

    private static DerivedIndexQueryDbContext CreateDerivedIndexQueryContext(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<DerivedIndexQueryDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    private static TestDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseDynamo();
        return new TestDbContext(optionsBuilder.Options);
    }

    private static TestDbContext CreateContextWithClient(IAmazonDynamoDB client)
        => new(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseDynamo(o => o.DynamoDbClient(client))
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);

    [Fact]
    public void WithIndex_EmptyName_ThrowsArgumentException()
    {
        using var context = CreateContext();

        var act = () => context.Entities.WithIndex(" ");

        act.Should().Throw<ArgumentException>().WithParameterName("indexName");
    }

    [Fact]
    public void WithIndex_EntityQueryProvider_WrapsExpressionInMethodCall()
    {
        using var context = CreateContext();

        var query = context.Entities.WithIndex("ByCustomerCreatedAt");

        query.Expression.Should().BeAssignableTo<MethodCallExpression>();
        var methodCall = (MethodCallExpression)query.Expression;
        methodCall.Method.Name.Should().Be(nameof(DynamoDbQueryableExtensions.WithIndex));
        methodCall.Arguments[1].Should().BeOfType<ConstantExpression>();
        ((ConstantExpression)methodCall.Arguments[1]).Value.Should().Be("ByCustomerCreatedAt");
    }

    [Fact]
    public void WithIndex_NonEntityQueryProvider_ReturnsOriginalQuery()
    {
        var source = new List<TestEntity>().AsQueryable();

        var query = source.WithIndex("ByCustomerCreatedAt");

        query.Should().BeSameAs(source);
    }

    [Fact]
    public async Task WithIndex_NonConstantExpression_ThrowsInvalidOperationException()
    {
        // Simulate a compiled query where the index name arrives as a ParameterExpression
        // rather than the ConstantExpression that [NotParameterized] guarantees for normal queries.
        // The translator must fail loud instead of silently falling back to the base table.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateContextWithClient(client);

        IQueryable<TestEntity> entities = context.Entities;
        var indexParam = Expression.Parameter(typeof(string), "indexName");
        var callExpr = Expression.Call(
            null,
            ((Func<IQueryable<TestEntity>, string, IQueryable<TestEntity>>)
                DynamoDbQueryableExtensions.WithIndex).Method,
            entities.Expression,
            indexParam);
        var query = entities.Provider.CreateQuery<TestEntity>(callExpr);

        var act = async () => await query.ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{nameof(DynamoDbQueryableExtensions.WithIndex)}*constant*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task WithIndex_UnknownIndexName_ThrowsInvalidOperationException()
    {
        // An index name not registered via HasGlobalSecondaryIndex/HasLocalSecondaryIndex must
        // fail at compile time with a clear message rather than silently reaching DynamoDB.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateGsiContext(client);

        var act = async () => await context
            .Orders
            .WithIndex("DoesNotExist")
            .Where(o => o.CustomerId == "C1")
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DoesNotExist*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task WithIndex_SharedTable_IndexOnlyOnOneEntityType_ThrowsForOtherEntityType()
    {
        // Regression: validation previously searched all entity type sources for the table group,
        // so an index configured only on Order would incorrectly pass validation for an Invoice
        // query.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateSharedTableContext(client);

        // Querying Invoice with an index that is only configured on Order must throw.
        var act = async () => await context
            .Invoices
            .WithIndex("ByStatus")
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByStatus*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task
        WithIndex_SharedTable_IndexOnlyOnOneEntityType_ProjectedQuery_ThrowsForOtherEntityType()
    {
        // Regression: projection rewrites can replace the final shaper expression, but explicit
        // index validation must remain scoped to the original query root entity type.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateSharedTableContext(client);

        var act = async () => await context
            .Invoices
            .WithIndex("ByStatus")
            .Select(i => i.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByStatus*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task
        WithIndex_SharedTable_DerivedTypeFromOtherRoot_IndexOnlyOnOneEntityType_Throws()
    {
        // Regression: runtime table descriptors are keyed by root entity type name. A query rooted
        // at a derived type must still validate against its root entity type scope.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateSharedTableContext(client);

        var act = async () => await context
            .CreditInvoices
            .WithIndex("ByStatus")
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByStatus*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task WithIndex_BaseQuery_AcceptsDerivedDefinedIndex()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        await using var context = CreateDerivedIndexQueryContext(client);

        var act = async () => await context
            .Orders
            .WithIndex("ByPriority")
            .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WithIndex_GsiPartitionKey_Contains_51Items_ThrowsPartitionKeyLimitError()
    {
        // Before the fix, CustomerId was not recognised as a partition key when querying via
        // WithIndex("ByCustomer"), so the 100-value (non-key) limit was used instead of the
        // stricter 50-value (partition-key) limit.  51 values must now throw.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateGsiContext(client);

        var customerIds = Enumerable.Range(1, 51).Select(i => $"CUSTOMER#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .WithIndex("ByCustomer")
                .Where(o => customerIds.Contains(o.CustomerId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*50*partition key*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task
        WithIndex_GsiPartitionKey_Contains_50Items_DoesNotThrowPartitionKeyLimitError()
    {
        // Exactly 50 values on the GSI partition key must succeed (boundary condition).
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        await using var context = CreateGsiContext(client);

        var customerIds = Enumerable.Range(1, 50).Select(i => $"CUSTOMER#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .WithIndex("ByCustomer")
                .Where(o => customerIds.Contains(o.CustomerId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task
        AutoSelectedIndex_GsiPartitionKey_Contains_51Items_ThrowsPartitionKeyLimitError()
    {
        // Regression: analyzer-selected indexes are applied after translation. Ensure SQL
        // generation
        // enforces the selected index partition-key limit instead of the base-table non-key limit.
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateGsiContextWithAutoAnalyzer(client);

        var customerIds = Enumerable.Range(1, 51).Select(i => $"CUSTOMER#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .Where(o => customerIds.Contains(o.CustomerId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*50*partition key*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }

    [Fact]
    public async Task
        AutoSelectedIndex_GsiPartitionKey_Contains_50Items_DoesNotThrowPartitionKeyLimitError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        await using var context = CreateGsiContextWithAutoAnalyzer(client);

        var customerIds = Enumerable.Range(1, 50).Select(i => $"CUSTOMER#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .Where(o => customerIds.Contains(o.CustomerId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task
        AutoSelectedIndex_BaseTablePartitionKey_Contains_51Items_DoesNotThrowPartitionKeyLimitError()
    {
        // Regression: when an analyzer selects ByCustomer, TenantId is a non-key attribute for
        // that query source and must use the non-key IN limit (100), not the 50 partition-key
        // limit.
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [] });

        await using var context = CreateGsiContextWithAutoAnalyzer(client);

        var tenantIds = Enumerable.Range(1, 51).Select(i => $"TENANT#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .Where(o => tenantIds.Contains(o.TenantId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task
        AutoSelectedIndex_BaseTablePartitionKey_Contains_101Items_ThrowsNonKeyLimitError()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        await using var context = CreateGsiContextWithAutoAnalyzer(client);

        var tenantIds = Enumerable.Range(1, 101).Select(i => $"TENANT#{i}").ToList();

        var act = async ()
            => await context
                .Orders
                .Where(o => tenantIds.Contains(o.TenantId))
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*100*non-key*");

        await client.DidNotReceiveWithAnyArgs().ExecuteStatementAsync(default!);
    }
}
