using System.Collections;
using System.Linq.Expressions;
using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

#pragma warning disable EF9102

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for DynamoDbQueryableExtensions — Limit, WithIndex, WithoutIndex.</summary>
public class DynamoDbQueryableExtensionsTests
{
    // ── ToPageAsync / WithNextToken argument validation ───────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void WithNextToken_Null_ThrowsArgumentNullException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.WithNextToken(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("nextToken");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void WithNextToken_Whitespace_ThrowsArgumentException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.WithNextToken("   ");

        act.Should().Throw<ArgumentException>().WithParameterName("nextToken");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToPageAsync_ZeroLimit_ThrowsArgumentOutOfRangeException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = async ()
            => await source.ToPageAsync(0, null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task ToPageAsync_WhitespaceToken_NormalizesToNullInProviderCall()
    {
        var provider = new CapturingAsyncQueryProvider();
        var source = new CapturingAsyncQueryable<TestEntity>(provider);

        _ = await source.ToPageAsync(5, "   ", TestContext.Current.CancellationToken);

        var call =
            provider.LastExecutedExpression.Should().BeAssignableTo<MethodCallExpression>().Subject;
        var tokenArg = call.Arguments[2].Should().BeAssignableTo<ConstantExpression>().Subject;
        tokenArg.Type.Should().Be(typeof(string));
        tokenArg.Value.Should().BeNull();
    }

    // ── Limit ────────────────────────────────────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Limit_Zero_ThrowsArgumentOutOfRangeException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.Limit(0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Limit_Negative_ThrowsArgumentOutOfRangeException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.Limit(-5);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Limit_Positive_ReturnsNewQueryable()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = LimitDbContext.Create(client);

        var original = context.Items.AsQueryable();
        var limited = original.Limit(10);

        // A new IQueryable wrapping a different expression is returned.
        limited.Should().NotBeSameAs(original);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Limit_OnNonEntityQueryProvider_ReturnsSourceUnchanged()
    {
        // Array.AsQueryable() uses EnumerableQuery<T>, not EntityQueryProvider.
        var source = new[] { new TestEntity() }.AsQueryable();

        var result = source.Limit(5);

        result.Should().BeSameAs(source);
    }

    // ── WithIndex / WithoutIndex (regression) ────────────────────────────────

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void WithIndex_EmptyString_ThrowsArgumentException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.WithIndex(string.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("indexName");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void WithoutIndex_ReturnsNewQueryable()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = LimitDbContext.Create(client);

        var original = context.Items.AsQueryable();
        var withoutIndex = original.WithoutIndex();

        withoutIndex.Should().NotBeSameAs(original);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void AllowScan_ReturnsNewQueryable()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var context = LimitDbContext.Create(client);

        var original = context.Items.AsQueryable();
        var allowScan = original.AllowScan();

        allowScan.Should().NotBeSameAs(original);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void AllowScan_OnNonEntityQueryProvider_ReturnsSourceUnchanged()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var result = source.AllowScan();

        result.Should().BeSameAs(source);
    }

    // ── Support types ────────────────────────────────────────────────────────

    private sealed class TestEntity;

    private sealed class CapturingAsyncQueryable<T>(CapturingAsyncQueryProvider provider)
        : IQueryable<T>
    {
        public Type ElementType => typeof(T);

        public Expression Expression { get; } = Expression.Constant(Array.Empty<T>().AsQueryable());

        public IQueryProvider Provider { get; } = provider;

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CapturingAsyncQueryProvider : IAsyncQueryProvider
    {
        public Expression? LastExecutedExpression { get; private set; }

        public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => throw new NotSupportedException();

        public object? Execute(Expression expression) => throw new NotSupportedException();

        public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();

        public TResult ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            LastExecutedExpression = expression;

            if (typeof(TResult).IsGenericType
                && typeof(TResult).GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = typeof(TResult).GetGenericArguments()[0];
                var defaultInnerValue =
                    innerType.IsValueType ? Activator.CreateInstance(innerType) : null;

                var fromResultMethod = typeof(Task)
                    .GetMethods()
                    .Single(m => m.Name == nameof(Task.FromResult))
                    .MakeGenericMethod(innerType);

                return (TResult)fromResultMethod.Invoke(null, [defaultInnerValue])!;
            }

            throw new NotSupportedException(
                "Only Task<T> ExecuteAsync results are supported in this test provider.");
        }
    }

    private sealed record LimitEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Pk { get; set; } = null!;
    }

    private sealed class LimitDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<LimitEntity> Items => Set<LimitEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<LimitEntity>(b =>
            {
                b.ToTable("LimitTable");
                b.HasPartitionKey(x => x.Pk);
            });

        /// <summary>Provides functionality for this member.</summary>
        public static LimitDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<LimitDbContext>()
                    .UseDynamo(options => options.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}

#pragma warning restore EF9102
