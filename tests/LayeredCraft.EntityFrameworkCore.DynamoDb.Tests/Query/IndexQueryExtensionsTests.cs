using System.Linq.Expressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
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
}
