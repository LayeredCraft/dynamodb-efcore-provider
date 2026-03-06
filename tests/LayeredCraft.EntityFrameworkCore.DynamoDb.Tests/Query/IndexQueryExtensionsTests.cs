using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class IndexQueryExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(builder => builder.HasKey(x => x.Id));
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
}
