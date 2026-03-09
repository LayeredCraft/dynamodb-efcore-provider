using System.Linq.Expressions;
using Amazon.DynamoDBv2;
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
}
